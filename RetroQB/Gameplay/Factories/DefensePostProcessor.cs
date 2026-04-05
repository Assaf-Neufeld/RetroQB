using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

internal static class DefensePostProcessor
{
    public static void ApplyStarPlayers(IReadOnlyList<Defender> defenders, SeasonStage stage)
    {
        if (stage == SeasonStage.RegularSeason)
        {
            return;
        }

        if (stage == SeasonStage.Playoff)
        {
            ApplyStarToSlot(defenders, DefenderSlot.FS);
            ApplyStarToSlot(defenders, DefenderSlot.DE1);
            return;
        }

        if (stage == SeasonStage.SuperBowl)
        {
            ApplyStarToSlot(defenders, DefenderSlot.DE1);
            ApplyStarToSlot(defenders, DefenderSlot.DE2);
            ApplyStarToSlot(defenders, DefenderSlot.MLB);
            ApplyStarToSlot(defenders, DefenderSlot.CB1);
            ApplyStarToSlot(defenders, DefenderSlot.FS);
        }
    }

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

    private static void ApplyStarToSlot(IReadOnlyList<Defender> defenders, DefenderSlot slot)
    {
        Defender? defender = defenders.FirstOrDefault(d => d.Slot == slot);
        if (defender == null)
        {
            return;
        }

        switch (defender.PositionRole)
        {
            case DefensivePosition.DB:
                defender.ApplyStarBoost(speedMultiplier: 1.08f, tackleMultiplier: 1.02f, interceptionMultiplier: 1.40f, blockShedMultiplier: 1.05f);
                break;
            case DefensivePosition.DE:
                defender.ApplyStarBoost(speedMultiplier: 1.10f, tackleMultiplier: 1.10f, interceptionMultiplier: 1.00f, blockShedMultiplier: 1.35f);
                break;
            case DefensivePosition.LB:
                defender.ApplyStarBoost(speedMultiplier: 1.07f, tackleMultiplier: 1.25f, interceptionMultiplier: 1.10f, blockShedMultiplier: 1.20f);
                break;
            default:
                defender.ApplyStarBoost(speedMultiplier: 1.05f, tackleMultiplier: 1.15f, interceptionMultiplier: 1.00f, blockShedMultiplier: 1.25f);
                break;
        }
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
