using System.Collections.Generic;
using Raylib_cs;
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
        Bulldozers,
        Phantoms,
        Cyclones,
        Ironclad,
        Firebirds
    };

    private static readonly IReadOnlyList<OffensiveTeamAttributes> TeamsWithSecret = new List<OffensiveTeamAttributes>
    {
        Ballers,
        Lightning,
        Bulldozers,
        Phantoms,
        Cyclones,
        Ironclad,
        Firebirds,
        GoldenLegion
    };

    public static OffensiveTeamAttributes Ballers => CreateTeam(
        name: "Ballers",
        description: "Balanced attack with no weak spot",
        primaryColor: new Color(70, 130, 220, 255),
        secondaryColor: new Color(24, 64, 138, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.56f,
            RbSpeed = 0.54f,
            QbThrowPower = 0.56f,
            QbThrowAccuracy = 0.60f,
            WrSpeed = 0.56f,
            WrSkill = 0.58f,
            OlStrength = 0.56f
        },
        qbName: "Ace",
        wideReceiverNames: new[] { "Stone", "Flynn", "Reed", "North" },
        tightEndName: "Griff",
        runningBackName: "Jet");

    public static OffensiveTeamAttributes Lightning => CreateTeam(
        name: "Lightning",
        description: "Fast spread passing and open grass",
        primaryColor: new Color(150, 90, 220, 255),
        secondaryColor: new Color(84, 44, 140, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.20f,
            RbSpeed = 0.82f,
            QbThrowPower = 0.90f,
            QbThrowAccuracy = 0.82f,
            WrSpeed = 0.96f,
            WrSkill = 0.88f,
            OlStrength = 0.10f
        },
        qbName: "Spark",
        wideReceiverNames: new[] { "Flash", "Bolt", "Blaze", "Surge" },
        tightEndName: "Arc",
        runningBackName: "Dash");

    public static OffensiveTeamAttributes Bulldozers => CreateTeam(
        name: "Bulldozers",
        description: "Heavy line and downhill run game",
        primaryColor: new Color(70, 160, 80, 255),
        secondaryColor: new Color(34, 96, 44, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.98f,
            RbSpeed = 0.72f,
            QbThrowPower = 0.24f,
            QbThrowAccuracy = 0.16f,
            WrSpeed = 0.08f,
            WrSkill = 0.10f,
            OlStrength = 0.98f
        },
        qbName: "Hammer",
        wideReceiverNames: new[] { "Brick", "Stone", "Grind", "Forge" },
        tightEndName: "Anvil",
        runningBackName: "Pound");

    public static OffensiveTeamAttributes Phantoms => CreateTeam(
        name: "Phantoms",
        description: "Timing offense built on sharp throws",
        primaryColor: new Color(92, 188, 208, 255),
        secondaryColor: new Color(24, 90, 118, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.16f,
            RbSpeed = 0.38f,
            QbThrowPower = 0.58f,
            QbThrowAccuracy = 0.98f,
            WrSpeed = 0.62f,
            WrSkill = 0.96f,
            OlStrength = 0.24f
        },
        qbName: "Shade",
        wideReceiverNames: new[] { "Silk", "Ghost", "Wisp", "Trace" },
        tightEndName: "Veil",
        runningBackName: "Slip");

    public static OffensiveTeamAttributes Cyclones => CreateTeam(
        name: "Cyclones",
        description: "Explosive speed built for yards after catch",
        primaryColor: new Color(232, 126, 52, 255),
        secondaryColor: new Color(120, 52, 18, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.18f,
            RbSpeed = 0.99f,
            QbThrowPower = 0.42f,
            QbThrowAccuracy = 0.18f,
            WrSpeed = 1.00f,
            WrSkill = 0.34f,
            OlStrength = 0.12f
        },
        qbName: "Vortex",
        wideReceiverNames: new[] { "Rush", "Gale", "Skid", "Jetstream" },
        tightEndName: "Tailwind",
        runningBackName: "Whirl");

    public static OffensiveTeamAttributes Ironclad => CreateTeam(
        name: "Ironclad",
        description: "Strong pocket arm with sturdy protection",
        primaryColor: new Color(118, 126, 138, 255),
        secondaryColor: new Color(52, 58, 66, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.32f,
            RbSpeed = 0.24f,
            QbThrowPower = 0.99f,
            QbThrowAccuracy = 0.68f,
            WrSpeed = 0.34f,
            WrSkill = 0.46f,
            OlStrength = 0.94f
        },
        qbName: "Forge",
        wideReceiverNames: new[] { "Rivet", "Pike", "Latch", "Barrow" },
        tightEndName: "Anchor",
        runningBackName: "Ram");

    public static OffensiveTeamAttributes Firebirds => CreateTeam(
        name: "Firebirds",
        description: "Crafty receivers who win on detail",
        primaryColor: new Color(220, 72, 72, 255),
        secondaryColor: new Color(118, 24, 38, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.24f,
            RbSpeed = 0.42f,
            QbThrowPower = 0.62f,
            QbThrowAccuracy = 0.92f,
            WrSpeed = 0.42f,
            WrSkill = 0.99f,
            OlStrength = 0.22f
        },
        qbName: "Ember",
        wideReceiverNames: new[] { "Flare", "Cinder", "Sparks", "Glow" },
        tightEndName: "Torch",
        runningBackName: "Kindle");

    public static OffensiveTeamAttributes GoldenLegion => CreateTeam(
        name: "Golden Legion",
        description: "Top-end talent across the whole offense",
        primaryColor: Palette.Gold,
        secondaryColor: Palette.Red,
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.98f,
            RbSpeed = 0.98f,
            QbThrowPower = 1.00f,
            QbThrowAccuracy = 0.98f,
            WrSpeed = 0.98f,
            WrSkill = 1.00f,
            OlStrength = 0.98f
        },
        qbName: "Crown",
        wideReceiverNames: new[] { "Solar", "Blaze", "Glory", "Prime" },
        tightEndName: "Titan",
        runningBackName: "Inferno",
        usePrimaryColorForUniforms: true);

    /// <summary>
    /// Preset list for menu selection.
    /// </summary>
    public static IReadOnlyList<OffensiveTeamAttributes> All => StandardTeams;

    public static int StandardTeamCount => StandardTeams.Count;

    public static IReadOnlyList<OffensiveTeamAttributes> GetMenuTeams(bool includeSecret)
    {
        return includeSecret ? TeamsWithSecret : StandardTeams;
    }

    private static OffensiveTeamAttributes CreateTeam(
        string name,
        string description,
        Color primaryColor,
        Color secondaryColor,
        OffensiveTeamSkills skills,
        string qbName,
        IReadOnlyList<string> wideReceiverNames,
        string tightEndName,
        string runningBackName,
        bool usePrimaryColorForUniforms = false)
    {
        return new OffensiveTeamAttributes
        {
            Name = name,
            Description = description,
            PrimaryColor = primaryColor,
            SecondaryColor = secondaryColor,
            UsePrimaryColorForUniforms = usePrimaryColorForUniforms,
            Skills = skills,
            Roster = OffensiveRosterFactory.Create(skills, qbName, wideReceiverNames, tightEndName, runningBackName),
            OverallRating = OffensiveRosterFactory.ComputeOverallRating(skills)
        };
    }
}