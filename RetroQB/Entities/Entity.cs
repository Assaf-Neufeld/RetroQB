using Raylib_cs;
using System.Numerics;
using RetroQB.Core;

namespace RetroQB.Entities;

public abstract class Entity
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public string Glyph;
    public Color Color;

    protected Entity(Vector2 position, float radius, string glyph, Color color)
    {
        Position = position;
        Velocity = Vector2.Zero;
        Radius = radius;
        Glyph = glyph;
        Color = color;
    }

    public virtual void Update(float dt)
    {
        Position += Velocity * dt;
    }

    public virtual void Draw()
    {
        Vector2 screen = Constants.WorldToScreen(Position);
        Raylib.DrawText(Glyph, (int)screen.X - 6, (int)screen.Y - 8, 18, Color);
    }
}
