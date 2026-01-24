using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Entities;

public enum CoverageRole
{
    None,
    DeepLeft,
    DeepRight,
    FlatLeft,
    FlatRight,
    HookLeft,
    HookMiddle,
    HookRight
}

public enum DefensivePosition
{
    DL,
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

    public Defender(Vector2 position, DefensivePosition role, DefensiveTeamAttributes? teamAttributes = null) 
        : base(position, Constants.DefenderRadius, role.ToString(), Palette.Red)
    {
        TeamAttributes = teamAttributes ?? DefensiveTeamAttributes.Default;
        PositionRole = role;
        Speed = TeamAttributes.GetEffectiveSpeed(role);
    }
}
