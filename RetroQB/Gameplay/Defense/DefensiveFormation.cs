using System.Numerics;

namespace RetroQB.Gameplay.Defense;

public enum DefensiveFront { Even, Over, Under, Wide, ShortYardage }

/// <summary>Stable personnel slots bind functional roles; a dime back never impersonates a linebacker.</summary>
public static class DefensivePersonnelRoles
{
    public static DefenderSlot Middle(DefensePackage package) => package == DefensePackage.Nickel ? DefenderSlot.NB : DefenderSlot.MLB;
    public static DefenderSlot Apex(DefensePackage package, bool left) => package == DefensePackage.Dime
        ? left ? DefenderSlot.NB : DefenderSlot.DB6 : left ? DefenderSlot.OLB1 : DefenderSlot.OLB2;
    public static DefenderSlot Controlled(DefensePackage package) => package == DefensePackage.Nickel ? DefenderSlot.OLB1 : DefenderSlot.MLB;
    public static string Label(DefensePackage package) => package switch
    { DefensePackage.Base => "4-3 Base", DefensePackage.Nickel => "4-2-5 Nickel", _ => "4-1-6 Dime" };
}

internal static class DefensiveFrontCompiler
{
    // Added-pressure packages already author their entire front. Keep those entries explicit until
    // a corresponding shifted-pressure template is authored rather than silently erasing either choice.
    public static void Validate(DefensiveFront front, PressureSelection pressure)
    {
        if (!Enum.IsDefined(front)) throw new ArgumentOutOfRangeException(nameof(front));
        if (front != DefensiveFront.Even && pressure.Kind != PressureKind.Base)
            throw new ArgumentException("This pressure package requires the Even front; select Even before adding pressure.");
    }

    public static void Bind(OffensiveAlignmentSnapshot offense, OffensiveSurface surface, DefensiveFront front,
        IDictionary<DefenderSlot, DefensiveAssignment> jobs)
    {
        if (front == DefensiveFront.Even) return;
        bool strongLeft = surface.Strength == FieldSide.Left || surface.Strength == FieldSide.Balanced && surface.ClosedLeft && !surface.ClosedRight;
        var left = surface.Gaps.Where(g => !g.Outside && g.Side == FieldSide.Left).OrderBy(g => g.X).ToArray();
        var right = surface.Gaps.Where(g => !g.Outside && g.Side == FieldSide.Right).OrderBy(g => g.X).ToArray();
        if (left.Length < 2 || right.Length < 2) throw new ArgumentException("This front needs two interior gaps on each side of the ball.");
        foreach (var actor in jobs.Keys.ToArray())
        {
            var job = jobs[actor]; var rush = (RushResponsibility)job.Pass;
            var gap = surface.Gaps.Single(g => g.Index == rush.GapIndex);
            if (actor is DefenderSlot.DT1 or DefenderSlot.DT2)
            {
                bool isLeft = actor == DefenderSlot.DT1;
                bool aGap = front == DefensiveFront.ShortYardage || front == DefensiveFront.Over && isLeft != strongLeft
                    || front == DefensiveFront.Under && isLeft == strongLeft;
                gap = isLeft ? aGap ? left[^1] : left[0] : aGap ? right[0] : right[^1];
            }
            float x = gap.X;
            if (front == DefensiveFront.Wide && gap.Outside)
            {
                // Start beyond attached ends as well as tackles; the named outside gap remains the entry.
                var attached = surface.Threats.Where(t => t.Attached && t.Side == gap.Side)
                    .Select(t => offense.Skills.Single(r => r.Id == t.ActorId).Position.X);
                x = gap.Side == FieldSide.Left ? MathF.Min(x, attached.DefaultIfEmpty(x).Min()) - 2.5f
                    : MathF.Max(x, attached.DefaultIfEmpty(x).Max()) + 2.5f;
            }
            float depth = front == DefensiveFront.ShortYardage ? .6f : 1.5f;
            jobs[actor] = job with { Alignment = DefenderMotor.Clamp(new Vector2(x, offense.LineOfScrimmage + depth * FieldGeometry.CoverageDepthScale(offense.LineOfScrimmage))),
                Pass = rush with { GapIndex = gap.Index } };
        }
    }
}
