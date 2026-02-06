using System;
using System.Collections.Generic;
using RetroQB.Entities;

using RetroQB.Core;

namespace RetroQB.Data;

/// <summary>
/// Profile for an individual defender with position-specific attributes.
/// </summary>
/// <param name="Name">Display name for the defender.</param>
/// <param name="SpeedRating">Speed multiplier (1.0 = baseline).</param>
/// <param name="TackleRating">Tackle ability multiplier (1.0 = baseline).</param>
/// <param name="CoverageRating">Coverage/interception ability multiplier (1.0 = baseline).</param>
/// <param name="BlockShedRating">Block shedding ability multiplier (1.0 = baseline).</param>
public sealed record DefenderProfile(
    string Name,
    float SpeedRating = 1.0f,
    float TackleRating = 1.0f,
    float CoverageRating = 1.0f,
    float BlockShedRating = 1.0f);

/// <summary>
/// Defensive position slots for roster management.
/// </summary>
public enum DefenderSlot
{
    // Defensive Line
    DT1,
    DT2,
    DE1,
    DE2,
    
    // Linebackers
    MLB,
    OLB1,
    OLB2,
    
    // Defensive Backs
    CB1,
    CB2,
    FS,
    SS,
    NB  // Nickel back
}

/// <summary>
/// Extension methods for DefenderSlot.
/// </summary>
public static class DefenderSlotExtensions
{
    /// <summary>
    /// Gets the defensive position type for a slot.
    /// </summary>
    public static DefensivePosition GetPositionType(this DefenderSlot slot)
    {
        return slot switch
        {
            DefenderSlot.DT1 or DefenderSlot.DT2 => DefensivePosition.DL,
            DefenderSlot.DE1 or DefenderSlot.DE2 => DefensivePosition.DE,
            DefenderSlot.MLB or DefenderSlot.OLB1 or DefenderSlot.OLB2 => DefensivePosition.LB,
            _ => DefensivePosition.DB
        };
    }
    
    /// <summary>
    /// Gets the display label for a slot.
    /// </summary>
    public static string GetLabel(this DefenderSlot slot)
    {
        return slot switch
        {
            DefenderSlot.DT1 => "DT1",
            DefenderSlot.DT2 => "DT2",
            DefenderSlot.DE1 => "DE1",
            DefenderSlot.DE2 => "DE2",
            DefenderSlot.MLB => "MLB",
            DefenderSlot.OLB1 => "OLB1",
            DefenderSlot.OLB2 => "OLB2",
            DefenderSlot.CB1 => "CB1",
            DefenderSlot.CB2 => "CB2",
            DefenderSlot.FS => "FS",
            DefenderSlot.SS => "SS",
            DefenderSlot.NB => "NB",
            _ => slot.ToString()
        };
    }
    
    /// <summary>
    /// Returns true if this slot is a linebacker position.
    /// </summary>
    public static bool IsLinebacker(this DefenderSlot slot)
    {
        return slot is DefenderSlot.MLB or DefenderSlot.OLB1 or DefenderSlot.OLB2;
    }
    
    /// <summary>
    /// Returns true if this slot is a defensive back position.
    /// </summary>
    public static bool IsDefensiveBack(this DefenderSlot slot)
    {
        return slot is DefenderSlot.CB1 or DefenderSlot.CB2 or DefenderSlot.FS or DefenderSlot.SS or DefenderSlot.NB;
    }
    
    /// <summary>
    /// Returns true if this slot is a defensive line position.
    /// </summary>
    public static bool IsDefensiveLine(this DefenderSlot slot)
    {
        return slot is DefenderSlot.DT1 or DefenderSlot.DT2 or DefenderSlot.DE1 or DefenderSlot.DE2;
    }
}

/// <summary>
/// Represents the defensive roster with per-player profiles and position-specific defaults.
/// Implements the Strategy pattern for position-specific attribute calculations.
/// </summary>
public sealed class DefensiveRoster
{
    /// <summary>
    /// Individual defender profiles by slot.
    /// </summary>
    public IReadOnlyDictionary<DefenderSlot, DefenderProfile> Defenders { get; init; }
        = new Dictionary<DefenderSlot, DefenderProfile>();

    /// <summary>
    /// Position-specific baseline attributes. These define the inherent differences
    /// between positions (e.g., DBs are faster, LBs tackle better).
    /// </summary>
    public PositionBaselineAttributes PositionBaselines { get; init; } = PositionBaselineAttributes.Default;

    /// <summary>
    /// Default roster with baseline profiles for all positions.
    /// </summary>
    public static DefensiveRoster Default => new()
    {
        Defenders = new Dictionary<DefenderSlot, DefenderProfile>
        {
            [DefenderSlot.DT1] = new DefenderProfile("DT1"),
            [DefenderSlot.DT2] = new DefenderProfile("DT2"),
            [DefenderSlot.DE1] = new DefenderProfile("DE1"),
            [DefenderSlot.DE2] = new DefenderProfile("DE2"),
            [DefenderSlot.MLB] = new DefenderProfile("MLB"),
            [DefenderSlot.OLB1] = new DefenderProfile("OLB1"),
            [DefenderSlot.OLB2] = new DefenderProfile("OLB2"),
            [DefenderSlot.CB1] = new DefenderProfile("CB1"),
            [DefenderSlot.CB2] = new DefenderProfile("CB2"),
            [DefenderSlot.FS] = new DefenderProfile("FS"),
            [DefenderSlot.SS] = new DefenderProfile("SS"),
            [DefenderSlot.NB] = new DefenderProfile("NB")
        }
    };

