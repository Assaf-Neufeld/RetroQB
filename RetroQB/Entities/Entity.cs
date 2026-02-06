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

        // Position-based pixel radius: linemen are bigger, skill players smaller
        float baseRadius = Glyph switch
        {
            "OL" or "DL" => 11.5f,
            "DE" or "LB" or "TE" => 10.5f,
            "QB" => 10f,
            "WR" or "RB" or "DB" => 9.5f,
            _ => 10f
        };

        float pixelRadius = baseRadius;

        // Drop shadow
        Raylib.DrawCircleV(screen + new Vector2(1f, 1.5f), pixelRadius, new Color(10, 10, 12, 120));

        // Filled circle body
        Raylib.DrawCircleV(screen, pixelRadius, Color);

        // Dark outline
        Raylib.DrawCircleLines((int)screen.X, (int)screen.Y, pixelRadius, new Color(16, 16, 20, 220));

        // Position label centered inside the circle
        int fontSize = 12;
        int textWidth = Raylib.MeasureText(Glyph, fontSize);
        int labelX = (int)screen.X - textWidth / 2;
        int labelY = (int)screen.Y - fontSize / 2;
        Raylib.DrawText(Glyph, labelX + 1, labelY + 1, fontSize, new Color(10, 10, 12, 160));
        Raylib.DrawText(Glyph, labelX, labelY, fontSize, Palette.White);
    }
}
