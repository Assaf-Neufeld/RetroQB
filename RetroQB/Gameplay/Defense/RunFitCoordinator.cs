using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public enum RunDuty { Read, Spy, Gap, Force, BacksideEdge, Cutback, LastSupport, Tackle, Pursuit }
public sealed record RunJob(DefenderSlot Actor, RunDuty Duty, Vector2 Target, int? GapIndex, string Reason);
public sealed record FitTransfer(int Version, float Seconds, RunDuty Duty, DefenderSlot Source,
    DefenderSlot? Destination, string Stage, string Reason);
public sealed record RunBoard(int Version, RunRead Read, bool Cutback, DefenderSlot? Tackler, DefenderSlot? Force,
    DefenderSlot? CutbackPlayer, DefenderSlot? LastSupport, IReadOnlyList<RunJob> Jobs,
    IReadOnlyList<FitTransfer> Transfers, IReadOnlyList<string> Concessions);

/// <summary>Chooses complementary carrier responses together, without moving actors or resolving contact outcomes.</summary>
public sealed class RunFitCoordinator
{
    private readonly ResolvedDefensivePlan _plan;
    private readonly Dictionary<DefenderSlot, Vector2> _readAnchors = new();
    private readonly Dictionary<DefenderSlot, float> _failedUntil = new();
    private DefenderSlot? _tackler, _force, _cutback, _last;
    private float _lastSeconds, _tacklerSince, _lastTackleDistance = float.PositiveInfinity, _directionTime;
    private int _direction, _candidateDirection, _version;
    private bool _wasConfirmed;
    private DefenderSlot? _relief;
    private float _stationarySeconds, _reliefX;
    private float _reliefProgressY, _reliefStall;
    private int _reliefStage;
    public RunBoard? Current { get; private set; }
    public RunFitCoordinator(ResolvedDefensivePlan plan) => _plan = plan;

