using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public enum DefensiveEngineMode { Legacy, Cover1Development, ManDevelopment, ZoneDevelopment, MatchDevelopment }

public static class DefensiveEngineCalls
{
    public static IEnumerable<DefensiveCallDefinition> For(DefensiveEngineMode mode) => mode switch
    {
        DefensiveEngineMode.MatchDevelopment => DefensiveCallDefinition.All,
        DefensiveEngineMode.ZoneDevelopment => DefensiveCallDefinition.Supported.Concat(DefensiveCallDefinition.SpotZones),
        DefensiveEngineMode.ManDevelopment => DefensiveCallDefinition.Supported,
        _ => DefensiveCallDefinition.Supported.Where(c => c.Id == "def.cover1")
    };
}

public static class DefensivePlanPresentation
{
    public static string DescribeAssignment(ResolvedDefensivePlan? plan, DefenderSlot actor, string fallback)
        => plan?.Assignments.Single(a => a.ActorId == actor).Pass is MatchResponsibility match
            ? match.Provisional is { } id ? $"Match: receiver {id.Value + 1}" : match.Deep ? "Deep match help; no primary" : "Underneath help; no primary"
            : fallback;
    public static string PressureSummary(ResolvedDefensivePlan plan, DefensiveDecisionFrame? frame)
    {
        string name = plan.Pressure.Selection.Kind switch
        {
            PressureKind.Base => "Four-man rush", PressureKind.ZoneFive => $"Zone pressure / {plan.Pressure.Selection.Side}",
            PressureKind.Simulated => $"Simulated / {plan.Pressure.Selection.Side}",
            PressureKind.GreenDog => $"Green dog / {plan.Pressure.Selection.Side}",
            PressureKind.Delayed => $"Delayed / {plan.Pressure.Selection.Side}", PressureKind.Stunt => $"Stunt / {plan.Pressure.Selection.Side}",
            _ => $"{plan.Pressure.Selection.Kind} / {plan.Pressure.Selection.Side}"
        };
        return $"{DefensivePersonnelRoles.Label(plan.Package)} | {plan.Front}\n{name}\nRush: {plan.Pressure.Entries.Count}" + (plan.Pressure.Conditional != null ? " + 1 conditional" : " called")
            + (frame?.Pressure is { } p ? $"\nActive rushers: {p.ActualCount}" : "");
    }
    public static string Describe(DefensiveIntent intent) => intent.Kind == DefensiveIntentKind.Pursuit ? "Pursue on a cutoff angle"
        : intent.Kind == DefensiveIntentKind.Tackle ? "Attack the ball carrier"
        : intent.Kind == DefensiveIntentKind.Force ? "Force the runner inside"
        : intent.Kind == DefensiveIntentKind.Cutback ? "Protect the cutback lane"
        : intent.Kind == DefensiveIntentKind.LastSupport ? "Stay between the runner and the end zone"
        : intent.Kind == DefensiveIntentKind.GapFit ? "Fit your assigned gap"
        : intent.Kind == DefensiveIntentKind.Spy ? "Spy the quarterback"
        : intent.Kind == DefensiveIntentKind.ReadStep ? "Read the exchange; retain coverage"
        : intent.Primary is { } primary ? $"Primary: receiver {primary.Value + 1} ({intent.Kind})"
        : intent.Helping is { } helper ? $"Help: receiver {helper.Value + 1} ({intent.Kind})" : intent.Kind.ToString();
    // Read-only compatibility projection for existing menus/replay labels. Never used to spawn a compiled defense.
    public static ResolvedDefensivePlay Project(ResolvedDefensivePlan plan, IReadOnlyList<Defender> actors)
        => new(Definition(plan), plan.Assignments.Select(a =>
        {
            var actor = actors.Single(d => d.Slot == a.ActorId);
            int man = (a.Pass as ManResponsibility)?.Target.Value ?? (a.Pass as MatchResponsibility)?.Provisional?.Value ?? -1;
            CoverageRole zone = a.Pass is HelpResponsibility help ? help.Role switch
            {
                HelperRole.DeepLeft => CoverageRole.DeepLeft, HelperRole.DeepRight => CoverageRole.DeepRight,
                HelperRole.DeepMiddle => CoverageRole.DeepMiddle, HelperRole.Robber => CoverageRole.Robber, _ => CoverageRole.HookMiddle
            } : a.Pass is ZoneResponsibility spot ? spot.Role switch
            {
                SpotZoneRole.DeepHalf => spot.Home.X < Constants.FieldWidth / 2 ? CoverageRole.DeepLeft : CoverageRole.DeepRight,
                SpotZoneRole.DeepQuarter => spot.Home.X < Constants.FieldWidth / 2 ? CoverageRole.DeepQuarterLeft : CoverageRole.DeepQuarterRight,
                SpotZoneRole.DeepThird => a.ActorId == DefenderSlot.FS ? CoverageRole.DeepMiddle : spot.Home.X < Constants.FieldWidth / 2 ? CoverageRole.DeepLeft : CoverageRole.DeepRight,
                SpotZoneRole.Flat or SpotZoneRole.CurlFlat => spot.Home.X < Constants.FieldWidth / 2 ? CoverageRole.FlatLeft : CoverageRole.FlatRight,
                _ => CoverageRole.HookMiddle
            } : a.Pass is MatchResponsibility { Provisional: null } ? CoverageRole.HookMiddle : CoverageRole.None;
            Vector2 target = man >= 0 ? plan.Offense.Skills.Single(r => r.Id.Value == man).Position
                : a.Pass is HelpResponsibility { BracketTarget: { } bracket } ? plan.Offense.Skills.Single(r => r.Id == bracket).Position
                : a.Pass is RushResponsibility rush ? new(plan.Surface.Gaps.Single(g => g.Index == rush.GapIndex).X, plan.Offense.LineOfScrimmage - 2)
                : a.Pass is ZoneResponsibility area ? area.Home : a.Alignment;
            return new Gameplay.DefensiveAssignment(a.ActorId, a.PositionRole, a.Alignment, a.Pass is RushResponsibility, man, zone,
                false, 0, 0, target, actor.SpeedMultiplier, actor.TackleMultiplier, actor.InterceptionMultiplier, actor.BlockShedMultiplier, actor.IsStarPlayer);
        }));
    private static DefensivePlayDefinition Definition(ResolvedDefensivePlan plan)
    {
        var actual = DefensivePlaybook.Get(plan.Call.Id);
        return plan.FallbackReason != null ? actual with { Name = actual.Name + " (Lock fallback)", Coaching = actual.Name + " fallback: " + plan.FallbackReason }
            : plan.Match != null ? actual with { Coaching = actual.Coaching + " " + plan.Match.Check } : actual;
    }
}
