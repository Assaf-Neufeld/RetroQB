using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.Gameplay.Defense;

public sealed record DefensiveOutcome(string DefenseId, string PackageId, string CallId, string RequestedCallId,
    string FrontId, DefensePackage Personnel, string PressureId, string PlanVersion,
    IReadOnlyList<DefenderSlot> ActualRushers, float? PressureArrivalSeconds, float? ThrowSeconds,
    FieldSide? EscapeSide, bool ScreenObserved, IReadOnlyList<DefenderSlot> HumanAbandonments)
{
    public IReadOnlyList<string> PressureTransitions { get; init; } = Array.Empty<string>();
}

public sealed record DefensiveHistory(int Samples, float QuickRate, float SlowRate, float ScreenRate,
    float EscapeRate, FieldSide EscapeSide);

/// <summary>Team-owned bounded history of completed observations. It never consumes an offensive call ID.</summary>
public sealed class DefensiveStrategyMemory
{
    private readonly Queue<DefensiveOutcome> _recent = new();
    private readonly Dictionary<string, float> _coverage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _pressure = new(StringComparer.Ordinal);
    private readonly HashSet<long> _recorded = new();
    public int CompletedPlays => _recorded.Count;
    public DefensiveHistory History
    {
        get
        {
            float count = Math.Max(1,_recent.Count);
            int left = _recent.Count(o=>o.EscapeSide==FieldSide.Left), right = _recent.Count(o=>o.EscapeSide==FieldSide.Right);
            return new(_recent.Count,_recent.Count(o=>o.ThrowSeconds is <= 1.5f)/count,
                _recent.Count(o=>o.ThrowSeconds is >= 2.5f)/count,_recent.Count(o=>o.ScreenObserved)/count,
                (left+right)/count,left>right?FieldSide.Left:FieldSide.Right);
        }
    }
    public float CoverageAdjustment(string key) => _coverage.GetValueOrDefault(key);
    public float PressureAdjustment(string key) => _pressure.GetValueOrDefault(key);
    public float Adjustment(string key) => Math.Clamp(CoverageAdjustment(key)+PressureAdjustment(key),-.6f,.6f);
    public void Record(long playId, DefensiveOutcome outcome, float gain, PlayEndReason reason)
    {
        if (!_recorded.Add(playId)) return;
        _recent.Enqueue(outcome); if(_recent.Count>12) _recent.Dequeue();
        // User departures remain visible in history but are not learned as an AI package defect.
        if(outcome.HumanAbandonments.Count>0) return;
        float rate=.12f*MathF.Min(1,CompletedPlays/6f);
        float coverage=reason==PlayEndReason.Touchdown?-1:Math.Clamp((3-gain)/15,-1,1);
        Update(_coverage,outcome.PackageId,coverage*rate);
        // A plain incompletion supplies no evidence that a called blitz reached the QB.
        float pressure=outcome.PressureArrivalSeconds is { } at && (outcome.ThrowSeconds==null||at<=outcome.ThrowSeconds)
            ? reason==PlayEndReason.Sack?1:.5f : 0;
        if(outcome.EscapeSide!=null) pressure-=.7f;
        Update(_pressure,outcome.PackageId,pressure*rate);
    }
    private static void Update(Dictionary<string,float> map,string key,float delta)
    {
        if(!map.ContainsKey(key)&&map.Count>=256) map.Remove(map.Keys.First());
        map[key]=Math.Clamp(map.GetValueOrDefault(key)+delta,-.4f,.4f);
    }
    public void Reset() { _recent.Clear();_coverage.Clear();_pressure.Clear();_recorded.Clear(); }
}

/// <summary>Accumulates actual participation and observable releases without inferring outcomes from labels.</summary>
internal sealed class DefensiveOutcomeTracker(ResolvedDefensivePlan plan, string defenseId)
{
    private readonly HashSet<DefenderSlot> _rushers = new(), _abandoned = new();
    private float? _arrival, _throw;
    private FieldSide? _escape;
    private bool _screen;
    private readonly List<string> _transitions = new();
    public void Observe(DefensiveDecisionFrame frame, BallState ball, DefenderSlot? human)
    {
        if(ball==BallState.InAir) _throw??=frame.Seconds;
        if(frame.Observation.Visible.Ball.State==BallState.HeldByQB)
        {
            foreach(var job in frame.Pressure?.Jobs.Where(j=>j.Participating) ?? [])
            {
                _rushers.Add(job.Actor);
                var actor=frame.Observation.Visible.Defenders.Single(a=>a.Id==job.Actor);
                if(!actor.Contact&&System.Numerics.Vector2.Distance(actor.Position,frame.Observation.Visible.Quarterback)<=2.5f) _arrival??=frame.Seconds;
            }
            var qb=frame.Observation.Visible.Quarterback;
            if(qb.X<plan.Surface.Gaps[0].X||qb.X>plan.Surface.Gaps[^1].X||qb.Y>plan.Offense.LineOfScrimmage+1)
                _escape??=qb.X<plan.Surface.CenterX?FieldSide.Left:FieldSide.Right;
        }
        _screen|=frame.Pressure?.Jobs.Any(j=>j.Stage==PressureStage.Screen)==true;
        foreach(var transition in frame.Events.Where(e=>e.StartsWith("PRESS-08:")||e.StartsWith("PRESS-09:")))
            if(_transitions.Count<24&&(_transitions.Count==0||_transitions[^1]!=transition)) _transitions.Add(transition);
        if(human is { } h)
        {
            if(frame.Coverage?.Commitments.Any(c=>c.Actor==h&&c.HumanAbandonment)==true) _abandoned.Add(h);
            var intent=frame.Decisions.Single(i=>i.Actor==h); var self=frame.Observation.Visible.Defenders.Single(a=>a.Id==h);
            if(intent.Primary is { } id&&!frame.Observation.ConfirmedCarrier)
            {
                var receiver=frame.Observation.Receivers.Single(r=>r.Id==id);
                // Record an exposed primary only when observed manual motion is away from its live cue.
                if(System.Numerics.Vector2.Distance(self.Position,receiver.Position)>self.Speed
                    &&System.Numerics.Vector2.Dot(self.Velocity,intent.Target-self.Position)<-.25f) _abandoned.Add(h);
            }
        }
    }
    public DefensiveOutcome Complete() => new(defenseId,DefensivePlaycaller.PackageId(plan),plan.Call.Id,plan.RequestedCall.Id,
        plan.FrontId,plan.Package,DefensivePlaycaller.PressureId(plan),plan.RuleVersion,
        Array.AsReadOnly(_rushers.Order().ToArray()),_arrival,_throw,_escape,_screen,Array.AsReadOnly(_abandoned.Order().ToArray()))
        { PressureTransitions=Array.AsReadOnly(_transitions.ToArray()) };
}
