using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.AI;

public static class DefenderAI
{
    public static void UpdateDefender(Defender defender, Quarterback qb, IReadOnlyList<Receiver> receivers, Ball ball, float speedMultiplier, float dt, bool qbIsRunner, bool useZoneCoverage, float lineOfScrimmage)
    {
        float speed = defender.Speed * speedMultiplier;
        Vector2 target;

        if (qbIsRunner)
        {
            target = qb.Position;
        }
        else if (ball.State == BallState.HeldByReceiver && ball.Holder != null)
        {
            // Chase the receiver carrying the ball - all defenders pursue
            target = ball.Holder.Position;
        }
        else if (ball.State == BallState.InAir)
        {
            // Only break on ball if defender is close enough to make a play
            float distToBall = Vector2.Distance(defender.Position, ball.Position);
            if (distToBall < 12f) // Only chase ball if within range
            {
                target = ball.Position;
            }
            else if (useZoneCoverage && defender.ZoneRole != CoverageRole.None)
            {
                target = GetZoneTarget(defender, receivers, lineOfScrimmage);
            }
            else if (defender.CoverageReceiverIndex >= 0 && defender.CoverageReceiverIndex < receivers.Count)
            {
                // Stay with assigned receiver
                target = receivers[defender.CoverageReceiverIndex].Position;
            }
            else
            {
                target = ball.Position;
            }
        }
        else if (defender.IsRusher)
        {
            float stuntBlend = Math.Clamp((defender.Position.Y - (lineOfScrimmage + 1.0f)) / 4.5f, 0f, 1f);
            float lateralOffset = defender.RushLaneOffsetX * (0.55f + 0.45f * stuntBlend);
            float closeIn = defender.RushLaneOffsetX * -0.35f * (1f - stuntBlend);
            target = qb.Position + new Vector2(lateralOffset + closeIn, 0f);
        }
        else if (useZoneCoverage && defender.ZoneRole != CoverageRole.None)
        {
            target = GetZoneTarget(defender, receivers, lineOfScrimmage);
        }
        else if (defender.CoverageReceiverIndex >= 0 && defender.CoverageReceiverIndex < receivers.Count)
        {
            target = receivers[defender.CoverageReceiverIndex].Position;
        }
        else
        {
            target = qb.Position;
        }

        Vector2 dir = target - defender.Position;
        if (dir.LengthSquared() > 0.001f)
        {
            dir = Vector2.Normalize(dir);
        }
        defender.Velocity = dir * speed;
        defender.Position += defender.Velocity * dt;
    }

