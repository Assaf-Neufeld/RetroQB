using RetroQB.Gameplay;

namespace RetroQB.AI;

/// <summary>
/// Tracks how well each coverage scheme and blitz configuration has performed
/// during a game. Produces weight multipliers that nudge future playcalling
/// toward what has worked and away from what has been burned.
///
/// Learning is graduated: nearly zero in the early game, full strength later.
/// All multipliers are clamped so no option is fully eliminated or absurdly dominant.
/// </summary>
public sealed class DefensiveMemory
{
    // --- Tuning constants ------------------------------------------------

    /// <summary>Number of completed plays before learning reaches full strength.</summary>
    private const float LearningRampPlays = 30f;

    /// <summary>Raw multiplier shift when a scheme is "burned" (big gain / TD).</summary>
    private const float SchemeBurnPenalty = 0.15f;

    /// <summary>Raw multiplier shift when a scheme holds (short/negative gain).</summary>
    private const float SchemeSuccessBonus = 0.06f;

    /// <summary>Raw multiplier shift when a blitz succeeds (sack, incompletion, negative gain).</summary>
    private const float BlitzSuccessBonus = 0.15f;

    /// <summary>Raw multiplier shift when a blitz fails (big gain allowed).</summary>
    private const float BlitzFailPenalty = 0.12f;

    /// <summary>Minimum allowed multiplier — no scheme/blitz is ever fully killed.</summary>
    private const float MinMultiplier = 0.35f;

    /// <summary>Maximum allowed multiplier — no scheme/blitz becomes absurdly dominant.</summary>
    private const float MaxMultiplier = 1.85f;

    /// <summary>Gain threshold above which a play is considered a "burn".</summary>
    private const float BigGainThreshold = 15f;

    /// <summary>Gain threshold above which a blitz is considered to have failed.</summary>
    private const float BlitzFailGainThreshold = 12f;

    // --- State -----------------------------------------------------------

    private readonly Dictionary<CoverageScheme, float> _schemeMultipliers = new();
    private readonly Dictionary<string, float> _blitzMultipliers = new();
    private int _completedPlays;

    public DefensiveMemory()
    {
        Reset();
    }

    /// <summary>
    /// Clears all learned adjustments. Called at the start of each new game.
    /// </summary>
    public void Reset()
    {
        _schemeMultipliers.Clear();
        foreach (CoverageScheme scheme in Enum.GetValues<CoverageScheme>())
        {
            _schemeMultipliers[scheme] = 1.0f;
        }

        _blitzMultipliers.Clear();
        _completedPlays = 0;
    }

    /// <summary>
    /// Current learning strength: 0 at game start, ramping to 1 over <see cref="LearningRampPlays"/> plays.
    /// </summary>
    public float LearningRate => Math.Clamp(_completedPlays / LearningRampPlays, 0f, 1f);

    /// <summary>
    /// Gets the current weight multiplier for a coverage scheme.
    /// Returns a value in [<see cref="MinMultiplier"/>, <see cref="MaxMultiplier"/>].
    /// </summary>
    public float GetSchemeMultiplier(CoverageScheme scheme)
    {
        return _schemeMultipliers.GetValueOrDefault(scheme, 1.0f);
    }

    /// <summary>
    /// Gets the current weight multiplier for a blitz package (keyed by name).
    /// Returns a value in [<see cref="MinMultiplier"/>, <see cref="MaxMultiplier"/>].
    /// </summary>
    public float GetBlitzMultiplier(string blitzPackageName)
    {
        return _blitzMultipliers.GetValueOrDefault(blitzPackageName, 1.0f);
    }

    /// <summary>
    /// Observes a completed play and adjusts internal multipliers.
    /// Should be called once per play, after the play record is finalized.
    /// </summary>
    public void RecordOutcome(PlayRecord record)
    {
        _completedPlays++;
        float lr = LearningRate;

        // --- Scheme learning ---
        bool isBurn = record.Outcome == PlayOutcome.Touchdown || record.Gain >= BigGainThreshold;
        bool isSchemeSuccess = record.Gain <= 3f && record.Outcome != PlayOutcome.Touchdown;

        if (isBurn)
        {
            AdjustScheme(record.CoverageScheme, -SchemeBurnPenalty * lr);
        }
        else if (isSchemeSuccess)
        {
            AdjustScheme(record.CoverageScheme, SchemeSuccessBonus * lr);
        }

        // --- Blitz learning ---
        bool hadBlitz = record.Blitzers.Count > 0;
        if (hadBlitz)
        {
            string blitzKey = BuildBlitzKey(record.Blitzers);
            bool blitzSuccess = record.IsSack
                || record.Outcome == PlayOutcome.Incomplete
                || record.Outcome == PlayOutcome.Interception
                || record.Gain < 0f;
            bool blitzFail = record.Gain >= BlitzFailGainThreshold || record.Outcome == PlayOutcome.Touchdown;

            if (blitzSuccess)
            {
                AdjustBlitz(blitzKey, BlitzSuccessBonus * lr);
            }
            else if (blitzFail)
            {
                AdjustBlitz(blitzKey, -BlitzFailPenalty * lr);
            }
        }
    }

    // --- Internals -------------------------------------------------------

    private void AdjustScheme(CoverageScheme scheme, float delta)
    {
        float current = _schemeMultipliers.GetValueOrDefault(scheme, 1.0f);
        _schemeMultipliers[scheme] = Math.Clamp(current + delta, MinMultiplier, MaxMultiplier);
    }

    private void AdjustBlitz(string blitzKey, float delta)
    {
        float current = _blitzMultipliers.GetValueOrDefault(blitzKey, 1.0f);
        _blitzMultipliers[blitzKey] = Math.Clamp(current + delta, MinMultiplier, MaxMultiplier);
    }

    /// <summary>
    /// Builds a stable key from the sorted blitzer labels so that the same
    /// combination always maps to the same dictionary entry.
    /// </summary>
    private static string BuildBlitzKey(List<string> blitzers)
    {
        var sorted = blitzers.OrderBy(b => b, StringComparer.Ordinal);
        return string.Join("+", sorted);
    }
}
