using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public interface ICollisionResolver
{
    void ClampToField(Entity entity, Ball ball, Quarterback qb);
    void ResolvePlayerOverlaps(
        Quarterback qb,
        List<Receiver> receivers,
        List<Blocker> blockers,
        List<Defender> defenders,
        Ball ball,
        Action<Entity> clampToField);
}

public sealed class CollisionResolver : ICollisionResolver
{
    public void ClampToField(Entity entity, Ball ball, Quarterback qb)
    {
        bool isCarrier = ball.State switch
        {
            BallState.HeldByQB => entity == qb,
            BallState.HeldByReceiver => entity == ball.Holder,
            _ => false
        };

        float x = entity.Position.X;
        if (!isCarrier)
        {
            x = MathF.Max(0.5f, MathF.Min(Constants.FieldWidth - 0.5f, x));
        }

        float y = MathF.Max(0.5f, MathF.Min(Constants.FieldLength - 0.5f, entity.Position.Y));
        entity.Position = new Vector2(x, y);
    }

    public void ResolvePlayerOverlaps(
        Quarterback qb,
        List<Receiver> receivers,
        List<Blocker> blockers,
        List<Defender> defenders,
        Ball ball,
        Action<Entity> clampToField)
    {
        Entity? ballCarrier = ball.State switch
        {
            BallState.HeldByQB => qb,
            BallState.HeldByReceiver => ball.Holder,
            _ => null
        };

        var entities = new List<Entity>(receivers.Count + defenders.Count + blockers.Count + 1) { qb };
        entities.AddRange(receivers);
        entities.AddRange(blockers);
        entities.AddRange(defenders);

        for (int i = 0; i < entities.Count; i++)
        {
            for (int j = i + 1; j < entities.Count; j++)
            {
                Entity a = entities[i];
                Entity b = entities[j];

                bool aIsDefender = a is Defender;
                bool bIsDefender = b is Defender;
                if (ballCarrier != null)
                {
                    if ((a == ballCarrier && bIsDefender) || (b == ballCarrier && aIsDefender))
                    {
                        continue;
                    }
                }

                if ((a == qb && b is Receiver rbB && rbB.IsRunningBack && rbB.IsBlocking) ||
                    (b == qb && a is Receiver rbA && rbA.IsRunningBack && rbA.IsBlocking))
                {
                    continue;
                }

                Vector2 delta = b.Position - a.Position;
                float minDist = a.Radius + b.Radius + 0.05f;
                float distSq = delta.LengthSquared();
                if (distSq <= 0.0001f) continue;

                float dist = MathF.Sqrt(distSq);
                if (dist < minDist)
                {
                    Vector2 pushDir = delta / dist;
                    float push = (minDist - dist) * 0.5f;
                    a.Position -= pushDir * push;
                    b.Position += pushDir * push;
                    clampToField(a);
                    clampToField(b);
                }
            }
        }
    }
}
