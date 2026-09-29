using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public static class ZoneTuning
{
    public const float OfferSeconds = .30f, HelpOverlapSeconds = .15f, CarrySeconds = .60f, CarryYards = 5;
    public const float ReachMarginSeconds = .10f, HumanHorizonSeconds = .75f;
}
public sealed record CoverageTransfer(int Transaction, int OwnershipVersion, string PlanVersion, float Seconds,
    DefenderSlot Source, DefenderSlot Destination, ReceiverId Receiver, string Stage, string Reason);
public sealed record ZoneCommitment(DefenderSlot Actor, ZoneArea HomeArea, Vector2 Home, ReceiverId? Primary,
    ReceiverId? Helping, string Status, bool HumanAbandonment);
public sealed record CoverageBoard(int OwnershipVersion, IReadOnlyList<ZoneCommitment> Commitments,
    IReadOnlyList<CoverageTransfer> Transfers, IReadOnlyList<string> Concessions);

/// <summary>All zone ownership changes commit before any actor receives an intent. No physical movement or hidden offensive reads.</summary>
public sealed partial class CoverageCoordinator
{
    private sealed class State
    {
        public ReceiverId? Primary, Helping;
        public float CommittedAt, ExitedAt = -1, HelpUntil, HelpCommittedAt;
        public string Status = "Home";
        public bool Abandoned;
    }
    private sealed record Offer(DefenderSlot Source, DefenderSlot Destination, ReceiverId Receiver, int Priority, double Urgency, int Bundle = 0);
    private sealed record Declined(float Until, string Evidence);
    private readonly ResolvedDefensivePlan _plan;
    private readonly Dictionary<DefenderSlot, ZoneResponsibility> _zones;
    private readonly Dictionary<DefenderSlot, State> _states;
    private readonly Dictionary<(DefenderSlot, DefenderSlot, ReceiverId), Declined> _declined = new();
    private int _version, _transaction;
    public CoverageBoard? Current { get; private set; }
    public IReadOnlyDictionary<ReceiverId, DefenderSlot> Owners => _states.Where(p => p.Value.Primary != null)
        .ToDictionary(p => p.Value.Primary!.Value, p => p.Key);

    public CoverageCoordinator(ResolvedDefensivePlan plan)
    {
        _plan = plan;
        _zones = plan.Assignments.Where(a => a.Pass is ZoneResponsibility or MatchResponsibility).ToDictionary(a => a.ActorId,
            a => a.Pass is ZoneResponsibility zone ? zone : MatchZone((MatchResponsibility)a.Pass));
        _states = _zones.Keys.ToDictionary(a => a, a => new State { Primary = (plan.Assignments.Single(j => j.ActorId == a).Pass as MatchResponsibility)?.Provisional });
    }

