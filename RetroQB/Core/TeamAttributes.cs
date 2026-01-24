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
/// Controls QB, receiver, and blocker capabilities.
/// </summary>
public sealed class OffensiveTeamAttributes : TeamAttributes
{
    // Quarterback attributes
    public float QbMaxSpeed { get; init; } = Constants.QbMaxSpeed;
    public float QbSprintSpeed { get; init; } = Constants.QbSprintSpeed;
    public float QbAcceleration { get; init; } = Constants.QbAcceleration;
    public float QbFriction { get; init; } = Constants.QbFriction;
    
    /// <summary>
    /// Throwing accuracy multiplier (lower = more accurate). 1.0 = baseline.
    /// </summary>
    public float ThrowInaccuracyMultiplier { get; init; } = 1.0f;

    /// <summary>
    /// Distance-based QB accuracy multipliers (lower = more accurate).
    /// Applied in addition to ThrowInaccuracyMultiplier.
    /// </summary>
    public float ShortAccuracyMultiplier { get; init; } = 0.9f;
    public float MediumAccuracyMultiplier { get; init; } = 1.0f;
    public float LongAccuracyMultiplier { get; init; } = 1.15f;

    // Receiver attributes
    public float WrSpeed { get; init; } = Constants.WrSpeed;
    public float TeSpeed { get; init; } = Constants.TeSpeed;
    public float RbSpeed { get; init; } = Constants.RbSpeed;
    
    /// <summary>
    /// Catching ability (higher = better). Affects contested catch success rate.
    /// Range: 0.0 - 1.0, where 0.7 is baseline (70% contested catch rate).
    /// </summary>
    public float CatchingAbility { get; init; } = 0.7f;
    
    /// <summary>
    /// RB tackle-breaking ability. Chance to break a tackle attempt.
    /// Range: 0.0 - 0.5, where 0.25 is baseline (25% chance to break tackle).
    /// </summary>
    public float RbTackleBreakChance { get; init; } = 0.25f;
    
    /// <summary>
    /// Catch radius multiplier. 1.0 = baseline.
    /// </summary>
    public float CatchRadiusMultiplier { get; init; } = 1.0f;

    // Blocker attributes
    public float OlSpeed { get; init; } = Constants.OlSpeed;
    
    /// <summary>
    /// Blocking effectiveness multiplier. 1.0 = baseline.
    /// </summary>
    public float BlockingStrength { get; init; } = 1.0f;

    /// <summary>
    /// Creates default offensive attributes (baseline team).
    /// </summary>
    public static OffensiveTeamAttributes Default => new()
    {
        Name = "Home"
    };
    
    /// <summary>
    /// Gets the speed for a receiver based on position type.
    /// </summary>
    public float GetReceiverSpeed(bool isRunningBack, bool isTightEnd)
    {
        if (isRunningBack) return RbSpeed;
        if (isTightEnd) return TeSpeed;
        return WrSpeed;
    }
}

/// <summary>
/// Attributes specific to the defensive team.
/// Controls defender speed, coverage, and interception capabilities.
/// </summary>
public sealed class DefensiveTeamAttributes : TeamAttributes
{
    // Position-specific speeds
    public float DlSpeed { get; init; } = Constants.DlSpeed;
    public float LbSpeed { get; init; } = Constants.LbSpeed;
    public float DbSpeed { get; init; } = Constants.DbSpeed;
    
    /// <summary>
    /// Overall speed multiplier applied to all defenders. 1.0 = baseline.
    /// </summary>
    public float SpeedMultiplier { get; init; } = 1.0f;
    
    /// <summary>
    /// Overall interception ability multiplier. Higher = better chance to intercept.
    /// Affects the intercept radius: actual = base * position multiplier * InterceptionAbility.
    /// Range: 0.5 - 1.5, where 1.0 is baseline.
    /// </summary>
    public float InterceptionAbility { get; init; } = 1.0f;
    
    /// <summary>
    /// Position-specific interception multipliers. DBs are best at intercepting,
    /// LBs are decent, and DL are poor at catching passes.
    /// </summary>
    public float DbInterceptionMultiplier { get; init; } = 1.0f;
    public float LbInterceptionMultiplier { get; init; } = 0.6f;
    public float DlInterceptionMultiplier { get; init; } = 0.15f;
    
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
    /// Tackle ability. Affects how reliably defenders make tackles.
    /// Range: 0.5 - 1.5, where 1.0 is baseline.
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
            Entities.DefensivePosition.LB => LbSpeed,
            _ => DbSpeed
        };
    }
    
    /// <summary>
    /// Gets the effective speed for a defender (base speed * multiplier).
    /// </summary>
    public float GetEffectiveSpeed(Entities.DefensivePosition position)
    {
        return GetPositionSpeed(position) * SpeedMultiplier;
    }
    
    /// <summary>
    /// Gets the effective intercept radius based on interception ability (team-wide).
    /// </summary>
    public float GetEffectiveInterceptRadius()
    {
        return Constants.InterceptRadius * InterceptionAbility;
    }
    
    /// <summary>
    /// Gets the position-specific interception multiplier.
    /// </summary>
    public float GetPositionInterceptionMultiplier(Entities.DefensivePosition position)
    {
        return position switch
        {
            Entities.DefensivePosition.DB => DbInterceptionMultiplier,
            Entities.DefensivePosition.LB => LbInterceptionMultiplier,
            _ => DlInterceptionMultiplier
        };
    }
    
    /// <summary>
    /// Gets the effective intercept radius for a specific defender position.
    /// DBs have the best interception ability, LBs are moderate, DL are poor.
    /// </summary>
    public float GetEffectiveInterceptRadius(Entities.DefensivePosition position)
    {
        return Constants.InterceptRadius * InterceptionAbility * GetPositionInterceptionMultiplier(position);
    }
}
