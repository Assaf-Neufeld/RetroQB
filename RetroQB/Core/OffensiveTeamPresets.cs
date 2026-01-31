using System.Collections.Generic;
using Raylib_cs;
using RetroQB.Entities;

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
        Description = "Balanced",
        PrimaryColor = new Color(110, 140, 180, 255),
        SecondaryColor = new Color(200, 180, 130, 255),
        Roster = new OffensiveRoster
        {
            Quarterback = new QuarterbackProfile("Ace", 1.05f),
            Receivers = new Dictionary<ReceiverSlot, ReceiverProfile>
            {
                [ReceiverSlot.WR1] = new ReceiverProfile("Stone", 1.05f),
                [ReceiverSlot.WR2] = new ReceiverProfile("Flynn", 1.0f),
                [ReceiverSlot.WR3] = new ReceiverProfile("Reed", 0.98f),
                [ReceiverSlot.WR4] = new ReceiverProfile("North", 0.96f),
                [ReceiverSlot.TE1] = new ReceiverProfile("Griff", 1.0f),
                [ReceiverSlot.RB1] = new ReceiverProfile("Jet", 1.05f)
            }
        },
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
        Description = "Speed & passing",
        PrimaryColor = new Color(225, 220, 200, 255),
        SecondaryColor = new Color(180, 170, 140, 255),
        Roster = new OffensiveRoster
        {
            Quarterback = new QuarterbackProfile("Spark", 1.12f),
            Receivers = new Dictionary<ReceiverSlot, ReceiverProfile>
            {
                [ReceiverSlot.WR1] = new ReceiverProfile("Flash", 1.18f),
                [ReceiverSlot.WR2] = new ReceiverProfile("Bolt", 1.12f),
                [ReceiverSlot.WR3] = new ReceiverProfile("Blaze", 1.08f),
                [ReceiverSlot.WR4] = new ReceiverProfile("Surge", 1.05f),
                [ReceiverSlot.TE1] = new ReceiverProfile("Arc", 1.0f),
                [ReceiverSlot.RB1] = new ReceiverProfile("Dash", 1.12f)
            }
        },
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
        Description = "Power running",
        PrimaryColor = new Color(60, 58, 65, 255),
        SecondaryColor = new Color(140, 135, 130, 255),
        Roster = new OffensiveRoster
        {
            Quarterback = new QuarterbackProfile("Hammer", 1.05f),
            Receivers = new Dictionary<ReceiverSlot, ReceiverProfile>
            {
                [ReceiverSlot.WR1] = new ReceiverProfile("Brick", 0.86f),
                [ReceiverSlot.WR2] = new ReceiverProfile("Stone", 0.82f),
                [ReceiverSlot.WR3] = new ReceiverProfile("Grind", 0.78f),
                [ReceiverSlot.WR4] = new ReceiverProfile("Forge", 0.75f),
                [ReceiverSlot.TE1] = new ReceiverProfile("Anvil", 0.92f),
                [ReceiverSlot.RB1] = new ReceiverProfile("Pound", 1.2f)
            }
        },
        OverallRating = 1.05f,
        QbMaxSpeed = Constants.QbMaxSpeed * 1.1f,
        QbSprintSpeed = Constants.QbSprintSpeed * 1.1f,
        QbAcceleration = Constants.QbAcceleration * 1.1f,
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
