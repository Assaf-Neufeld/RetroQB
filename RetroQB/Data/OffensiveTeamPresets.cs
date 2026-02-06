using System.Collections.Generic;
using Raylib_cs;
using RetroQB.Entities;

using RetroQB.Core;

namespace RetroQB.Data;

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
            Quarterback = new QbProfile
            {
                Name = "Ace",
                MaxSpeed = Constants.QbMaxSpeed * 1.05f,
                SprintSpeed = Constants.QbSprintSpeed * 1.05f,
                Acceleration = Constants.QbAcceleration * 1.05f,
                ThrowInaccuracy = 0.9f,
                ShortAccuracy = 0.85f,
                MediumAccuracy = 0.9f,
                LongAccuracy = 1.0f
            },
            WideReceivers = new Dictionary<ReceiverSlot, WrProfile>
            {
                [ReceiverSlot.WR1] = new WrProfile { Name = "Stone", Speed = Constants.WrSpeed * 1.08f, CatchingAbility = 0.8f, CatchRadius = 1.05f },
                [ReceiverSlot.WR2] = new WrProfile { Name = "Flynn", Speed = Constants.WrSpeed * 1.02f, CatchingAbility = 0.75f, CatchRadius = 1.0f },
                [ReceiverSlot.WR3] = new WrProfile { Name = "Reed", Speed = Constants.WrSpeed * 1.0f, CatchingAbility = 0.72f, CatchRadius = 0.98f },
                [ReceiverSlot.WR4] = new WrProfile { Name = "North", Speed = Constants.WrSpeed * 0.98f, CatchingAbility = 0.7f, CatchRadius = 0.95f }
            },
            TightEnds = new Dictionary<ReceiverSlot, TeProfile>
            {
                [ReceiverSlot.TE1] = new TeProfile { Name = "Griff", Speed = Constants.TeSpeed * 1.05f, CatchingAbility = 0.7f, CatchRadius = 1.0f, BlockingStrength = 1.05f }
            },
            RunningBacks = new Dictionary<ReceiverSlot, RbProfile>
            {
                [ReceiverSlot.RB1] = new RbProfile { Name = "Jet", Speed = Constants.RbSpeed * 1.08f, CatchingAbility = 0.65f, CatchRadius = 1.0f, TackleBreakChance = 0.28f }
            },
            OffensiveLine = new OLineProfile { Speed = Constants.OlSpeed * 1.05f, BlockingStrength = 1.05f }
        },
        OverallRating = 1.05f
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
            Quarterback = new QbProfile
            {
                Name = "Spark",
                MaxSpeed = Constants.QbMaxSpeed * 0.85f,
                SprintSpeed = Constants.QbSprintSpeed * 0.9f,
                Acceleration = Constants.QbAcceleration * 0.9f,
                ThrowInaccuracy = 0.75f,
                ShortAccuracy = 0.7f,
                MediumAccuracy = 0.75f,
                LongAccuracy = 0.85f
            },
            WideReceivers = new Dictionary<ReceiverSlot, WrProfile>
            {
                [ReceiverSlot.WR1] = new WrProfile { Name = "Flash", Speed = Constants.WrSpeed * 1.25f, CatchingAbility = 0.85f, CatchRadius = 1.12f },
                [ReceiverSlot.WR2] = new WrProfile { Name = "Bolt", Speed = Constants.WrSpeed * 1.2f, CatchingAbility = 0.82f, CatchRadius = 1.08f },
                [ReceiverSlot.WR3] = new WrProfile { Name = "Blaze", Speed = Constants.WrSpeed * 1.15f, CatchingAbility = 0.78f, CatchRadius = 1.05f },
                [ReceiverSlot.WR4] = new WrProfile { Name = "Surge", Speed = Constants.WrSpeed * 1.1f, CatchingAbility = 0.75f, CatchRadius = 1.02f }
            },
            TightEnds = new Dictionary<ReceiverSlot, TeProfile>
            {
                [ReceiverSlot.TE1] = new TeProfile { Name = "Arc", Speed = Constants.TeSpeed * 1.1f, CatchingAbility = 0.72f, CatchRadius = 1.05f, BlockingStrength = 0.85f }
            },
            RunningBacks = new Dictionary<ReceiverSlot, RbProfile>
            {
                [ReceiverSlot.RB1] = new RbProfile { Name = "Dash", Speed = Constants.RbSpeed * 1.2f, CatchingAbility = 0.7f, CatchRadius = 1.05f, TackleBreakChance = 0.3f }
            },
            OffensiveLine = new OLineProfile { Speed = Constants.OlSpeed * 0.8f, BlockingStrength = 0.8f }
        },
        OverallRating = 1.0f
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
            Quarterback = new QbProfile
            {
                Name = "Hammer",
                MaxSpeed = Constants.QbMaxSpeed * 1.1f,
                SprintSpeed = Constants.QbSprintSpeed * 1.1f,
                Acceleration = Constants.QbAcceleration * 1.1f,
                ThrowInaccuracy = 1.05f,
                ShortAccuracy = 0.95f,
                MediumAccuracy = 1.0f,
                LongAccuracy = 1.1f
            },
            WideReceivers = new Dictionary<ReceiverSlot, WrProfile>
            {
                [ReceiverSlot.WR1] = new WrProfile { Name = "Brick", Speed = Constants.WrSpeed * 0.88f, CatchingAbility = 0.6f, CatchRadius = 0.9f },
                [ReceiverSlot.WR2] = new WrProfile { Name = "Stone", Speed = Constants.WrSpeed * 0.85f, CatchingAbility = 0.58f, CatchRadius = 0.88f },
                [ReceiverSlot.WR3] = new WrProfile { Name = "Grind", Speed = Constants.WrSpeed * 0.82f, CatchingAbility = 0.55f, CatchRadius = 0.85f },
                [ReceiverSlot.WR4] = new WrProfile { Name = "Forge", Speed = Constants.WrSpeed * 0.8f, CatchingAbility = 0.52f, CatchRadius = 0.82f }
            },
            TightEnds = new Dictionary<ReceiverSlot, TeProfile>
            {
                [ReceiverSlot.TE1] = new TeProfile { Name = "Anvil", Speed = Constants.TeSpeed * 0.9f, CatchingAbility = 0.62f, CatchRadius = 0.95f, BlockingStrength = 1.25f }
            },
            RunningBacks = new Dictionary<ReceiverSlot, RbProfile>
            {
                [ReceiverSlot.RB1] = new RbProfile { Name = "Pound", Speed = Constants.RbSpeed * 1.15f, CatchingAbility = 0.55f, CatchRadius = 0.9f, TackleBreakChance = 0.4f }
            },
            OffensiveLine = new OLineProfile { Speed = Constants.OlSpeed * 1.2f, BlockingStrength = 1.3f }
        },
        OverallRating = 1.05f
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

