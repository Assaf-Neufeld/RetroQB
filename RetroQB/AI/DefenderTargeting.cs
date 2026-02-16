using System.Numerics;
using RetroQB.Entities;
using RetroQB.Routes;

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
        float distanceFocus = Math.Clamp(1f - (distToBall / maxBreakDistance), 0f, 1f);

        // Defenders should break later than receivers to preserve offensive advantage,
        // but still close decisively once the throw is clearly committed.
        float flightProgress = ball.GetFlightProgress();
        float startBreakAt = defender.PositionRole == DefensivePosition.DB ? 0.36f : 0.46f;
        float progressFocus = Math.Clamp((flightProgress - startBreakAt) / (1f - startBreakAt), 0f, 1f);
        float ballFocus = distanceFocus * progressFocus;

        Vector2 ballLead = GetDefenderBallLead(ball, flightProgress, ballFocus);

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

    private static Vector2 GetDefenderBallLead(Ball ball, float flightProgress, float ballFocus)
    {
        if (ball.Velocity.LengthSquared() < 0.001f)
        {
            return ball.Position;
        }

        Vector2 throwDir = Vector2.Normalize(ball.Velocity);
        Vector2 predictedLanding = ball.ThrowStart + throwDir * ball.IntendedDistance;
        Vector2 remainingPath = predictedLanding - ball.Position;

        if (remainingPath.LengthSquared() < 0.001f)
        {
            return predictedLanding;
        }

        // Break to an ahead point on the current throw path (not the current ball position).
        // This keeps defender pursuit focused on where the pass is headed.
        float lookAhead = Math.Clamp(0.35f + flightProgress * 0.45f + ballFocus * 0.20f, 0.35f, 1f);
        return ball.Position + remainingPath * lookAhead;
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

            if (defender.PositionRole == DefensivePosition.DB)
            {
                return GetDbCoverageTarget(defender, receiver);
            }

            return receiver.Position;
        }

        return qb.Position;
    }

    private static Vector2 GetDbCoverageTarget(Defender defender, Receiver receiver)
    {
        // Off coverage DBs maintain cushion until receiver approaches.
        if (!defender.IsPressCoverage)
        {
            float distToReceiver = Vector2.Distance(defender.Position, receiver.Position);
            // Only pursue if receiver is within 8 yards or receiver is past the defender.
            if (distToReceiver > 8f && receiver.Position.Y < defender.Position.Y)
            {
                float targetX = defender.Position.X * 0.7f + receiver.Position.X * 0.3f;
                return new Vector2(targetX, defender.Position.Y);
            }
        }

        // Route-end behavior: try to undercut from in front (between QB and WR).
        if (IsNearRouteEnd(receiver))
        {
            float xShade = -receiver.RouteSide * 0.25f;
            return receiver.Position + new Vector2(xShade, -0.95f);
        }

        // Default behavior: trail the receiver slightly in-phase.
        Vector2 recvPos = receiver.Position;
        if (receiver.Velocity.LengthSquared() > 0.01f)
        {
            Vector2 trailOffset = Vector2.Normalize(receiver.Velocity) * -0.6f;
            recvPos += trailOffset;
        }
        return recvPos;
    }

    private static bool IsNearRouteEnd(Receiver receiver)
    {
        if (receiver.IsRunningBack || receiver.IsTightEnd)
        {
            return false;
        }

        float yProgress = receiver.Position.Y - receiver.RouteStart.Y;
        var stems = RouteGeometry.GetStemDistances(receiver);

        return receiver.Route switch
        {
            RouteType.Go => yProgress >= stems.Deep,
            RouteType.Slant => Vector2.Distance(receiver.Position, receiver.RouteStart) >= RouteGeometry.SlantLength * 0.9f,
            RouteType.OutShallow => HasReachedHorizontalBreakEnd(receiver, stems.Shallow, RouteGeometry.OutBreakLength),
            RouteType.OutDeep => HasReachedHorizontalBreakEnd(receiver, stems.Deep, RouteGeometry.OutBreakLength),
            RouteType.InShallow => HasReachedHorizontalBreakEnd(receiver, stems.Shallow, RouteGeometry.InBreakLength),
            RouteType.InDeep => HasReachedHorizontalBreakEnd(receiver, stems.Deep, RouteGeometry.InBreakLength),
            RouteType.DoubleMove => HasReachedHorizontalBreakEnd(receiver, stems.Deep, RouteGeometry.InBreakLength),
            RouteType.PostShallow => yProgress >= stems.Shallow + RouteGeometry.PostBreakLength * 0.75f,
            RouteType.PostDeep => yProgress >= stems.Deep + RouteGeometry.PostBreakLength * 0.75f,
            RouteType.Flat => yProgress >= 2.5f,
            _ => false
        };
    }

    private static bool HasReachedHorizontalBreakEnd(Receiver receiver, float stemDistance, float breakLength)
    {
        if (receiver.Position.Y - receiver.RouteStart.Y < stemDistance)
        {
            return false;
        }

        float expectedBreakX = receiver.Route switch
        {
            RouteType.OutShallow or RouteType.OutDeep => receiver.RouteStart.X + receiver.RouteSide * breakLength,
            RouteType.InShallow or RouteType.InDeep or RouteType.DoubleMove => receiver.RouteStart.X - receiver.RouteSide * breakLength,
            _ => receiver.RouteStart.X
        };

        return MathF.Abs(receiver.Position.X - expectedBreakX) <= 1.0f;
    }
}
