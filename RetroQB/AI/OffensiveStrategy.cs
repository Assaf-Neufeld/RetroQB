using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.AI;

public sealed record OffensiveCallContext(PlaySituation Situation, float ScoreMargin = 0,
    double SecondsRemaining = 120, bool LateGame = false, bool Overtime = false, int Timeouts = 3);

/// <summary>Only own snapped calls and completed-play observations enter offensive memory.</summary>
public sealed class OffensiveStrategyMemory
{
    private readonly Queue<string> _recent = new();
    private long _lastObserved;
    public IReadOnlyList<string> Recent => _recent.Reverse().ToArray();
    public float Pressure { get; private set; }
    public float TightCoverage { get; private set; }
    public int ObservedPlays { get; private set; }
    public void RecordCall(string id) { _recent.Enqueue(id); while (_recent.Count > 8) _recent.Dequeue(); }
    public void ObserveCompleted(long id, float pressure, float tightCoverage)
    {
        if (id <= _lastObserved) return;
        _lastObserved = id; ObservedPlays++;
        float alpha = ObservedPlays == 1 ? 1 : .25f;
        Pressure += (Math.Clamp(pressure, 0, 1) - Pressure) * alpha;
        TightCoverage += (Math.Clamp(tightCoverage, 0, 1) - TightCoverage) * alpha;
    }
    public void Reset() { _recent.Clear(); _lastObserved = 0; Pressure = TightCoverage = 0; ObservedPlays = 0; }
}

/// <summary>Shared own-call scoring; no defensive definitions, assignments or call IDs are accepted.</summary>
public static class OffensiveCallScoring
{
    public static float Weight(ResolvedPlay play, OffensiveCallContext context, OffensiveTeamAttributes roster,
        OffensiveStrategyMemory? memory = null)
    {
        var situation = context.Situation;
        float fit = PlaySuggestion.GetPlayWeight(play, situation);
        if (play.Family == PlayType.Run)
        {
            bool edge = play.RunConcept is RunConcept.Stretch or RunConcept.Sweep;
            fit *= .6f + roster.BlockingStrength * .4f + (edge ? roster.Skills.RbSpeed : roster.Skills.RbPower) * .6f;
            if (play.Personnel.Slots.Any(s => s is ReceiverSlot.FB or ReceiverSlot.RB2)) fit *= .85f + roster.Skills.OlStrength * .3f;
        }
        else
        {
            var primary = play.Reads.FirstOrDefault();
            if (primary != null)
            {
                var route = play.Assignments[primary.Slot];
                float depth = route.RouteDefinition == null ? 10 : route.RouteDefinition.Steps.Max(s => s.Offset.Y) - route.RouteDefinition.OriginDepth;
                fit *= .7f + roster.GetReceiverCatchingAbility(primary.Slot) * .5f;
                if (depth > 15) fit *= .6f + roster.Skills.QbThrowPower * .7f + roster.Skills.WrSpeed * .3f;
                if (situation.IsMustConvert && depth < situation.Distance - 3) fit *= .7f;
            }
            if (play.Screen != null)
                fit *= (situation.IsLongYardage ? .6f : 1.15f) * (1 + (memory?.Pressure ?? 0) * 1.7f)
                    * (situation.HasFieldPosition && situation.LineOfScrimmage < FieldGeometry.EndZoneDepth + 4 ? .4f : 1);
            else if (play.QuarterbackPath != null) fit *= .8f + roster.GetQbMaxSpeed() / Constants.QbMaxSpeed * .35f;
            else if (play.Protection.Mode == ProtectionMode.MaxProtect) fit *= 1 + (memory?.Pressure ?? 0) * .65f;
            if (play.Definition.Info.Category == PlayCategory.Crossers) fit *= 1 + (memory?.TightCoverage ?? 0) * .35f;
        }
        if (context.LateGame && !context.Overtime && context.ScoreMargin > 0 && context.SecondsRemaining < 90)
            fit *= play.Family == PlayType.Run ? 1.8f : play.InfoCategory() == PlayCategory.Quick ? .8f : .5f;
        if (context.LateGame && !context.Overtime && context.ScoreMargin <= 0 && context.SecondsRemaining < 60)
        {
            bool clockSafe = play.Assignments.Values.Any(a => a.Route is RouteType.OutDeep or RouteType.OutShallow or RouteType.Flat);
            fit *= play.Family == PlayType.Run ? context.Timeouts == 0 ? .3f : .6f : clockSafe ? 1.25f : 1;
            if (play.Backfield.Action == BackfieldAction.PlayAction) fit *= .65f;
        }
        if (memory != null)
        {
            int index = memory.Recent.ToList().IndexOf(play.Id);
            fit *= index switch { 0 => .16f, 1 => .4f, >= 2 => .7f, _ => 1 };
        }
        return MathF.Max(.01f, fit * (play.IsWildcard ? .2f : 1));
    }
    private static PlayCategory InfoCategory(this ResolvedPlay play) => play.Definition.Info.Category;
}
