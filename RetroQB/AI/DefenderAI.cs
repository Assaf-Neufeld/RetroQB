using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.AI;

public static class DefenderAI
{
    public static void UpdateDefender(Defender defender, Quarterback qb, IReadOnlyList<Receiver> receivers, Ball ball, float speedMultiplier, float dt, bool qbIsRunner)
    {
        float speed = Constants.DefenderSpeed * speedMultiplier;
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
}
