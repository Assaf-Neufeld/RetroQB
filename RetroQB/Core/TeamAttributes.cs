using System;
using Raylib_cs;
using RetroQB.Entities;

namespace RetroQB.Core;

/// <summary>
/// Base attributes for a team, providing configurable player qualities.
/// Teams can have different skill levels affecting gameplay.
/// </summary>
public abstract class TeamAttributes
{
    /// <summary>
    /// Team name for display purposes.
    /// </summary>
    public string Name { get; init; } = "Team";

    /// <summary>
    /// Overall skill level (1.0 = baseline). Affects speed and other attributes.
    /// </summary>
    public float OverallRating { get; init; } = 1.0f;
}

/// <summary>
/// Attributes specific to the offensive team.
/// Controls QB, receiver, and blocker capabilities via roster profiles.
/// </summary>
public sealed class OffensiveTeamAttributes : TeamAttributes
{
    /// <summary>
    /// Offensive roster with per-player profiles.
    /// </summary>
    public OffensiveRoster Roster { get; init; } = OffensiveRoster.Default;

    /// <summary>
    /// Short description for menu display.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Primary team color (used for QB and key UI accents).
    /// </summary>
    public Color PrimaryColor { get; init; } = Palette.QB;

    /// <summary>
    /// Secondary team color (used for receivers/OL and UI accents).
    /// </summary>
    public Color SecondaryColor { get; init; } = Palette.Receiver;

    /// <summary>
    /// Creates default offensive attributes (baseline team).
    /// </summary>
    public static OffensiveTeamAttributes Default => new()
    {
        Name = "Home",
        PrimaryColor = Palette.QB,
        SecondaryColor = Palette.Receiver,
        Roster = OffensiveRoster.Default
    };

    // QB delegations
    public float GetQbMaxSpeed() => Roster.GetQbMaxSpeed();
    public float GetQbSprintSpeed() => Roster.GetQbSprintSpeed();
    public float GetQbAcceleration() => Roster.GetQbAcceleration();
    public float GetQbFriction() => Roster.GetQbFriction();
    public float GetQbThrowInaccuracyMultiplier() => Roster.GetQbThrowInaccuracy();
    public float GetQbDistanceAccuracyMultiplier(float distance) => Roster.GetQbDistanceAccuracy(distance);

    // Receiver delegations
    public float GetReceiverSpeed(ReceiverSlot slot) => Roster.GetReceiverSpeed(slot);
    public float GetReceiverCatchingAbility(ReceiverSlot slot) => Roster.GetReceiverCatchingAbility(slot);
    public float GetReceiverCatchRadiusMultiplier(ReceiverSlot slot) => Roster.GetReceiverCatchRadius(slot);
    public float GetRbTackleBreakChance(ReceiverSlot slot) => Roster.GetRbTackleBreakChance(slot);

    // OLine delegations
    public float OlSpeed => Roster.GetOLineSpeed();
    public float BlockingStrength => Roster.GetOLineBlockingStrength();
    
    // TE blocking delegation
    public float GetTeBlockingStrength(ReceiverSlot slot) => Roster.GetTeBlockingStrength(slot);
}

/// <summary>
/// Attributes specific to the defensive team.
/// Controls defender speed, coverage, and interception capabilities.
/// Uses DefensiveRoster for per-player and position-specific attributes.
/// </summary>
public sealed class DefensiveTeamAttributes : TeamAttributes
{
    /// <summary>
    /// Defensive roster with per-player profiles and position baselines.
    /// </summary>
    public DefensiveRoster Roster { get; init; } = DefensiveRoster.Default;
    
    /// <summary>
    /// Short description for menu display.
    /// </summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// Primary team color (used for defenders and UI accents).
    /// </summary>
    public Color PrimaryColor { get; init; } = Palette.Red;
    
    /// <summary>
    /// Secondary team color (used for UI accents).
    /// </summary>
    public Color SecondaryColor { get; init; } = new Color(140, 140, 145, 255);

