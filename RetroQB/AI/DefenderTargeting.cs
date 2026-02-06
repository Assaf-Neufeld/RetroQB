using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.AI;

/// <summary>
/// Handles target selection logic for defenders based on game state.
/// </summary>
public static class DefenderTargeting
{
    /// <summary>
    /// Updates a defender's position and velocity based on current game state.
    /// </summary>
    public static void UpdateDefender(
        Defender defender,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        Ball ball,
        float speedMultiplier,
        float dt,
        bool qbIsRunner,
        bool useZoneCoverage,
        float lineOfScrimmage)
    {
        float speed = defender.Speed * speedMultiplier;

        Vector2 target = GetTarget(
            defender, qb, receivers, ball,
            qbIsRunner, useZoneCoverage, lineOfScrimmage);

        Vector2 dir = target - defender.Position;
        if (dir.LengthSquared() > 0.001f)
        {
            dir = Vector2.Normalize(dir);
        }
        defender.Velocity = dir * speed;
        defender.Position += defender.Velocity * dt;
    }

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
        float distToBall = Vector2.Distance(defender.Position, ball.Position);
        float maxBreakDistance = defender.PositionRole == DefensivePosition.DB ? 18f : 14f;
        float ballFocus = Math.Clamp(1f - (distToBall / maxBreakDistance), 0f, 1f);
        Vector2 ballLead = ball.Position + ball.Velocity * 0.20f;

        if (useZoneCoverage && defender.ZoneRole != CoverageRole.None)
        {
            Vector2 zoneTarget = ZoneCoverage.GetZoneTarget(defender, receivers, lineOfScrimmage);
            return ballFocus > 0f ? Vector2.Lerp(zoneTarget, ballLead, ballFocus) : zoneTarget;
        }

        if (defender.CoverageReceiverIndex >= 0 && defender.CoverageReceiverIndex < receivers.Count)
        {
            Vector2 receiverTarget = receivers[defender.CoverageReceiverIndex].Position;
            return ballFocus > 0f ? Vector2.Lerp(receiverTarget, ballLead, ballFocus) : receiverTarget;
        }

        return ballLead;
    }

    private static Vector2 GetRushTarget(Defender defender, Quarterback qb, float lineOfScrimmage)
    {
        // Defensive Ends use a circular outside rush path to bypass blockers
        if (defender.PositionRole == DefensivePosition.DE)
        {
            return GetEdgeRushTarget(defender, qb, lineOfScrimmage);
        }

        // Standard DL rush - straight at the QB with lane offset
        float stuntBlend = Math.Clamp((defender.Position.Y - (lineOfScrimmage + 1.0f)) / 4.5f, 0f, 1f);
        float lateralOffset = defender.RushLaneOffsetX * (0.55f + 0.45f * stuntBlend);
        float closeIn = defender.RushLaneOffsetX * -0.35f * (1f - stuntBlend);

        return qb.Position + new Vector2(lateralOffset + closeIn, 0f);
    }

    /// <summary>
    /// Calculates edge rush target for Defensive Ends.
    /// DEs take a circular arc path around the outside to bypass blockers.
    /// </summary>
    private static Vector2 GetEdgeRushTarget(Defender defender, Quarterback qb, float lineOfScrimmage)
    {
        float distToQb = Vector2.Distance(defender.Position, qb.Position);
        
        // Determine which side the DE is on (left = negative X offset, right = positive)
        bool isLeftSide = defender.RushLaneOffsetX < 0;
        float sideMultiplier = isLeftSide ? -1f : 1f;
        
        // Once close to QB, go straight at him
        float closeRange = 3.5f;
        if (distToQb < closeRange)
        {
            return qb.Position;
        }
        
        // Calculate arc based on distance to QB (not distance from LOS)
        // This ensures continuous pursuit regardless of field position
        float maxArcDistance = 16f;
        float arcProgress = Math.Clamp(1f - (distToQb / maxArcDistance), 0f, 1f);
        
        // Outside offset decreases as we get closer to QB (arc tightens)
        // Start at 7 yards wide, shrink to 1.5 yards as we approach
        float maxOutsideOffset = 7f;
        float minOutsideOffset = 1.5f;
        float outsideOffset = maxOutsideOffset - (arcProgress * (maxOutsideOffset - minOutsideOffset));
        
        // Target point is offset from QB position toward the outside
        float targetX = qb.Position.X + (sideMultiplier * outsideOffset);
        
        // Y target leads slightly ahead of QB to create the rushing arc
        // As we get closer, target becomes more directly at QB
        float yLead = (1f - arcProgress) * 3f;
        float targetY = qb.Position.Y - yLead;
        
        return new Vector2(targetX, targetY);
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
            var receiver = receivers[defender.CoverageReceiverIndex];
            
            // Off coverage DBs maintain cushion until receiver approaches
            if (!defender.IsPressCoverage && defender.PositionRole == DefensivePosition.DB)
            {
                float distToReceiver = Vector2.Distance(defender.Position, receiver.Position);
                // Only pursue if receiver is within 8 yards or receiver is past the defender
                if (distToReceiver > 8f && receiver.Position.Y < defender.Position.Y)
                {
                    // Stay in current position with slight drift toward receiver's X
                    float targetX = defender.Position.X * 0.7f + receiver.Position.X * 0.3f;
                    return new Vector2(targetX, defender.Position.Y);
                }
            }
            
            return receiver.Position;
        }

        return qb.Position;
    }
}
