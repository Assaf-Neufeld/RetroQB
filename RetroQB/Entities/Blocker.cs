using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Entities;

public sealed class Blocker : Entity
{
    public float Speed { get; }

    public Blocker(Vector2 position) : base(position, Constants.ReceiverRadius, "OL", Palette.White)
    {
        Speed = Constants.OlSpeed;
    }
}
