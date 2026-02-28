using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Gameplay;

namespace RetroQB.AI;

/// <summary>
/// The single decision-point for all pre-snap defensive choices.
///
/// Consolidates three independent factors:
///   1. Situational weights (down, distance, field position, score)
///   2. Season-stage coverage pool (which schemes are available)
///   3. Adaptive memory (what has worked / been burned this game)
///
/// Produces a <see cref="DefensiveCallDecision"/> containing the chosen
/// coverage scheme and blitz decision which the <see cref="DefenseFactory"/>
/// uses to lay out defenders.
/// </summary>
public sealed class DefensiveCoordinator
{
    private readonly IBlitzDecisionStrategy _blitzStrategy;
    private readonly DefensiveMemory _memory;

    public DefensiveCoordinator(DefensiveMemory memory, IBlitzDecisionStrategy? blitzStrategy = null)
    {
        _memory = memory;
        _blitzStrategy = blitzStrategy ?? new DefaultBlitzDecisionStrategy();
    }

    /// <summary>
    /// Makes the defensive call for one play: scheme + blitz.
    /// </summary>
    public DefensiveCallDecision Decide(DefensiveContext context, DefensiveTeamAttributes attributes, Random rng)
    {
        // --- Step 1: Choose coverage scheme ---
        CoverageScheme scheme = SelectScheme(context, rng);

        // --- Step 2: Choose blitz package (strategy may use its own memory hooks) ---
        BlitzDecision blitz = _blitzStrategy.DecideBlitzers(scheme, attributes, context, _memory, rng);

        return new DefensiveCallDecision(scheme, blitz);
    }

    /// <summary>
    /// Consolidated scheme selection pipeline:
    ///   situational weights → stage pool → memory multipliers → weighted pick.
    /// </summary>
    private CoverageScheme SelectScheme(DefensiveContext context, Random rng)
    {
        // 1. Situational weights (down/distance/score/field)
        Dictionary<CoverageScheme, float> weights =
            CoverageSchemeSelector.GetSituationalWeights(
                context.Down, context.Distance, context.LineOfScrimmage,
                context.Score, context.AwayScore);

        // 2. Stage filter
        CoverageSchemeSelector.ApplyStagePool(weights, context.Stage);

        // 3. Memory adjustments
        foreach (CoverageScheme scheme in weights.Keys.ToList())
        {
            weights[scheme] *= _memory.GetSchemeMultiplier(scheme);
        }

        // 4. Safeguard: if everything was zeroed out, fall back to zone basics
        float total = weights.Values.Sum();
        if (total <= 0f)
        {
            weights[CoverageScheme.Cover2Zone] = 1f;
            weights[CoverageScheme.Cover3Zone] = 1f;
        }

        return CoverageSchemeSelector.PickScheme(weights, rng);
    }
}

/// <summary>
/// The result of the coordinator's pre-snap decision.
/// </summary>
public readonly record struct DefensiveCallDecision(CoverageScheme Scheme, BlitzDecision Blitz);
