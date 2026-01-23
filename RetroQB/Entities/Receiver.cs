using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.AI;

namespace RetroQB.Entities;

public enum OffensivePosition
{
    WR,
    TE,
    RB
}

public sealed class Receiver : Entity
{
    public int Index { get; }
    public bool IsRunningBack { get; }
    public bool IsTightEnd { get; }
    public OffensivePosition PositionRole { get; }
    public float Speed { get; }
    public bool HasBall { get; set; }
    public bool Eligible { get; set; } = true;
    public bool IsBlocking { get; set; }
    public Vector2 RouteStart { get; set; }
    public RouteType Route { get; set; }
    public float RouteProgress { get; set; }
    public Color HighlightColor { get; set; } = Palette.Yellow;
    public int RouteSide { get; set; }

    public Receiver(int index, Vector2 position, bool isRunningBack = false, bool isTightEnd = false) : base(position, Constants.ReceiverRadius, ResolveGlyph(isRunningBack, isTightEnd), ResolveColor(isRunningBack, isTightEnd))
    {
        Index = index;
        IsRunningBack = isRunningBack;
        IsTightEnd = isTightEnd && !isRunningBack;
        PositionRole = IsRunningBack ? OffensivePosition.RB : IsTightEnd ? OffensivePosition.TE : OffensivePosition.WR;
        Speed = IsRunningBack ? Constants.RbSpeed : IsTightEnd ? Constants.TeSpeed : Constants.WrSpeed;
        RouteStart = position;
    }

    private static string ResolveGlyph(bool isRunningBack, bool isTightEnd)
    {
        if (isRunningBack) return "RB";
        if (isTightEnd) return "TE";
        return "WR";
    }

    private static Color ResolveColor(bool isRunningBack, bool isTightEnd)
    {
        if (isRunningBack) return Palette.Lime;
        if (isTightEnd) return Palette.Orange;
        return Palette.Blue;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
    }
}
