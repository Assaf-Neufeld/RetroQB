using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.AI;

public static class DefenderAI
{
    public static void UpdateDefender(Defender defender, Quarterback qb, IReadOnlyList<Receiver> receivers, Ball ball, float speedMultiplier, float dt)
    {
        float speed = Constants.DefenderSpeed * speedMultiplier;
        Vector2 target;

        if (ball.State == BallState.InAir)
        {
            target = ball.Position;
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
