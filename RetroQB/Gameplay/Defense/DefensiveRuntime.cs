using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public sealed record DefensiveDecisionFrame(string Version, float Seconds, DefensiveObservationFrame Observation,
    IReadOnlyList<DefensiveIntent> Decisions, IReadOnlyList<string> Events)
{
    public CoverageBoard? Coverage { get; init; }
    public RunBoard? Run { get; init; }
    public PressureBoard? Pressure { get; init; }
}

/// <summary>One team decision authority. All reads use the same immutable observation; only the motor/contact layer moves actors.</summary>
public sealed class DefensiveRuntime
{
    public const string Version = "defense-runtime-p6-v1";
    private sealed class State
    {
        public ReceiverId? Primary, Helping;
        public float CommittedAt;
    }
    private readonly Dictionary<DefenderSlot, State> _states;
    private readonly DefensiveObservationBuilder _observer;
    private readonly CoverageCoordinator? _coverage;
    private readonly RunFitCoordinator _run;
    private readonly PressureCoordinator _pressure;
    private readonly HashSet<int> _settledGroups = new();
    public int SwitchCommitCount { get; private set; }
    public ResolvedDefensivePlan Plan { get; }
    public DefensiveDecisionFrame? Current { get; private set; }
    public bool Stopped { get; private set; }

    public DefensiveRuntime(ResolvedDefensivePlan plan)
    {
        DefensivePlanValidator.ThrowIfInvalid(plan); Plan = plan; _observer = new(plan);
        _run = new(plan); _pressure = new(plan);
        if (plan.Call.Zone != SpotZoneFamily.None) _coverage = new(plan);
        _states = plan.Assignments.ToDictionary(a => a.ActorId, a => new State { Primary = (a.Pass as ManResponsibility)?.Target });
    }

    public void Stop() => Stopped = true;

