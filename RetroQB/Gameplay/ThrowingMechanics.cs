using System.Numerics;

namespace RetroQB.Gameplay;

public interface IThrowingMechanics
{
    Vector2 CalculateThrowVelocity(
        Vector2 qbPosition,
        Vector2 qbVelocity,
        Vector2 targetPosition,
        Vector2 targetVelocity,
        float ballSpeed,
        float pressure,
        Random rng);
    float CalculateInterceptTime(Vector2 toTarget, Vector2 targetVelocity, float projectileSpeed);
}

public sealed class ThrowingMechanics : IThrowingMechanics
{
    private const float ThrowBaseInaccuracyDeg = 2f;
    private const float ThrowMaxInaccuracyDeg = 12f;
    private const float BallMaxAirTime = 2.5f;

    public Vector2 CalculateThrowVelocity(
        Vector2 qbPosition,
        Vector2 qbVelocity,
        Vector2 targetPosition,
        Vector2 targetVelocity,
        float ballSpeed,
        float pressure,
        Random rng)
    {
        Vector2 toReceiver = targetPosition - qbPosition;
        float leadTime = CalculateInterceptTime(toReceiver, targetVelocity, ballSpeed);
        leadTime = Math.Clamp(leadTime, 0f, BallMaxAirTime);
        Vector2 leadTarget = targetPosition + targetVelocity * leadTime;

        Vector2 dir = leadTarget - qbPosition;
        if (dir.LengthSquared() > 0.001f)
        {
            dir = Vector2.Normalize(dir);
        }

        float movementPenalty = GetMovementInaccuracyPenalty(qbVelocity, dir);
        float combinedFactor = Math.Clamp(pressure + movementPenalty, 0f, 1f);
        float inaccuracyDeg = Lerp(ThrowBaseInaccuracyDeg, ThrowMaxInaccuracyDeg, combinedFactor);
        float inaccuracyRad = inaccuracyDeg * (MathF.PI / 180f);
        float angle = ((float)rng.NextDouble() * 2f - 1f) * inaccuracyRad;
        dir = Rotate(dir, angle);

        return dir * ballSpeed;
    }

    public float CalculateInterceptTime(Vector2 toTarget, Vector2 targetVelocity, float projectileSpeed)
    {
        float a = Vector2.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
        float b = 2f * Vector2.Dot(targetVelocity, toTarget);
        float c = Vector2.Dot(toTarget, toTarget);

        if (MathF.Abs(a) < 0.0001f)
        {
            if (MathF.Abs(b) < 0.0001f)
            {
                return 0f;
            }
            float time = -c / b;
            return time > 0f ? time : 0f;
        }

        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
        {
            return 0f;
        }

        float sqrt = MathF.Sqrt(discriminant);
        float t1 = (-b - sqrt) / (2f * a);
        float t2 = (-b + sqrt) / (2f * a);
        float t = (t1 > 0f && t2 > 0f) ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
        return t > 0f ? t : 0f;
    }

    private static float GetMovementInaccuracyPenalty(Vector2 qbVelocity, Vector2 throwDir)
    {
        float speed = qbVelocity.Length();
        if (speed < 0.2f) return 0f;

        Vector2 moveDir = qbVelocity / speed;
        float dot = Vector2.Dot(moveDir, throwDir);
        dot = Math.Clamp(dot, -1f, 1f);

        float penalty = (1f - dot) * 0.5f;
        return Math.Clamp(penalty, 0f, 1f);
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
