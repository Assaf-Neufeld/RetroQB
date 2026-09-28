using System.Numerics;

namespace RetroQB.Gameplay.Defense;

public enum SpotZoneFamily { None, Cover2, Cover3, Buzz, Cover4 }
public enum SpotZoneRole { Flat, Hook, CurlFlat, DeepHalf, DeepThird, DeepQuarter }
public sealed record ZoneArea(float Left, float Right, float Shallow, float Deep)
{
    public bool Contains(Vector2 p) => p.X >= Left && p.X <= Right && p.Y >= Shallow && p.Y <= Deep;
    public float LateralExit(Vector2 p) => MathF.Max(0, MathF.Max(Left - p.X, p.X - Right));
}
public sealed record ZoneResponsibility(SpotZoneRole Role, ZoneArea Area, Vector2 Home,
    IReadOnlyList<DefenderSlot> Neighbors, bool Rotates = false) : PassResponsibility
{
    public bool IsDeep => Role is SpotZoneRole.DeepHalf or SpotZoneRole.DeepThird or SpotZoneRole.DeepQuarter;
}

/// <summary>Spot-area allocations are authored independently from legacy zone targeting and match rules.</summary>
internal static class SpotZoneCompiler
{
    public static ResolvedDefensivePlan Compile(OffensiveAlignmentSnapshot offense, DefensiveCallDefinition call,
        OffensiveSurface surface, DefensePackage package, int seed, IEnumerable<DefensiveAssignment> front, PressureSelection? pressure = null)
    {
        var jobs = front.ToList(); float width = Constants.FieldWidth, los = offense.LineOfScrimmage;
        float scale = FieldGeometry.CoverageDepthScale(los);
        float Y(float depth) => Math.Clamp(los + depth * scale, los + .1f, Constants.FieldLength - Constants.DefenderRadius);
        DefenderSlot middle = package == DefensePackage.Base ? DefenderSlot.MLB : DefenderSlot.NB;
        pressure ??= PressureSelection.For(call);
        if (pressure.Kind is PressureKind.ZoneFive or PressureKind.Simulated)
        {
            bool left = pressure.Side == FieldSide.Left;
            var drop = left ? DefenderSlot.DE1 : DefenderSlot.DE2;
            var farLinebacker = left ? DefenderSlot.OLB2 : DefenderSlot.OLB1;
            Add(DefenderSlot.CB1, SpotZoneRole.DeepThird, 0, width / 3, 12);
            Add(DefenderSlot.FS, SpotZoneRole.DeepThird, width / 3, width * 2 / 3, 16);
            Add(DefenderSlot.CB2, SpotZoneRole.DeepThird, width * 2 / 3, width, 12);
            float edge = pressure.Kind == PressureKind.ZoneFive ? width / 3 : width / 4;
            Add(drop, SpotZoneRole.CurlFlat, left ? 0 : width - edge, left ? edge : width, 5);
            Add(DefenderSlot.SS, SpotZoneRole.CurlFlat, left ? width - edge : 0, left ? width : edge, 6);
            if (pressure.Kind == PressureKind.ZoneFive) Add(farLinebacker, SpotZoneRole.Hook, edge, width - edge, 7);
            else
            {
                Add(middle, SpotZoneRole.Hook, left ? edge : width / 2, left ? width / 2 : width - edge, 7);
                Add(farLinebacker, SpotZoneRole.Hook, left ? width / 2 : edge, left ? width - edge : width / 2, 7);
            }
        }
        else if (call.Zone == SpotZoneFamily.Cover2)
        {
            Add(DefenderSlot.FS, SpotZoneRole.DeepHalf, 0, width / 2, 14);
            Add(DefenderSlot.SS, SpotZoneRole.DeepHalf, width / 2, width, 14);
            Add(DefenderSlot.CB1, SpotZoneRole.Flat, 0, 10, 4);
            Add(DefenderSlot.OLB1, SpotZoneRole.Hook, 10, 21, 7);
            Add(middle, SpotZoneRole.Hook, 21, width - 21, 8);
            Add(DefenderSlot.OLB2, SpotZoneRole.Hook, width - 21, width - 10, 7);
            Add(DefenderSlot.CB2, SpotZoneRole.Flat, width - 10, width, 4);
        }
        else if (call.Zone is SpotZoneFamily.Cover3 or SpotZoneFamily.Buzz)
        {
            Add(DefenderSlot.CB1, SpotZoneRole.DeepThird, 0, width / 3, 12);
            Add(DefenderSlot.FS, SpotZoneRole.DeepThird, width / 3, width * 2 / 3, 16);
            Add(DefenderSlot.CB2, SpotZoneRole.DeepThird, width * 2 / 3, width, 12);
            Add(DefenderSlot.OLB1, SpotZoneRole.CurlFlat, 0, width / 4, 6);
            Add(middle, SpotZoneRole.Hook, width / 4, width / 2, 8);
            Add(DefenderSlot.OLB2, SpotZoneRole.Hook, width / 2, width * 3 / 4, 8);
            Add(DefenderSlot.SS, SpotZoneRole.CurlFlat, width * 3 / 4, width, 6, call.Zone == SpotZoneFamily.Buzz);
        }
        else
        {
            Add(DefenderSlot.CB1, SpotZoneRole.DeepQuarter, 0, width / 4, 12);
            Add(DefenderSlot.FS, SpotZoneRole.DeepQuarter, width / 4, width / 2, 14);
            Add(DefenderSlot.SS, SpotZoneRole.DeepQuarter, width / 2, width * 3 / 4, 14);
            Add(DefenderSlot.CB2, SpotZoneRole.DeepQuarter, width * 3 / 4, width, 12);
            Add(DefenderSlot.OLB1, SpotZoneRole.CurlFlat, 0, width / 3, 6);
            Add(middle, SpotZoneRole.Hook, width / 3, width * 2 / 3, 7);
            Add(DefenderSlot.OLB2, SpotZoneRole.CurlFlat, width * 2 / 3, width, 6);
        }
        // Neighbors share an area boundary or overlap at a different depth. Copy the table into the immutable plan.
        var zones = jobs.Where(a => a.Pass is ZoneResponsibility).ToArray();
        jobs = jobs.Select(a => a.Pass is ZoneResponsibility z ? a with { Pass = z with
        {
            Neighbors = Array.AsReadOnly(zones.Where(b => b.ActorId != a.ActorId && b.Pass is ZoneResponsibility other
                && z.Area.Left <= other.Area.Right + .01f && z.Area.Right >= other.Area.Left - .01f)
                .Select(b => b.ActorId).Order().ToArray())
        }} : a).ToList();
        // Compressed fields can put shell/underneath actors at similar depths. Separate without changing jobs.
        var placed = new List<DefensiveAssignment>();
        foreach (var job in jobs.OrderBy(a => a.Pass is RushResponsibility ? 0 : 1).ThenBy(a => a.ActorId))
        {
            Vector2 p = job.Alignment;
            if (placed.Any(a => Vector2.Distance(a.Alignment, p) < DefensivePlanCompiler.MinimumSpacing))
            {
                var options = from y in Enumerable.Range(0, 13) from x in Enumerable.Range(-12, 25)
                    let candidate = p + new Vector2(x * .5f, y * .5f)
                    where DefensivePlanCompiler.LegalAlignment(candidate, los)
                        && placed.All(a => Vector2.Distance(a.Alignment, candidate) >= DefensivePlanCompiler.MinimumSpacing)
                    orderby MathF.Abs(x) * 2 + y, x select candidate;
                p = options.Cast<Vector2?>().FirstOrDefault() ?? throw new InvalidOperationException("No legal zone alignment.");
            }
            placed.Add(job with { Alignment = p });
        }
        var plan = new ResolvedDefensivePlan(call, offense, surface, package, seed, RunFitCompiler.Bind(surface, placed).Assignments, 0, pressure: pressure);
        DefensivePlanValidator.ThrowIfInvalid(plan); return plan;

        void Add(DefenderSlot slot, SpotZoneRole role, float left, float right, float depth, bool rotates = false)
        {
            bool deep = role is SpotZoneRole.DeepHalf or SpotZoneRole.DeepThird or SpotZoneRole.DeepQuarter;
            var home = new Vector2((left + right) / 2, Y(depth));
            var area = new ZoneArea(left, right, deep ? Y(7) : los - 2, deep ? Constants.FieldLength - Constants.DefenderRadius : Y(12));
            // Buzz starts above its home and physically travels down/out; it is never counted as a fourth deep role.
            Vector2 alignment = rotates ? new(home.X - 3, Y(12)) : home;
            if (slot is DefenderSlot.DE1 or DefenderSlot.DE2)
            {
                float x = slot == DefenderSlot.DE1 ? surface.Gaps.Min(g => g.X) : surface.Gaps.Max(g => g.X);
                alignment = DefenderMotor.Clamp(new(x, Y(1.5f)));
                home.X = Math.Clamp(x, left + 1.3f, right - 1.3f);
            }
            jobs.Add(new(slot, slot.GetPositionType(), alignment, new ZoneResponsibility(role, area, home, Array.Empty<DefenderSlot>(), rotates),
                new(deep ? RunRole.DeepSupport : RunRole.Pursuit, home.X < width / 2 ? FieldSide.Left : FieldSide.Right, RunTrigger.ObservedRunCommitment)));
        }
    }
}
