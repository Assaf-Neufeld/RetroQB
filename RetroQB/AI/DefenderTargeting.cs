using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.AI;

/// <summary>
/// Handles target selection logic for defenders based on game state.
/// </summary>
public static class DefenderTargeting
{
    /// <summary>
    /// Determines the target position for a defender based on current game state.
    /// </summary>
    public static Vector2 GetTarget(
        Defender defender,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        Ball ball,
        bool qbIsRunner,
        bool useZoneCoverage,
        float lineOfScrimmage)
    {
        if (qbIsRunner)
        {
            return qb.Position;
        }

        if (ball.State == BallState.HeldByReceiver && ball.Holder != null)
        {
            return ball.Holder.Position;
        }

        if (ball.State == BallState.InAir)
        {
            return GetTargetDuringPass(defender, receivers, ball, useZoneCoverage, lineOfScrimmage);
        }

        if (defender.IsRusher)
        {
            return GetRushTarget(defender, qb, lineOfScrimmage);
        }

        return GetCoverageTarget(defender, qb, receivers, useZoneCoverage, lineOfScrimmage);
    }

    private static Vector2 GetTargetDuringPass(
        Defender defender,
        IReadOnlyList<Receiver> receivers,
        Ball ball,
        bool useZoneCoverage,
        float lineOfScrimmage)
    {
        // Only break on ball if defender is close enough to make a play
        float distToBall = Vector2.Distance(defender.Position, ball.Position);
        if (distToBall < 12f)
        {
            return ball.Position;
        }

        if (useZoneCoverage && defender.ZoneRole != CoverageRole.None)
        {
            return ZoneCoverage.GetZoneTarget(defender, receivers, lineOfScrimmage);
        }

        if (defender.CoverageReceiverIndex >= 0 && defender.CoverageReceiverIndex < receivers.Count)
        {
            return receivers[defender.CoverageReceiverIndex].Position;
        }

        return ball.Position;
    }

    private static Vector2 GetRushTarget(Defender defender, Quarterback qb, float lineOfScrimmage)
    {
        float stuntBlend = Math.Clamp((defender.Position.Y - (lineOfScrimmage + 1.0f)) / 4.5f, 0f, 1f);
        float lateralOffset = defender.RushLaneOffsetX * (0.55f + 0.45f * stuntBlend);
        float closeIn = defender.RushLaneOffsetX * -0.35f * (1f - stuntBlend);

        return qb.Position + new Vector2(lateralOffset + closeIn, 0f);
    }

    private static Vector2 GetCoverageTarget(
        Defender defender,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        bool useZoneCoverage,
        float lineOfScrimmage)
    {
        if (useZoneCoverage && defender.ZoneRole != CoverageRole.None)
        {
            return ZoneCoverage.GetZoneTarget(defender, receivers, lineOfScrimmage);
        }

        if (defender.CoverageReceiverIndex >= 0 && defender.CoverageReceiverIndex < receivers.Count)
        {
            return receivers[defender.CoverageReceiverIndex].Position;
        }

        return qb.Position;
    }
}
