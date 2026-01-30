using System.Collections.Generic;
using Raylib_cs;

namespace RetroQB.Core;

/// <summary>
/// Catalog of predefined offensive team presets.
/// </summary>
public static class OffensiveTeamPresets
{
    /// <summary>
    /// Balanced team with strong all-around attributes.
    /// </summary>
    public static OffensiveTeamAttributes Ballers => new()
    {
        Name = "Ballers",
        Description = "Good all around",
        PrimaryColor = new Color(255, 200, 70, 255),
        SecondaryColor = new Color(90, 170, 255, 255),
        OverallRating = 1.05f,
        QbMaxSpeed = Constants.QbMaxSpeed * 1.05f,
        QbSprintSpeed = Constants.QbSprintSpeed * 1.05f,
        QbAcceleration = Constants.QbAcceleration * 1.05f,
        ThrowInaccuracyMultiplier = 0.9f,
        ShortAccuracyMultiplier = 0.9f,
        MediumAccuracyMultiplier = 0.95f,
        LongAccuracyMultiplier = 1.0f,
        WrSpeed = Constants.WrSpeed * 1.05f,
        TeSpeed = Constants.TeSpeed * 1.05f,
        RbSpeed = Constants.RbSpeed * 1.05f,
        CatchingAbility = 0.78f,
        RbTackleBreakChance = 0.28f,
        CatchRadiusMultiplier = 1.05f,
        OlSpeed = Constants.OlSpeed * 1.05f,
        BlockingStrength = 1.05f
    };

    /// <summary>
    /// Speed and precision passing team with weak offensive line.
    /// </summary>
    public static OffensiveTeamAttributes Lightning => new()
    {
        Name = "Lightning",
        Description = "Elite receivers/RB, accurate QB, weak OL",
        PrimaryColor = new Color(255, 230, 90, 255),
        SecondaryColor = new Color(80, 220, 255, 255),
        OverallRating = 1.0f,
        QbMaxSpeed = Constants.QbMaxSpeed * 0.85f,
        QbSprintSpeed = Constants.QbSprintSpeed * 0.9f,
        QbAcceleration = Constants.QbAcceleration * 0.9f,
        ThrowInaccuracyMultiplier = 0.75f,
        ShortAccuracyMultiplier = 0.75f,
        MediumAccuracyMultiplier = 0.8f,
        LongAccuracyMultiplier = 0.9f,
        WrSpeed = Constants.WrSpeed * 1.25f,
        TeSpeed = Constants.TeSpeed * 1.15f,
        RbSpeed = Constants.RbSpeed * 1.25f,
        CatchingAbility = 0.82f,
        RbTackleBreakChance = 0.3f,
        CatchRadiusMultiplier = 1.1f,
        OlSpeed = Constants.OlSpeed * 0.8f,
        BlockingStrength = 0.8f
    };

    /// <summary>
    /// Power running team with dominant line and QB, limited receivers.
    /// </summary>
    public static OffensiveTeamAttributes Bulldozers => new()
    {
        Name = "Bulldozers",
        Description = "Dominant OL/RB/QB, slow receivers",
        PrimaryColor = new Color(220, 80, 60, 255),
        SecondaryColor = new Color(255, 140, 70, 255),
        OverallRating = 1.05f,
        QbMaxSpeed = Constants.QbMaxSpeed * 1.2f,
        QbSprintSpeed = Constants.QbSprintSpeed * 1.2f,
        QbAcceleration = Constants.QbAcceleration * 1.2f,
        ThrowInaccuracyMultiplier = 1.05f,
        ShortAccuracyMultiplier = 1.0f,
        MediumAccuracyMultiplier = 1.05f,
        LongAccuracyMultiplier = 1.1f,
        WrSpeed = Constants.WrSpeed * 0.8f,
        TeSpeed = Constants.TeSpeed * 0.75f,
        RbSpeed = Constants.RbSpeed * 1.25f,
        CatchingAbility = 0.6f,
        RbTackleBreakChance = 0.4f,
        CatchRadiusMultiplier = 0.9f,
        OlSpeed = Constants.OlSpeed * 1.25f,
        BlockingStrength = 1.3f
    };

    /// <summary>
    /// Preset list for menu selection.
    /// </summary>
    public static IReadOnlyList<OffensiveTeamAttributes> All { get; } = new List<OffensiveTeamAttributes>
    {
        Ballers,
        Lightning,
        Bulldozers
    };
}
