namespace RetroQB.AI;

/// <summary>
/// Represents different defensive coverage schemes.
/// Each scheme defines how safeties, cornerbacks, and linebackers are deployed.
/// </summary>
public enum CoverageScheme
{
    /// <summary>
    /// All man coverage with no deep safety help. High blitz chance.
    /// Aggressive scheme that leaves no safety net over the top.
    /// </summary>
    Cover0,

    /// <summary>
    /// Man coverage with one free safety playing deep middle zone.
    /// Balanced man scheme with a single-high safety look.
    /// </summary>
    Cover1,

    /// <summary>
    /// Two deep safeties in zone, CBs in flat zones, LBs in hook/curl zones.
    /// Classic two-high shell that defends deep sideline passes.
    /// </summary>
    Cover2Zone,

    /// <summary>
    /// Three deep defenders (2 CBs + FS) each covering a deep third.
    /// Strong safety and LBs handle underneath zones. Good against deep passes.
    /// </summary>
    Cover3Zone,

    /// <summary>
    /// Four deep defenders (2 CBs + 2 safeties) each covering a quarter.
    /// Very strong deep coverage but leaves underneath areas open.
    /// </summary>
    Cover4Zone,

    /// <summary>
    /// Three-deep shell with underneath defenders matching inside releases more tightly.
    /// Plays like a simplified zone-match concept without full pattern-match logic.
    /// </summary>
    Cover3Match,

    /// <summary>
    /// Four-deep quarters shell with underneath linebackers carrying inside routes.
    /// More aggressive on vertical releases than static Cover 4 zone.
    /// </summary>
    QuartersMatch,

    /// <summary>
    /// Two deep safeties in zone with CBs and LBs playing man underneath.
    /// Hybrid scheme blending man tightness with deep zone safety.
    /// </summary>
    Cover2Man,

    /// <summary>
    /// Single-high man shell with a low-hole robber sitting inside to jump crossers.
    /// Sacrifices pure underneath man integrity for extra middle help.
    /// </summary>
    Robber
}

/// <summary>
/// Provides situational scheme selection logic for defensive playcalling.
/// Methods are designed to be called as a pipeline by <see cref="DefensiveCoordinator"/>:
///   GetSituationalWeights → ApplyStagePool → (external memory multipliers) → PickScheme
/// </summary>
public static class CoverageSchemeSelector
{
    // ---- Baseline weight tables (C0, C1, C2Z, C3Z, C4Z, C3M, QMatch, C2M, Robber) ----

    private const float ManCoverageBias = 1.18f;
    private const float ZoneCoverageBias = 0.92f;

    private static readonly float[] Baseline     = { 5f, 18f, 24f, 20f, 8f, 9f, 6f, 13f, 11f };
    private static readonly float[] Aggressive   = { 15f, 26f, 9f, 9f, 4f, 13f, 4f, 24f, 18f };
    private static readonly float[] Conservative = { 0f, 5f, 18f, 28f, 22f, 11f, 18f, 8f, 6f };
    private static readonly float[] ShortYardage = { 10f, 28f, 14f, 9f, 4f, 11f, 5f, 23f, 17f };
    private static readonly float[] RedZone      = { 12f, 30f, 13f, 12f, 7f, 14f, 10f, 13f, 19f };

    private static readonly CoverageScheme[] SchemeOrder =
    {
        CoverageScheme.Cover0,
        CoverageScheme.Cover1,
        CoverageScheme.Cover2Zone,
        CoverageScheme.Cover3Zone,
        CoverageScheme.Cover4Zone,
        CoverageScheme.Cover3Match,
        CoverageScheme.QuartersMatch,
        CoverageScheme.Cover2Man,
        CoverageScheme.Robber
    };

    // ---- Pipeline: Step 1 — situational weights ----

    /// <summary>
    /// Returns a mutable weight dictionary based on down, distance, field position and score.
    /// </summary>
    public static Dictionary<CoverageScheme, float> GetSituationalWeights(
        int down, float distance, float lineOfScrimmage,
        int score, int awayScore)
    {
        float[] raw = GetSituationalRaw(down, distance, lineOfScrimmage, score, awayScore);
        Dictionary<CoverageScheme, float> weights = ToDictionary(raw);
        ApplyCoverageBias(weights);
        return weights;
    }

    // ---- Pipeline: Step 2 — stage pool filter ----

