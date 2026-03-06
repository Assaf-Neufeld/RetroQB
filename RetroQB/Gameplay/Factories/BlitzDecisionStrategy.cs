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
    BlitzDecision DecideBlitzers(CoverageScheme scheme, DefensiveTeamAttributes attributes, DefensiveContext context, DefensiveMemory? memory, Random rng);
}

public sealed class DefaultBlitzDecisionStrategy : IBlitzDecisionStrategy
{
    private enum BlitzPackage
    {
        None,
        WillPressure,
        MikePressure,
        StrongSafetyPressure,
        NickelCat,
        DoubleLinebacker,
        DoubleEdge,
        ZeroPressure
    }

    public BlitzDecision DecideBlitzers(CoverageScheme scheme, DefensiveTeamAttributes attributes, DefensiveContext context, DefensiveMemory? memory, Random rng)
    {
        bool usesNickelPackage = UsesNickelPackage(scheme);
        BlitzPackage selectedPackage = SelectBlitzPackage(scheme, context, attributes, usesNickelPackage, memory, rng);
        HashSet<DefenderSlot> blitzingSlots = BuildBlitzersFromPackage(selectedPackage, usesNickelPackage, rng);
        return new BlitzDecision(blitzingSlots);
    }

    private static BlitzPackage SelectBlitzPackage(
        CoverageScheme scheme,
        DefensiveContext context,
        DefensiveTeamAttributes attributes,
        bool usesNickelPackage,
        DefensiveMemory? memory,
        Random rng)
    {
        Dictionary<BlitzPackage, float> weights = GetBasePackageWeights(scheme, usesNickelPackage);

        float pressureIntent = Math.Clamp(attributes.BlitzFrequency * GetBlitzSituationalMultiplier(context), 0.5f, 1.9f);
        foreach (BlitzPackage package in weights.Keys.ToList())
        {
            if (package == BlitzPackage.None)
            {
                weights[package] *= 1.20f / pressureIntent;
            }
            else
            {
                weights[package] *= pressureIntent;
            }
        }

        bool clearPassDown = context.Down >= 3 && context.Distance >= 8f;
        bool shortYardage = context.Down >= 3 && context.Distance <= 2f;
        if (clearPassDown)
        {
            ScaleWeight(weights, BlitzPackage.None, 0.72f);
            ScaleWeight(weights, BlitzPackage.DoubleLinebacker, 1.25f);
            ScaleWeight(weights, BlitzPackage.ZeroPressure, 1.20f);
            ScaleWeight(weights, BlitzPackage.StrongSafetyPressure, 0.78f);
            ScaleWeight(weights, BlitzPackage.NickelCat, 0.72f);
        }
        else if (shortYardage)
        {
            ScaleWeight(weights, BlitzPackage.None, 1.08f);
            ScaleWeight(weights, BlitzPackage.ZeroPressure, 0.82f);
            ScaleWeight(weights, BlitzPackage.DoubleEdge, 0.90f);
        }

        bool inRedZone = context.LineOfScrimmage >= FieldGeometry.OpponentGoalLine - 15f;
        if (inRedZone)
        {
            ScaleWeight(weights, BlitzPackage.ZeroPressure, 0.85f);
            ScaleWeight(weights, BlitzPackage.StrongSafetyPressure, 0.80f);
            ScaleWeight(weights, BlitzPackage.NickelCat, 0.75f);
        }

        // Apply adaptive memory multipliers (if available)
        if (memory != null)
        {
            foreach (BlitzPackage package in weights.Keys.ToList())
            {
                float memMult = memory.GetBlitzMultiplier(package.ToString());
                weights[package] *= memMult;
            }
        }

        ScaleDbPressurePackages(weights, 0.62f);

        return WeightedPick(weights, rng);
    }