    public void Apply(DefensiveObservationFrame frame, IDictionary<DefenderSlot, DefensiveIntent> intents,
        DefenderSlot? human, float speedMultiplier, List<string> events)
    {
        var run = frame.Run;
        if (run == null) { Current = null; return; }
        float dt = MathF.Max(0, frame.Seconds - _lastSeconds); _lastSeconds = frame.Seconds;
        var actors = frame.Visible.Defenders.ToDictionary(d => d.Id, d => d with { Speed = d.Speed * speedMultiplier });
        var jobs = new List<RunJob>(); var transfers = new List<FitTransfer>(); var concessions = new List<string>();
        if (!run.Confirmed)
        {
            _wasConfirmed = false; _tackler = _force = _cutback = _last = null; _direction = 0;
            _relief = null; _stationarySeconds = 0; _reliefStage = 0;
            if (run.Read != RunRead.Mesh) _readAnchors.Clear();
            if (run.Read == RunRead.Mesh)
            foreach (var assignment in _plan.Assignments.Where(a => a.Pass is not RushResponsibility && !RunFitCompiler.DeepPass(a.Pass)))
            {
                var intent = intents[assignment.ActorId]; var actor = actors[assignment.ActorId];
                if (assignment.Pass is HelpResponsibility { Role: HelperRole.Spy }) continue;
                var receiver = frame.Receivers.FirstOrDefault(r => r.Id == intent.Primary);
                if (receiver != null && (receiver.LateRelease || receiver.Vertical || receiver.Position.Y >= _plan.Offense.LineOfScrimmage
                    || receiver.Release != ReleaseDirection.Unknown && receiver.Id != run.MeshParticipant)) continue;
                if (!_readAnchors.TryGetValue(actor.Id, out var anchor)) _readAnchors[actor.Id] = anchor = actor.Position;
                var gap = assignment.Run.GapIndex is { } index ? _plan.Surface.Gaps.Single(g => g.Index == index)
                    : _plan.Surface.Gaps.MinBy(g => MathF.Abs(g.X - actor.Position.X))!;
                Vector2 point = new(gap.X, _plan.Offense.LineOfScrimmage + .5f);
                var delta = point - anchor;
                Vector2 step = anchor + (delta.Length() > RunTuning.ReadStep ? Vector2.Normalize(delta) * RunTuning.ReadStep : delta);
                // The normal coverage intent is the recovery destination. Never make an unreachable release worse.
                if (Vector2.Distance(step, intent.Target) > actor.Speed * RunTuning.ReadRecoverySeconds) continue;
                intents[actor.Id] = intent with { Kind = DefensiveIntentKind.ReadStep, Target = DefenderMotor.Clamp(step), Rule = "RUN-01", Reason = "VisibleMesh;BoundedRecoverableRead" };
                jobs.Add(new(actor.Id, RunDuty.Read, DefenderMotor.Clamp(step), assignment.Run.GapIndex, "RecoverableFalseStep"));
            }
            foreach (var assignment in _plan.Assignments.Where(a => a.Pass is HelpResponsibility { Role: HelperRole.Spy }))
            {
                if (frame.Visible.Ball.State != BallState.HeldByQB) continue;
                float spySide = run.Position.X < _plan.Surface.CenterX ? -1 : 1;
                var point = DefenderMotor.Clamp(new(run.Position.X + spySide * 1.2f,
                    MathF.Max(_plan.Offense.LineOfScrimmage + 1, run.Position.Y + 2)));
                intents[assignment.ActorId] = new(assignment.ActorId, DefensiveIntentKind.Spy, point, null, null, "RUN-05",
                    run.OutsidePocket ? "CalledSpy;PreserveEscapeAngle" : "CalledSpy;MirrorQuarterback", false);
                jobs.Add(new(assignment.ActorId, RunDuty.Spy, point, null, "ConsumesLowHelper"));
            }
            if (run.OutsidePocket && frame.Visible.Ball.State == BallState.HeldByQB)
                foreach (var assignment in _plan.Assignments.Where(a => a.Pass is RushResponsibility { Contain: not ContainRole.None }))
                {
                    var intent = intents[assignment.ActorId];
                    intents[assignment.ActorId] = intent with { Rule = "PRESS-11", Reason = "QuarterbackCanStillThrow;NearOutsideHip;FarReversal" };
                }
            Current = new(_version, run.Read, false, null, null, null, null, jobs.AsReadOnly(), transfers.AsReadOnly(), concessions.AsReadOnly());
            return;
        }

        _readAnchors.Clear();
        if (!_wasConfirmed) { _version++; events.Add($"DEC-01:{run.Read}:ActivateConditionalFits"); }
        _wasConfirmed = true;
        float los = _plan.Offense.LineOfScrimmage, center = _plan.Surface.CenterX;
        var lead = DefenderMotor.Clamp(run.Position + run.Velocity * RunTuning.PredictionSeconds);
        int observedDirection = MathF.Abs(run.Velocity.X) > 1 ? Math.Sign(run.Velocity.X) : 0;
        if (_direction == 0) _direction = observedDirection != 0 ? observedDirection : run.Position.X < center ? -1 : 1;
        bool cut = false;
        if (observedDirection != 0 && observedDirection != _direction)
        {
            if (_candidateDirection != observedDirection) { _candidateDirection = observedDirection; _directionTime = 0; }
            _directionTime += dt;
            if (_directionTime + .00001f >= RunTuning.CutbackRecognitionSeconds) { _direction = observedDirection; cut = true; _directionTime = 0; }
        }
        else { _candidateDirection = 0; _directionTime = 0; }
        bool outside = run.Position.X < _plan.Offense.Linemen.Min(l => l.Position.X)
            || run.Position.X > _plan.Offense.Linemen.Max(l => l.Position.X)
            || MathF.Abs(run.Velocity.X) > MathF.Abs(run.Velocity.Y) * .7f && MathF.Abs(run.Position.X - center) > 2;
        int side = outside ? run.Position.X < center ? -1 : 1 : _direction;
        if (cut) side = _direction;
        var oldCutback = _cutback;
        var namedForce = side < 0 ? _plan.Fits.LeftForce : _plan.Fits.RightForce;
        var farEdge = side < 0 ? _plan.Fits.RightForce : _plan.Fits.LeftForce;
        var namedCutback = side < 0 ? _plan.Fits.RightCutback : _plan.Fits.LeftCutback;
        var last = _last ?? _plan.Fits.LastSupport;
        if (actors[last].Contact || actors[last].Position.Y < run.Position.Y)
        {
            var replacement = actors.Values.Where(d => !d.Contact && d.Id != _tackler && d.Position.Y >= run.Position.Y + 1)
                .OrderByDescending(d => d.Position.Y).ThenBy(d => d.Id).FirstOrDefault();
            if (replacement != null) { Change(RunDuty.LastSupport, last, replacement.Id, "GoalSideSupportReplacement"); last = replacement.Id; }
            else concessions.Add("RUN-08:NoGoalSideSupport;BeatenBySpeedOrContact");
        }
        var cutback = namedCutback == last ? OtherSupport(last, namedForce, farEdge) : namedCutback;
        bool stopped = run.Velocity.LengthSquared() < 1;
        _stationarySeconds = stopped ? _stationarySeconds + dt : 0;
        if (!stopped || run.Position.Y >= los + 3) { _relief = null; _reliefStage = 0; }
        Vector2 forcePoint = DefenderMotor.Clamp(lead + new Vector2(side * (stopped ? 1.2f : 2.2f), .5f));
        Vector2 cutbackPoint = DefenderMotor.Clamp(new(run.Position.Y < los + 6 ? center - side * 4 : lead.X - side * 4,
            MathF.Max(los + 1, lead.Y + 2)));
        DefenderSlot force = namedForce;
        // Keep the committed force owner until a real replacement accepts. Resetting
        // to the original owner during contact repeatedly steals the recovery actor.
        if (_force is { } prior && prior != farEdge && prior != last && prior != cutback && prior != _relief) force = prior;
        if (force == last || force == cutback || actors[force].Contact || !CanReach(actors[force], forcePoint, RunTuning.PursuitHorizon, human == force))
        {
            var replacement = actors.Values.Where(d => d.Id != last && d.Id != cutback && d.Id != farEdge && d.Id != force && d.Id != _relief
                && CanReach(d, forcePoint, RunTuning.PursuitHorizon, human == d.Id))
                .OrderBy(d => TravelSeconds(d, forcePoint)).ThenBy(d => d.Id).FirstOrDefault();
            if (replacement != null) { Change(RunDuty.Force, force, replacement.Id, "RUN-03:ReachableOutsideReplacement"); force = replacement.Id; }
            else
            {
                transfers.Add(new(_version, frame.Seconds, RunDuty.Force, force, null, "Decline", "ContactOrUnreachable;NoFreeOutsideReplacement"));
                concessions.Add($"RUN-03:ForceExposed:{force}");
            }
        }
        // Roles remain exclusive even in a broken structure; a nominal force actor cannot also be last support.
        DefenderSlot? forceOwner = force == last || force == cutback ? null : force;
        if (_tackler is { } previous)
        {
            var actor = actors[previous]; float distance = Vector2.Distance(actor.Position, run.Position);
            bool beaten = _lastTackleDistance <= actor.Radius + 1.8f && distance > _lastTackleDistance + .3f
                && Vector2.Dot(run.Position - actor.Position, run.Velocity) > 0;
            if (actor.Contact || beaten)
            {
                _failedUntil[previous] = frame.Seconds + .30f;
                concessions.Add($"RUN-08:{previous}:{(actor.Contact ? "BlockedFirstTackler" : "BeatenFirstTackler")}");
            }
        }
        var intercepts = actors.Values.Where(d => !d.Contact && (!_failedUntil.TryGetValue(d.Id, out var until) || until <= frame.Seconds))
            .Select(d => (Actor: d.Id, Intercept: Intercept(d, run, human == d.Id)))
            .Where(p => p.Intercept.Reachable).OrderBy(p => p.Intercept.Seconds).ThenBy(p => p.Actor).ToArray();
        // Protected roles commit only at a real immediate opportunity. Other defenders are evaluated as a team.
        var candidates = intercepts.Where(p => p.Actor != last && p.Actor != cutback && p.Actor != forceOwner
            && (p.Actor != farEdge || stopped && forceOwner != null)
            || p.Intercept.Seconds <= .10f).ToArray();
        // The opposite edge may close a trapped carrier when the other layers still own
        // force, cutback and last support. Requiring contact before allowing this approach
        // strands a stationary runner just beyond tackle range indefinitely.
        if (candidates.Length == 0 && forceOwner != null && cutback != farEdge && last != farEdge)
        {
            candidates = intercepts.Where(p => p.Actor == farEdge).ToArray();
            if (candidates.Length != 0) concessions.Add("RUN-08:BacksideEdgeCloses;ForceCutbackAndLastSupportRetained");
        }
        DefenderSlot? tackler = candidates.Length == 0 ? null : candidates[0].Actor;
        if (cut && oldCutback is { } responding && responding != last && candidates.Any(p => p.Actor == responding)
            && (candidates.Length == 0 || candidates[0].Intercept.Seconds > .1f)) tackler = responding;
        if (!cut && _tackler is { } held && tackler != held && candidates.Any(p => p.Actor == held))
        {
            float oldEta = candidates.Single(p => p.Actor == held).Intercept.Seconds;
            if (frame.Seconds - _tacklerSince < DefensiveTuning.CommitmentSeconds
                || candidates[0].Intercept.Seconds * DefensiveTuning.ChallengerRatio >= oldEta) tackler = held;
        }
        if (tackler != _tackler)
        {
            _version++; _tacklerSince = frame.Seconds;
            if (_tackler == human && _tackler != null && !candidates.Any(p => p.Actor == human)) concessions.Add("RUN-08:HumanTacklerOutOfReach;NoAutomaticSteering");
            if (_tackler is { } previousOwner) Change(RunDuty.Tackle, previousOwner, tackler, "RUN-08:NextReachableTackler");
        }
        if (tackler == null) concessions.Add("RUN-08:NoReachableImmediateStop;PursuitContinuesAtActualSpeed");
        if (tackler == forceOwner) { forceOwner = null; concessions.Add("RUN-03:ForceCommitsAtContact"); }
        DefenderSlot? cutbackOwner = tackler == cutback ? null : cutback;
        DefenderSlot? lastOwner = tackler == last ? null : last;
        if (lastOwner == null) concessions.Add("RUN-08:LastSupportCommitsAtContact");
        // A stopped runner behind the line can leave the direct pursuit lanes occupied
        // by blockers. Send one free support actor around the observed offensive surface,
        // retaining the force/cutback/last-support layers and ordinary contact physics.
        if (_relief is { } assigned && (assigned == lastOwner || assigned == cutbackOwner || assigned == forceOwner)) _relief = null;
        if (_relief == null && _stationarySeconds >= .75f && run.Position.Y < los)
        {
            float left = _plan.Offense.Linemen.Min(l => l.Position.X) - 4;
            float right = _plan.Offense.Linemen.Max(l => l.Position.X) + 4;
            var paths = actors.Values.Where(a => a.Id != lastOwner && a.Id != cutbackOwner && a.Id != forceOwner && a.Id != human)
                .SelectMany(a => new[] { left, right }.Select(x => (Actor: a, X: x,
                    Cost: (MathF.Abs(a.Position.X - x) + MathF.Abs(x - run.Position.X)) / a.Speed)))
                .OrderBy(p => p.Cost).ThenBy(p => p.Actor.Id).ToArray();
            if (paths.Length > 0)
            { _relief = paths[0].Actor.Id; _reliefX = paths[0].X; _reliefStage = 0; _reliefStall = 0; _reliefProgressY = paths[0].Actor.Position.Y; }
        }
        if (_relief is { } relief && _reliefStage < 2)
        {
            var actor = actors[relief];
            if (_reliefStage == 0 && MathF.Abs(actor.Position.X - _reliefX) < 1.5f) _reliefStage = 1;
            if (_reliefStage == 1 && actor.Position.Y <= run.Position.Y) _reliefStage = 2;
            if (_reliefStage == 1)
            {
                if (actor.Position.Y < _reliefProgressY - .25f) { _reliefProgressY = actor.Position.Y; _reliefStall = 0; }
                else _reliefStall += dt;
                if (_reliefStall >= .75f)
                {
                    // Widen the same committed path when a visible lack of progress
                    // indicates another obstacle. Never reverse direction each tick.
                    _reliefX = Math.Clamp(_reliefX + (_reliefX < center ? -3 : 3), Constants.DefenderRadius, Constants.FieldWidth - Constants.DefenderRadius);
                    _reliefStage = 0; _reliefStall = 0; _reliefProgressY = actor.Position.Y;
                }
            }
        }
        if (_relief == tackler && _reliefStage < 2) tackler = null;
        string rule = run.Read == RunRead.AfterCatch ? "RUN-07" : run.Read == RunRead.QuarterbackRun ? "RUN-06" : cut ? "RUN-04" : outside ? "RUN-03" : "RUN-02";
        foreach (var assignment in _plan.Assignments)
        {
            var actor = actors[assignment.ActorId]; var gapIndex = assignment.Run.GapIndex;
            RunDuty duty; Vector2 target; string reason;
            if (actor.Id == tackler)
            { duty = RunDuty.Tackle; target = intercepts.Single(p => p.Actor == actor.Id).Intercept.Point; reason = cut ? "BacksideRespondsToObservedCutback" : "ReachableIntercept;PhysicalContactRequired"; }
            else if (actor.Id == lastOwner)
            {
                duty = RunDuty.LastSupport; target = new(lead.X, MathF.Max(lead.Y + 6, MathF.Min(actor.Position.Y, lead.Y + 10)));
                reason = actor.Position.Y >= run.Position.Y ? "GoalSideLastSupport" : "BeatenLastSupport;RecoveryAngle";
            }
            else if (actor.Id == forceOwner) { duty = RunDuty.Force; target = forcePoint; reason = "OutsideIn;ProtectSidelineEscape"; }
            else if (actor.Id == cutbackOwner) { duty = RunDuty.Cutback; target = cutbackPoint; reason = "HoldBacksideLane"; }
            else if (actor.Id == farEdge) { duty = RunDuty.BacksideEdge; target = lead + new Vector2(-side * (stopped ? 1.2f : 3), stopped ? .5f : 1); reason = "PreserveReversalEdge"; }
            else if (gapIndex is { } index && run.Read == RunRead.Handoff && run.Position.Y < los + 3)
            {
                duty = RunDuty.Gap; target = new(_plan.Surface.Gaps.Single(g => g.Index == index).X, Math.Clamp(lead.Y, los - .5f, los + 3));
                reason = "NamedGap;SqueezeAdjacentSpace";
            }
            else
            {
                duty = RunDuty.Pursuit;
                float inside = actor.Position.X < lead.X ? -1 : 1;
                target = lead + new Vector2(inside * (1.5f + (int)actor.Id % 3), 1 + (int)actor.Id % 4);
                reason = "InsideOutCutoff;SeparateSupportAngle";
            }
            if (actor.Id == _relief && actor.Id != tackler)
            {
                duty = RunDuty.Pursuit;
                target = _reliefStage == 0 ? new(_reliefX, MathF.Max(los + 1, actor.Position.Y))
                    : _reliefStage == 1 ? new(_reliefX, run.Position.Y - 1) : run.Position;
                reason = "RUN-08:BlockedInterior;OneOutsideRecoveryPath";
            }
            if (gapIndex != null && duty != RunDuty.Gap && run.Read == RunRead.Handoff && run.Position.Y < los + 3)
                concessions.Add($"RUN-02:Gap{gapIndex}:Owner{actor.Id}CommittedTo{duty}");
            target = DefenderMotor.Clamp(target);
            var kind = duty switch { RunDuty.Tackle => DefensiveIntentKind.Tackle, RunDuty.Force => DefensiveIntentKind.Force,
                RunDuty.Cutback or RunDuty.BacksideEdge => DefensiveIntentKind.Cutback, RunDuty.LastSupport => DefensiveIntentKind.LastSupport,
                RunDuty.Gap => DefensiveIntentKind.GapFit, _ => DefensiveIntentKind.Pursuit };
            intents[actor.Id] = new(actor.Id, kind, target, null, null, rule, reason, false);
            jobs.Add(new(actor.Id, duty, target, duty == RunDuty.Gap ? gapIndex : null, reason));
        }
        _tackler = tackler; _force = forceOwner; _cutback = cutbackOwner; _last = lastOwner;
        _lastTackleDistance = tackler is { } t ? Vector2.Distance(actors[t].Position, run.Position) : float.PositiveInfinity;
        if (cut) _version++;
        transfers = transfers.Select(t => t with { Version = _version }).ToList();
        Current = new(_version, run.Read, cut, tackler, forceOwner, cutbackOwner, lastOwner, jobs.AsReadOnly(), transfers.AsReadOnly(), concessions.AsReadOnly());
        events.AddRange(concessions); events.AddRange(transfers.Select(t => $"{t.Reason}:{t.Duty}:{t.Source}->{t.Destination}:{t.Stage}:v{t.Version}"));

        DefenderSlot OtherSupport(params DefenderSlot[] excluded) => actors.Values.Where(d => !excluded.Contains(d.Id))
            .OrderBy(d => d.Contact).ThenBy(d => Vector2.Distance(d.Position, lead)).ThenBy(d => d.Id).First().Id;
        void Change(RunDuty duty, DefenderSlot source, DefenderSlot? dest, string reason)
        {
            _version++;
            transfers.Add(new(_version, frame.Seconds, duty, source, dest, "Offer", reason));
            if (dest != null)
            {
                transfers.Add(new(_version, frame.Seconds, duty, source, dest, "Reserve", "ExclusiveRoleInSharedFrame"));
                transfers.Add(new(_version, frame.Seconds, duty, source, dest, "Accept", reason));
            }
            transfers.Add(new(_version, frame.Seconds, duty, source, dest, dest == null ? "Decline" : "Commit", reason));
        }
    }

