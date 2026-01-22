using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Entities;

public sealed class Blocker : Entity
{
    public Blocker(Vector2 position) : base(position, Constants.ReceiverRadius, "B", Palette.White)
    {
    }
}
