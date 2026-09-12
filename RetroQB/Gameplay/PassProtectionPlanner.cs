using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

/// <summary>Distributes live rushers across the line without crossing protection lanes.</summary>
public static class PassProtectionPlanner
{
    public static Dictionary<Blocker, Defender> Assign(IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Defender> defenders, bool openingComplete)
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
                Consider((next.Count + 1, next.Cost + distance + continuity), 3);
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
}
