using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Entities;

public enum CoverageRole
{
    None,
    DeepLeft,
    DeepMiddle,
    DeepRight,
    DeepQuarterLeft,
    DeepQuarterRight,
    FlatLeft,
    FlatRight,
    HookLeft,
    HookMiddle,
    HookRight
}

public enum DefensivePosition
{
    DL,
    DE,
    LB,
    DB
}

public sealed class Defender : Entity
{
    public DefensivePosition PositionRole { get; }
    public float Speed { get; }
    public bool IsRusher { get; set; }
    public int CoverageReceiverIndex { get; set; } = -1;
    public bool HasBall { get; set; }
    public CoverageRole ZoneRole { get; set; } = CoverageRole.None;
    public float RushLaneOffsetX { get; set; }
    
    /// <summary>
    /// Reference to the team attributes for this defender.
    /// </summary>
    public DefensiveTeamAttributes TeamAttributes { get; }
    
    /// <summary>
    /// If true, DB plays press coverage close to the line of scrimmage.
    /// If false, DB plays off coverage and maintains cushion before pursuing.
    /// </summary>
    public bool IsPressCoverage { get; set; }

    /// <summary>
    /// Per-play random horizontal offset applied to zone anchor positions.
    /// Shifts zone coverage seams to create play-to-play variety.
    /// </summary>
    public float ZoneJitterX { get; set; }

    /// <summary>
    /// Set each frame by blocking logic. True when a blocker has active contact with this defender.
    /// Reset at the start of each blocking update cycle.
    /// </summary>
    public bool IsBeingBlocked { get; set; }

    /// <summary>
    /// Number of active blockers currently in contact this frame.
    /// Reset at the start of each blocking update cycle.
    /// </summary>
    public int ActiveBlockersCount { get; set; }

    public Defender(Vector2 position, DefensivePosition role, DefensiveTeamAttributes? teamAttributes = null) 
        : base(position, Constants.DefenderRadius, role.ToString(), Palette.Red)
    {
        TeamAttributes = teamAttributes ?? DefensiveTeamAttributes.Default;
        PositionRole = role;
        Speed = TeamAttributes.GetEffectiveSpeed(role);
    }
}
