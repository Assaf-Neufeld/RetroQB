using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.AI;

namespace RetroQB.Entities;

public sealed class Receiver : Entity
{
    public int Index { get; }
    public bool HasBall { get; set; }
    public bool Eligible { get; set; } = true;
    public Vector2 RouteStart { get; set; }
    public RouteType Route { get; set; }
    public float RouteProgress { get; set; }
    public Color HighlightColor { get; set; } = Palette.Yellow;

    public Receiver(int index, Vector2 position) : base(position, Constants.ReceiverRadius, "R", Palette.Blue)
    {
        Index = index;
        RouteStart = position;
    }

    public override void Update(float dt)
    {
        base.Update(dt);
    }
}
