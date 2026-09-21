using Raylib_cs;
using System.Numerics;
using RetroQB.Core;
using RetroQB.Rendering;

namespace RetroQB.Entities;

public abstract class Entity
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public string Glyph;
    public Color Color;
    public bool IsStarPlayer { get; protected set; }
    public PlayerAnimation Animation { get; } = new();

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
        PixelPlayerRenderer.Draw(screen, Velocity, Glyph, Color, Animation.Frame);
        if (IsStarPlayer) DrawStar(screen);
    }

    public static void DrawStar(Vector2 screen)
    {
        Raylib.DrawText("*", (int)screen.X + 5, (int)screen.Y - 18, 14, Palette.Ink);
        Raylib.DrawText("*", (int)screen.X + 4, (int)screen.Y - 19, 14, Palette.Gold);
    }
}
