using System.Numerics;
using RetroQB.Core;

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
        OffensiveTeamAttributes offensiveTeam,
        Random rng);
    float CalculateInterceptTime(Vector2 toTarget, Vector2 targetVelocity, float projectileSpeed);
}

public sealed class ThrowingMechanics : IThrowingMechanics
{
    private const float ThrowBaseInaccuracyDeg = 1.0f;
    private const float ThrowMaxInaccuracyDeg = 8f;
    private const float BallMaxAirTime = 2.5f;

    public Vector2 CalculateThrowVelocity(
        Vector2 qbPosition,
        Vector2 qbVelocity,
        Vector2 targetPosition,
        Vector2 targetVelocity,
        float ballSpeed,
        float pressure,
        OffensiveTeamAttributes offensiveTeam,
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
        float distanceMultiplier = GetDistanceAccuracyMultiplier(toReceiver.Length(), offensiveTeam);
        float combinedFactor = Math.Clamp(pressure + movementPenalty, 0f, 1f);
        float inaccuracyDeg = Lerp(ThrowBaseInaccuracyDeg, ThrowMaxInaccuracyDeg, combinedFactor);
        inaccuracyDeg *= offensiveTeam.ThrowInaccuracyMultiplier * distanceMultiplier;
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
        
        // Penalty for throwing across the body (opposite horizontal direction)
        // Running right (+X) and throwing left (-X) = penalty
        // Running sideways and throwing forward = OK
        // Running forward and throwing forward = OK
        float moveX = moveDir.X;
        float throwX = throwDir.X;
        
        float horizontalMovement = MathF.Abs(moveX);
        
        // If not moving much horizontally, no cross-body penalty
        if (horizontalMovement < 0.3f) return 0f;
        
        // If throw is in opposite X direction from movement, apply penalty
        if (moveX * throwX < 0)
        {
            // Penalty scales with how much you're moving sideways and how far across you're throwing
            float crossBodyFactor = horizontalMovement * MathF.Abs(throwX);
            return Math.Clamp(crossBodyFactor * 1.5f, 0f, 1f);
        }
        
        return 0f;
    }

    private static float GetDistanceAccuracyMultiplier(float distance, OffensiveTeamAttributes offensiveTeam)
    {
        if (distance <= Constants.ShortPassMaxDistance)
        {
            return offensiveTeam.ShortAccuracyMultiplier;
        }

        if (distance <= Constants.MediumPassMaxDistance)
        {
            return offensiveTeam.MediumAccuracyMultiplier;
        }

        return offensiveTeam.LongAccuracyMultiplier;
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