    private readonly struct ZoneBounds
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
    }

    private static Vector2 GetZoneTarget(Defender defender, IReadOnlyList<Receiver> receivers, float los)
    {
        Vector2 baseTarget = GetZoneAnchor(defender, los);
        if (defender.ZoneRole is CoverageRole.DeepLeft or CoverageRole.DeepRight)
        {
            baseTarget = new Vector2(baseTarget.X, GetDeepZoneDepth(defender, receivers, los));
        }
        if (TryGetZoneMatch(defender, receivers, los, out var match))
        {
            if (defender.ZoneRole is CoverageRole.DeepLeft or CoverageRole.DeepRight)
            {
                float targetY = MathF.Max(match.Position.Y + Constants.ZoneDeepCushion, baseTarget.Y);
                return new Vector2(match.Position.X, targetY);
            }

            return match.Position;
        }

        return baseTarget;
    }

    private static float GetDeepZoneDepth(Defender defender, IReadOnlyList<Receiver> receivers, float los)
    {
        ZoneBounds bounds = GetZoneBounds(defender.ZoneRole, los);
        float deepest = float.NegativeInfinity;

        foreach (var receiver in receivers)
        {
            if (!receiver.Eligible || receiver.IsBlocking)
            {
                continue;
            }

            if (receiver.Position.X < bounds.XMin || receiver.Position.X > bounds.XMax)
            {
                continue;
            }

            if (receiver.Position.Y < los)
            {
                continue;
            }

            if (receiver.Position.Y > deepest)
            {
                deepest = receiver.Position.Y;
            }
        }

        float baseDepth = los + Constants.ZoneCoverageDepthDb;
        float targetDepth = baseDepth;
        if (deepest > float.NegativeInfinity)
        {
            targetDepth = MathF.Max(baseDepth, deepest + Constants.ZoneDeepCushion);
        }

        float maxFieldY = Constants.EndZoneDepth + 100f - 0.5f;
        return MathF.Min(targetDepth, maxFieldY);
    }

    private static bool TryGetZoneMatch(Defender defender, IReadOnlyList<Receiver> receivers, float los, out Receiver match)
    {
        match = null!;
        ZoneBounds bounds = GetZoneBounds(defender.ZoneRole, los);

        float bestScore = float.NegativeInfinity;

        foreach (var receiver in receivers)
        {
            if (!receiver.Eligible || receiver.IsBlocking)
            {
                continue;
            }

            if (receiver.Position.X < bounds.XMin || receiver.Position.X > bounds.XMax)
            {
                continue;
            }

            if (receiver.Position.Y < bounds.YMin)
            {
                continue;
            }

            bool inZoneDepth = receiver.Position.Y <= bounds.YMax;
            bool carryDeep = receiver.Position.Y > bounds.YMax && receiver.Position.Y <= los + Constants.ZoneCarryDepth;
            if (!inZoneDepth && !carryDeep)
            {
                continue;
            }

            float score;
            if (defender.ZoneRole is CoverageRole.DeepLeft or CoverageRole.DeepRight)
            {
                score = receiver.Position.Y;
            }
            else
            {
                float dist = Vector2.Distance(defender.Position, receiver.Position);
                score = (inZoneDepth ? 1000f : 800f) + receiver.Position.Y - dist;
            }

            if (score > bestScore)
            {
                bestScore = score;
                match = receiver;
            }
        }

        return bestScore > float.NegativeInfinity;
    }

    private static ZoneBounds GetZoneBounds(CoverageRole role, float los)
    {
        float yMin = los + 1.0f;
        float yMax;
        float xCenter;
        float width;

        switch (role)
        {
            case CoverageRole.FlatLeft:
                xCenter = Constants.FieldWidth * 0.12f;
                width = Constants.ZoneMatchWidthFlat;
                yMax = los + Constants.ZoneCoverageDepthFlat + Constants.ZoneMatchDepthBuffer;
                break;
            case CoverageRole.FlatRight:
                xCenter = Constants.FieldWidth * 0.88f;
                width = Constants.ZoneMatchWidthFlat;
                yMax = los + Constants.ZoneCoverageDepthFlat + Constants.ZoneMatchDepthBuffer;
                break;
            case CoverageRole.HookLeft:
                xCenter = Constants.FieldWidth * 0.38f;
                width = Constants.ZoneMatchWidthHook;
                yMax = los + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer;
                break;
            case CoverageRole.HookMiddle:
                xCenter = Constants.FieldWidth * 0.50f;
                width = Constants.ZoneMatchWidthHook;
                yMax = los + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer;
                break;
            case CoverageRole.HookRight:
                xCenter = Constants.FieldWidth * 0.62f;
                width = Constants.ZoneMatchWidthHook;
                yMax = los + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer;
                break;
            case CoverageRole.DeepLeft:
                xCenter = Constants.FieldWidth * 0.30f;
                width = Constants.ZoneMatchWidthDeep;
                yMin = los + Constants.ZoneCoverageDepth;
                yMax = los + Constants.FieldLength;
                break;
            case CoverageRole.DeepRight:
                xCenter = Constants.FieldWidth * 0.70f;
                width = Constants.ZoneMatchWidthDeep;
                yMin = los + Constants.ZoneCoverageDepth;
                yMax = los + Constants.FieldLength;
                break;
            default:
                xCenter = Constants.FieldWidth * 0.50f;
                width = Constants.ZoneMatchWidthHook;
                yMax = los + Constants.ZoneCoverageDepth + Constants.ZoneMatchDepthBuffer;
                break;
        }

        float halfWidth = width * 0.5f;
        float xMin = Math.Clamp(xCenter - halfWidth, 0.5f, Constants.FieldWidth - 0.5f);
        float xMax = Math.Clamp(xCenter + halfWidth, 0.5f, Constants.FieldWidth - 0.5f);
        return new ZoneBounds(xMin, xMax, yMin, yMax);
    }

    private static Vector2 GetZoneAnchor(Defender defender, float los)
    {
        float depth = defender.PositionRole == DefensivePosition.DB
            ? Constants.ZoneCoverageDepthDb
            : Constants.ZoneCoverageDepth;

        return defender.ZoneRole switch
        {
            CoverageRole.DeepLeft => new Vector2(Constants.FieldWidth * 0.30f, los + Constants.ZoneCoverageDepthDb),
            CoverageRole.DeepRight => new Vector2(Constants.FieldWidth * 0.70f, los + Constants.ZoneCoverageDepthDb),
            CoverageRole.FlatLeft => new Vector2(Constants.FieldWidth * 0.12f, los + Constants.ZoneCoverageDepthFlat),
            CoverageRole.FlatRight => new Vector2(Constants.FieldWidth * 0.88f, los + Constants.ZoneCoverageDepthFlat),
            CoverageRole.HookLeft => new Vector2(Constants.FieldWidth * 0.38f, los + Constants.ZoneCoverageDepth),
            CoverageRole.HookMiddle => new Vector2(Constants.FieldWidth * 0.50f, los + Constants.ZoneCoverageDepth),
            CoverageRole.HookRight => new Vector2(Constants.FieldWidth * 0.62f, los + Constants.ZoneCoverageDepth),
            _ => new Vector2(Constants.FieldWidth * 0.50f, los + depth)
        };
    }
}
