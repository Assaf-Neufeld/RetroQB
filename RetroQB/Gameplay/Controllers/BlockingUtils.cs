using System;
using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Shared blocking utility methods used by both OffensiveLinemanAI and BlockingController.
/// </summary>
public static class BlockingUtils
{
    /// <summary>
    /// Normalizes a vector, returning Vector2.Zero if the vector is too short.
    /// </summary>
    public static Vector2 SafeNormalize(Vector2 vector)
    {
        return vector.LengthSquared() > 0.001f ? Vector2.Normalize(vector) : Vector2.Zero;
    }

    /// <summary>
    /// Calculates how much a defender should slow down when being blocked.
    /// </summary>
    public static float GetDefenderSlowdown(float blockMultiplier, float baseSlow)
    {
        float bonus = Math.Clamp(blockMultiplier - 1f, -0.6f, 0.6f);
        float adjusted = baseSlow - bonus * 0.06f;
        return Math.Clamp(adjusted, 0.05f, 0.22f);
    }

    /// <summary>
    /// Calculates a boost factor for block shedding based on proximity to the ball carrier.
    /// Defenders closer to the ball carrier shed blocks more easily.
    /// </summary>
    public static float GetTackleShedBoost(Vector2 defenderPosition, Vector2? ballCarrierPosition)
    {
        if (ballCarrierPosition == null) return 0f;

        float distance = Vector2.Distance(defenderPosition, ballCarrierPosition.Value);
        const float shedRange = 6.0f;
        if (distance >= shedRange) return 0f;

        float t = 1f - (distance / shedRange);
        return Math.Clamp(t, 0f, 1f);
    }

    /// <summary>
    /// Returns true if the formation is a sweep or toss play.
    /// </summary>
    public static bool IsSweepFormation(FormationType formation)
    {
        return formation is FormationType.RunSweepLeft
            or FormationType.RunSweepRight
            or FormationType.RunTossLeft
            or FormationType.RunTossRight;
    }

    /// <summary>
    /// Gets the current ball carrier's position, if any.
    /// </summary>
    public static Vector2? GetBallCarrierPosition(Ball ball, Quarterback qb)
    {
        return ball.State switch
        {
            BallState.HeldByQB => qb.Position,
            BallState.HeldByReceiver => ball.Holder?.Position,
            _ => null
        };
    }

    /// <summary>
    /// Returns true if a run play is active with a running back carrying the ball.
    /// </summary>
    public static bool IsRunPlayActiveWithRunningBack(PlayType selectedPlayType, Ball ball)
    {
        if (selectedPlayType != PlayType.Run) return false;
        if (ball.State != BallState.HeldByReceiver) return false;
        return ball.Holder is Receiver receiver && receiver.IsRunningBack;
    }

    /// <summary>
    /// Returns a normalized drive direction for run blocking.
    /// </summary>
    public static Vector2 GetDriveDirection(int runSide, float xFactor)
    {
        return SafeNormalize(new Vector2(runSide * xFactor, 1f));
    }
}
