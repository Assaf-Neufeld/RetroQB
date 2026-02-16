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
    /// Two deep safeties in zone with CBs and LBs playing man underneath.
    /// Hybrid scheme blending man tightness with deep zone safety.
    /// </summary>
    Cover2Man
}

/// <summary>
/// Provides situational scheme selection logic for defensive playcalling.
/// </summary>
public static class CoverageSchemeSelector
{
    /// <summary>
    /// Weights for each coverage scheme in a given situation.
    /// Higher weight = more likely to be selected.
    /// </summary>
    private readonly struct SchemeWeights
    {
        public readonly float Cover0;
        public readonly float Cover1;
        public readonly float Cover2Zone;
        public readonly float Cover3Zone;
        public readonly float Cover4Zone;
        public readonly float Cover2Man;

        public SchemeWeights(float c0, float c1, float c2z, float c3z, float c4z, float c2m)
        {
            Cover0 = c0;
            Cover1 = c1;
            Cover2Zone = c2z;
            Cover3Zone = c3z;
            Cover4Zone = c4z;
            Cover2Man = c2m;
        }

        public float Total => Cover0 + Cover1 + Cover2Zone + Cover3Zone + Cover4Zone + Cover2Man;
    }

    // Baseline scheme distribution: a balanced mix
    private static readonly SchemeWeights Baseline = new(5f, 20f, 25f, 25f, 10f, 15f);

    // Aggressive (3rd/4th and long, trailing): more man/blitz
    private static readonly SchemeWeights Aggressive = new(15f, 30f, 10f, 10f, 5f, 30f);

    // Conservative (protecting lead, prevent): deep zone heavy
    private static readonly SchemeWeights Conservative = new(0f, 5f, 20f, 35f, 30f, 10f);

    // Short yardage (3rd/4th and short): tight man, control
    private static readonly SchemeWeights ShortYardage = new(10f, 30f, 15f, 10f, 5f, 30f);

    // Red zone: aggressive man coverage
    private static readonly SchemeWeights RedZone = new(12f, 35f, 15f, 15f, 8f, 15f);

    /// <summary>
    /// Selects a coverage scheme based on game situation and randomness.
    /// </summary>
    public static CoverageScheme SelectScheme(
        int down, float distance, float lineOfScrimmage,
        int score, int awayScore, SeasonStage stage, Random rng)
    {
        SchemeWeights weights = GetSituationalWeights(down, distance, lineOfScrimmage, score, awayScore);
        weights = ApplyStageCoveragePool(weights, stage);
        return PickScheme(weights, rng);
    }

    private static SchemeWeights ApplyStageCoveragePool(SchemeWeights baseWeights, SeasonStage stage)
    {
        SchemeWeights filtered = stage switch
        {
            // Regular season: basic zone shells only.
            SeasonStage.RegularSeason => new SchemeWeights(
                c0: 0f,
                c1: 0f,
                c2z: baseWeights.Cover2Zone,
                c3z: baseWeights.Cover3Zone,
                c4z: 0f,
                c2m: 0f),

            // Playoff: broader mix, but keep out all-out Cover 0 pressure look.
            SeasonStage.Playoff => new SchemeWeights(
                c0: 0f,
                c1: baseWeights.Cover1,
                c2z: baseWeights.Cover2Zone,
                c3z: baseWeights.Cover3Zone,
                c4z: baseWeights.Cover4Zone,
                c2m: baseWeights.Cover2Man),

            // Super Bowl: full coverage menu available.
            SeasonStage.SuperBowl => baseWeights,
            _ => baseWeights
        };

        // Safety fallback: never return an empty pool.
        if (filtered.Total <= 0f)
        {
            return new SchemeWeights(0f, 0f, 1f, 1f, 0f, 0f);
        }

        return filtered;
    }

    private static SchemeWeights GetSituationalWeights(
        int down, float distance, float lineOfScrimmage,
        int score, int awayScore)
    {
        bool isRedZone = lineOfScrimmage >= FieldGeometry.OpponentGoalLine - 20f;
        bool isPassingDown = (down >= 3 && distance >= 7f);
        bool isShortYardage = (down >= 3 && distance <= 3f);
        bool isTrailing = awayScore > score + 7;
        bool isProtectingLead = score > awayScore + 7;

        // Priority: most specific situation wins
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

        return Baseline;
    }

    private static SchemeWeights Blend(SchemeWeights a, SchemeWeights b, float t)
    {
        float u = 1f - t;
        return new SchemeWeights(
            a.Cover0 * t + b.Cover0 * u,
            a.Cover1 * t + b.Cover1 * u,
            a.Cover2Zone * t + b.Cover2Zone * u,
            a.Cover3Zone * t + b.Cover3Zone * u,
            a.Cover4Zone * t + b.Cover4Zone * u,
            a.Cover2Man * t + b.Cover2Man * u
        );
    }

    private static CoverageScheme PickScheme(SchemeWeights weights, Random rng)
    {
        float total = weights.Total;
        float roll = (float)rng.NextDouble() * total;

        roll -= weights.Cover0;
        if (roll <= 0) return CoverageScheme.Cover0;

        roll -= weights.Cover1;
        if (roll <= 0) return CoverageScheme.Cover1;

        roll -= weights.Cover2Zone;
        if (roll <= 0) return CoverageScheme.Cover2Zone;

        roll -= weights.Cover3Zone;
        if (roll <= 0) return CoverageScheme.Cover3Zone;

        roll -= weights.Cover4Zone;
        if (roll <= 0) return CoverageScheme.Cover4Zone;

        return CoverageScheme.Cover2Man;
    }
}
