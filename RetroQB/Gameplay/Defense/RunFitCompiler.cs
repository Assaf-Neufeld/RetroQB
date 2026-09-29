using System.Numerics;

namespace RetroQB.Gameplay.Defense;

public sealed record GapFit(int GapIndex, DefenderSlot Owner);
public sealed record RunFitPlan(IReadOnlyList<GapFit> Gaps, DefenderSlot LeftForce, DefenderSlot RightForce,
    DefenderSlot LeftCutback, DefenderSlot RightCutback, DefenderSlot LastSupport);

/// <summary>Conditional jobs use the actual line gaps. No pass ownership or physical alignment is changed.</summary>
internal static class RunFitCompiler
{
    internal static bool DeepPass(PassResponsibility pass) => pass is ZoneResponsibility { IsDeep: true }
        or MatchResponsibility { Deep: true }
        or HelpResponsibility { Role: HelperRole.DeepLeft or HelperRole.DeepMiddle or HelperRole.DeepRight };

    internal static (IReadOnlyList<DefensiveAssignment> Assignments, RunFitPlan Fits) Bind(
        OffensiveSurface surface, IEnumerable<DefensiveAssignment> assignments)
    {
        var jobs = assignments.OrderBy(a => a.ActorId).ToArray();
        var leftForce = jobs.Single(a => a.Pass is RushResponsibility { Contain: ContainRole.LeftEdge }).ActorId;
        var rightForce = jobs.Single(a => a.Pass is RushResponsibility { Contain: ContainRole.RightEdge }).ActorId;
        var last = jobs.Where(a => a.Pass is not RushResponsibility).OrderByDescending(a => DeepPass(a.Pass))
            .ThenByDescending(a => a.Alignment.Y).ThenBy(a => a.ActorId).First().ActorId;
        var gaps = new Dictionary<int, DefenderSlot>();
        foreach (var job in jobs.Where(a => a.Pass is RushResponsibility))
            gaps.Add(((RushResponsibility)job.Pass).GapIndex, job.ActorId);
        foreach (var gap in surface.Gaps.Where(g => !gaps.ContainsKey(g.Index)))
        {
            var owner = jobs.Where(a => a.ActorId != last && !gaps.Values.Contains(a.ActorId))
                .OrderBy(a => DeepPass(a.Pass)).ThenBy(a => MathF.Abs(a.Alignment.X - gap.X))
                .ThenBy(a => a.Alignment.Y).ThenBy(a => a.ActorId).First();
            gaps.Add(gap.Index, owner.ActorId);
        }
        var available = jobs.Where(a => a.ActorId != last && a.ActorId != leftForce && a.ActorId != rightForce).ToArray();
        var left = available.OrderBy(a => gaps.Values.Contains(a.ActorId)).ThenBy(a => DeepPass(a.Pass))
            .ThenBy(a => MathF.Abs(a.Alignment.X - (surface.CenterX - 5))).ThenBy(a => a.ActorId).First().ActorId;
        var right = available.Where(a => a.ActorId != left).OrderBy(a => gaps.Values.Contains(a.ActorId)).ThenBy(a => DeepPass(a.Pass))
            .ThenBy(a => MathF.Abs(a.Alignment.X - (surface.CenterX + 5))).ThenBy(a => a.ActorId).First().ActorId;
        var fits = new RunFitPlan(Array.AsReadOnly(gaps.OrderBy(p => p.Key).Select(p => new GapFit(p.Key, p.Value)).ToArray()),
            leftForce, rightForce, left, right, last);
        var bound = jobs.Select(a =>
        {
            int? gap = fits.Gaps.FirstOrDefault(g => g.Owner == a.ActorId)?.GapIndex;
            RunRole role = a.ActorId == last ? RunRole.DeepSupport : a.ActorId == leftForce || a.ActorId == rightForce ? RunRole.Force
                : a.ActorId == left || a.ActorId == right ? RunRole.Cutback : gap != null ? RunRole.Gap : RunRole.Pursuit;
            FieldSide side = a.ActorId == left || a.ActorId == leftForce ? FieldSide.Left
                : a.ActorId == right || a.ActorId == rightForce ? FieldSide.Right
                : gap is { } index ? surface.Gaps.Single(g => g.Index == index).Side
                : a.Alignment.X < surface.CenterX ? FieldSide.Left : FieldSide.Right;
            return a with { Run = new(role, side, RunTrigger.ObservedRunCommitment, gap) };
        }).ToArray();
        return (Array.AsReadOnly(bound), fits);
    }
}
