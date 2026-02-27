using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;

namespace RetroQB.Gameplay;

public readonly record struct BlitzDecision(IReadOnlySet<DefenderSlot> BlitzingSlots)
{
    public bool IsBlitzer(DefenderSlot slot)
    {
        return BlitzingSlots.Contains(slot);
    }
}

public interface IBlitzDecisionStrategy
{
    BlitzDecision DecideBlitzers(CoverageScheme scheme, DefensiveTeamAttributes attributes, DefensiveContext context, Random rng);
}

public sealed class DefaultBlitzDecisionStrategy : IBlitzDecisionStrategy
{
    public BlitzDecision DecideBlitzers(CoverageScheme scheme, DefensiveTeamAttributes attributes, DefensiveContext context, Random rng)
    {
        var blitzingSlots = new HashSet<DefenderSlot>();
        foreach (DefenderSlot slot in GetActiveBackfieldSlots(scheme))
        {
            float blitzChance = GetBackfieldSlotBlitzChance(slot, scheme, attributes, context);
            if (rng.NextDouble() < blitzChance)
            {
                blitzingSlots.Add(slot);
            }
        }

        return new BlitzDecision(blitzingSlots);
    }

    private static IEnumerable<DefenderSlot> GetActiveBackfieldSlots(CoverageScheme scheme)
    {
        yield return DefenderSlot.OLB1;
        if (!UsesNickelPackage(scheme))
        {
            yield return DefenderSlot.MLB;
        }

        yield return DefenderSlot.OLB2;
        yield return DefenderSlot.CB1;
        yield return DefenderSlot.CB2;
        yield return DefenderSlot.FS;
        yield return DefenderSlot.SS;

        if (UsesNickelPackage(scheme))
        {
            yield return DefenderSlot.NB;
        }
    }

    private static bool UsesNickelPackage(CoverageScheme scheme)
    {
        return scheme is CoverageScheme.Cover2Zone or CoverageScheme.Cover3Zone or CoverageScheme.Cover4Zone;
    }

    private static float GetBackfieldSlotBlitzChance(DefenderSlot slot, CoverageScheme scheme, DefensiveTeamAttributes attributes, DefensiveContext context)
    {
        float baseChance = slot switch
        {
            DefenderSlot.OLB1 or DefenderSlot.MLB or DefenderSlot.OLB2 => GetLbBaseBlitzChance(scheme),
            DefenderSlot.CB1 or DefenderSlot.CB2 => GetCbBaseBlitzChance(scheme),
            DefenderSlot.FS => GetSafetyBaseBlitzChance(scheme),
            DefenderSlot.SS or DefenderSlot.NB => GetSafetyBaseBlitzChance(scheme),
            _ => 0f
        };

        float slotScale = attributes.GetBlitzSlotMultiplier(slot);
        float situationalScale = GetBlitzSituationalMultiplier(context);
        float chance = baseChance * slotScale * attributes.BlitzFrequency * situationalScale;
        return Math.Clamp(chance, 0f, 0.70f);
    }

    private static float GetLbBaseBlitzChance(CoverageScheme scheme)
    {
        return scheme switch
        {
            CoverageScheme.Cover0 => 0.35f,
            CoverageScheme.Cover1 => 0.15f,
            CoverageScheme.Cover2Man => 0.12f,
            CoverageScheme.Cover2Zone => 0.08f,
            CoverageScheme.Cover3Zone => 0.08f,
            CoverageScheme.Cover4Zone => 0.05f,
            _ => 0.10f
        };
    }

    private static float GetCbBaseBlitzChance(CoverageScheme scheme)
    {
        return scheme switch
        {
            CoverageScheme.Cover0 => 0.20f,
            CoverageScheme.Cover1 => 0.05f,
            _ => 0f
        };
    }

    private static float GetSafetyBaseBlitzChance(CoverageScheme scheme)
    {
        return scheme switch
        {
            CoverageScheme.Cover0 => 0.12f,
            CoverageScheme.Cover1 => 0.05f,
            CoverageScheme.Cover2Man => 0.03f,
            CoverageScheme.Cover2Zone => 0.01f,
            CoverageScheme.Cover3Zone => 0.02f,
            CoverageScheme.Cover4Zone => 0.005f,
            _ => 0.01f
        };
    }

    private static float GetBlitzSituationalMultiplier(DefensiveContext context)
    {
        float stageScale = context.Stage switch
        {
            SeasonStage.RegularSeason => 0.82f,
            SeasonStage.Playoff => 1.00f,
            SeasonStage.SuperBowl => 1.15f,
            _ => 1.00f
        };

        float downDistanceScale = 1f;
        if (context.Down >= 3 && context.Distance >= 7f)
        {
            downDistanceScale *= 1.15f;
        }
        else if (context.Down >= 3 && context.Distance <= 2f)
        {
            downDistanceScale *= 0.92f;
        }

        bool isRedZone = context.LineOfScrimmage >= FieldGeometry.OpponentGoalLine - 15f;
        float fieldScale = isRedZone ? 0.94f : 1f;

        int defenseLead = context.AwayScore - context.Score;
        float scoreScale = defenseLead switch
        {
            <= -8 => 1.10f,
            >= 8 => 0.92f,
            _ => 1f
        };

        return stageScale * downDistanceScale * fieldScale * scoreScale;
    }
}
