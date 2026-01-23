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
        Vector2 baseTarget = GetZoneAnchor(defender, lineOfScrimmage);

        if (IsDeepZone(defender.ZoneRole))
        {
            baseTarget = new Vector2(baseTarget.X, GetDeepZoneDepth(defender, receivers, lineOfScrimmage));
        }

        if (TryGetZoneMatch(defender, receivers, lineOfScrimmage, out var match))
        {
            return CalculateMatchTarget(defender, match, baseTarget);
        }

        return baseTarget;
    }

    /// <summary>
    /// Gets the anchor position for a zone based on the defender's role.
    /// </summary>
    public static Vector2 GetZoneAnchor(Defender defender, float lineOfScrimmage)
    {
        float depth = defender.PositionRole == DefensivePosition.DB
            ? Constants.ZoneCoverageDepthDb
            : Constants.ZoneCoverageDepth;

        return defender.ZoneRole switch
        {
            CoverageRole.DeepLeft => new Vector2(Constants.FieldWidth * 0.30f, lineOfScrimmage + Constants.ZoneCoverageDepthDb),
            CoverageRole.DeepRight => new Vector2(Constants.FieldWidth * 0.70f, lineOfScrimmage + Constants.ZoneCoverageDepthDb),
            CoverageRole.FlatLeft => new Vector2(Constants.FieldWidth * 0.12f, lineOfScrimmage + Constants.ZoneCoverageDepthFlat),
            CoverageRole.FlatRight => new Vector2(Constants.FieldWidth * 0.88f, lineOfScrimmage + Constants.ZoneCoverageDepthFlat),
            CoverageRole.HookLeft => new Vector2(Constants.FieldWidth * 0.38f, lineOfScrimmage + Constants.ZoneCoverageDepth),
            CoverageRole.HookMiddle => new Vector2(Constants.FieldWidth * 0.50f, lineOfScrimmage + Constants.ZoneCoverageDepth),
            CoverageRole.HookRight => new Vector2(Constants.FieldWidth * 0.62f, lineOfScrimmage + Constants.ZoneCoverageDepth),
            _ => new Vector2(Constants.FieldWidth * 0.50f, lineOfScrimmage + depth)
        };
    }

    /// <summary>
    /// Gets the bounds for a specific zone role.
    /// </summary>
    public static ZoneBounds GetZoneBounds(CoverageRole role, float lineOfScrimmage)
    {
        float yMin = lineOfScrimmage + 1.0f;
        var (xCenter, width, yMax) = GetZoneParameters(role, lineOfScrimmage, ref yMin);

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
        ZoneBounds bounds = GetZoneBounds(defender.ZoneRole, lineOfScrimmage);

        float bestScore = float.NegativeInfinity;

        foreach (var receiver in receivers)
        {
            if (!IsValidZoneTarget(receiver, bounds, lineOfScrimmage))
            {
                continue;
            }

            float score = CalculateZoneMatchScore(defender, receiver, bounds, lineOfScrimmage);

            if (score > bestScore)
            {
                bestScore = score;
                match = receiver;
            }
        }

        return bestScore > float.NegativeInfinity;
    }

    private static bool IsDeepZone(CoverageRole role) =>
        role is CoverageRole.DeepLeft or CoverageRole.DeepRight;

    private static Vector2 CalculateMatchTarget(Defender defender, Receiver match, Vector2 baseTarget)
    {
        if (IsDeepZone(defender.ZoneRole))
        {
            float targetY = MathF.Max(match.Position.Y + Constants.ZoneDeepCushion, baseTarget.Y);
            return new Vector2(match.Position.X, targetY);
        }

        return match.Position;
    }

    private static float GetDeepZoneDepth(Defender defender, IReadOnlyList<Receiver> receivers, float lineOfScrimmage)
    {
        ZoneBounds bounds = GetZoneBounds(defender.ZoneRole, lineOfScrimmage);
        float deepest = FindDeepestReceiverInZone(receivers, bounds, lineOfScrimmage);

        float baseDepth = lineOfScrimmage + Constants.ZoneCoverageDepthDb;
        float targetDepth = baseDepth;

        if (deepest > float.NegativeInfinity)
        {
            targetDepth = MathF.Max(baseDepth, deepest + Constants.ZoneDeepCushion);
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

    private static bool IsValidZoneTarget(Receiver receiver, ZoneBounds bounds, float lineOfScrimmage)
    {
        if (!receiver.Eligible || receiver.IsBlocking)
        {
            return false;
        }

        if (!bounds.ContainsX(receiver.Position.X))
        {
            return false;
        }

        if (receiver.Position.Y < bounds.YMin)
        {
            return false;
        }

        bool inZoneDepth = receiver.Position.Y <= bounds.YMax;
        bool carryDeep = receiver.Position.Y > bounds.YMax && receiver.Position.Y <= lineOfScrimmage + Constants.ZoneCarryDepth;

        return inZoneDepth || carryDeep;
    }

    private static float CalculateZoneMatchScore(Defender defender, Receiver receiver, ZoneBounds bounds, float lineOfScrimmage)
    {
        bool inZoneDepth = receiver.Position.Y <= bounds.YMax;

        if (IsDeepZone(defender.ZoneRole))
        {
            return receiver.Position.Y;
        }
        else
        {
            float dist = Vector2.Distance(defender.Position, receiver.Position);
            return (inZoneDepth ? 1000f : 800f) + receiver.Position.Y - dist;
        }
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
                Constants.FieldWidth * 0.38f,
                Constants.ZoneMatchWidthHook,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.HookMiddle => (
                Constants.FieldWidth * 0.50f,
                Constants.ZoneMatchWidthHook,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.HookRight => (
                Constants.FieldWidth * 0.62f,
                Constants.ZoneMatchWidthHook,
                lineOfScrimmage + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer
            ),
            CoverageRole.DeepLeft => GetDeepZoneParameters(0.30f, lineOfScrimmage, ref yMin),
            CoverageRole.DeepRight => GetDeepZoneParameters(0.70f, lineOfScrimmage, ref yMin),
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
