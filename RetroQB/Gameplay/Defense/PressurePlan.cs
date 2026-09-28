using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public enum PressureKind { Base, Mike, Edge, Nickel, Zero, ZoneFive, Simulated }
public sealed record PressureSelection(PressureKind Kind, FieldSide Side = FieldSide.Left)
{
    public static PressureSelection For(DefensiveCallDefinition call) => new(call.Id == "def.edge" ? PressureKind.Edge
        : call.Id == "def.zero" ? PressureKind.Zero : PressureKind.Base);
    public PressureSelection Flip() => this with { Side = Side == FieldSide.Left ? FieldSide.Right : FieldSide.Left };
}

public sealed record PressureEntry(DefenderSlot Actor, int Gap, int? AlternateGap, ContainRole Contain, Vector2 Entry);
public sealed record PressureDrop(DefenderSlot Actor, Vector2 Home, float ArrivalWindow);
public sealed record PressurePlan(PressureSelection Selection, IReadOnlyList<PressureEntry> Entries, IReadOnlyList<PressureDrop> Drops);

/// <summary>Authors a complete rush budget before coverage pairing; no receiver owner is borrowed at runtime.</summary>
internal static class PressureCompiler
{
    public static void Bind(OffensiveAlignmentSnapshot offense, OffensiveSurface surface, DefensePackage package,
        DefensiveCallDefinition call, PressureSelection selection, IDictionary<DefenderSlot, DefensiveAssignment> jobs)
    {
        if (!Enum.IsDefined(selection.Kind) || selection.Side is not (FieldSide.Left or FieldSide.Right))
            throw new ArgumentException("Unknown pressure package or side.");
        bool zonePressure = call.Zone == SpotZoneFamily.Cover3 && selection.Kind is PressureKind.ZoneFive or PressureKind.Simulated;
        if (!zonePressure && (call.RushCount == 4 && selection.Kind != PressureKind.Base
            || call.RushCount == 5 && selection.Kind is not (PressureKind.Mike or PressureKind.Edge or PressureKind.Nickel)
            || call.RushCount == 6 && selection.Kind != PressureKind.Zero))
            throw new ArgumentException("Pressure variant does not fit the called coverage budget.");
        if (selection.Kind == PressureKind.Nickel && package != DefensePackage.Nickel)
            throw new ArgumentException("Nickel pressure requires the nickel roster.");

        var gaps = surface.Gaps.OrderBy(g => g.X).ToArray();
        bool left = selection.Side == FieldSide.Left;
        var nearEnd = left ? DefenderSlot.DE1 : DefenderSlot.DE2;
        var farEnd = left ? DefenderSlot.DE2 : DefenderSlot.DE1;
        var nearTackle = left ? DefenderSlot.DT1 : DefenderSlot.DT2;
        var farTackle = left ? DefenderSlot.DT2 : DefenderSlot.DT1;
        var ordered = left ? gaps : gaps.Reverse().ToArray();
        Add(farEnd, ordered[^1], left ? ContainRole.RightEdge : ContainRole.LeftEdge);
        if (zonePressure)
        {
            Add(left ? DefenderSlot.OLB1 : DefenderSlot.OLB2, ordered[0], left ? ContainRole.LeftEdge : ContainRole.RightEdge);
            Add(nearTackle, ordered[1]); Add(farTackle, ordered[^2]);
            if (selection.Kind == PressureKind.ZoneFive) Add(package == DefensePackage.Base ? DefenderSlot.MLB : DefenderSlot.NB, ordered[2]);
            return;
        }
        if (selection.Kind == PressureKind.Base)
        {
            Add(nearEnd, ordered[0], left ? ContainRole.LeftEdge : ContainRole.RightEdge);
            Add(nearTackle, ordered[1]); Add(farTackle, ordered[^2]); return;
        }
        DefenderSlot extra = selection.Kind == PressureKind.Nickel ? DefenderSlot.NB
            : selection.Kind == PressureKind.Mike ? package == DefensePackage.Base ? DefenderSlot.MLB : DefenderSlot.OLB1
            : left ? DefenderSlot.OLB1 : DefenderSlot.OLB2;
        if (selection.Kind is PressureKind.Edge or PressureKind.Nickel)
        {
            Add(extra, ordered[0], left ? ContainRole.LeftEdge : ContainRole.RightEdge);
            Add(nearEnd, ordered[1]); Add(nearTackle, ordered[2]); Add(farTackle, ordered[^2]);
        }
        else
        {
            Add(nearEnd, ordered[0], left ? ContainRole.LeftEdge : ContainRole.RightEdge);
            Add(nearTackle, ordered[1]); Add(farTackle, ordered[^2]);
            var free = ordered.Where(g => jobs.Values.All(j => ((RushResponsibility)j.Pass).GapIndex != g.Index)).ToArray();
            Add(extra, free.MinBy(g => MathF.Abs(g.X - surface.CenterX))!);
            if (selection.Kind == PressureKind.Zero)
            {
                var second = package == DefensePackage.Base ? DefenderSlot.MLB : left ? DefenderSlot.OLB2 : DefenderSlot.OLB1;
                Add(second, ordered.First(g => jobs.Values.All(j => ((RushResponsibility)j.Pass).GapIndex != g.Index)));
            }
        }
        void Add(DefenderSlot actor, LineGap gap, ContainRole contain = ContainRole.None)
        {
            var p = new Vector2(Math.Clamp(gap.X, Constants.DefenderRadius, Constants.FieldWidth - Constants.DefenderRadius),
                offense.LineOfScrimmage + 1.5f * FieldGeometry.CoverageDepthScale(offense.LineOfScrimmage));
            jobs.Add(actor, new(actor, actor.GetPositionType(), p, new RushResponsibility(gap.Index, contain),
                new(contain == ContainRole.None ? RunRole.Gap : RunRole.Force, gap.Side, RunTrigger.ObservedRunCommitment, gap.Index)));
        }
    }

