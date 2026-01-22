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

public sealed class Defender : Entity
{
    public bool IsRusher { get; set; }
    public int CoverageReceiverIndex { get; set; } = -1;
    public bool HasBall { get; set; }
    public CoverageRole ZoneRole { get; set; } = CoverageRole.None;

    public Defender(Vector2 position) : base(position, Constants.DefenderRadius, "D", Palette.Red)
    {
    }
}
