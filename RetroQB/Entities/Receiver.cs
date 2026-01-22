using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.AI;

namespace RetroQB.Entities;

public sealed class Receiver : Entity
{
    public int Index { get; }
    public bool IsRunningBack { get; }
    public bool HasBall { get; set; }
    public bool Eligible { get; set; } = true;
    public bool IsBlocking { get; set; }
    public Vector2 RouteStart { get; set; }
    public RouteType Route { get; set; }
    public float RouteProgress { get; set; }
    public Color HighlightColor { get; set; } = Palette.Yellow;
    public int RouteSide { get; set; }

    public Receiver(int index, Vector2 position, bool isRunningBack = false) : base(position, Constants.ReceiverRadius, "R", Palette.Blue)
    {
        Index = index;
        IsRunningBack = isRunningBack;
        RouteStart = position;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
    }
}
