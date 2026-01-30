using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Entities;

public sealed class Blocker : Entity
{
    public float Speed { get; }
    public float HomeX { get; }
    public float HomeY { get; }
    
    /// <summary>
    /// Reference to the team attributes for this blocker.
    /// </summary>
    public OffensiveTeamAttributes TeamAttributes { get; }

    public Blocker(Vector2 position, OffensiveTeamAttributes? teamAttributes = null) 
        : base(position, Constants.ReceiverRadius, "OL", teamAttributes?.SecondaryColor ?? Palette.OffensiveLine)
    {
        TeamAttributes = teamAttributes ?? OffensiveTeamAttributes.Default;
        Speed = TeamAttributes.OlSpeed;
        HomeX = position.X;
        HomeY = position.Y;
    }
}
