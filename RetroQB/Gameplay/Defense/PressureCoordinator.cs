using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public enum PressureStage { Approach, Engage, Counter, Contain, Close, Released }
public sealed record PressureJob(DefenderSlot Actor, int Gap, PressureStage Stage, Vector2 Target,
    ContainRole Contain, bool Planned, bool Participating, bool Manual, string Reason);
public sealed record PressureBoard(int PlannedCount, int ActualCount, IReadOnlyList<PressureJob> Jobs);

/// <summary>Coordinates rush lanes from one observation; all progress still passes through motor and blocking physics.</summary>
public sealed class PressureCoordinator
{
    private sealed class State(int gap)
    {
        public int Gap = gap;
        public float Stall, BestDistance = float.PositiveInfinity, Width;
        public bool Counter, Widening;
    }
    private readonly ResolvedDefensivePlan _plan;
    private readonly Dictionary<DefenderSlot, State> _states;
    private float _seconds;
    public PressureBoard? Current { get; private set; }
    public PressureCoordinator(ResolvedDefensivePlan plan)
    { _plan = plan; _states = plan.Pressure.Entries.ToDictionary(e => e.Actor, e => new State(e.Gap)); }

    public IReadOnlyList<DefensiveIntent> Decide(DefensiveObservationFrame frame, DefenderSlot? human)
    {
        float dt = MathF.Max(0, frame.Seconds - _seconds); _seconds = frame.Seconds;
        var jobs = new List<PressureJob>(); var intents = new List<DefensiveIntent>();
        var occupied = _states.Values.Select(s => s.Gap).ToHashSet();
        foreach (var entry in _plan.Pressure.Entries)
        {
            var actor = frame.Visible.Defenders.Single(a => a.Id == entry.Actor); var state = _states[entry.Actor];
            var qb = frame.Visible.Quarterback; float los = _plan.Offense.LineOfScrimmage;
            var gap = _plan.Surface.Gaps.Single(g => g.Index == state.Gap);
            Vector2 target = new(gap.X, los - 1.5f);
            string reason = "DeclaredEntry;CoverageReplacementCompiled";
            var stage = actor.Contact ? PressureStage.Engage : state.Counter ? PressureStage.Counter : PressureStage.Approach;
            bool release = frame.ConfirmedCarrier || frame.Visible.Ball.State != BallState.HeldByQB;
            if (!release)
            {
                var progressTarget = target;
                if (entry.Contain != ContainRole.None)
                {
                    int edgeSide = entry.Contain == ContainRole.LeftEdge ? -1 : 1;
                    progressTarget = DefenderMotor.Clamp(new(edgeSide < 0 ? MathF.Min(gap.X - state.Width, qb.X - 1.2f)
                        : MathF.Max(gap.X + state.Width, qb.X + 1.2f), qb.Y - .8f));
                }
                float distance = Vector2.Distance(actor.Position, progressTarget);
                if (distance < state.BestDistance - .25f) { state.BestDistance = distance; state.Stall = 0; }
                else state.Stall += dt;
                if (entry.Contain != ContainRole.None)
                {
                    int side = entry.Contain == ContainRole.LeftEdge ? -1 : 1;
                    float boundary = side < 0 ? MathF.Min(gap.X - state.Width, qb.X - 1.2f) : MathF.Max(gap.X + state.Width, qb.X + 1.2f);
                    boundary = Math.Clamp(boundary, Constants.DefenderRadius, Constants.FieldWidth - Constants.DefenderRadius);
                    if (state.Stall >= .55f && actor.Position.Y > qb.Y && actor.Id != human)
                    {
                        state.Width = MathF.Min(9, state.Width + 2.5f); state.Widening = true;
                        state.Stall = 0; state.BestDistance = float.PositiveInfinity;
                    }
                    if (state.Widening && (actor.Position.X - boundary) * side >= -.5f) state.Widening = false;
                    target = state.Widening ? new(boundary, actor.Position.Y)
                        : new(boundary, qb.Y - .8f);
                    stage = state.Widening ? PressureStage.Counter : PressureStage.Contain;
                    reason = state.Widening ? "MOVE-03:WidenSameOutsidePath;RetainContain" : "NearOutsideHip;FarEdgeRetainsReversal";
                    if (!state.Widening && actor.Position.Y <= qb.Y - .4f && (actor.Position.X - qb.X) * side >= 0)
                    { target = qb + new Vector2(side * .6f, -.3f); stage = PressureStage.Close; reason = "OutsideShoulderEstablished;ClosePhysically"; }
                }
                else if (actor.Position.Y <= los - 1)
                { target = qb; stage = PressureStage.Close; reason = "EntryCleared;CompressPocket"; }
                else if (state.Stall >= .55f && !state.Counter && entry.AlternateGap is { } alternate && !occupied.Contains(alternate) && actor.Id != human)
                {
                    occupied.Remove(state.Gap); occupied.Add(alternate); state.Gap = alternate; state.Counter = true;
                    state.Stall = 0; state.BestDistance = float.PositiveInfinity;
                    target = new(_plan.Surface.Gaps.Single(g => g.Index == alternate).X, los - 1.5f);
                    stage = PressureStage.Counter; reason = "MOVE-03:ReserveFreeAdjacentLane";
                }
                else if (actor.Contact) reason = "MOVE-03:Engaged;SharedShedRules;NoFreeCounter";
            }
            else { stage = PressureStage.Released; reason = "ThrowOrCarrier;PressureEnds"; }
            target = DefenderMotor.Clamp(target);
            bool manual = actor.Id == human;
            bool participating = !release && (!manual || actor.Contact || Vector2.Dot(actor.Velocity, target - actor.Position) > .5f);
            string rule = _plan.Pressure.Selection.Kind switch { PressureKind.Mike => "PRESS-02", PressureKind.Edge => "PRESS-03",
                PressureKind.Nickel => "PRESS-04", PressureKind.Zero => "PRESS-05", _ => "PRESS-01" };
            jobs.Add(new(actor.Id, state.Gap, stage, target, entry.Contain, true, participating, manual,
                manual && !participating && !release ? "HUMAN-02:ManualActorNotParticipating" : reason));
            intents.Add(new(actor.Id, entry.Contain == ContainRole.None ? DefensiveIntentKind.Rush : DefensiveIntentKind.Contain,
                target, null, null, rule, jobs[^1].Reason, participating));
        }
        Current = new(_plan.Pressure.Entries.Count, jobs.Count(j => j.Participating), jobs.AsReadOnly());
        return intents.AsReadOnly();
    }
}
