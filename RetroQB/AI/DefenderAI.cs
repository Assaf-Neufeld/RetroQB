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
                target = GetZoneTarget(defender.ZoneRole, lineOfScrimmage);
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
            target = qb.Position;
        }
        else if (useZoneCoverage && defender.ZoneRole != CoverageRole.None)
        {
            target = GetZoneTarget(defender.ZoneRole, lineOfScrimmage);
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

    private static Vector2 GetZoneTarget(CoverageRole role, float los)
    {
        return role switch
        {
            CoverageRole.DeepLeft => new Vector2(Constants.FieldWidth * 0.25f, los + Constants.ZoneCoverageDepth),
            CoverageRole.DeepRight => new Vector2(Constants.FieldWidth * 0.75f, los + Constants.ZoneCoverageDepth),
            CoverageRole.FlatLeft => new Vector2(Constants.FieldWidth * 0.18f, los + Constants.ZoneCoverageDepth),
            CoverageRole.FlatRight => new Vector2(Constants.FieldWidth * 0.82f, los + Constants.ZoneCoverageDepth),
            CoverageRole.HookLeft => new Vector2(Constants.FieldWidth * 0.38f, los + Constants.ZoneCoverageDepth),
            CoverageRole.HookMiddle => new Vector2(Constants.FieldWidth * 0.50f, los + Constants.ZoneCoverageDepth),
            CoverageRole.HookRight => new Vector2(Constants.FieldWidth * 0.62f, los + Constants.ZoneCoverageDepth),
            _ => new Vector2(Constants.FieldWidth * 0.50f, los + Constants.ZoneCoverageDepth)
        };
    }
}
