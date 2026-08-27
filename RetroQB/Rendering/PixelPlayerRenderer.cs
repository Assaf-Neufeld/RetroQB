using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

public static class PixelPlayerRenderer
{
    public static void Draw(Vector2 screen, Vector2 velocity, string glyph, Color color)
    {
        float baseRadius = glyph switch
        {
            "OL" or "DL" => 11.5f,
            "DE" or "LB" or "TE" => 10.5f,
            "QB" => 10f,
            "WR" or "RB" or "DB" => 9.5f,
            _ => 10f
        };

        bool moving = velocity.LengthSquared() > 0.35f;
        float stride = moving ? MathF.Sin((float)Raylib.GetTime() * 12f + screen.X * 0.1f) : 0f;
        int bob = moving && stride > 0.55f ? -1 : 0;
        int cx = (int)MathF.Round(screen.X);
        int cy = (int)MathF.Round(screen.Y) + bob;

        Color outline = Palette.Ink;
        Color highlight = Adjust(color, 48);
        Color shade = Adjust(color, -46);

        Raylib.DrawEllipse(cx + 1, cy + 7, baseRadius - 2f, 4f, new Color(0, 0, 0, 120));
        int legKick = moving ? (stride > 0f ? 1 : -1) : 0;
        Raylib.DrawRectangle(cx - 6, cy + 4 + legKick, 4, 6, outline);
        Raylib.DrawRectangle(cx + 2, cy + 4 - legKick, 4, 6, outline);
        Raylib.DrawRectangle(cx - 5, cy + 4 + legKick, 3, 4, shade);
        Raylib.DrawRectangle(cx + 2, cy + 4 - legKick, 3, 4, shade);

        Raylib.DrawRectangle(cx - 9, cy - 5, 18, 10, outline);
        Raylib.DrawRectangle(cx - 8, cy - 4, 16, 8, color);
        Raylib.DrawRectangle(cx - 8, cy - 4, 16, 2, highlight);
        Raylib.DrawRectangle(cx - 10, cy - 3, 3, 6, shade);
        Raylib.DrawRectangle(cx + 7, cy - 3, 3, 6, shade);

        Raylib.DrawCircle(cx, cy - 7, 6f, outline);
        Raylib.DrawCircle(cx, cy - 7, 5f, color);
        Raylib.DrawRectangle(cx - 4, cy - 10, 7, 2, highlight);

        int facing = velocity.X < -0.15f ? -1 : 1;
        Raylib.DrawRectangle(cx + (facing > 0 ? 4 : -6), cy - 7, 2, 4, Palette.White);

        int fontSize = glyph.Length > 2 ? 8 : 9;
        int textWidth = Raylib.MeasureText(glyph, fontSize);
        int labelX = cx - textWidth / 2;
        int labelY = cy - 3;
        Raylib.DrawText(glyph, labelX + 1, labelY + 1, fontSize, new Color(0, 0, 0, 180));
        Raylib.DrawText(glyph, labelX, labelY, fontSize, Palette.White);
    }

    private static Color Adjust(Color color, int amount) => new(
        (byte)Math.Clamp(color.R + amount, 0, 255),
        (byte)Math.Clamp(color.G + amount, 0, 255),
        (byte)Math.Clamp(color.B + amount, 0, 255),
        color.A);
}
