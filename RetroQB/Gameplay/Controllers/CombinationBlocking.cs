using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

public readonly record struct RunBlockDecision(BlockingAssignment Assignment, Defender? Target, bool Coordinated);

/// <summary>Decide from one position snapshot, before any line actor integrates.</summary>
public static class CombinationBlocking
{
    public static IReadOnlyDictionary<Blocker, RunBlockDecision> Decide(IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Defender> defenders, ResolvedPlay play, float los, float dt)
    {
        var result = new Dictionary<Blocker, RunBlockDecision>();
        if (play.RunPlan == null) return result;
        foreach (var b in blockers) b.AssignmentElapsed += MathF.Max(0, dt);
        foreach (var pair in play.RunPlan.Combinations)
        {
            var anchor = blockers.SingleOrDefault(b => b.LineSlot == pair.AnchorSlot);
            var release = blockers.SingleOrDefault(b => b.LineSlot == pair.ReleaseSlot);
            if (anchor == null || release == null) continue;
            var first = anchor.BlockingState.Target;
            if (first == null || !defenders.Contains(first) || Vector2.Distance(first.Position, anchor.Position) > 5)
                first = defenders.Where(d => d.Position.Y < los + 4 && Vector2.Distance(d.Position, anchor.Position) < 5)
                    .MinBy(d => Vector2.DistanceSquared(d.Position, anchor.Position));
            // Climb only once the anchor has contact and leverage, or the first level is clear.
            bool held = first != null && Vector2.Distance(first.Position, anchor.Position) <= anchor.Radius + first.Radius + .8f
                && first.Position.Y >= anchor.Position.Y - .5f;
            bool penetration = defenders.Any(d => d != first && d.Position.Y < los + 1
                && Vector2.Distance(d.Position, release.Position) < 3);
            if (!release.CombinationReleased && release.AssignmentElapsed >= pair.MinimumSeconds
                && !penetration && (first == null || held))
                release.CombinationReleased = true;
            result[anchor] = new(play.LineBlocking[pair.AnchorSlot], first, true);
            if (release.CombinationReleased)
            {
                var job = new BlockingAssignment(BlockingJob.Lead, pair.SecondLevel, BlockingAnchor.FieldCenter);
                // Use the same immutable job each frame so steering retains its selected defender.
                if (release.BlockingState.Assignment is { Job: BlockingJob.Lead } previous
                    && previous.TargetOffset == pair.SecondLevel) job = previous;
                Vector2 landmark = OffensiveGeometry.At(pair.SecondLevel, los);
                var second = defenders.Where(d => d != first && Vector2.Distance(d.Position, release.Position) < 2.6f)
                    .OrderBy(d => Vector2.DistanceSquared(d.Position, release.Position)).ThenBy(d => d.Slot).FirstOrDefault()
                    ?? defenders.Where(d => d != first && d.Position.Y >= los + 3 && Vector2.Distance(d.Position, landmark) < 5)
                        .OrderBy(d => Vector2.DistanceSquared(d.Position, landmark)).ThenBy(d => d.Slot).FirstOrDefault();
                // The anchor keeps the first-level defender; generic local pickup must not
                // immediately reclaim that same defender and undo the coordinated release.
                result[release] = new(job, second, true);
            }
            else result[release] = new(play.LineBlocking[pair.ReleaseSlot], first, true);
        }
        return result;
    }
}
