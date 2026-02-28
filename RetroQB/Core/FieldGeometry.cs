namespace RetroQB.Core;

/// <summary>
/// Constants and utilities for field geometry calculations.
/// Centralizes field-related magic numbers.
/// </summary>
public static class FieldGeometry
{
    public static float EndZoneDepth => Constants.EndZoneDepth;
    public static float FieldLength => 100f;
    public static float OpponentGoalLine => EndZoneDepth + FieldLength;

    /// <summary>
    /// World-Y where the player starts after a kickoff (own 25-yard line).
    /// </summary>
    public static float PlayerKickoffStartY => EndZoneDepth + 25f;

    /// <summary>
    /// World-Y where the opponent starts after a kickoff (opponent's 25-yard line).
    /// </summary>
    public static float OpponentKickoffStartY => OpponentGoalLine - 25f;

    /// <summary>
    /// Converts a world Y position to a display yard line (0-100).
    /// </summary>
    public static float GetYardLineDisplay(float worldY)
    {
        float yardLine = worldY - EndZoneDepth;
        return MathF.Max(0f, MathF.Min(FieldLength, yardLine));
    }
}
