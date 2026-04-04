using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.AI;

/// <summary>
/// Handles zone coverage logic for defenders including zone bounds, anchors, and receiver matching.
/// </summary>
public static class ZoneCoverage
{
    public readonly struct ZoneBounds
    {
        public readonly float XMin;
        public readonly float XMax;
        public readonly float YMin;
        public readonly float YMax;

        public ZoneBounds(float xMin, float xMax, float yMin, float yMax)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
        }

        public bool ContainsX(float x) => x >= XMin && x <= XMax;
        public bool ContainsY(float y) => y >= YMin && y <= YMax;
    }

    /// <summary>
    /// Gets the target position for a defender in zone coverage.
    /// </summary>
    public static Vector2 GetZoneTarget(Defender defender, IReadOnlyList<Receiver> receivers, float lineOfScrimmage)
    {
        ZoneBounds bounds = GetZoneBounds(defender, lineOfScrimmage);
        Vector2 baseTarget = GetZoneAnchor(defender, lineOfScrimmage);

        if (IsDeepZone(defender.ZoneRole))
        {
            baseTarget = new Vector2(baseTarget.X, GetDeepZoneDepth(defender, receivers, bounds, lineOfScrimmage));
        }

        if (TryGetZoneMatch(defender, receivers, lineOfScrimmage, out var match))
        {
            return CalculateMatchTarget(defender, match, receivers, baseTarget, bounds, lineOfScrimmage);
        }

        if (IsDeepZone(defender.ZoneRole)
            && TryGetNearestReceiverInLane(defender, receivers, bounds, lineOfScrimmage, out var nearest))
        {
            return CalculateMatchTarget(defender, nearest, receivers, baseTarget, bounds, lineOfScrimmage);
        }

        return baseTarget;
    }

    /// <summary>
    /// Gets the anchor position for a zone based on the defender's role.
    /// Applies per-defender jitter to shift zone seams play-to-play.
    /// </summary>
    public static Vector2 GetZoneAnchor(Defender defender, float lineOfScrimmage)
    {
        float depth = defender.PositionRole == DefensivePosition.DB
            ? Constants.ZoneCoverageDepthDb
            : Constants.ZoneCoverageDepth;

        float jitterX = defender.ZoneJitterX;

        Vector2 anchor = defender.ZoneRole switch
        {
            CoverageRole.DeepLeft => new Vector2(Constants.FieldWidth * 0.25f, lineOfScrimmage + Constants.ZoneCoverageDepthDb - 0.9f),
            CoverageRole.DeepMiddle => new Vector2(Constants.FieldWidth * 0.50f, lineOfScrimmage + Constants.ZoneCoverageDepthDb + 1.4f),
            CoverageRole.DeepRight => new Vector2(Constants.FieldWidth * 0.75f, lineOfScrimmage + Constants.ZoneCoverageDepthDb - 0.9f),
            CoverageRole.DeepQuarterLeft => new Vector2(Constants.FieldWidth * 0.40f, lineOfScrimmage + Constants.ZoneCoverageDepthDb + 0.6f),
            CoverageRole.DeepQuarterRight => new Vector2(Constants.FieldWidth * 0.60f, lineOfScrimmage + Constants.ZoneCoverageDepthDb + 0.6f),
            CoverageRole.FlatLeft => new Vector2(Constants.FieldWidth * 0.12f, lineOfScrimmage + Constants.ZoneCoverageDepthFlat - 0.3f),
            CoverageRole.FlatRight => new Vector2(Constants.FieldWidth * 0.88f, lineOfScrimmage + Constants.ZoneCoverageDepthFlat - 0.3f),
            CoverageRole.HookLeft => new Vector2(Constants.FieldWidth * 0.34f, lineOfScrimmage + Constants.ZoneCoverageDepth - 0.6f),
            CoverageRole.HookMiddle => new Vector2(Constants.FieldWidth * 0.50f, lineOfScrimmage + Constants.ZoneCoverageDepth + 0.2f),
            CoverageRole.HookRight => new Vector2(Constants.FieldWidth * 0.66f, lineOfScrimmage + Constants.ZoneCoverageDepth - 0.6f),
            CoverageRole.Robber => new Vector2(Constants.FieldWidth * 0.50f, lineOfScrimmage + Constants.ZoneCoverageDepthFlat + 1.2f),
            _ => new Vector2(Constants.FieldWidth * 0.50f, lineOfScrimmage + depth)
        };

        // Apply horizontal jitter, clamped to field bounds
        anchor.X = Math.Clamp(anchor.X + jitterX, 0.5f, Constants.FieldWidth - 0.5f);
        return anchor;
    }

    /// <summary>
    /// Gets the bounds for a specific zone role.
    /// </summary>
    public static ZoneBounds GetZoneBounds(Defender defender, float lineOfScrimmage)
    {
        float yMin = lineOfScrimmage + 1.0f;
        var (xCenter, width, yMax) = GetZoneParameters(defender.ZoneRole, lineOfScrimmage, ref yMin);
        xCenter += defender.ZoneJitterX;

        float halfWidth = width * 0.5f;
        float xMin = Math.Clamp(xCenter - halfWidth, 0.5f, Constants.FieldWidth - 0.5f);
        float xMax = Math.Clamp(xCenter + halfWidth, 0.5f, Constants.FieldWidth - 0.5f);

        return new ZoneBounds(xMin, xMax, yMin, yMax);
    }

    /// <summary>
    /// Tries to find a receiver that matches within the defender's zone.
    /// </summary>
    public static bool TryGetZoneMatch(Defender defender, IReadOnlyList<Receiver> receivers, float lineOfScrimmage, out Receiver match)
    {
        match = null!;
        ZoneBounds bounds = GetZoneBounds(defender, lineOfScrimmage);
        Vector2 anchor = GetZoneAnchor(defender, lineOfScrimmage);

        float bestScore = float.NegativeInfinity;

        foreach (var receiver in receivers)
        {
            if (!IsValidZoneTarget(defender, receiver, receivers, bounds, lineOfScrimmage))
            {
                continue;
            }

            float score = CalculateZoneMatchScore(defender, receiver, bounds, anchor, lineOfScrimmage);

            if (score > bestScore)
            {
                bestScore = score;
                match = receiver;
            }
        }

        return bestScore > float.NegativeInfinity;
    }

    private static bool IsDeepZone(CoverageRole role) =>
        role is CoverageRole.DeepLeft or CoverageRole.DeepMiddle or CoverageRole.DeepRight
            or CoverageRole.DeepQuarterLeft or CoverageRole.DeepQuarterRight;

    private static bool IsHookZone(CoverageRole role) =>
        role is CoverageRole.HookLeft or CoverageRole.HookMiddle or CoverageRole.HookRight or CoverageRole.Robber;

    private static bool IsFlatZone(CoverageRole role) =>
        role is CoverageRole.FlatLeft or CoverageRole.FlatRight;

    private static Vector2 CalculateMatchTarget(
        Defender defender,
        Receiver match,
        IReadOnlyList<Receiver> receivers,
        Vector2 baseTarget,
        ZoneBounds bounds,
        float lineOfScrimmage)
    {
        Vector2 projected = GetProjectedReceiverPosition(match, defender.ZoneRole);

        if (IsDeepZone(defender.ZoneRole))
        {
            return CalculateDeepMatchTarget(defender, match, receivers, projected, baseTarget, bounds, lineOfScrimmage);
        }

        return CalculateUnderneathMatchTarget(defender, match, projected, baseTarget, bounds, lineOfScrimmage);
    }

    private static float GetDeepZoneDepth(Defender defender, IReadOnlyList<Receiver> receivers, ZoneBounds bounds, float lineOfScrimmage)
    {
        float deepest = FindDeepestReceiverInZone(receivers, bounds, lineOfScrimmage);

        float baseDepth = GetZoneAnchor(defender, lineOfScrimmage).Y;
        float targetDepth = baseDepth;

        if (deepest > float.NegativeInfinity)
        {
            float stagger = defender.ZoneRole switch
            {
                CoverageRole.DeepMiddle => 0.7f,
                CoverageRole.DeepQuarterLeft or CoverageRole.DeepQuarterRight => 0.35f,
                _ => 0f
            };
            targetDepth = MathF.Max(baseDepth, deepest + Constants.ZoneDeepCushion + stagger);
        }

        float maxFieldY = Constants.EndZoneDepth + 100f - 0.5f;
        return MathF.Min(targetDepth, maxFieldY);
    }

    private static float FindDeepestReceiverInZone(IReadOnlyList<Receiver> receivers, ZoneBounds bounds, float lineOfScrimmage)
    {
        float deepest = float.NegativeInfinity;

        foreach (var receiver in receivers)
        {
            if (!receiver.Eligible || receiver.IsBlocking)
            {
                continue;
            }

            if (!bounds.ContainsX(receiver.Position.X))
            {
                continue;
            }

            if (receiver.Position.Y < lineOfScrimmage)
            {
                continue;
            }

            if (receiver.Position.Y > deepest)
            {
                deepest = receiver.Position.Y;
            }
        }

        return deepest;
    }

    private static bool TryGetNearestReceiverInLane(
        Defender defender,
        IReadOnlyList<Receiver> receivers,
        ZoneBounds bounds,
        float lineOfScrimmage,
        out Receiver nearest)
    {
        nearest = null!;
        float bestDistSq = float.PositiveInfinity;

        foreach (var receiver in receivers)
        {
            if (!receiver.Eligible || receiver.IsBlocking)
            {
                continue;
            }

            if (!IsReceiverInDeepLane(defender, receiver, bounds, lineOfScrimmage))
            {
                continue;
            }

            float distSq = Vector2.DistanceSquared(defender.Position, receiver.Position);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                nearest = receiver;
            }
        }

        return bestDistSq < float.PositiveInfinity;
    }

    private static bool IsValidZoneTarget(
        Defender defender,
        Receiver receiver,
        IReadOnlyList<Receiver> receivers,
        ZoneBounds bounds,
        float lineOfScrimmage)
    {
        if (!receiver.Eligible || receiver.IsBlocking)
        {
            return false;
        }

        float padding = Constants.ZoneMatchAttachRadius;
        if (receiver.Position.X < bounds.XMin - padding || receiver.Position.X > bounds.XMax + padding)
        {
            return false;
        }

        if (receiver.Position.Y < bounds.YMin - padding)
        {
            return false;
        }

        float yMax = bounds.YMax + padding;
        float carryCeiling = GetZoneCarryCeiling(defender.ZoneRole, bounds, lineOfScrimmage) + padding;
        bool inZoneDepth = receiver.Position.Y <= yMax;
        bool carryDeep = receiver.Position.Y > yMax && receiver.Position.Y <= carryCeiling;

        if (inZoneDepth || carryDeep)
        {
            return true;
        }

        return IsDeepZone(defender.ZoneRole)
            && ShouldCarryIsolatedVertical(defender, receiver, receivers, bounds, lineOfScrimmage);
    }

    private static float CalculateZoneMatchScore(Defender defender, Receiver receiver, ZoneBounds bounds, Vector2 anchor, float lineOfScrimmage)
    {
        Vector2 projected = GetProjectedReceiverPosition(receiver, defender.ZoneRole);
        bool inZoneDepth = projected.Y <= bounds.YMax;
        float horizontalDelta = MathF.Abs(projected.X - anchor.X);
        float travelWidth = MathF.Abs(GetReceiverTravelDirection(receiver).X);

        if (IsDeepZone(defender.ZoneRole))
        {
            float verticalThreat = MathF.Max(receiver.Position.Y, projected.Y);
            return verticalThreat * 4.2f - horizontalDelta * 1.35f + travelWidth * 1.1f;
        }

        float distancePenalty = Vector2.Distance(defender.Position, projected) * 0.55f;
        float sidelinePenalty = IsHookZone(defender.ZoneRole)
            ? MathF.Max(0f, horizontalDelta - GetHorizontalStretch(defender.ZoneRole) * 0.65f) * 2.4f
            : 0f;
        float widthPenalty = IsHookZone(defender.ZoneRole)
            ? horizontalDelta * 1.7f
            : horizontalDelta * 0.7f;

        return (inZoneDepth ? 1000f : 820f)
            + projected.Y * 2.1f
            + travelWidth * 2.0f
            - widthPenalty
            - sidelinePenalty
            - distancePenalty;
    }

    private static Vector2 CalculateDeepMatchTarget(
        Defender defender,
        Receiver receiver,
        IReadOnlyList<Receiver> receivers,
        Vector2 projected,
        Vector2 baseTarget,
        ZoneBounds bounds,
        float lineOfScrimmage)
    {
        Vector2 travelDir = GetReceiverTravelDirection(receiver);
        bool isolatedVerticalCarry = ShouldCarryIsolatedVertical(defender, receiver, receivers, bounds, lineOfScrimmage);
        float leverageX = defender.ZoneRole switch
        {
            CoverageRole.DeepLeft => -0.9f,
            CoverageRole.DeepRight => 0.9f,
            CoverageRole.DeepQuarterLeft => -0.45f,
            CoverageRole.DeepQuarterRight => 0.45f,
            _ => 0f
        };

        if (isolatedVerticalCarry)
        {
            leverageX *= 0.35f;
        }

        float xMin = bounds.XMin + 0.35f;
        float xMax = bounds.XMax - 0.35f;
        float desiredX = Math.Clamp(projected.X + leverageX, xMin, xMax);
        float targetX = isolatedVerticalCarry
            ? Lerp(baseTarget.X, desiredX, 0.84f)
            : desiredX;
        float routeThreatY = MathF.Max(receiver.Position.Y, projected.Y);
        float angleBonus = MathF.Abs(travelDir.X) * 0.75f;
        float deepCushion = isolatedVerticalCarry
            ? Constants.ZoneDeepCushion + 0.65f
            : Constants.ZoneDeepCushion;
        float targetY = MathF.Max(baseTarget.Y, routeThreatY + deepCushion + angleBonus);

        float maxFieldY = Constants.EndZoneDepth + 100f - 0.5f;
        return new Vector2(targetX, MathF.Min(targetY, maxFieldY));
    }

    private static bool ShouldCarryIsolatedVertical(
        Defender defender,
        Receiver receiver,
        IReadOnlyList<Receiver> receivers,
        ZoneBounds bounds,
        float lineOfScrimmage)
    {
        Vector2 projected = GetProjectedReceiverPosition(receiver, defender.ZoneRole);
        Vector2 travelDir = GetReceiverTravelDirection(receiver);
        if (travelDir.Y < 0.88f || !IsReceiverInDeepLane(defender, receiver, bounds, lineOfScrimmage))
        {
            return false;
        }

        return !HasNearbyCompetingReceiver(receiver, projected, receivers, bounds, lineOfScrimmage, defender.ZoneRole);
    }

    private static bool IsReceiverInDeepLane(
        Defender defender,
        Receiver receiver,
        ZoneBounds bounds,
        float lineOfScrimmage)
    {
        if (receiver.Position.Y < lineOfScrimmage)
        {
            return false;
        }

        Vector2 projected = GetProjectedReceiverPosition(receiver, defender.ZoneRole);
        float padding = Constants.ZoneMatchAttachRadius + 0.6f;
        return projected.X >= bounds.XMin - padding
            && projected.X <= bounds.XMax + padding;
    }

    private static bool HasNearbyCompetingReceiver(
        Receiver primary,
        Vector2 primaryProjected,
        IReadOnlyList<Receiver> receivers,
        ZoneBounds bounds,
        float lineOfScrimmage,
        CoverageRole role)
    {
        float nearbyWidth = MathF.Max(4.5f, Constants.ZoneMatchWidthDeep * 0.38f);

        foreach (var receiver in receivers)
        {
            if (ReferenceEquals(receiver, primary) || !receiver.Eligible || receiver.IsBlocking)
            {
                continue;
            }

            if (receiver.Position.Y < lineOfScrimmage + Constants.ZoneCoverageDepth - 2f)
            {
                continue;
            }

            Vector2 projected = GetProjectedReceiverPosition(receiver, role);
            if (projected.X < bounds.XMin - Constants.ZoneMatchAttachRadius
                || projected.X > bounds.XMax + Constants.ZoneMatchAttachRadius)
            {
                continue;
            }

            float horizontalGap = MathF.Abs(projected.X - primaryProjected.X);
            float verticalGap = MathF.Abs(projected.Y - primaryProjected.Y);
            bool sameWindow = horizontalGap <= nearbyWidth && verticalGap <= 12f;
            bool stackedThreat = projected.Y >= primaryProjected.Y - 3f && horizontalGap <= nearbyWidth * 0.72f;

            if (sameWindow || stackedThreat)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector2 CalculateUnderneathMatchTarget(
        Defender defender,
        Receiver receiver,
        Vector2 projected,
        Vector2 baseTarget,
        ZoneBounds bounds,
        float lineOfScrimmage)
    {
        Vector2 travelDir = GetReceiverTravelDirection(receiver);
        float stretch = GetHorizontalStretch(defender.ZoneRole);
        float xMin = MathF.Max(bounds.XMin + 0.25f, baseTarget.X - stretch);
        float xMax = MathF.Min(bounds.XMax - 0.25f, baseTarget.X + stretch);

        float leverageX = defender.ZoneRole switch
        {
            CoverageRole.HookLeft => 0.9f,
            CoverageRole.HookRight => -0.9f,
            _ => 0f
        };

        float desiredX = Math.Clamp(projected.X + leverageX, xMin, xMax);
        float xTrackBlend = defender.ZoneRole switch
        {
            CoverageRole.Robber => 0.74f,
            CoverageRole.HookMiddle => 0.58f,
            CoverageRole.HookLeft or CoverageRole.HookRight => 0.66f,
            CoverageRole.FlatLeft or CoverageRole.FlatRight => 0.82f,
            _ => 0.55f
        };
        float targetX = Lerp(baseTarget.X, desiredX, xTrackBlend);

        float routeCutoff = projected.Y - (1.15f + MathF.Abs(travelDir.X) * 0.8f);
        float carryCeiling = GetZoneCarryCeiling(defender.ZoneRole, bounds, lineOfScrimmage);
        float desiredY = Math.Clamp(routeCutoff, bounds.YMin, carryCeiling);
        float minDrop = defender.ZoneRole switch
        {
            CoverageRole.FlatLeft or CoverageRole.FlatRight => lineOfScrimmage + 2.6f,
            CoverageRole.Robber => lineOfScrimmage + 3.8f,
            _ => lineOfScrimmage + 4.6f
        };
        desiredY = MathF.Max(desiredY, minDrop);

        float yTrackBlend = defender.ZoneRole switch
        {
            CoverageRole.Robber => 0.76f,
            CoverageRole.HookMiddle => 0.62f,
            CoverageRole.HookLeft or CoverageRole.HookRight => 0.68f,
            CoverageRole.FlatLeft or CoverageRole.FlatRight => 0.72f,
            _ => 0.55f
        };
        float targetY = Lerp(baseTarget.Y, desiredY, yTrackBlend);

        return new Vector2(targetX, targetY);
    }

    private static Vector2 GetProjectedReceiverPosition(Receiver receiver, CoverageRole role)
    {
        Vector2 travelDir = GetReceiverTravelDirection(receiver);
        float lookAhead = IsDeepZone(role)
            ? Constants.ZoneLookAheadDeep
            : Constants.ZoneLookAheadUnderneath;

        if (IsFlatZone(role) && receiver.PositionRole == OffensivePosition.RB)
        {
            lookAhead += 0.5f;
        }

        return receiver.Position + travelDir * lookAhead;
    }

    private static Vector2 GetReceiverTravelDirection(Receiver receiver)
    {
        if (receiver.Velocity.LengthSquared() > 0.01f)
        {
            return Vector2.Normalize(receiver.Velocity);
        }

        Vector2 fromStart = receiver.Position - receiver.RouteStart;
        if (fromStart.LengthSquared() > 0.01f)
        {
            return Vector2.Normalize(fromStart);
        }

        return new Vector2(0f, 1f);
    }

    private static float GetHorizontalStretch(CoverageRole role)
    {
        return role switch
        {
            CoverageRole.Robber => Constants.ZoneHookMiddleStretch * 0.58f,
            CoverageRole.HookMiddle => Constants.ZoneHookMiddleStretch,
            CoverageRole.HookLeft or CoverageRole.HookRight => Constants.ZoneHookSideStretch,
            CoverageRole.FlatLeft or CoverageRole.FlatRight => Constants.ZoneFlatStretch,
            _ => Constants.ZoneMatchWidthDeep * 0.5f
        };
    }

    private static float GetZoneCarryCeiling(CoverageRole role, ZoneBounds bounds, float lineOfScrimmage)
    {
        return role switch
        {
            CoverageRole.FlatLeft or CoverageRole.FlatRight => lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer,
            CoverageRole.Robber => lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer * 0.75f,
            CoverageRole.HookLeft or CoverageRole.HookMiddle or CoverageRole.HookRight => lineOfScrimmage + Constants.ZoneCoverageDepthDb + Constants.ZoneMatchDepthBuffer,
            _ => MathF.Max(bounds.YMax, lineOfScrimmage + Constants.ZoneCarryDepth)
        };
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private static (float xCenter, float width, float yMax) GetZoneParameters(CoverageRole role, float lineOfScrimmage, ref float yMin)
    {
        return role switch
        {
            CoverageRole.FlatLeft => (
                Constants.FieldWidth * 0.12f,
                Constants.ZoneMatchWidthFlat,
                lineOfScrimmage + Constants.ZoneCoverageDepthFlat + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.FlatRight => (
                Constants.FieldWidth * 0.88f,
                Constants.ZoneMatchWidthFlat,
                lineOfScrimmage + Constants.ZoneCoverageDepthFlat + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.HookLeft => (
                Constants.FieldWidth * 0.34f,
                Constants.ZoneMatchWidthHook,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.HookMiddle => (
                Constants.FieldWidth * 0.50f,
                Constants.ZoneMatchWidthHook,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.HookRight => (
                Constants.FieldWidth * 0.66f,
                Constants.ZoneMatchWidthHook,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.Robber => (
                Constants.FieldWidth * 0.50f,
                Constants.ZoneMatchWidthHook * 0.62f,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer * 0.7f
            ),
            CoverageRole.DeepLeft => GetDeepZoneParameters(0.25f, lineOfScrimmage, ref yMin),
            CoverageRole.DeepMiddle => GetDeepZoneParameters(0.50f, lineOfScrimmage, ref yMin),
            CoverageRole.DeepRight => GetDeepZoneParameters(0.75f, lineOfScrimmage, ref yMin),
            CoverageRole.DeepQuarterLeft => GetDeepZoneParameters(0.40f, lineOfScrimmage, ref yMin),
            CoverageRole.DeepQuarterRight => GetDeepZoneParameters(0.60f, lineOfScrimmage, ref yMin),
            _ => (
                Constants.FieldWidth * 0.50f,
                Constants.ZoneMatchWidthHook,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer
            )
        };
    }

    private static (float xCenter, float width, float yMax) GetDeepZoneParameters(float xPercent, float lineOfScrimmage, ref float yMin)
    {
        yMin = lineOfScrimmage + Constants.ZoneCoverageDepth;
        return (
            Constants.FieldWidth * xPercent,
            Constants.ZoneMatchWidthDeep,
            lineOfScrimmage + Constants.FieldLength
        );
    }
}
