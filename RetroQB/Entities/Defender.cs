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

    public Defender(Vector2 position, DefensivePosition role) : base(position, Constants.DefenderRadius, role.ToString(), Palette.Red)
    {
        PositionRole = role;
        Speed = role switch
        {
            DefensivePosition.DL => Constants.DlSpeed,
            DefensivePosition.LB => Constants.LbSpeed,
            _ => Constants.DbSpeed
        };
    }
}