    public IReadOnlyList<DefensiveIntent> Decide(DefensiveObservationFrame frame, DefenderSlot? human)
    {
        if (_plan.Match != null) return DecideMatch(frame, human);
        var transfers = new List<CoverageTransfer>(); var concessions = new List<string>();
        var receivers = frame.Receivers.ToDictionary(r => r.Id);
        var actors = frame.Visible.Defenders.ToDictionary(d => d.Id);
        foreach (var state in _states.Values) state.Status = state.Primary != null ? "ProtectPrimary" : "RecoverHome";
        bool coverage = !frame.ConfirmedCarrier && frame.Visible.Ball.State != BallState.InAir;
        if (coverage)
        {
            _transaction++;
            // Established primaries survive cuts. Vacancies choose by scheme class, then bounded arrival urgency.
            AcquireVacancies();
            var offers = new List<Offer>();
            foreach (var (actor, state) in _states.OrderBy(p => p.Key))
            {
                if (state.Primary is not { } id) continue;
                var r = receivers[id]; var z = _zones[actor];
                bool outside = !z.Area.Contains(r.Position) && !FlatReplacement(actor, r);
                if (outside && state.ExitedAt < 0) state.ExitedAt = frame.Seconds;
                if (!outside) state.ExitedAt = -1;
                var destination = Destination(actor, r);
                if (destination is not { } dest) continue;
                bool reachable = Reachable(actors[dest], r, _zones[dest], human == dest);
                string evidence = $"{_version}:{reachable}:{_states[dest].Primary}:{LostTop(actor, r)}";
                var key = (actor, dest, id);
                if (_declined.TryGetValue(key, out var old) && old.Until > frame.Seconds && old.Evidence == evidence) continue;
                offers.Add(new(actor, dest, id, Priority(dest, r) + (LostTop(actor, r) ? 10 : 0), Urgency(r, actors[dest], _zones[dest])));
                Log(actor, dest, id, "Offer", human == dest ? "XFER-04:ActualHumanState" : "ZONE-01:DeclaredNeighbor");
                _declined[key] = new(frame.Seconds + ZoneTuning.OfferSeconds, evidence);
            }
            CommitOffers(frame, human, offers, transfers);
            foreach (var (actor, state) in _states.OrderBy(p => p.Key))
            {
                if (state.Primary is not { } id || state.ExitedAt < 0) continue;
                var r = receivers[id]; var z = _zones[actor];
                if (frame.Seconds - state.ExitedAt < ZoneTuning.CarrySeconds && z.Area.LateralExit(r.Position) < ZoneTuning.CarryYards)
                { state.Status = "Carry;AwaitAcceptance"; continue; }
                // A Cover 2 flat corner is also the provisional outside-vertical carrier (Z2-01/02).
                // Its failed half-safety transfer concedes the flat, not the unaccepted vertical.
                if ((z.IsDeep || _plan.Call.Zone == SpotZoneFamily.Cover2 && z.Role == SpotZoneRole.Flat) && DeepThreat(r))
                {
                    state.Status = "EmergencyDeepCarry";
                    concessions.Add($"ZONE-01:{actor}:VacatedHomeForVertical:{id.Value}");
                }
                else
                {
                    concessions.Add($"ZONE-01:{actor}:ConcededReceiver:{id.Value};RecoverHome");
                    state.Primary = null; state.ExitedAt = -1; state.Status = "Conflict;RecoverHome"; state.Abandoned = false; _version++;
                }
            }
            AcquireVacancies();
            // Ordinary replacement only by an unowned local challenger, with the shared time/score gate.
            foreach (var (actor, state) in _states.OrderBy(p => p.Key))
            {
                if (state.Primary is not { } id || state.ExitedAt >= 0) continue;
                var current = receivers[id];
                var challenger = Candidates(actor).FirstOrDefault(r => !Owners.ContainsKey(r.Id));
                if (challenger == null || challenger.Id == id) continue;
                int oldClass = Priority(actor, current), newClass = Priority(actor, challenger);
                double oldScore = Urgency(current, actors[actor], _zones[actor]), newScore = Urgency(challenger, actors[actor], _zones[actor]);
                bool emergency = newClass > oldClass && DeepThreat(challenger);
                if (!emergency && (newClass < oldClass || frame.Seconds - state.CommittedAt < DefensiveTuning.CommitmentSeconds
                    || newScore <= oldScore || newScore < oldScore * DefensiveTuning.ChallengerRatio)) continue;
                concessions.Add($"OBS-08:{actor}:ConcededReceiver:{id.Value};HigherPriority:{challenger.Id.Value}");
                Assign(actor, challenger.Id, emergency ? "EmergencyDeepPriority" : "StablePriorityChange"); _version++;
            }
        }
        var intents = new List<DefensiveIntent>();
        foreach (var (actor, state) in _states.OrderBy(p => p.Key))
        {
            var z = _zones[actor]; var self = actors[actor]; Vector2 target = z.Home;
            string rule = _plan.Call.Zone switch { SpotZoneFamily.Cover2 => "Z2-01", SpotZoneFamily.Cover4 => "Z4-01", _ => "Z3-01" };
            if (state.HelpUntil > 0 && state.HelpUntil <= frame.Seconds) { state.Helping = null; state.HelpUntil = 0; }
            if (state.Primary is { } id)
            {
                var r = receivers[id]; target = Leverage(r, z);
                if (FlatReplacement(actor, r))
                {
                    state.Status = "DeclaredFlatReplacement"; rule = "Z2-01";
                    concessions.Add($"Z2-01:{actor}:VacatedHookForFlatReplacement:{id.Value}");
                }
                if (!coverage) state.Abandoned = false;
                else if (actor == human)
                {
                    bool departed = !Reachable(self, r, z, true);
                    if (departed && !state.Abandoned) concessions.Add($"HUMAN-03:{actor}:HumanAbandonment:{id.Value};ExposedResponsibility");
                    state.Abandoned = departed;
                }
                if (state.Abandoned) { state.Status = "HumanAbandonment;Exposed"; rule = "HUMAN-03"; }
                if (z.IsDeep && r.Position.Y > self.Position.Y + .5f)
                { state.Status = self.Contact ? "Obstructed;RetainDeepPrimary" : "BeatenBySpeed;RetainDeepPrimary"; rule = "MOVE-02"; }
            }
            else if (state.Helping is { } overlap && state.HelpUntil > frame.Seconds)
            { target = Leverage(receivers[overlap], z); state.Status = "Accepted;TemporaryHelp"; rule = "XFER-01"; }
            else if (z.IsDeep)
            {
                // Empty deep space can squeeze to a cap, never collapse onto a shallow route.
                var threats = frame.Receivers.Where(r => DeepThreat(r) && r.Position.X >= z.Area.Left - 5 && r.Position.X <= z.Area.Right + 5)
                    .OrderByDescending(r => Urgency(r, self, z)).ThenBy(r => r.Id.Value).ToArray();
                var help = threats.FirstOrDefault(); var previous = threats.FirstOrDefault(r => r.Id == state.Helping);
                if (previous != null && help != null && help.Id != previous.Id
                    && (frame.Seconds - state.HelpCommittedAt < DefensiveTuning.CommitmentSeconds
                        || Urgency(help, self, z) <= Urgency(previous, self, z)
                        || Urgency(help, self, z) < Urgency(previous, self, z) * DefensiveTuning.ChallengerRatio)) help = previous;
                if (state.Helping != help?.Id) state.HelpCommittedAt = frame.Seconds;
                state.Helping = help?.Id;
                if (help != null) { target.X = Math.Clamp(help.Position.X, z.Area.Left + 1, z.Area.Right - 1); state.Helping = help.Id; }
                target.Y = MathF.Max(z.Home.Y, help == null ? z.Home.Y : Leverage(help, z).Y);
                state.Status = "RetainDeepStructure"; rule = _plan.Call.Zone == SpotZoneFamily.Cover4 ? "Z4-03" : "Z3-01";
            }
            else if (Vector2.Distance(self.Position, z.Home) <= 1.5f) state.Status = "Home";
            if (z.Rotates && state.Primary == null && Vector2.Distance(self.Position, z.Home) > 1.5f)
            {
                target = z.Home; state.Status = "RotatingToCurlFlat"; rule = "Z3-04";
                if (frame.Receivers.Any(r => DeepThreat(r) && z.Area.LateralExit(r.Position) == 0))
                    concessions.Add($"Z3-04:{actor}:RotationIncomplete;LocalWindowExposed");
            }
            if (target.Y >= Constants.FieldLength - Constants.DefenderRadius)
            { target.X += target.X < Constants.FieldWidth / 2 ? 1 : -1; state.Status += ";BackLineLateralLeverage"; }
            intents.Add(new(actor, state.Primary != null ? DefensiveIntentKind.Leverage : state.Helping != null ? DefensiveIntentKind.Help : DefensiveIntentKind.Hold,
                DefenderMotor.Clamp(target), state.Primary, state.Helping, rule, state.Status, false));
        }
        foreach (var r in frame.Receivers.Where(r => !r.Blocking && r.Position.Y >= _plan.Offense.LineOfScrimmage && !Owners.ContainsKey(r.Id)))
            concessions.Add($"CoverageConflict:UnownedWindow:{r.Id.Value};NoInventedPrimary");
        Current = new(_version, Array.AsReadOnly(_states.OrderBy(p => p.Key).Select(p => new ZoneCommitment(p.Key, _zones[p.Key].Area,
            _zones[p.Key].Home, p.Value.Primary, p.Value.Helping, p.Value.Status, p.Value.Abandoned)).ToArray()),
            transfers.AsReadOnly(), Array.AsReadOnly(concessions.Distinct().ToArray()));
        return intents.AsReadOnly();

        void Assign(DefenderSlot actor, ReceiverId id, string status)
        {
            var s = _states[actor]; s.Primary = id; s.Helping = null; s.CommittedAt = frame.Seconds;
            s.ExitedAt = -1; s.Status = status; s.Abandoned = false;
        }
        void AcquireVacancies()
        {
            foreach (var actor in _zones.Keys.OrderBy(a => _zones[a].IsDeep ? 0 : 1).ThenBy(a => a))
            {
                if (_states[actor].Primary != null) continue;
                var r = Candidates(actor).FirstOrDefault(r => !Owners.ContainsKey(r.Id));
                if (r != null) { Assign(actor, r.Id, "LocalThreatAcquired"); _version++; }
            }
        }
        IEnumerable<ObservedReceiver> Candidates(DefenderSlot actor)
        {
            var z = _zones[actor];
            return frame.Receivers.Where(r => !r.Blocking && (r.Position.X >= z.Area.Left && r.Position.X < z.Area.Right || FlatReplacement(actor, r))
                && r.Position.Y >= _plan.Offense.LineOfScrimmage - 1
                && (z.IsDeep ? DeepThreat(r) && (!(_plan.Call.Zone == SpotZoneFamily.Cover2
                    || _plan.Call.Zone is SpotZoneFamily.Cover3 or SpotZoneFamily.Buzz && actor == DefenderSlot.FS) || r.Position.Y >= z.Area.Shallow)
                    : r.Position.Y <= z.Area.Deep))
                .OrderByDescending(r => Priority(actor, r)).ThenByDescending(r => Urgency(r, actors[actor], z)).ThenBy(r => r.Id.Value);
        }
        DefenderSlot? Destination(DefenderSlot source, ObservedReceiver r)
        {
            var z = _zones[source];
            if (FlatReplacement(source, r)) return null;
            var local = z.Neighbors.Where(n => r.Position.X >= _zones[n].Area.Left && r.Position.X < _zones[n].Area.Right
                && (_zones[n].IsDeep ? DeepThreat(r) && r.Position.Y >= _zones[n].Area.Shallow
                    : !z.IsDeep && r.Position.Y <= _zones[n].Area.Deep && r.Position.Y >= _zones[n].Area.Shallow)
                && (!z.IsDeep || _zones[n].IsDeep) && (z.IsDeep || _zones[n].IsDeep || !z.Area.Contains(r.Position)))
                .OrderByDescending(n => _zones[n].IsDeep).ThenBy(n => Vector2.Distance(_zones[n].Home, r.Position)).ThenBy(n => n);
            // Deep owners transfer only after crossing their lateral seam, not to overlapping underneath roles.
            return z.IsDeep && z.Area.LateralExit(r.Position) == 0 ? null : local.Cast<DefenderSlot?>().FirstOrDefault();
        }
        bool LostTop(DefenderSlot a, ObservedReceiver r) => DeepThreat(r) && r.Position.Y + MathF.Max(0, r.Velocity.Y) * .6f > actors[a].Position.Y;
        bool FlatReplacement(DefenderSlot actor, ObservedReceiver r)
        {
            var z = _zones[actor];
            return coverage && _plan.Call.Zone == SpotZoneFamily.Cover2 && z.Role == SpotZoneRole.Hook
                && r.Position.Y <= _plan.Offense.LineOfScrimmage + 6 * FieldGeometry.CoverageDepthScale(_plan.Offense.LineOfScrimmage)
                && z.Area.LateralExit(r.Position) <= ZoneTuning.CarryYards
                && z.Neighbors.Any(n => _zones[n].Role == SpotZoneRole.Flat && _zones[n].Area.Contains(r.Position)
                    && _states[n].Primary is { } carried && DeepThreat(receivers[carried]))
                && Reachable(actors[actor], r, z, human == actor);
        }
        int Priority(DefenderSlot a, ObservedReceiver r)
        {
            var z = _zones[a];
            if (z.IsDeep)
            {
                // Cover 2 safety favors a seam while the flat corner can retain the outside vertical.
                var surface = _plan.Surface.Threats.Single(t => t.ActorId == r.Id);
                bool outsideProtected = _plan.Surface.Threats.Where(t => t.Side == surface.Side && t.OutsideIn == 1)
                    .Any(t => Owners.TryGetValue(t.ActorId, out var owner) && !LostTop(owner, receivers[t.ActorId]));
                return DeepThreat(r) ? 30 + (_plan.Call.Zone == SpotZoneFamily.Cover2 && surface.OutsideIn > 1 && outsideProtected ? 2 : 0) : 0;
            }
            return DeepThreat(r) ? 20 : r.Position.Y >= _plan.Offense.LineOfScrimmage + 4 ? 10 : 1;
        }
        void Log(DefenderSlot from, DefenderSlot to, ReceiverId id, string stage, string reason)
            => transfers.Add(new(_transaction, _version, _plan.RuleVersion, frame.Seconds, from, to, id, stage, reason));

    }