    /// <summary>
    /// Gets the profile for a defender slot, or a default profile if not found.
    /// </summary>
    public DefenderProfile GetProfile(DefenderSlot slot)
    {
        return Defenders.TryGetValue(slot, out var profile) 
            ? profile 
            : new DefenderProfile(slot.GetLabel());
    }

    /// <summary>
    /// Gets the display name for a defender.
    /// </summary>
    public string GetDefenderName(DefenderSlot slot)
    {
        return Defenders.TryGetValue(slot, out var profile) 
            ? profile.Name 
            : slot.GetLabel();
    }

    /// <summary>
    /// Gets the effective speed multiplier for a position, combining position baseline and individual rating.
    /// </summary>
    public float GetSpeedMultiplier(DefensivePosition position)
    {
        return PositionBaselines.GetSpeedMultiplier(position);
    }

    /// <summary>
    /// Gets the effective tackle multiplier for a position, combining position baseline and individual rating.
    /// </summary>
    public float GetTackleMultiplier(DefensivePosition position)
    {
        return PositionBaselines.GetTackleMultiplier(position);
    }

    /// <summary>
    /// Gets the effective interception multiplier for a position.
    /// </summary>
    public float GetInterceptionMultiplier(DefensivePosition position)
    {
        return PositionBaselines.GetInterceptionMultiplier(position);
    }

    /// <summary>
    /// Gets the effective block shed multiplier for a position.
    /// </summary>
    public float GetBlockShedMultiplier(DefensivePosition position)
    {
        return PositionBaselines.GetBlockShedMultiplier(position);
    }

    private static float ClampRating(float rating)
        => Math.Clamp(rating, 0.7f, 1.4f);
}

/// <summary>
/// Defines the baseline attribute multipliers for each defensive position.
/// This encapsulates the inherent strengths/weaknesses of each position type.
/// Implements the Single Responsibility Principle - only handles position baselines.
/// </summary>
public sealed class PositionBaselineAttributes
{
    // Speed multipliers (DBs fastest, DL slowest)
    public float DbSpeedMultiplier { get; init; } = 1.0f;
    public float LbSpeedMultiplier { get; init; } = 0.85f;
    public float DeSpeedMultiplier { get; init; } = 0.77f;
    public float DlSpeedMultiplier { get; init; } = 0.81f;

    // Tackle multipliers (LBs best, DBs weakest)
    public float LbTackleMultiplier { get; init; } = 1.25f;
    public float DlTackleMultiplier { get; init; } = 1.1f;
    public float DeTackleMultiplier { get; init; } = 1.05f;
    public float DbTackleMultiplier { get; init; } = 0.85f;

    // Interception multipliers (DBs best, DL worst)
    public float DbInterceptionMultiplier { get; init; } = 1.3f;
    public float LbInterceptionMultiplier { get; init; } = 0.55f;
    public float DeInterceptionMultiplier { get; init; } = 0.2f;
    public float DlInterceptionMultiplier { get; init; } = 0.15f;

    // Block shed multipliers (LBs best, DBs worst)
    public float LbBlockShedMultiplier { get; init; } = 1.35f;
    public float DlBlockShedMultiplier { get; init; } = 1.15f;
    public float DeBlockShedMultiplier { get; init; } = 1.1f;
    public float DbBlockShedMultiplier { get; init; } = 0.7f;

    public static PositionBaselineAttributes Default => new();

    public float GetSpeedMultiplier(DefensivePosition position)
    {
        return position switch
        {
            DefensivePosition.DB => DbSpeedMultiplier,
            DefensivePosition.LB => LbSpeedMultiplier,
            DefensivePosition.DE => DeSpeedMultiplier,
            _ => DlSpeedMultiplier
        };
    }

    public float GetTackleMultiplier(DefensivePosition position)
    {
        return position switch
        {
            DefensivePosition.LB => LbTackleMultiplier,
            DefensivePosition.DL => DlTackleMultiplier,
            DefensivePosition.DE => DeTackleMultiplier,
            _ => DbTackleMultiplier
        };
    }

    public float GetInterceptionMultiplier(DefensivePosition position)
    {
        return position switch
        {
            DefensivePosition.DB => DbInterceptionMultiplier,
            DefensivePosition.LB => LbInterceptionMultiplier,
            DefensivePosition.DE => DeInterceptionMultiplier,
            _ => DlInterceptionMultiplier
        };
    }

    public float GetBlockShedMultiplier(DefensivePosition position)
    {
        return position switch
        {
            DefensivePosition.LB => LbBlockShedMultiplier,
            DefensivePosition.DL => DlBlockShedMultiplier,
            DefensivePosition.DE => DeBlockShedMultiplier,
            _ => DbBlockShedMultiplier
        };
    }
}

