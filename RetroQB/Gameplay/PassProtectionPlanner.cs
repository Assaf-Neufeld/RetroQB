using System.Numerics;
using RetroQB.Entities;
using RetroQB.Gameplay.Controllers;

namespace RetroQB.Gameplay;

/// <summary>Distributes live rushers across the line without crossing protection lanes.</summary>
public static class PassProtectionPlanner
{
    public static Dictionary<Blocker, Defender> Assign(IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Defender> defenders, bool openingComplete, ProtectionPlan? plan = null)
    {
        var line = blockers.Where(b => !((openingComplete ? b.BlockingAssignment : b.OpeningAssignment)
                ?? throw new InvalidOperationException("Lineman has no resolved blocking assignment.")).IsRunBlock)
            .OrderBy(b => b.Position.X).ThenBy(b => b.HomeX).ToArray();
        var rush = defenders.Where(d => d.IsRusher).OrderBy(d => d.Position.X).ThenBy(d => d.Slot).ToArray();
        var best = new (int Count, float Cost)[line.Length + 1, rush.Length + 1];
        var choice = new byte[line.Length, rush.Length];
        // Match as many threats as possible first, then minimize travel. Skipping
        // either side lets the whole protection slide with an overloaded front.
        for (int i = line.Length - 1; i >= 0; i--)
        for (int j = rush.Length - 1; j >= 0; j--)
        {
            best[i, j] = best[i + 1, j];
            choice[i, j] = 1;
            Consider(best[i, j + 1], 2);
            float distance = Vector2.Distance(line[i].Position, rush[j].Position);
            if (distance <= Constants.BlockEngageRadius * 2f)
            {
                var next = best[i + 1, j + 1];
                float continuity = line[i].BlockingState.Target == rush[j] ? 0 : 2f;
                int slide = plan?.Mode == ProtectionMode.SlideLeft ? -1 : plan?.Mode == ProtectionMode.SlideRight ? 1 : 0;
                float setDistance = MathF.Abs(rush[j].Position.X - (line[i].HomeX + slide * 1.5f));
                Consider((next.Count + 1, next.Cost + distance + continuity + setDistance * .35f), 3);
            }

            void Consider((int Count, float Cost) candidate, byte action)
            {
                var current = best[i, j];
                if (candidate.Count > current.Count || candidate.Count == current.Count && candidate.Cost < current.Cost)
                {
                    best[i, j] = candidate;
                    choice[i, j] = action;
                }
            }
        }
        var assignments = new Dictionary<Blocker, Defender>();
        int player = 0, threat = 0;
        while (player < line.Length && threat < rush.Length)
        {
            switch (choice[player, threat])
            {
                case 1: player++; break;
                case 2: threat++; break;
                default: assignments.Add(line[player++], rush[threat++]); break;
            }
        }
        return assignments;
    }

