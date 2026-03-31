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
    private static readonly IReadOnlyList<OffensiveTeamAttributes> StandardTeams = new List<OffensiveTeamAttributes>
    {
        Ballers,
        Lightning,
        Bulldozers
    };

    private static readonly IReadOnlyList<OffensiveTeamAttributes> TeamsWithSecret = new List<OffensiveTeamAttributes>
    {
        Ballers,
        Lightning,
        Bulldozers,
        GoldenLegion
    };

    /// <summary>
    /// Balanced team with strong all-around attributes.
    /// </summary>
    public static OffensiveTeamAttributes Ballers => new()
    {
        Name = "Ballers",
        Description = "Balanced",
        PrimaryColor = new Color(70, 130, 220, 255),
        SecondaryColor = new Color(24, 64, 138, 255),
        Roster = new OffensiveRoster
        {
            Quarterback = new QbProfile
            {
                Name = "Ace",
                MaxSpeed = Constants.QbMaxSpeed * 1.05f,
                SprintSpeed = Constants.QbSprintSpeed * 1.05f,
                Acceleration = Constants.QbAcceleration * 1.05f,
                ArmStrength = 1.0f,
                Accuracy = 0.765f,
                DeepAccuracyPenalty = 1.176f
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
        PrimaryColor = new Color(150, 90, 220, 255),
        SecondaryColor = new Color(84, 44, 140, 255),
        Roster = new OffensiveRoster
        {
            Quarterback = new QbProfile
            {
                Name = "Spark",
                MaxSpeed = Constants.QbMaxSpeed * 0.85f,
                SprintSpeed = Constants.QbSprintSpeed * 0.9f,
                Acceleration = Constants.QbAcceleration * 0.9f,
                ArmStrength = 1.15f,
                Accuracy = 0.525f,
                DeepAccuracyPenalty = 1.214f
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
        PrimaryColor = new Color(70, 160, 80, 255),
        SecondaryColor = new Color(34, 96, 44, 255),
        Roster = new OffensiveRoster
        {
            Quarterback = new QbProfile
            {
                Name = "Hammer",
                MaxSpeed = Constants.QbMaxSpeed * 1.1f,
                SprintSpeed = Constants.QbSprintSpeed * 1.1f,
                Acceleration = Constants.QbAcceleration * 1.1f,
                ArmStrength = 0.9f,
                Accuracy = 0.998f,
                DeepAccuracyPenalty = 1.158f
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

    public static OffensiveTeamAttributes GoldenLegion => new()
    {
        Name = "Golden Legion",
        Description = "Elite balance",
        PrimaryColor = Palette.Gold,
        SecondaryColor = Palette.Red,
        UsePrimaryColorForUniforms = true,
        Roster = new OffensiveRoster
        {
            Quarterback = new QbProfile
            {
                Name = "Crown",
                    MaxSpeed = Constants.QbMaxSpeed * 0.85f,
                    SprintSpeed = Constants.QbSprintSpeed * 0.9f,
                    Acceleration = Constants.QbAcceleration * 0.9f,
                ArmStrength = 1.22f,
                Accuracy = 0.6f,
                DeepAccuracyPenalty = 1.02f
            },
            WideReceivers = new Dictionary<ReceiverSlot, WrProfile>
            {
                [ReceiverSlot.WR1] = new WrProfile { Name = "Solar", Speed = Constants.WrSpeed * 1.28f, CatchingAbility = 0.94f, CatchRadius = 1.16f },
                [ReceiverSlot.WR2] = new WrProfile { Name = "Blaze", Speed = Constants.WrSpeed * 1.24f, CatchingAbility = 0.92f, CatchRadius = 1.13f },
                [ReceiverSlot.WR3] = new WrProfile { Name = "Glory", Speed = Constants.WrSpeed * 1.2f, CatchingAbility = 0.9f, CatchRadius = 1.1f },
                [ReceiverSlot.WR4] = new WrProfile { Name = "Prime", Speed = Constants.WrSpeed * 1.16f, CatchingAbility = 0.88f, CatchRadius = 1.07f }
            },
            TightEnds = new Dictionary<ReceiverSlot, TeProfile>
            {
                [ReceiverSlot.TE1] = new TeProfile { Name = "Titan", Speed = Constants.TeSpeed * 1.08f, CatchingAbility = 0.82f, CatchRadius = 1.07f, BlockingStrength = 1.3f }
            },
            RunningBacks = new Dictionary<ReceiverSlot, RbProfile>
            {
                [ReceiverSlot.RB1] = new RbProfile { Name = "Inferno", Speed = Constants.RbSpeed * 1.22f, CatchingAbility = 0.78f, CatchRadius = 1.06f, TackleBreakChance = 0.45f }
            },
            OffensiveLine = new OLineProfile { Speed = Constants.OlSpeed * 1.22f, BlockingStrength = 1.34f }
        },
        OverallRating = 1.16f
    };

    /// <summary>
    /// Preset list for menu selection.
    /// </summary>
    public static IReadOnlyList<OffensiveTeamAttributes> All => StandardTeams;

    public static int StandardTeamCount => StandardTeams.Count;

    public static IReadOnlyList<OffensiveTeamAttributes> GetMenuTeams(bool includeSecret)
    {
        return includeSecret ? TeamsWithSecret : StandardTeams;
    }
}

