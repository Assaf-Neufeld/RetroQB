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
        description: "Balanced units; no elite specialty",
        primaryColor: new Color(70, 130, 220, 255),
        secondaryColor: new Color(24, 64, 138, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.64f,
            RbSpeed = 0.64f,
            QbThrowPower = 0.68f,
            QbThrowAccuracy = 0.70f,
            WrSpeed = 0.64f,
            WrSkill = 0.66f,
            OlStrength = 0.64f
        },
        qbName: "Ace",
        wideReceiverNames: new[] { "Stone", "Flynn", "Reed", "North" },
        tightEndName: "Griff",
        runningBackName: "Jet");

    public static OffensiveTeamAttributes Lightning => CreateTeam(
        name: "Lightning",
        description: "Elite aerial attack; fragile defense",
        primaryColor: new Color(150, 90, 220, 255),
        secondaryColor: new Color(84, 44, 140, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.28f,
            RbSpeed = 0.78f,
            QbThrowPower = 0.84f,
            QbThrowAccuracy = 0.84f,
            WrSpeed = 0.91f,
            WrSkill = 0.84f,
            OlStrength = 0.38f
        },
        qbName: "Spark",
        wideReceiverNames: new[] { "Flash", "Bolt", "Blaze", "Surge" },
        tightEndName: "Arc",
        runningBackName: "Dash");

    public static OffensiveTeamAttributes Bulldozers => CreateTeam(
        name: "Bulldozers",
        description: "Power football; vulnerable deep",
        primaryColor: new Color(70, 160, 80, 255),
        secondaryColor: new Color(34, 96, 44, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.92f,
            RbSpeed = 0.40f,
            QbThrowPower = 0.44f,
            QbThrowAccuracy = 0.44f,
            WrSpeed = 0.38f,
            WrSkill = 0.44f,
            OlStrength = 0.90f
        },
        qbName: "Hammer",
        wideReceiverNames: new[] { "Brick", "Stone", "Grind", "Forge" },
        tightEndName: "Anvil",
        runningBackName: "Pound");

    public static OffensiveTeamAttributes Phantoms => CreateTeam(
        name: "Phantoms",
        description: "Coverage stars; short-pass offense",
        primaryColor: new Color(92, 188, 208, 255),
        secondaryColor: new Color(24, 90, 118, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.32f,
            RbSpeed = 0.48f,
            QbThrowPower = 0.38f,
            QbThrowAccuracy = 0.88f,
            WrSpeed = 0.60f,
            WrSkill = 0.72f,
            OlStrength = 0.42f
        },
        qbName: "Shade",
        wideReceiverNames: new[] { "Silk", "Ghost", "Wisp", "Trace" },
        tightEndName: "Veil",
        runningBackName: "Slip");

    public static OffensiveTeamAttributes Cyclones => CreateTeam(
        name: "Cyclones",
        description: "Open-field speed; light in the trenches",
        primaryColor: new Color(232, 126, 52, 255),
        secondaryColor: new Color(120, 52, 18, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.36f,
            RbSpeed = 0.86f,
            QbThrowPower = 0.56f,
            QbThrowAccuracy = 0.50f,
            WrSpeed = 0.86f,
            WrSkill = 0.58f,
            OlStrength = 0.40f
        },
        qbName: "Vortex",
        wideReceiverNames: new[] { "Rush", "Gale", "Skid", "Jetstream" },
        tightEndName: "Tailwind",
        runningBackName: "Whirl");

    public static OffensiveTeamAttributes Ironclad => CreateTeam(
        name: "Ironclad",
        description: "Elite run defense; methodical offense",
        primaryColor: new Color(118, 126, 138, 255),
        secondaryColor: new Color(52, 58, 66, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.64f,
            RbSpeed = 0.40f,
            QbThrowPower = 0.52f,
            QbThrowAccuracy = 0.54f,
            WrSpeed = 0.38f,
            WrSkill = 0.52f,
            OlStrength = 0.78f
        },
        qbName: "Forge",
        wideReceiverNames: new[] { "Rivet", "Pike", "Latch", "Barrow" },
        tightEndName: "Anchor",
        runningBackName: "Ram");

    public static OffensiveTeamAttributes Firebirds => CreateTeam(
        name: "Firebirds",
        description: "Receiving stars; leaky defense",
        primaryColor: new Color(220, 72, 72, 255),
        secondaryColor: new Color(118, 24, 38, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.36f,
            RbSpeed = 0.50f,
            QbThrowPower = 0.74f,
            QbThrowAccuracy = 0.76f,
            WrSpeed = 0.64f,
            WrSkill = 0.90f,
            OlStrength = 0.50f
        },
        qbName: "Ember",
        wideReceiverNames: new[] { "Flare", "Cinder", "Sparks", "Glow" },
        tightEndName: "Torch",
        runningBackName: "Kindle");

    public static OffensiveTeamAttributes Mustangs => CreateTeam(
        name: "Mustangs",
        description: "Fast rushing attack; soft coverage",
        primaryColor: new Color(42, 164, 148, 255),
        secondaryColor: new Color(16, 82, 78, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.78f,
            RbSpeed = 0.92f,
            QbThrowPower = 0.44f,
            QbThrowAccuracy = 0.54f,
            WrSpeed = 0.56f,
            WrSkill = 0.44f,
            OlStrength = 0.78f
        },
        qbName: "Ranger",
        wideReceiverNames: new[] { "Mesa", "Trail", "Spur", "Ridge" },
        tightEndName: "Saddle",
        runningBackName: "Gallop");

    public static OffensiveTeamAttributes Bombers => CreateTeam(
        name: "Bombers",
        description: "Accurate deep passing and pressure; solid across the roster",
        primaryColor: new Color(78, 94, 184, 255),
        secondaryColor: new Color(30, 38, 92, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.65f,
            RbSpeed = 0.65f,
            QbThrowPower = 0.90f,
            QbThrowAccuracy = 0.72f,
            WrSpeed = 0.80f,
            WrSkill = 0.72f,
            OlStrength = 0.70f
        },
        qbName: "Cannon",
        wideReceiverNames: new[] { "Rocket", "Comet", "Streak", "Orbit" },
        tightEndName: "Hangar",
        runningBackName: "Fuse");

    public static OffensiveTeamAttributes Sentinels => CreateTeam(
        name: "Sentinels",
        description: "Coverage and protection; slow offense",
        primaryColor: new Color(174, 118, 68, 255),
        secondaryColor: new Color(88, 54, 30, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.50f,
            RbSpeed = 0.30f,
            QbThrowPower = 0.42f,
            QbThrowAccuracy = 0.72f,
            WrSpeed = 0.30f,
            WrSkill = 0.92f,
            OlStrength = 0.86f
        },
        qbName: "Keeper",
        wideReceiverNames: new[] { "Shield", "Sentry", "Watch", "Ward" },
        tightEndName: "Rampart",
        runningBackName: "Bunker");

    public static OffensiveTeamAttributes Sharks => CreateTeam(
        name: "Sharks",
        description: "Quick possession passing and takeaways; weak in the trenches",
        primaryColor: new Color(45, 190, 194, 255),
        secondaryColor: new Color(18, 68, 92, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.45f, RbSpeed = 0.62f, QbThrowPower = 0.56f,
            QbThrowAccuracy = 0.76f, WrSpeed = 0.66f, WrSkill = 0.74f, OlStrength = 0.40f
        },
        qbName: "Reef",
        wideReceiverNames: new[] { "Fin", "Tide", "Coral", "Current" },
        tightEndName: "Breaker",
        runningBackName: "Jaws");

    public static OffensiveTeamAttributes Vipers => CreateTeam(
        name: "Vipers",
        description: "Elite passing and pressure; vulnerable to a patient run game",
        primaryColor: new Color(138, 42, 190, 255),
        secondaryColor: new Color(54, 80, 28, 255),
        skills: new OffensiveTeamSkills
        {
            RbPower = 0.66f, RbSpeed = 0.76f, QbThrowPower = 0.90f,
            QbThrowAccuracy = 0.94f, WrSpeed = 0.88f, WrSkill = 0.90f, OlStrength = 0.78f
        },
        qbName: "Venom",
        wideReceiverNames: new[] { "Fang", "Coil", "Strike", "Adder" },
        tightEndName: "Mamba",
        runningBackName: "Viper",
        usePrimaryColorForUniforms: true);

    public static OffensiveTeamAttributes GoldenLegion => CreateTeam(
        name: "Golden Legion",
        description: "Secret all-star roster on both sides",
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
            Firebirds,
            Mustangs,
            Bombers,
            Sentinels,
            Sharks,
            Vipers
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
            TeamScore = TeamDefinition.CalculateScore(skills, PlayableDefensePresets.For(new OffensiveTeamAttributes { Name = name, Skills = skills })),
            Description = description,
            PrimaryColor = primaryColor,
            SecondaryColor = secondaryColor,
            UsePrimaryColorForUniforms = usePrimaryColorForUniforms,
            Skills = skills,
            Roster = OffensiveRosterFactory.Create(skills, qbName, wideReceiverNames, tightEndName, runningBackName, RosterStars.ForOffense(name)),
            OverallRating = OffensiveRosterFactory.ComputeOverallRating(skills)
        };
    }
}