    internal static float TravelSeconds(DefenderMotion actor, Vector2 point)
    {
        Vector2 delta = point - actor.Position;
        float turn = actor.Velocity.LengthSquared() < .1f || delta.LengthSquared() < .1f ? 0
            : MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(actor.Velocity), Vector2.Normalize(delta)), -1, 1)) / DefensiveTuning.TurnRadiansPerSecond;
        float acceleration = MathF.Max(0, actor.Speed - actor.Velocity.Length()) / DefensiveTuning.Acceleration * .5f;
        return MathF.Max(0, delta.Length() - actor.Radius) / MathF.Max(.1f, actor.Speed) + turn + acceleration;
    }
    internal static bool CanReach(DefenderMotion actor, Vector2 point, float seconds, bool human)
        => !actor.Contact && TravelSeconds(actor, point) + RunTuning.ReplacementMarginSeconds <= MathF.Min(seconds, human ? RunTuning.HumanHorizon : RunTuning.PursuitHorizon);
    internal static (bool Reachable, float Seconds, Vector2 Point) Intercept(DefenderMotion actor, ObservedRun carrier, bool human)
    {
        if (!actor.Contact && Vector2.Distance(actor.Position, carrier.Position) <= actor.Radius + 1)
            return (true, 0, DefenderMotor.Clamp(carrier.Position));
        for (float t = .1f; t <= (human ? RunTuning.HumanHorizon : RunTuning.PursuitHorizon) + .001f; t += .1f)
        {
            Vector2 point = DefenderMotor.Clamp(carrier.Position + carrier.Velocity * t);
            if (CanReach(actor, point, t, human)) return (true, t, point);
        }
        return (false, float.PositiveInfinity, DefenderMotor.Clamp(carrier.Position + carrier.Velocity * RunTuning.PursuitHorizon));
    }
}