    /// <summary>
    /// Modifies the weight dictionary in-place to restrict the scheme pool
    /// based on the current season stage.
    /// </summary>
    public static void ApplyStagePool(Dictionary<CoverageScheme, float> weights, SeasonStage stage)
    {
        switch (stage)
        {
            case SeasonStage.RegularSeason:
                weights[CoverageScheme.Cover0] *= 0.18f;
                weights[CoverageScheme.Cover1] *= 0.60f;
                weights[CoverageScheme.Cover2Zone] *= 1.08f;
                weights[CoverageScheme.Cover3Zone] *= 0.98f;
                weights[CoverageScheme.Cover4Zone] *= 0.22f;
                weights[CoverageScheme.Cover3Match] *= 0.20f;
                weights[CoverageScheme.QuartersMatch] *= 0.18f;
                weights[CoverageScheme.Cover2Man] *= 0.55f;
                weights[CoverageScheme.Robber] *= 0.55f;
                break;

            case SeasonStage.Playoff:
                weights[CoverageScheme.Cover0] *= 0.50f;
                weights[CoverageScheme.Cover1] *= 1.08f;
                weights[CoverageScheme.Cover3Match] *= 1.05f;
                weights[CoverageScheme.QuartersMatch] *= 0.90f;
                weights[CoverageScheme.Cover2Man] *= 1.10f;
                weights[CoverageScheme.Robber] *= 1.15f;
                break;

            case SeasonStage.SuperBowl:
            default:
                // full menu
                break;
        }

        // Safety fallback: if pool is empty, give basic zone a floor.
        float total = weights.Values.Sum();
        if (total <= 0f)
        {
            weights[CoverageScheme.Cover2Zone] = 1f;
            weights[CoverageScheme.Cover3Zone] = 1f;
        }
    }

    // ---- Pipeline: Step 3 — weighted random pick ----

    /// <summary>
    /// Picks a scheme from a weight dictionary using weighted random selection.
    /// </summary>
    public static CoverageScheme PickScheme(Dictionary<CoverageScheme, float> weights, Random rng)
    {
        float total = 0f;
        foreach (float w in weights.Values)
        {
            total += MathF.Max(0f, w);
        }

        if (total <= 0f)
        {
            return CoverageScheme.Cover2Zone;
        }

        float roll = (float)rng.NextDouble() * total;
        float cumulative = 0f;
        foreach (CoverageScheme scheme in SchemeOrder)
        {
            if (!weights.TryGetValue(scheme, out float w))
            {
                continue;
            }

            cumulative += MathF.Max(0f, w);
            if (roll <= cumulative)
            {
                return scheme;
            }
        }

        return CoverageScheme.Cover2Zone;
    }

    // ---- Internals ----

    private static float[] GetSituationalRaw(
        int down, float distance, float lineOfScrimmage,
        int score, int awayScore)
    {
        bool isRedZone = lineOfScrimmage >= FieldGeometry.OpponentGoalLine - 20f;
        bool isPassingDown = (down >= 3 && distance >= 7f);
        bool isShortYardage = (down >= 3 && distance <= 3f);
        bool isTrailing = awayScore > score + 7;
        bool isProtectingLead = score > awayScore + 7;

        if (isRedZone)
            return Blend(RedZone, isPassingDown ? Aggressive : Baseline, 0.6f);

        if (isPassingDown)
            return Blend(Aggressive, Baseline, 0.7f);

        if (isShortYardage)
            return Blend(ShortYardage, Baseline, 0.65f);

        if (isProtectingLead)
            return Blend(Conservative, Baseline, 0.5f);

        if (isTrailing)
            return Blend(Aggressive, Baseline, 0.4f);

        return (float[])Baseline.Clone();
    }

    private static float[] Blend(float[] a, float[] b, float t)
    {
        float u = 1f - t;
        var result = new float[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            result[i] = a[i] * t + b[i] * u;
        }
        return result;
    }

    private static Dictionary<CoverageScheme, float> ToDictionary(float[] raw)
    {
        var dict = new Dictionary<CoverageScheme, float>();
        for (int i = 0; i < SchemeOrder.Length && i < raw.Length; i++)
        {
            dict[SchemeOrder[i]] = raw[i];
        }
        return dict;
    }

    private static void ApplyCoverageBias(Dictionary<CoverageScheme, float> weights)
    {
        foreach (CoverageScheme scheme in SchemeOrder)
        {
            if (!weights.TryGetValue(scheme, out float weight))
            {
                continue;
            }

            weights[scheme] = IsManOrientedScheme(scheme)
                ? weight * ManCoverageBias
                : weight * ZoneCoverageBias;
        }
    }

    private static bool IsManOrientedScheme(CoverageScheme scheme) => scheme switch
    {
        CoverageScheme.Cover0 => true,
        CoverageScheme.Cover1 => true,
        CoverageScheme.Cover2Man => true,
        CoverageScheme.Robber => true,
        _ => false
    };
}