    // A post/curl that has reached the deep window remains a threat after its vertical velocity disappears.
    private bool DeepThreat(ObservedReceiver r) => r.Vertical || r.Velocity.Y > .8f
        || r.Position.Y >= _plan.Offense.LineOfScrimmage + MathF.Max(1.5f, 7 * FieldGeometry.CoverageDepthScale(_plan.Offense.LineOfScrimmage));
    private Vector2 Leverage(ObservedReceiver r, ZoneResponsibility z)
    {
        var target = r.Position + r.Velocity * DefensiveTuning.ProjectionSeconds;
        target.Y += DeepThreat(r) ? 3 : 1;
        if (z.IsDeep) target.Y = MathF.Max(z.Home.Y, target.Y);
        return target;
    }
    internal static double Urgency(ObservedReceiver r, DefenderMotion actor, ZoneResponsibility z)
    {
        float speed = z.IsDeep ? MathF.Max(0, r.Velocity.Y) : r.Velocity.Length();
        if (speed < .1f) return 0;
        float distance = z.IsDeep ? MathF.Max(0, actor.Position.Y - r.Position.Y) : Vector2.Distance(r.Position, z.Home);
        float seconds = distance / speed;
        return seconds <= DefensiveTuning.HelpHorizonSeconds ? 1 / Math.Max(.1, seconds) : 0;
    }
    internal static bool Reachable(DefenderMotion actor, ObservedReceiver r, ZoneResponsibility z, bool human)
    {
        float horizon = human ? ZoneTuning.HumanHorizonSeconds : DefensiveTuning.HelpHorizonSeconds;
        // A low target has a short window; a deep target's top-leverage window shrinks as it approaches.
        float arrival = z.IsDeep && r.Velocity.Y > .1f ? Math.Clamp((actor.Position.Y + 3 - r.Position.Y) / r.Velocity.Y, .25f, horizon) : horizon;
        Vector2 point = DefenderMotor.Clamp(r.Position + r.Velocity * arrival + new Vector2(0, z.IsDeep ? 2 : 1));
        Vector2 delta = point - actor.Position;
        float turn = actor.Velocity.LengthSquared() < .1f || delta.LengthSquared() < .1f ? 0
            : MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(actor.Velocity), Vector2.Normalize(delta)), -1, 1)) / DefensiveTuning.TurnRadiansPerSecond;
        float travel = MathF.Max(0, delta.Length() - actor.Radius) / MathF.Max(.1f, actor.Speed);
        return !actor.Contact && travel + turn + DefensiveRuntime.RecognitionDelay(actor) + ZoneTuning.ReachMarginSeconds <= arrival;
    }
}
