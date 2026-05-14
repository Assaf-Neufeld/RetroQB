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
/// Produces coverage first, then a personnel-aware blitz once the offensive
/// surface is known.
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
    /// Makes the defensive call for one play when personnel is not yet known.
    /// </summary>
    public DefensiveCallDecision Decide(DefensiveContext context, DefensiveTeamAttributes attributes, Random rng)
    {
        return DecideCoverage(context, rng);
    }

    /// <summary>
    /// Chooses coverage from situation, stage and memory before personnel is known.
    /// </summary>
    public DefensiveCallDecision DecideCoverage(DefensiveContext context, Random rng)
    {
        return new DefensiveCallDecision(SelectScheme(context, rng), BlitzDecision.None);
    }

    /// <summary>
    /// Chooses blitzers from the active personnel package for the play.
    /// </summary>
    public BlitzDecision DecideBlitz(CoverageScheme scheme, DefensiveContext context, DefensiveTeamAttributes attributes, DefensivePersonnel personnel, Random rng)
    {
        return _blitzStrategy.DecideBlitzers(scheme, attributes, context, personnel, _memory, rng);
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