    /// <summary>Jointly allocate rushers, preserving line order while allowing backs to cover either edge.</summary>
    public static Dictionary<Entity, Defender> AssignAll(IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Receiver> receivers, IReadOnlyList<Defender> defenders, ResolvedPlay play, bool openingComplete,
        DesignedOffenseController? design = null)
    {
        var line = blockers.Where(b => !(design?.LineJob(b.LineSlot) ?? (openingComplete ? b.BlockingAssignment : b.OpeningAssignment))!.IsRunBlock)
            .OrderBy(b => b.Position.X).ThenBy(b => b.HomeX).ToArray();
        var support = receivers.Where(r => r.IsBlocking && (design?.SkillJob(r.Slot) ?? play.Assignments[r.Slot].Blocking) is { IsRunBlock: false })
            .OrderBy(r => r.Slot.GetPriorityOrder()).ToArray();
        var rush = defenders.Where(d => d.IsRusher).OrderBy(d => d.Position.X).ThenBy(d => d.Slot).ToArray();
        var memo = new Dictionary<(int Line, int Rush, int Mask), (int Count, float Cost, int Action)>();
        Solve(0, 0, 0);
        var result = new Dictionary<Entity, Defender>();
        int i = 0, j = 0, mask = 0;
        while (j < rush.Length)
        {
            int action = memo[(i, j, mask)].Action;
            if (action == -1) { i++; continue; }
            if (action == -2) { j++; continue; }
            if (action == -3) result.Add(line[i++], rush[j++]);
            else { result.Add(support[action], rush[j++]); mask |= 1 << action; }
        }
        // Only a declared helper may double an already-covered threat, after all reachable free rushers are allocated.
        var helper = support.FirstOrDefault(r => r.Slot == play.Protection.HelpSlot && !result.ContainsKey(r));
        if (helper != null && !rush.Any(d => !result.ContainsValue(d)
            && Vector2.Distance(helper.Position, d.Position) <= Constants.BlockEngageRadius * 2.5f))
        {
            var target = result.Values.Distinct().Where(d => Vector2.Distance(helper.Position, d.Position) <= 5)
                .OrderBy(d => Vector2.DistanceSquared(helper.Position, d.Position)).ThenBy(d => d.Slot).FirstOrDefault();
            if (target != null) result.Add(helper, target);
        }
        return result;

        (int Count, float Cost, int Action) Solve(int index, int threat, int used)
        {
            if (threat == rush.Length) return (0, 0, -2);
            var key = (index, threat, used);
            if (memo.TryGetValue(key, out var cached)) return cached;
            var skip = Solve(index, threat + 1, used);
            var best = (skip.Count, skip.Cost, Action: -2);
            if (index < line.Length)
            {
                Consider(Solve(index + 1, threat, used), 0, -1, false);
                float distance = Vector2.Distance(line[index].Position, rush[threat].Position);
                if (distance <= Constants.BlockEngageRadius * 2)
                {
                    int slide = play.Protection.Mode == ProtectionMode.SlideLeft ? -1 : play.Protection.Mode == ProtectionMode.SlideRight ? 1 : 0;
                    float cost = distance + (line[index].BlockingState.Target == rush[threat] ? 0 : 2)
                        + .35f * MathF.Abs(rush[threat].Position.X - line[index].HomeX - slide * 1.5f);
                    if (rush[threat].Position.Y < line[index].HomeY - 2
                        && support.Any(r => Vector2.Distance(r.Position, rush[threat].Position) <= 3)) cost += 6;
                    Consider(Solve(index + 1, threat + 1, used), cost, -3, true);
                }
            }
            for (int s = 0; s < support.Length; s++)
            {
                if ((used & (1 << s)) != 0) continue;
                float distance = Vector2.Distance(support[s].Position, rush[threat].Position);
                if (distance <= Constants.BlockEngageRadius * 2.5f)
                    Consider(Solve(index, threat + 1, used | (1 << s)),
                        distance + (support[s].BlockingState.Target == rush[threat] ? 0 : 2), s, true);
            }
            memo[key] = best;
            return best;

            void Consider((int Count, float Cost, int Action) next, float cost, int action, bool match)
            {
                int count = next.Count + (match ? 1 : 0); cost += next.Cost;
                if (count > best.Count || count == best.Count && cost < best.Cost) best = (count, cost, action);
            }
        }
    }

    public static bool CanRelease(Receiver player, PlayerAssignment assignment, Vector2 qb,
        IReadOnlyList<Defender> defenders, IReadOnlyDictionary<Entity, Defender> protection)
    {
        if (player.AssignmentElapsed < assignment.ReleaseAfterSeconds) return false;
        if (assignment.ProtectionRelease is not { } rule) return true;
        foreach (var threat in defenders.Where(d => d.IsRusher))
        {
            // Another protector must be close enough to supply real help, not merely assigned on paper.
            bool covered = protection.Any(p => p.Key != player && p.Value == threat
                && Vector2.Distance(p.Key.Position, threat.Position) <= 3.5f);
            bool immediate = Vector2.Distance(threat.Position, qb) <= 5
                || Vector2.Distance(threat.Position + threat.Velocity * .35f, qb) <= 5
                || Vector2.Distance(threat.Position, player.Position) <= 4.5f;
            bool assigned = protection.TryGetValue(player, out var target) && target == threat;
            if (!covered && (immediate || assigned && player.AssignmentElapsed < rule.DecisionTimeout)) return false;
        }
        return true;
    }
}
