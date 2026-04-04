using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Data;

/// <summary>
/// Catalog of predefined offensive team presets.
/// </summary>
public static class OffensiveTeamPresets
{
    private static readonly IReadOnlyList<OffensiveTeamAttributes> StandardTeams = BuildStandardTeams();

    private static readonly IReadOnlyList<OffensiveTeamAttributes> TeamsWithSecret = BuildTeamsWithSecret();

    public static OffensiveTeamAttributes Ballers => CreateTeam(
        name: "Ballers",
        teamScore: 88,
        description: "Balanced attack with no weak spot",
        primaryColor: new Color(70, 130, 220, 255),
        secondaryColor: new Color(24, 64, 138, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.74f,
            RbSpeed = 0.74f,
            QbThrowPower = 0.82f,
            QbThrowAccuracy = 0.84f,
            WrSpeed = 0.80f,
            WrSkill = 0.82f,
            OlStrength = 0.78f
        },
        qbName: "Ace",
        wideReceiverNames: new[] { "Stone", "Flynn", "Reed", "North" },
        tightEndName: "Griff",
        runningBackName: "Jet");

    public static OffensiveTeamAttributes Lightning => CreateTeam(
        name: "Lightning",
        teamScore: 83,
        description: "Fast spread passing and open grass",
        primaryColor: new Color(150, 90, 220, 255),
        secondaryColor: new Color(84, 44, 140, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.28f,
            RbSpeed = 0.86f,
            QbThrowPower = 0.90f,
            QbThrowAccuracy = 0.88f,
            WrSpeed = 0.95f,
            WrSkill = 0.90f,
            OlStrength = 0.34f
        },
        qbName: "Spark",
        wideReceiverNames: new[] { "Flash", "Bolt", "Blaze", "Surge" },
        tightEndName: "Arc",
        runningBackName: "Dash");

    public static OffensiveTeamAttributes Bulldozers => CreateTeam(
        name: "Bulldozers",
        teamScore: 60,
        description: "Heavy line and downhill run game",
        primaryColor: new Color(70, 160, 80, 255),
        secondaryColor: new Color(34, 96, 44, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.80f,
            RbSpeed = 0.52f,
            QbThrowPower = 0.34f,
            QbThrowAccuracy = 0.30f,
            WrSpeed = 0.32f,
            WrSkill = 0.36f,
            OlStrength = 0.78f
        },
        qbName: "Hammer",
        wideReceiverNames: new[] { "Brick", "Stone", "Grind", "Forge" },
        tightEndName: "Anvil",
        runningBackName: "Pound");

    public static OffensiveTeamAttributes Phantoms => CreateTeam(
        name: "Phantoms",
        teamScore: 71,
        description: "Timing offense built on sharp throws",
        primaryColor: new Color(92, 188, 208, 255),
        secondaryColor: new Color(24, 90, 118, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.34f,
            RbSpeed = 0.48f,
            QbThrowPower = 0.68f,
            QbThrowAccuracy = 0.84f,
            WrSpeed = 0.70f,
            WrSkill = 0.82f,
            OlStrength = 0.32f
        },
        qbName: "Shade",
        wideReceiverNames: new[] { "Silk", "Ghost", "Wisp", "Trace" },
        tightEndName: "Veil",
        runningBackName: "Slip");

    public static OffensiveTeamAttributes Cyclones => CreateTeam(
        name: "Cyclones",
        teamScore: 66,
        description: "Explosive speed built for yards after catch",
        primaryColor: new Color(232, 126, 52, 255),
        secondaryColor: new Color(120, 52, 18, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.30f,
            RbSpeed = 0.88f,
            QbThrowPower = 0.60f,
            QbThrowAccuracy = 0.34f,
            WrSpeed = 0.92f,
            WrSkill = 0.62f,
            OlStrength = 0.28f
        },
        qbName: "Vortex",
        wideReceiverNames: new[] { "Rush", "Gale", "Skid", "Jetstream" },
        tightEndName: "Tailwind",
        runningBackName: "Whirl");

    public static OffensiveTeamAttributes Ironclad => CreateTeam(
        name: "Ironclad",
        teamScore: 79,
        description: "Strong pocket arm with sturdy protection",
        primaryColor: new Color(118, 126, 138, 255),
        secondaryColor: new Color(52, 58, 66, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.56f,
            RbSpeed = 0.46f,
            QbThrowPower = 0.90f,
            QbThrowAccuracy = 0.78f,
            WrSpeed = 0.42f,
            WrSkill = 0.58f,
            OlStrength = 0.86f
        },
        qbName: "Forge",
        wideReceiverNames: new[] { "Rivet", "Pike", "Latch", "Barrow" },
        tightEndName: "Anchor",
        runningBackName: "Ram");

    public static OffensiveTeamAttributes Firebirds => CreateTeam(
        name: "Firebirds",
        teamScore: 75,
        description: "Crafty receivers who win on detail",
        primaryColor: new Color(220, 72, 72, 255),
        secondaryColor: new Color(118, 24, 38, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.34f,
            RbSpeed = 0.46f,
            QbThrowPower = 0.72f,
            QbThrowAccuracy = 0.84f,
            WrSpeed = 0.56f,
            WrSkill = 0.88f,
            OlStrength = 0.34f
        },
        qbName: "Ember",
        wideReceiverNames: new[] { "Flare", "Cinder", "Sparks", "Glow" },
        tightEndName: "Torch",
        runningBackName: "Kindle");

    public static OffensiveTeamAttributes GoldenLegion => CreateTeam(
        name: "Golden Legion",
        teamScore: 95,
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

    private static IReadOnlyList<OffensiveTeamAttributes> BuildStandardTeams()
    {
        return new[]
        {
            Ballers,
            Lightning,
            Bulldozers,
            Phantoms,
            Cyclones,
            Ironclad,
            Firebirds
        }
        .OrderByDescending(team => team.TeamScore)
        .ThenBy(team => team.Name)
        .ToArray();
    }

    private static IReadOnlyList<OffensiveTeamAttributes> BuildTeamsWithSecret()
    {
        return StandardTeams.Concat(new[] { GoldenLegion }).ToArray();
    }

    private static OffensiveTeamAttributes CreateTeam(
        string name,
        int teamScore,
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
            TeamScore = teamScore,
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