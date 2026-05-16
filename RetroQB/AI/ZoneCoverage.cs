using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.AI;

/// <summary>
/// Handles zone coverage logic for defenders including zone bounds, anchors, and receiver matching.
/// </summary>
public static class ZoneCoverage
{
    private readonly struct ZoneContext
    {
        public readonly Vector2 Anchor;
        public readonly ZoneBounds Bounds;
        public readonly bool IsDeepZone;

        public ZoneContext(Vector2 anchor, ZoneBounds bounds, bool isDeepZone)
        {
            Anchor = anchor;
            Bounds = bounds;
            IsDeepZone = isDeepZone;
        }
    }

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
        ZoneContext context = BuildZoneContext(defender, lineOfScrimmage);
        Vector2 baseTarget = context.Anchor;

        if (context.IsDeepZone)
        {
            baseTarget = new Vector2(baseTarget.X, GetDeepZoneDepth(defender, receivers, context.Bounds, lineOfScrimmage, baseTarget.Y));
        }

        if (TrySelectZoneMatch(defender, receivers, context, lineOfScrimmage, out var match))
        {
            return BuildZoneTarget(defender, match, receivers, baseTarget, context, lineOfScrimmage);
        }

        if (context.IsDeepZone
            && TryGetFallbackDeepReceiverInLane(defender, receivers, context.Bounds, lineOfScrimmage, out var nearest))
        {
            return BuildZoneTarget(defender, nearest, receivers, baseTarget, context, lineOfScrimmage);
        }

