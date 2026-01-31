using System.Collections.Generic;
using Raylib_cs;
using RetroQB.Entities;

namespace RetroQB.Core;

/// <summary>
/// Catalog of predefined defensive team presets.
/// Follows the same pattern as OffensiveTeamPresets for consistency.
/// </summary>
public static class DefensiveTeamPresets
{
    /// <summary>
    /// Balanced defense with solid all-around attributes.
    /// </summary>
    public static DefensiveTeamAttributes Sentinels => new()
    {
        Name = "Sentinels",
        Description = "Balanced",
        PrimaryColor = new Color(100, 120, 160, 255),
        SecondaryColor = new Color(180, 175, 165, 255),
        Roster = new DefensiveRoster
        {
            Defenders = new Dictionary<DefenderSlot, DefenderProfile>
            {
                [DefenderSlot.DT1] = new DefenderProfile("Tank", 1.0f, 1.05f, 0.9f, 1.05f),
                [DefenderSlot.DT2] = new DefenderProfile("Anchor", 0.98f, 1.02f, 0.9f, 1.0f),
                [DefenderSlot.DE1] = new DefenderProfile("Edge", 1.05f, 1.0f, 0.95f, 1.02f),
                [DefenderSlot.DE2] = new DefenderProfile("Rush", 1.02f, 0.98f, 0.92f, 1.0f),
                [DefenderSlot.MLB] = new DefenderProfile("Core", 1.0f, 1.08f, 1.0f, 1.08f),
                [DefenderSlot.OLB1] = new DefenderProfile("Hawk", 1.02f, 1.05f, 1.0f, 1.05f),
                [DefenderSlot.OLB2] = new DefenderProfile("Scout", 1.0f, 1.02f, 0.98f, 1.02f),
                [DefenderSlot.CB1] = new DefenderProfile("Lock", 1.05f, 0.95f, 1.08f, 0.9f),
                [DefenderSlot.CB2] = new DefenderProfile("Shadow", 1.02f, 0.92f, 1.05f, 0.88f),
                [DefenderSlot.FS] = new DefenderProfile("Patrol", 1.05f, 0.9f, 1.1f, 0.85f),
                [DefenderSlot.SS] = new DefenderProfile("Enforcer", 1.0f, 1.0f, 1.0f, 0.95f),
                [DefenderSlot.NB] = new DefenderProfile("Slot", 1.08f, 0.88f, 1.05f, 0.82f)
            },
            PositionBaselines = PositionBaselineAttributes.Default
        },
        OverallRating = 1.05f,
        SpeedMultiplier = 1.0f,
        InterceptionAbility = 1.0f,
        TackleAbility = 1.05f,
        CoverageTightness = 1.0f,
        PassRushAbility = 1.0f,
        BlitzFrequency = 1.0f
    };

