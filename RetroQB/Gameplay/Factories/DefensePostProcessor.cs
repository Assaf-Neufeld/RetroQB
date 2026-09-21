using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

internal static class DefensePostProcessor
{
    public static List<string> BuildBlitzerSummary(IReadOnlyList<Defender> defenders)
    {
        int lbCount = 0;
        int dbCount = 0;

        for (int i = 0; i < defenders.Count; i++)
        {
            Defender defender = defenders[i];
            if (!defender.IsRusher)
            {
                continue;
            }

            if (IsLinebackerSlot(defender.Slot))
            {
                lbCount++;
                continue;
            }

            if (IsDefensiveBackSlot(defender.Slot))
            {
                dbCount++;
                continue;
            }

            switch (defender.PositionRole)
            {
                case DefensivePosition.LB:
                    lbCount++;
                    break;
                case DefensivePosition.DB:
                    dbCount++;
                    break;
            }
        }

        var blitzers = new List<string>(lbCount + dbCount);
        for (int i = 0; i < lbCount; i++)
        {
            blitzers.Add("LB");
        }

        for (int i = 0; i < dbCount; i++)
        {
            blitzers.Add("DB");
        }

        return blitzers;
    }

    private static bool IsLinebackerSlot(DefenderSlot slot)
    {
        return slot is DefenderSlot.MLB or DefenderSlot.OLB1 or DefenderSlot.OLB2;
    }

    private static bool IsDefensiveBackSlot(DefenderSlot slot)
    {
        return slot is DefenderSlot.CB1 or DefenderSlot.CB2 or DefenderSlot.FS or DefenderSlot.SS or DefenderSlot.NB;
    }
}