    public static PressurePlan Describe(OffensiveAlignmentSnapshot offense, OffensiveSurface surface,
        IEnumerable<DefensiveAssignment> assignments, PressureSelection selection)
    {
        var rushers = assignments.Where(a => a.Pass is RushResponsibility).ToArray();
        var occupied = rushers.Select(a => ((RushResponsibility)a.Pass).GapIndex).ToHashSet();
        return new(selection, Array.AsReadOnly(rushers.Select(a =>
        {
            var r = (RushResponsibility)a.Pass; var gap = surface.Gaps.Single(g => g.Index == r.GapIndex);
            var alternate = r.Contain == ContainRole.None ? surface.Gaps.Where(g => !occupied.Contains(g.Index) && !g.Outside
                && Math.Abs(g.Index - gap.Index) == 1).OrderBy(g => MathF.Abs(g.X - gap.X)).ThenBy(g => g.Index).FirstOrDefault() : null;
            return new PressureEntry(a.ActorId, gap.Index, alternate?.Index, r.Contain,
                DefenderMotor.Clamp(new(gap.X, offense.LineOfScrimmage - 1.5f)));
        }).OrderBy(e => e.Actor).ToArray()), Array.AsReadOnly(assignments.Where(a => a.Pass is ZoneResponsibility && a.PositionRole == DefensivePosition.DE)
            .Select(a => new PressureDrop(a.ActorId, ((ZoneResponsibility)a.Pass).Home, 2.5f)).ToArray()));
    }

    public static void ValidateDrops(ResolvedDefensivePlan plan, IEnumerable<Defender> actors)
    {
        foreach (var drop in plan.Pressure.Drops)
        {
            var actor = actors.Single(a => a.Slot == drop.Actor);
            var motion = new DefenderMotion(actor.Slot, actor.Position, Vector2.Zero, actor.Speed, actor.Radius, actor.InterceptionMultiplier, false);
            if (actor.PositionRole != DefensivePosition.DE || actor.InterceptionMultiplier < .7f
                || RunFitCoordinator.TravelSeconds(motion, drop.Home) + .1f > drop.ArrivalWindow)
                throw new InvalidOperationException($"{drop.Actor} cannot reach the declared drop with this roster; choose another pressure variant.");
        }
    }
}

public static class PressureVariants
{
    public static IReadOnlyList<PressureSelection> Available(ResolvedDefensivePlan plan, DefensiveTeamAttributes? team = null)
    {
        var kinds = plan.Call.Zone == SpotZoneFamily.Cover3 ? new[] { PressureKind.Base, PressureKind.ZoneFive, PressureKind.Simulated }
            : plan.Call.RushCount == 5 ? new[] { PressureKind.Mike, PressureKind.Edge, PressureKind.Nickel }
            : new[] { PressureSelection.For(plan.Call).Kind };
        var result = new List<PressureSelection>();
        foreach (var kind in kinds)
        {
            var selection = new PressureSelection(kind, plan.Pressure.Selection.Side);
            try
            {
                var candidate = DefensivePlanCompiler.Compile(plan.Offense, plan.Call, plan.Package, plan.ResolutionSeed, plan.Adjustments, selection);
                PressureCompiler.ValidateDrops(candidate, candidate.Assignments.Select(a => new Defender(a.Alignment, a.PositionRole, a.ActorId, team)));
                result.Add(selection);
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }
        return result.AsReadOnly();
    }
}