    /// <summary>
    /// Aggressive blitz-heavy defense with strong pass rush but weaker coverage.
    /// </summary>
    public static DefensiveTeamAttributes Blitzkrieg => new()
    {
        Name = "Blitzkrieg",
        Description = "Aggressive rush",
        PrimaryColor = new Color(180, 80, 80, 255),
        SecondaryColor = new Color(60, 55, 55, 255),
        Roster = new DefensiveRoster
        {
            Defenders = new Dictionary<DefenderSlot, DefenderProfile>
            {
                [DefenderSlot.DT1] = new DefenderProfile("Wrecker", 1.1f, 1.12f, 0.8f, 1.2f),
                [DefenderSlot.DT2] = new DefenderProfile("Crusher", 1.08f, 1.1f, 0.75f, 1.18f),
                [DefenderSlot.DE1] = new DefenderProfile("Fury", 1.15f, 1.05f, 0.85f, 1.15f),
                [DefenderSlot.DE2] = new DefenderProfile("Storm", 1.12f, 1.02f, 0.82f, 1.12f),
                [DefenderSlot.MLB] = new DefenderProfile("Hammer", 1.05f, 1.18f, 0.9f, 1.25f),
                [DefenderSlot.OLB1] = new DefenderProfile("Blaze", 1.1f, 1.15f, 0.88f, 1.2f),
                [DefenderSlot.OLB2] = new DefenderProfile("Strike", 1.08f, 1.12f, 0.85f, 1.18f),
                [DefenderSlot.CB1] = new DefenderProfile("Risk", 1.0f, 0.9f, 0.95f, 0.85f),
                [DefenderSlot.CB2] = new DefenderProfile("Gamble", 0.98f, 0.88f, 0.92f, 0.82f),
                [DefenderSlot.FS] = new DefenderProfile("Dive", 1.02f, 0.95f, 0.9f, 0.88f),
                [DefenderSlot.SS] = new DefenderProfile("Crash", 1.05f, 1.05f, 0.88f, 1.0f),
                [DefenderSlot.NB] = new DefenderProfile("Dart", 1.05f, 0.85f, 0.9f, 0.8f)
            },
            PositionBaselines = new PositionBaselineAttributes
            {
                // Enhanced LB attributes for blitzing
                LbTackleMultiplier = 1.35f,
                LbBlockShedMultiplier = 1.45f,
                // Weaker DB coverage
                DbInterceptionMultiplier = 1.1f,
                DbTackleMultiplier = 0.8f
            }
        },
        OverallRating = 1.0f,
        SpeedMultiplier = 1.05f,
        InterceptionAbility = 0.85f,
        TackleAbility = 1.15f,
        CoverageTightness = 0.85f,
        PassRushAbility = 1.3f,
        BlitzFrequency = 1.8f
    };

    /// <summary>
    /// Coverage-focused defense with excellent DBs but weaker run defense.
    /// </summary>
    public static DefensiveTeamAttributes Lockdown => new()
    {
        Name = "Lockdown",
        Description = "Coverage elite",
        PrimaryColor = new Color(70, 130, 120, 255),
        SecondaryColor = new Color(200, 210, 205, 255),
        Roster = new DefensiveRoster
        {
            Defenders = new Dictionary<DefenderSlot, DefenderProfile>
            {
                [DefenderSlot.DT1] = new DefenderProfile("Hold", 0.9f, 0.95f, 0.9f, 0.9f),
                [DefenderSlot.DT2] = new DefenderProfile("Stand", 0.88f, 0.92f, 0.88f, 0.88f),
                [DefenderSlot.DE1] = new DefenderProfile("Contain", 0.95f, 0.9f, 0.95f, 0.92f),
                [DefenderSlot.DE2] = new DefenderProfile("Seal", 0.92f, 0.88f, 0.92f, 0.9f),
                [DefenderSlot.MLB] = new DefenderProfile("Read", 0.95f, 1.0f, 1.1f, 0.95f),
                [DefenderSlot.OLB1] = new DefenderProfile("Zone", 1.0f, 0.95f, 1.12f, 0.92f),
                [DefenderSlot.OLB2] = new DefenderProfile("Drop", 0.98f, 0.92f, 1.1f, 0.9f),
                [DefenderSlot.CB1] = new DefenderProfile("Island", 1.15f, 0.9f, 1.25f, 0.85f),
                [DefenderSlot.CB2] = new DefenderProfile("Blanket", 1.12f, 0.88f, 1.22f, 0.82f),
                [DefenderSlot.FS] = new DefenderProfile("Hawk", 1.18f, 0.85f, 1.3f, 0.8f),
                [DefenderSlot.SS] = new DefenderProfile("Range", 1.1f, 0.92f, 1.2f, 0.88f),
                [DefenderSlot.NB] = new DefenderProfile("Stick", 1.2f, 0.82f, 1.25f, 0.78f)
            },
            PositionBaselines = new PositionBaselineAttributes
            {
                // Elite DB coverage
                DbSpeedMultiplier = 1.1f,
                DbInterceptionMultiplier = 1.5f,
                // Weaker front seven
                LbTackleMultiplier = 1.1f,
                LbBlockShedMultiplier = 1.15f,
                DlTackleMultiplier = 0.95f,
                DlBlockShedMultiplier = 1.0f
            }
        },
        OverallRating = 1.0f,
        DbSpeed = Constants.DbSpeed * 1.15f,
        LbSpeed = Constants.LbSpeed * 1.05f,
        SpeedMultiplier = 1.0f,
        InterceptionAbility = 1.25f,
        TackleAbility = 0.9f,
        CoverageTightness = 1.25f,
        PassRushAbility = 0.85f,
        BlitzFrequency = 0.7f
    };