    // Base position speeds (can be overridden per-team)
    public float DlSpeed { get; init; } = Constants.DlSpeed;
    public float DeSpeed { get; init; } = Constants.DeSpeed;
    public float LbSpeed { get; init; } = Constants.LbSpeed;
    public float DbSpeed { get; init; } = Constants.DbSpeed;
    
    /// <summary>
    /// Overall speed multiplier applied to all defenders. 1.0 = baseline.
    /// </summary>
    public float SpeedMultiplier { get; init; } = 1.0f;
    
    /// <summary>
    /// Overall interception ability multiplier. 1.0 = baseline.
    /// </summary>
    public float InterceptionAbility { get; init; } = 1.0f;
    
    /// <summary>
    /// Coverage tightness. Higher = defenders stick closer to receivers.
    /// Range: 0.5 - 1.5, where 1.0 is baseline.
    /// </summary>
    public float CoverageTightness { get; init; } = 1.0f;
    
    /// <summary>
    /// Pass rush effectiveness. Affects how quickly rushers pressure the QB.
    /// Range: 0.5 - 1.5, where 1.0 is baseline.
    /// </summary>
    public float PassRushAbility { get; init; } = 1.0f;
    
    /// <summary>
    /// Blitz frequency multiplier. Higher = more blitzes.
    /// Range: 0.5 - 2.0, where 1.0 is baseline (10% base blitz rate).
    /// </summary>
    public float BlitzFrequency { get; init; } = 1.0f;
    
    /// <summary>
    /// Overall tackle ability multiplier. 1.0 = baseline.
    /// </summary>
    public float TackleAbility { get; init; } = 1.0f;

    /// <summary>
    /// Creates default defensive attributes (baseline team).
    /// </summary>
    public static DefensiveTeamAttributes Default => new()
    {
        Name = "Away"
    };
    
    /// <summary>
    /// Gets the base speed for a defensive position.
    /// </summary>
    public float GetPositionSpeed(Entities.DefensivePosition position)
    {
        return position switch
        {
            Entities.DefensivePosition.DL => DlSpeed,
            Entities.DefensivePosition.DE => DeSpeed,
            Entities.DefensivePosition.LB => LbSpeed,
            _ => DbSpeed
        };
    }
    
    /// <summary>
    /// Gets the effective speed for a defender (base speed * team multiplier).
    /// </summary>
    public float GetEffectiveSpeed(Entities.DefensivePosition position)
    {
        return GetPositionSpeed(position) * SpeedMultiplier;
    }
    
    /// <summary>
    /// Gets the position-specific interception multiplier from roster.
    /// </summary>
    public float GetPositionInterceptionMultiplier(Entities.DefensivePosition position)
    {
        return Roster.GetInterceptionMultiplier(position);
    }
    
    /// <summary>
    /// Gets the effective intercept radius for a specific defender position.
    /// </summary>
    public float GetEffectiveInterceptRadius(Entities.DefensivePosition position)
    {
        return Constants.InterceptRadius * InterceptionAbility * GetPositionInterceptionMultiplier(position);
    }
    
    /// <summary>
    /// Gets the position-specific tackle multiplier from roster.
    /// </summary>
    public float GetPositionTackleMultiplier(Entities.DefensivePosition position)
    {
        return Roster.GetTackleMultiplier(position);
    }
    
    /// <summary>
    /// Gets the effective tackle ability for a specific defender position.
    /// </summary>
    public float GetEffectiveTackleAbility(Entities.DefensivePosition position)
    {
        return TackleAbility * GetPositionTackleMultiplier(position);
    }
    
    /// <summary>
    /// Gets the position-specific block shed multiplier from roster.
    /// </summary>
    public float GetPositionBlockShedMultiplier(Entities.DefensivePosition position)
    {
        return Roster.GetBlockShedMultiplier(position);
    }
}