        return baseTarget;
    }

    private static ZoneContext BuildZoneContext(Defender defender, float lineOfScrimmage)
    {
        Vector2 anchor = GetZoneAnchor(defender, lineOfScrimmage);
        ZoneBounds bounds = GetZoneBounds(defender, lineOfScrimmage, anchor.X);
        bool isDeepZone = defender.ZoneRole.IsDeepZone();
        return new ZoneContext(anchor, bounds, isDeepZone);
    }

    /// <summary>
    /// Gets the anchor position for a zone based on the defender's role.
    /// Applies per-defender jitter to shift zone seams play-to-play.
    /// </summary>
    public static Vector2 GetZoneAnchor(Defender defender, float lineOfScrimmage)
    {
        Vector2 anchor = defender.AlignmentPosition;
        anchor.X += defender.ZoneJitterX;
        anchor.X = ClampAnchorInsideZoneBorders(defender, lineOfScrimmage, anchor.X);

        if (defender.Slot == DefenderSlot.NB)
        {
            float nickelDropBoost = GetNickelAnchorDepthBoost(defender.ZoneRole);
            if (nickelDropBoost > 0f)
            {
                float maxFieldY = Constants.EndZoneDepth + 100f - 0.5f;
                anchor.Y = MathF.Min(anchor.Y + nickelDropBoost, maxFieldY);
            }
        }

        return anchor;
    }

    private static float ClampAnchorInsideZoneBorders(Defender defender, float lineOfScrimmage, float xCenter)
    {
        float yMin = lineOfScrimmage + 1.0f;
        var (width, _) = GetZoneDimensions(defender, lineOfScrimmage, ref yMin);
        float halfWidth = width * 0.5f;
        float minX = 0.5f + halfWidth;
        float maxX = Constants.FieldWidth - 0.5f - halfWidth;

        if (minX > maxX)
        {
            return Constants.FieldWidth * 0.5f;
        }

        return Math.Clamp(xCenter, minX, maxX);
    }

    /// <summary>
    /// Gets the bounds for a specific zone role.
    /// </summary>
    public static ZoneBounds GetZoneBounds(Defender defender, float lineOfScrimmage)
    {
        return GetZoneBounds(defender, lineOfScrimmage, GetZoneAnchor(defender, lineOfScrimmage).X);
    }

    private static ZoneBounds GetZoneBounds(Defender defender, float lineOfScrimmage, float xCenter)
    {
        float yMin = lineOfScrimmage + 1.0f;
        var (width, yMax) = GetZoneDimensions(defender, lineOfScrimmage, ref yMin);

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
        ZoneContext context = BuildZoneContext(defender, lineOfScrimmage);
        return TrySelectZoneMatch(defender, receivers, context, lineOfScrimmage, out match);
    }

    private static bool TrySelectZoneMatch(
        Defender defender,
        IReadOnlyList<Receiver> receivers,
        ZoneContext context,
        float lineOfScrimmage,
        out Receiver match)
    {
        match = null!;

        float bestScore = float.NegativeInfinity;

        foreach (var receiver in receivers)
        {
            if (!IsValidZoneTarget(defender, receiver, receivers, context, lineOfScrimmage))
            {
                continue;
            }

            float score = CalculateZoneMatchScore(defender, receiver, context.Bounds, context.Anchor);

            if (score > bestScore)
            {
                bestScore = score;
                match = receiver;
            }
        }

        return bestScore > float.NegativeInfinity;
    }

    private static Vector2 BuildZoneTarget(
        Defender defender,
        Receiver match,
        IReadOnlyList<Receiver> receivers,
        Vector2 baseTarget,
        ZoneContext context,
        float lineOfScrimmage)
    {
        Vector2 projected = GetProjectedReceiverPosition(match, defender.ZoneRole);

        if (context.IsDeepZone)
        {
            return CalculateDeepMatchTarget(defender, match, receivers, projected, baseTarget, context.Bounds, lineOfScrimmage);
        }

        return CalculateUnderneathMatchTarget(defender, match, projected, baseTarget, context.Bounds, lineOfScrimmage);
    }

    private static float GetDeepZoneDepth(Defender defender, IReadOnlyList<Receiver> receivers, ZoneBounds bounds, float lineOfScrimmage, float baseDepth)
    {
        float deepest = FindDeepestReceiverInZone(receivers, bounds, lineOfScrimmage);
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

    private static bool TryGetFallbackDeepReceiverInLane(
        Defender defender,
        IReadOnlyList<Receiver> receivers,
        ZoneBounds bounds,
        float lineOfScrimmage,
        out Receiver match)
    {
        match = null!;
        float bestScore = float.NegativeInfinity;

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

            Vector2 projected = GetProjectedReceiverPosition(receiver, defender.ZoneRole);
            Vector2 travelDir = GetReceiverTravelDirection(receiver);
            if (receiver.Position.Y < bounds.YMin - Constants.ZoneMatchAttachRadius
                || (travelDir.Y < 0.45f && projected.Y < bounds.YMin + 2f))
            {
                continue;
            }

            float laneCenter = (bounds.XMin + bounds.XMax) * 0.5f;
            float verticalThreat = MathF.Max(receiver.Position.Y, projected.Y);
            float lateralPenalty = MathF.Abs(projected.X - laneCenter) * 0.65f;
            float distancePenalty = Vector2.DistanceSquared(defender.Position, receiver.Position) * 0.01f;
            float score = verticalThreat * 3f - lateralPenalty - distancePenalty;

            if (score > bestScore)
            {
                bestScore = score;
                match = receiver;
            }
        }

        return bestScore > float.NegativeInfinity;
    }

    private static bool IsValidZoneTarget(
        Defender defender,
        Receiver receiver,
        IReadOnlyList<Receiver> receivers,
        ZoneContext context,
        float lineOfScrimmage)
    {
        return IsValidZoneTarget(defender, receiver, receivers, context.Bounds, lineOfScrimmage);
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

        return defender.ZoneRole.IsDeepZone()
            && ShouldCarryIsolatedVertical(defender, receiver, receivers, bounds, lineOfScrimmage);
    }

    private static float CalculateZoneMatchScore(Defender defender, Receiver receiver, ZoneBounds bounds, Vector2 anchor)
    {
        Vector2 projected = GetProjectedReceiverPosition(receiver, defender.ZoneRole);
        bool inZoneDepth = projected.Y <= bounds.YMax;
        float horizontalDelta = MathF.Abs(projected.X - anchor.X);
        float travelWidth = MathF.Abs(GetReceiverTravelDirection(receiver).X);
        float horizontalPreference = GetHorizontalReceiverPreference(defender.ZoneRole, projected.X);

        if (defender.ZoneRole.IsDeepZone())
        {
            float verticalThreat = MathF.Max(receiver.Position.Y, projected.Y);
            return verticalThreat * 4.2f - horizontalDelta * 1.35f + travelWidth * 1.1f + horizontalPreference;
        }

        float distancePenalty = Vector2.Distance(defender.Position, projected) * 0.55f;
        float sidelinePenalty = defender.ZoneRole.IsHookZone()
            ? MathF.Max(0f, horizontalDelta - GetHorizontalStretch(defender.ZoneRole) * 0.65f) * 2.4f
            : 0f;
        float widthPenalty = defender.ZoneRole.IsHookZone()
            ? horizontalDelta * 1.7f
            : horizontalDelta * 0.7f;

        return (inZoneDepth ? 1000f : 820f)
            + projected.Y * 2.1f
            + travelWidth * 2.0f
            + horizontalPreference
            - widthPenalty
            - sidelinePenalty
            - distancePenalty;
    }

    private static float GetHorizontalReceiverPreference(CoverageRole role, float projectedX)
    {
        return role switch
        {
            CoverageRole.DeepLeft => (Constants.FieldWidth - projectedX) * 0.60f,
            CoverageRole.DeepRight => projectedX * 0.60f,
            CoverageRole.FlatLeft => projectedX * 0.42f,
            CoverageRole.FlatRight => (Constants.FieldWidth - projectedX) * 0.42f,
            _ => 0f
        };
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

        if (defender.Slot == DefenderSlot.NB)
        {
            minDrop += GetNickelMinDropBoost(defender.ZoneRole);
        }

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
        float lookAhead = role.IsDeepZone()
            ? Constants.ZoneLookAheadDeep
            : Constants.ZoneLookAheadUnderneath;

        if (role.IsFlatZone() && receiver.PositionRole == OffensivePosition.RB)
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

    private static float GetNickelAnchorDepthBoost(CoverageRole role)
    {
        return role switch
        {
            CoverageRole.HookMiddle => 2.2f,
            CoverageRole.HookLeft or CoverageRole.HookRight => 1.6f,
            CoverageRole.FlatLeft or CoverageRole.FlatRight => 1.0f,
            _ => 0f
        };
    }

    private static float GetNickelMinDropBoost(CoverageRole role)
    {
        return role switch
        {
            CoverageRole.HookMiddle => 1.8f,
            CoverageRole.HookLeft or CoverageRole.HookRight => 1.3f,
            CoverageRole.FlatLeft or CoverageRole.FlatRight => 0.9f,
            _ => 0f
        };
    }

    private static (float width, float yMax) GetZoneDimensions(Defender defender, float lineOfScrimmage, ref float yMin)
    {
        return defender.ZoneRole switch
        {
            CoverageRole.FlatLeft or CoverageRole.FlatRight => (
                Constants.ZoneMatchWidthFlat,
                lineOfScrimmage + Constants.ZoneCoverageDepthFlat + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.HookLeft or CoverageRole.HookMiddle or CoverageRole.HookRight => (
                Constants.ZoneMatchWidthHook,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.Robber => (
                Constants.ZoneMatchWidthHook * 0.62f,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer * 0.7f
            ),
            CoverageRole.DeepLeft or CoverageRole.DeepRight => GetDeepZoneDimensions(GetOutsideDeepWidth(defender), lineOfScrimmage, ref yMin),
            CoverageRole.DeepMiddle => GetDeepZoneDimensions(Constants.FieldWidth * 0.42f, lineOfScrimmage, ref yMin),
            CoverageRole.DeepQuarterLeft or CoverageRole.DeepQuarterRight => GetDeepZoneDimensions(Constants.FieldWidth * 0.30f, lineOfScrimmage, ref yMin),
            _ => (
                Constants.ZoneMatchWidthHook,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer
            )
        };
    }

    private static float GetOutsideDeepWidth(Defender defender)
    {
        return defender.Slot is DefenderSlot.FS or DefenderSlot.SS
            ? Constants.ZoneMatchWidthDeep
            : Constants.FieldWidth * 0.38f;
    }

    private static (float width, float yMax) GetDeepZoneDimensions(float width, float lineOfScrimmage, ref float yMin)
    {
        yMin = lineOfScrimmage + Constants.ZoneCoverageDepth;
        return (
            width,
            lineOfScrimmage + Constants.FieldLength
        );
    }
}