    /// <summary>
    /// Run-stuffing defense with dominant front seven but slower secondary.
    /// </summary>
    public static DefensiveTeamAttributes IronCurtain => new()
    {
        Name = "Iron Curtain",
        Description = "Run stoppers",
        PrimaryColor = new Color(85, 85, 95, 255),
        SecondaryColor = new Color(165, 140, 100, 255),
        Roster = new DefensiveRoster
        {
            Defenders = new Dictionary<DefenderSlot, DefenderProfile>
            {
                [DefenderSlot.DT1] = new DefenderProfile("Wall", 1.0f, 1.2f, 0.8f, 1.3f),
                [DefenderSlot.DT2] = new DefenderProfile("Pillar", 0.98f, 1.18f, 0.78f, 1.28f),
                [DefenderSlot.DE1] = new DefenderProfile("Gate", 1.02f, 1.15f, 0.85f, 1.22f),
                [DefenderSlot.DE2] = new DefenderProfile("Bolt", 1.0f, 1.12f, 0.82f, 1.2f),
                [DefenderSlot.MLB] = new DefenderProfile("Fortress", 0.95f, 1.25f, 0.95f, 1.35f),
                [DefenderSlot.OLB1] = new DefenderProfile("Bulwark", 0.98f, 1.2f, 0.92f, 1.3f),
                [DefenderSlot.OLB2] = new DefenderProfile("Bastion", 0.95f, 1.18f, 0.9f, 1.28f),
                [DefenderSlot.CB1] = new DefenderProfile("Trail", 0.95f, 0.98f, 0.95f, 0.9f),
                [DefenderSlot.CB2] = new DefenderProfile("Chase", 0.92f, 0.95f, 0.92f, 0.88f),
                [DefenderSlot.FS] = new DefenderProfile("Support", 0.95f, 1.0f, 0.9f, 0.92f),
                [DefenderSlot.SS] = new DefenderProfile("Box", 0.92f, 1.1f, 0.85f, 1.05f),
                [DefenderSlot.NB] = new DefenderProfile("Fill", 0.9f, 1.0f, 0.88f, 0.95f)
            },
            PositionBaselines = new PositionBaselineAttributes
            {
                // Dominant front seven
                LbTackleMultiplier = 1.4f,
                LbBlockShedMultiplier = 1.5f,
                DlTackleMultiplier = 1.25f,
                DlBlockShedMultiplier = 1.35f,
                DeTackleMultiplier = 1.2f,
                DeBlockShedMultiplier = 1.3f,
                // Slower secondary
                DbSpeedMultiplier = 0.92f,
                DbInterceptionMultiplier = 1.0f,
                DbTackleMultiplier = 0.95f
            }
        },
        OverallRating = 1.05f,
        DlSpeed = Constants.DlSpeed * 0.95f,
        DeSpeed = Constants.DeSpeed * 0.95f,
        LbSpeed = Constants.LbSpeed * 0.95f,
        DbSpeed = Constants.DbSpeed * 0.9f,
        SpeedMultiplier = 0.95f,
        InterceptionAbility = 0.9f,
        TackleAbility = 1.25f,
        CoverageTightness = 0.95f,
        PassRushAbility = 1.1f,
        BlitzFrequency = 0.9f
    };

    /// <summary>
    /// Preset list for menu selection.
    /// </summary>
    public static IReadOnlyList<DefensiveTeamAttributes> All { get; } = new List<DefensiveTeamAttributes>
    {
        Sentinels,
        Blitzkrieg,
        Lockdown,
        IronCurtain
    };
}
