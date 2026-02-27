using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;

namespace RetroQB.Gameplay;

public readonly record struct BlitzDecision(
    bool LeftOutsideLinebacker,
    bool MiddleLinebacker,
    bool RightOutsideLinebacker,
    bool LeftCornerback,
    bool RightCornerback);

public interface IBlitzDecisionStrategy
{
    BlitzDecision DecideBlitzers(CoverageScheme scheme, DefensiveTeamAttributes attributes, DefensiveContext context, bool useNickel, Random rng);
}

public sealed class DefaultBlitzDecisionStrategy : IBlitzDecisionStrategy
{
    public BlitzDecision DecideBlitzers(CoverageScheme scheme, DefensiveTeamAttributes attributes, DefensiveContext context, bool useNickel, Random rng)
    {
        float lbBlitzChance = GetLbBlitzChance(scheme, attributes, context);
        float cbBlitzChance = GetCbBlitzChance(scheme, attributes, context);

        return new BlitzDecision(
            LeftOutsideLinebacker: rng.NextDouble() < lbBlitzChance,
            MiddleLinebacker: !useNickel && rng.NextDouble() < lbBlitzChance,
            RightOutsideLinebacker: rng.NextDouble() < lbBlitzChance,
            LeftCornerback: rng.NextDouble() < cbBlitzChance,
            RightCornerback: rng.NextDouble() < cbBlitzChance);
    }

    private static float GetLbBlitzChance(CoverageScheme scheme, DefensiveTeamAttributes attributes, DefensiveContext context)
    {
        float baseChance = scheme switch
        {
            CoverageScheme.Cover0 => 0.35f,
            CoverageScheme.Cover1 => 0.15f,
            CoverageScheme.Cover2Man => 0.12f,
            CoverageScheme.Cover2Zone => 0.08f,
            CoverageScheme.Cover3Zone => 0.08f,
            CoverageScheme.Cover4Zone => 0.05f,
            _ => 0.10f
        };

        float situationalScale = GetBlitzSituationalMultiplier(context);
        float chance = baseChance * attributes.BlitzFrequency * situationalScale;
        return Math.Clamp(chance, 0f, 0.70f);
    }

    private static float GetCbBlitzChance(CoverageScheme scheme, DefensiveTeamAttributes attributes, DefensiveContext context)
    {
        float baseChance = scheme switch
        {
            CoverageScheme.Cover0 => 0.20f,
            CoverageScheme.Cover1 => 0.05f,
            _ => 0f
        };

        float situationalScale = GetBlitzSituationalMultiplier(context);
        float chance = baseChance * attributes.BlitzFrequency * situationalScale;
        return Math.Clamp(chance, 0f, 0.35f);
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