    public DefensiveDecisionFrame? Decide(DefensiveSighting sighting, float dt, bool suspended = false, DefenderSlot? human = null, float speedMultiplier = 1)
    {
        if (!float.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        if (!float.IsFinite(speedMultiplier) || speedMultiplier <= 0) throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        if (Stopped || suspended || dt == 0) return Current; // DEC-00: frozen tactical time.
        if (sighting.Ball.State == BallState.Dead) { Stop(); return Current; }
        var frame = _observer.Observe(sighting, dt, _coverage?.Owners ?? _states.Where(s => s.Value.Primary != null).ToDictionary(s => s.Value.Primary!.Value, s => s.Key));
        var decisions = new Dictionary<DefenderSlot, DefensiveIntent>(); var events = new List<string>();
        foreach (var intent in _pressure.Decide(frame, human)) decisions.Add(intent.Actor, intent);
        if (_coverage != null)
        {
            foreach (var intent in _coverage.Decide(frame, human)) decisions.Add(intent.Actor, intent);
            events.AddRange(_coverage.Current!.Transfers.Select(t => $"{t.Reason}:{t.Stage}:{t.Source}->{t.Destination}:{t.Receiver.Value}:v{t.OwnershipVersion}"));
            events.AddRange(_coverage.Current.Concessions);
        }
        ResolveSwitches(frame, events);
        var allocatedHelp = new HashSet<ReceiverId>();
        foreach (var assignment in Plan.Assignments.Where(a => a.Pass is HelpResponsibility))
        {
            var helper = (HelpResponsibility)assignment.Pass;
            if (Plan.Pressure.Conditional is { ReleaseKey: null } delayed && delayed.Actor == assignment.ActorId)
            {
                // A staged rusher cannot justify another defender surrendering his own top.
                decisions[assignment.ActorId] = new(assignment.ActorId, DefensiveIntentKind.Spy, assignment.Alignment,
                    null, null, "PRESS-09", "HoldAuthoredStagingRole;NoCoverageCredit", false);
                continue;
            }
            if (helper.Role == HelperRole.Spy)
            {
                decisions[assignment.ActorId] = new(assignment.ActorId, DefensiveIntentKind.Spy, assignment.Alignment, null, null, "RUN-05", "CalledSpy;ConsumesLowHelper", false);
                continue;
            }
            var self = sighting.Defenders.Single(d => d.Id == assignment.ActorId);
            var state = _states[assignment.ActorId];
            bool deep = helper.Role is HelperRole.DeepLeft or HelperRole.DeepMiddle or HelperRole.DeepRight;
            var threats = frame.Receivers.Where(r => !r.Blocking && (!deep || !allocatedHelp.Contains(r.Id))
                && (helper.BracketTarget == null || r.Id == helper.BracketTarget)
                && (deep ? r.Vertical || r.Velocity.Y > .5f || LostTop(r, sighting) : MathF.Abs(r.Position.X - Plan.Surface.CenterX) <= 8
                    && r.Position.Y >= Plan.Offense.LineOfScrimmage && r.Position.Y <= Plan.Offense.LineOfScrimmage + 10)
                && (helper.Role != HelperRole.DeepLeft || r.Position.X <= Plan.Surface.CenterX + 1)
                && (helper.Role != HelperRole.DeepRight || r.Position.X >= Plan.Surface.CenterX - 1))
                .OrderByDescending(r => deep && LostTop(r, sighting)).ThenByDescending(r => Urgency(r, self)).ThenBy(r => r.Id.Value).ToArray();
            var chosen = threats.FirstOrDefault();
            var previous = threats.FirstOrDefault(r => r.Id == state.Helping);
            if (previous != null && chosen != null && chosen.Id != previous.Id && !LostTop(chosen, sighting))
                if (frame.Seconds - state.CommittedAt < DefensiveTuning.CommitmentSeconds
                    || Urgency(chosen, self) <= Urgency(previous, self)
                    || Urgency(chosen, self) < Urgency(previous, self) * DefensiveTuning.ChallengerRatio) chosen = previous;
            if (chosen?.Id != state.Helping)
            { events.Add($"OBS-08:{assignment.ActorId}:help:{state.Helping?.Value}->{chosen?.Id.Value}"); state.Helping = chosen?.Id; state.CommittedAt = frame.Seconds; }
            Vector2 target = assignment.Alignment;
            string rule = helper.Role == HelperRole.Robber ? "MAN-05" : deep ? Plan.Call.Shell == ManShell.TwoHigh ? "MAN-04" : "MAN-03" : "MAN-02";
            string reason = "RetainCalledHelpDepth";
            if (chosen != null)
            {
                allocatedHelp.Add(chosen.Id);
                target = Project(chosen, .6f) + new Vector2(0, deep ? 3 : 1);
                if (deep)
                {
                    target.Y = MathF.Max(assignment.Alignment.Y, target.Y);
                    if (threats.Length > 1 && !LostTop(chosen, sighting))
                    {
                        Vector2 second = Project(threats[1], .6f) + new Vector2(0, 3);
                        Vector2 midpoint = (target + second) / 2;
                        if (Vector2.Distance(midpoint, target) <= self.Speed * .6f && Vector2.Distance(midpoint, second) <= self.Speed * .6f)
                        { target = midpoint; target.Y = MathF.Max(assignment.Alignment.Y, MathF.Max(target.Y, second.Y)); reason = "MidpointTwoThreats;OtherPrimaryRetained"; }
                        else reason = $"CommitOne;ConcedeHelp:{threats[1].Id.Value}";
                    }
                    else reason = LostTop(chosen, sighting) ? "DEC-03:ImminentDeepLoss" : "CapSelectedThreat";
                }
                else
                {
                    target.X = Math.Clamp(target.X, Plan.Surface.CenterX - 8, Plan.Surface.CenterX + 8);
                    target.Y = Math.Clamp(target.Y, Plan.Offense.LineOfScrimmage + 2, Plan.Offense.LineOfScrimmage + 9);
                    if (Vector2.Distance(self.Position, target) > self.Speed * 1.2f
                        || Vector2.Distance(assignment.Alignment, target) > self.Speed * 1.2f)
                    { target = assignment.Alignment; state.Helping = null; allocatedHelp.Remove(chosen.Id); reason = "UnreachableJump;RetainLowArea"; }
                    else reason = "BoundedLowHelp;PrimaryRetained";
                }
            }
            if (helper.BracketTarget != null) { rule = "MAN-09"; reason = "DeclaredBracket;" + reason; }
            decisions[assignment.ActorId] = new(assignment.ActorId, DefensiveIntentKind.Help, DefenderMotor.Clamp(target), null, state.Helping, rule, reason, false);
        }
        if (Plan.Call.Shell == ManShell.TwoHigh && events.Any(e => e.StartsWith("OBS-08:"))) events.Add("MAN-04:AtomicHalfHelpUpdate;BothPrimariesRetained");
        foreach (var assignment in Plan.Assignments)
        {
            var self = sighting.Defenders.Single(d => d.Id == assignment.ActorId);
            var state = _states[assignment.ActorId];
            if (assignment.Pass is ManResponsibility)
            {
                var receiver = frame.Receivers.Single(r => r.Id == state.Primary);
                bool help = decisions.Values.Any(i => i.Helping == receiver.Id && i.Kind == DefensiveIntentKind.Help
                    && i.Reason != "UnreachableJump;RetainLowArea"
                    && ReachableHelp(sighting.Defenders.Single(d => d.Id == i.Actor), receiver));
                float side = receiver.Position.X < Plan.Surface.CenterX ? 1 : -1;
                float cushion = help ? 1.25f : 3f;
                Vector2 target = Project(receiver, DefensiveTuning.ProjectionSeconds) + new Vector2(side * .6f, cushion);
                string reason = help ? "ReachableHelp;InsideLeverage" : "NoReachableHelp;ProtectOwnTop";
                string rule = Plan.Call.Shell == ManShell.Zero ? "MAN-01" : Plan.Call.Shell == ManShell.TwoHigh ? "MAN-04" : "MAN-02";
                var kind = DefensiveIntentKind.Leverage;
                if (receiver.Blocking || receiver.Release == ReleaseDirection.Unknown && receiver.Position.Y < Plan.Offense.LineOfScrimmage)
                {
                    target = new(receiver.Position.X + side * .6f, MathF.Max(receiver.Position.Y + 2, Plan.Offense.LineOfScrimmage + 2));
                    rule = "MAN-08"; reason = receiver.Blocking ? "ObservedBlock;RetainReleaseKey" : "OBS-01:Unknown;RetainPrimary";
                }
                if (receiver.LateRelease) { rule = "MAN-08"; reason = "OBS-05:ObservedLateRelease;RecoverSamePrimary"; }
                if (receiver.Position.Y > self.Position.Y + .5f && receiver.Velocity.Y > 0)
                {
                    kind = DefensiveIntentKind.Recovery; rule = "MOVE-02";
                    bool reachable = TryIntercept(self, receiver, out target);
                    reason = self.Contact ? "Obstructed;RetainPrimary" : reachable ? "ReachableRecovery;RetainPrimary" : "BeatenBySpeed;TacklePrevention;RequestAvailableHelp";
                }
                if (target.Y > Constants.FieldLength - Constants.DefenderRadius)
                { target.X += side * 1.2f; reason += ";MOVE-04:LateralBackLineLeverage"; }
                decisions[assignment.ActorId] = new(assignment.ActorId, kind, DefenderMotor.Clamp(target), state.Primary, null, rule, reason, false);
            }
            var current = decisions[assignment.ActorId];
            if (sighting.Ball.State == BallState.InAir && frame.AirSeconds >= RecognitionDelay(self))
            {
                if (TryContest(self, frame, out var contest))
                    current = current with { Kind = DefensiveIntentKind.Contest, Target = contest, Rule = "MOVE-05", Reason = "DEC-02:ObservedFlight;ReachableLegalHeight", ActiveRusher = false };
                else current = current with { Reason = current.Reason + ";MOVE-05:FlightUnreachable;RetainLeverage", ActiveRusher = false };
            }
            decisions[assignment.ActorId] = current;
        }
        _pressure.ApplyConditionalAndScreens(frame, decisions, human, events);
        _run.Apply(frame, decisions, human, speedMultiplier, events);
        Current = new(Version, frame.Seconds, frame, Array.AsReadOnly(decisions.Values.OrderBy(d => d.Actor).ToArray()), events.AsReadOnly())
            { Coverage = frame.ConfirmedCarrier ? null : _coverage?.Current, Run = _run.Current, Pressure = _pressure.Current };
        return Current;
    }

    private void ResolveSwitches(DefensiveObservationFrame frame, List<string> events)
    {
        foreach (var group in Plan.SwitchGroups)
        {
            if (_settledGroups.Contains(group.Id)) continue;
            if (frame.ConfirmedCarrier || frame.Visible.Ball.State == BallState.InAir || frame.Seconds > DefensiveTuning.SwitchWindowSeconds + .00001f)
            { _settledGroups.Add(group.Id); events.Add($"MAN-07:Group{group.Id}:ProvisionalLocksRetained"); continue; }
            var first = frame.Receivers.Single(r => r.Id == group.First); var second = frame.Receivers.Single(r => r.Id == group.Second);
            if (first.Contact || second.Contact) continue;
            ObservedReceiver? outside = first.Release == ReleaseDirection.Outward && second.Release == ReleaseDirection.Inward ? first
                : second.Release == ReleaseDirection.Outward && first.Release == ReleaseDirection.Inward ? second : null;
            if (outside == null) continue;
            ReceiverId inside = outside.Id == first.Id ? second.Id : first.Id;
            _states[group.OutsideDefender].Primary = outside.Id;
            _states[group.InsideDefender].Primary = inside;
            _settledGroups.Add(group.Id); SwitchCommitCount++;
            events.Add($"MAN-07:Group{group.Id}:AtomicCommit:{group.OutsideDefender}->{outside.Id.Value},{group.InsideDefender}->{inside.Value}");
        }
    }

    private static Vector2 Project(ObservedReceiver receiver, float seconds) => DefenderMotor.Clamp(receiver.Position + receiver.Velocity * seconds);
    private bool LostTop(ObservedReceiver r, DefensiveSighting sighting)
    {
        var owner = _states.FirstOrDefault(p => p.Value.Primary == r.Id);
        return owner.Value != null && r.Position.Y + MathF.Max(0, r.Velocity.Y) * .6f >= sighting.Defenders.Single(d => d.Id == owner.Key).Position.Y + 1;
    }
    private double Urgency(ObservedReceiver receiver, DefenderMotion helper)
    {
        float speed = MathF.Max(0, receiver.Velocity.Y);
        if (speed < .1f) return 0;
        float seconds = MathF.Max(0, helper.Position.Y - receiver.Position.Y) / speed;
        return seconds <= DefensiveTuning.HelpHorizonSeconds ? 1 / Math.Max(.1, seconds) : 0;
    }
    private static bool ReachableHelp(DefenderMotion helper, ObservedReceiver receiver)
        => helper.Position.Y >= receiver.Position.Y && Vector2.Distance(helper.Position, Project(receiver, .8f)) <= helper.Speed * .8f + 1.5f;
    public static float RecognitionDelay(DefenderMotion defender) => Math.Clamp(DefensiveTuning.ThrowRecognitionSeconds / Math.Clamp(defender.RecognitionRating, .7f, 1.4f), .08f, .25f);
    private static bool TryIntercept(DefenderMotion self, ObservedReceiver r, out Vector2 target)
    {
        for (float t = .2f; t <= 1.5f; t += .1f)
        {
            target = Project(r, t);
            if (Vector2.Distance(self.Position, target) <= self.Speed * t + self.Radius) return true;
        }
        target = Project(r, 1.5f); return false;
    }
    private static bool TryContest(DefenderMotion self, DefensiveObservationFrame frame, out Vector2 target)
    {
        var ball = frame.Visible.Ball;
        for (float t = 0; t <= .6f; t += .05f)
        {
            float height = ball.Height + frame.HeightVelocity * t;
            if (height > Constants.PassCatchMaxHeight || height < -.2f) continue;
            Vector2 point = ball.Position + ball.Velocity * t;
            if (point != DefenderMotor.Clamp(point)) continue;
            if (Vector2.Distance(self.Position, point) <= self.Speed * t + self.Radius + .6f)
            { target = point; return true; }
        }
        target = default; return false;
    }
}