    private static Dictionary<BlitzPackage, float> GetBasePackageWeights(CoverageScheme scheme, bool usesNickelPackage)
    {
        var weights = scheme switch
        {
            CoverageScheme.Cover0 => new Dictionary<BlitzPackage, float>
            {
                [BlitzPackage.None] = 0.18f,
                [BlitzPackage.WillPressure] = 0.14f,
                [BlitzPackage.MikePressure] = 0.10f,
                [BlitzPackage.StrongSafetyPressure] = 0.11f,
                [BlitzPackage.NickelCat] = 0.10f,
                [BlitzPackage.DoubleLinebacker] = 0.16f,
                [BlitzPackage.DoubleEdge] = 0.11f,
                [BlitzPackage.ZeroPressure] = 0.10f
            },
            CoverageScheme.Cover1 => new Dictionary<BlitzPackage, float>
            {
                [BlitzPackage.None] = 0.44f,
                [BlitzPackage.WillPressure] = 0.16f,
                [BlitzPackage.MikePressure] = 0.09f,
                [BlitzPackage.StrongSafetyPressure] = 0.10f,
                [BlitzPackage.NickelCat] = 0.05f,
                [BlitzPackage.DoubleLinebacker] = 0.11f,
                [BlitzPackage.DoubleEdge] = 0.05f
            },
            CoverageScheme.Cover2Man => new Dictionary<BlitzPackage, float>
            {
                [BlitzPackage.None] = 0.50f,
                [BlitzPackage.WillPressure] = 0.14f,
                [BlitzPackage.MikePressure] = 0.08f,
                [BlitzPackage.StrongSafetyPressure] = 0.09f,
                [BlitzPackage.NickelCat] = 0.04f,
                [BlitzPackage.DoubleLinebacker] = 0.10f,
                [BlitzPackage.DoubleEdge] = 0.05f
            },
            CoverageScheme.Cover2Zone => new Dictionary<BlitzPackage, float>
            {
                [BlitzPackage.None] = 0.72f,
                [BlitzPackage.WillPressure] = 0.09f,
                [BlitzPackage.MikePressure] = 0.05f,
                [BlitzPackage.StrongSafetyPressure] = 0.07f,
                [BlitzPackage.NickelCat] = 0.07f
            },
            CoverageScheme.Cover3Zone => new Dictionary<BlitzPackage, float>
            {
                [BlitzPackage.None] = 0.66f,
                [BlitzPackage.WillPressure] = 0.10f,
                [BlitzPackage.MikePressure] = 0.06f,
                [BlitzPackage.StrongSafetyPressure] = 0.09f,
                [BlitzPackage.NickelCat] = 0.09f
            },
            CoverageScheme.Cover4Zone => new Dictionary<BlitzPackage, float>
            {
                [BlitzPackage.None] = 0.76f,
                [BlitzPackage.WillPressure] = 0.08f,
                [BlitzPackage.MikePressure] = 0.04f,
                [BlitzPackage.StrongSafetyPressure] = 0.07f,
                [BlitzPackage.NickelCat] = 0.05f
            },
            _ => new Dictionary<BlitzPackage, float>
            {
                [BlitzPackage.None] = 0.70f,
                [BlitzPackage.WillPressure] = 0.12f,
                [BlitzPackage.MikePressure] = 0.06f,
                [BlitzPackage.StrongSafetyPressure] = 0.07f,
                [BlitzPackage.NickelCat] = 0.05f
            }
        };

        if (!usesNickelPackage)
        {
            weights.Remove(BlitzPackage.NickelCat);
        }
        else
        {
            weights[BlitzPackage.MikePressure] = 0f;
        }

        if (scheme is CoverageScheme.Cover2Zone or CoverageScheme.Cover3Zone or CoverageScheme.Cover4Zone)
        {
            weights[BlitzPackage.ZeroPressure] = 0f;
            weights[BlitzPackage.DoubleLinebacker] = 0f;
            weights[BlitzPackage.DoubleEdge] = 0f;
        }

        return weights;
    }

    private static HashSet<DefenderSlot> BuildBlitzersFromPackage(BlitzPackage package, bool usesNickelPackage, Random rng)
    {
        List<DefenderSlot> linebackerSlots = usesNickelPackage
            ? new List<DefenderSlot> { DefenderSlot.OLB1, DefenderSlot.OLB2 }
            : new List<DefenderSlot> { DefenderSlot.OLB1, DefenderSlot.MLB, DefenderSlot.OLB2 };

        var blitzers = new HashSet<DefenderSlot>();
        switch (package)
        {
            case BlitzPackage.None:
                return blitzers;

            case BlitzPackage.WillPressure:
                AddRandom(linebackerSlots.Where(slot => slot != DefenderSlot.MLB).ToList(), blitzers, rng);
                return blitzers;

            case BlitzPackage.MikePressure:
                if (!usesNickelPackage)
                {
                    blitzers.Add(DefenderSlot.MLB);
                }
                else
                {
                    AddRandom(linebackerSlots, blitzers, rng);
                }
                return blitzers;

            case BlitzPackage.StrongSafetyPressure:
                blitzers.Add(DefenderSlot.SS);
                return blitzers;

            case BlitzPackage.NickelCat:
                if (usesNickelPackage)
                {
                    blitzers.Add(DefenderSlot.NB);
                }
                else
                {
                    blitzers.Add(DefenderSlot.CB2);
                }
                return blitzers;

            case BlitzPackage.DoubleLinebacker:
                AddRandom(linebackerSlots, blitzers, rng);
                AddRandom(linebackerSlots.Except(blitzers).ToList(), blitzers, rng);
                return blitzers;

            case BlitzPackage.DoubleEdge:
                blitzers.Add(DefenderSlot.OLB1);
                blitzers.Add(DefenderSlot.OLB2);
                return blitzers;

            case BlitzPackage.ZeroPressure:
                blitzers.Add(DefenderSlot.OLB1);
                blitzers.Add(DefenderSlot.OLB2);
                if (usesNickelPackage)
                {
                    blitzers.Add(DefenderSlot.NB);
                }
                else
                {
                    blitzers.Add(DefenderSlot.MLB);
                }
                return blitzers;

            default:
                return blitzers;
        }
    }

    private static void AddRandom(List<DefenderSlot> choices, HashSet<DefenderSlot> target, Random rng)
    {
        if (choices.Count == 0)
        {
            return;
        }

        DefenderSlot picked = choices[rng.Next(choices.Count)];
        target.Add(picked);
    }

    private static void ScaleWeight(Dictionary<BlitzPackage, float> weights, BlitzPackage package, float multiplier)
    {
        if (!weights.TryGetValue(package, out float weight))
        {
            return;
        }

        weights[package] = Math.Max(0f, weight * multiplier);
    }

    private static void ScaleDbPressurePackages(Dictionary<BlitzPackage, float> weights, float multiplier)
    {
        ScaleWeight(weights, BlitzPackage.StrongSafetyPressure, multiplier);
        ScaleWeight(weights, BlitzPackage.NickelCat, multiplier);
    }

    private static BlitzPackage WeightedPick(Dictionary<BlitzPackage, float> weights, Random rng)
    {
        float total = 0f;
        foreach (float weight in weights.Values)
        {
            total += Math.Max(0f, weight);
        }

        if (total <= 0f)
        {
            return BlitzPackage.None;
        }

        float roll = (float)rng.NextDouble() * total;
        float cumulative = 0f;
        foreach (var kvp in weights)
        {
            float weight = Math.Max(0f, kvp.Value);
            cumulative += weight;
            if (roll <= cumulative)
            {
                return kvp.Key;
            }
        }

        return BlitzPackage.None;
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
