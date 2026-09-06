using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Rendering;

public static class PixelPlayerRenderer
{
    public static void Draw(Vector2 screen, Vector2 velocity, string glyph, Color color, PlayerVisualFrame visual = default)
    {
        float baseRadius = glyph switch
        {
            "OL" or "DL" => 11.5f,
            "DE" or "LB" or "TE" => 10.5f,
            "QB" => 10f,
            "WR" or "RB" or "DB" => 9.5f,
            _ => 10f
        };

        Vector2 facingDirection = GetFacing(velocity, visual);
        int facing = facingDirection.X < -0.15f ? -1 : 1;
        bool moving = velocity.LengthSquared() > 0.35f && visual.Pose != PlayerPose.Tackled;
        float stride = moving ? MathF.Sin(visual.MotionTime * 12f) : 0f;
        int bob = moving && stride > 0.55f ? -1 : 0;
        int cx = (int)MathF.Round(screen.X);
        int cy = (int)MathF.Round(screen.Y) + bob;

        Color outline = Palette.Ink;
        Color highlight = Adjust(color, 48);
        Color shade = Adjust(color, -46);

        Raylib.DrawEllipse(cx + 1, cy + 7, baseRadius - 2f, 4f, new Color(0, 0, 0, 120));
        if (visual.Pose == PlayerPose.Tackled)
        {
            if (visual.ActionTime >= 0.12f)
            {
                DrawFallen(cx, cy, facing, glyph, color, shade, highlight);
                return;
            }
            cy += (int)(visual.ActionTime * 32f);
        }
        int legKick = moving ? (stride > 0f ? 1 : -1) : 0;
        Raylib.DrawRectangle(cx - 6, cy + 4 + legKick, 4, 6, outline);
        Raylib.DrawRectangle(cx + 2, cy + 4 - legKick, 4, 6, outline);
        Raylib.DrawRectangle(cx - 5, cy + 4 + legKick, 3, 4, shade);
        Raylib.DrawRectangle(cx + 2, cy + 4 - legKick, 3, 4, shade);

        int shoulder = MathF.Abs(facingDirection.X) > 0.7f ? 7 : 9;
        Raylib.DrawRectangle(cx - shoulder, cy - 5, shoulder * 2, 10, outline);
        Raylib.DrawRectangle(cx - shoulder + 1, cy - 4, shoulder * 2 - 2, 8, color);
        Raylib.DrawRectangle(cx - shoulder + 1, cy - 4, shoulder * 2 - 2, 2, highlight);
        int armSwing = moving ? (stride > 0f ? 2 : -2) : 0;
        Raylib.DrawRectangle(cx - shoulder - 1, cy - 3 + armSwing, 3, 6, shade);
        Raylib.DrawRectangle(cx + shoulder - 2, cy - 3 - armSwing, 3, 6, shade);

        Raylib.DrawCircle(cx, cy - 7, 6f, outline);
        Raylib.DrawCircle(cx, cy - 7, 5f, color);
        Raylib.DrawRectangle(cx - 4, cy - 10, 7, 2, highlight);

        if (MathF.Abs(facingDirection.Y) > MathF.Abs(facingDirection.X))
            Raylib.DrawRectangle(cx - 3, cy + (facingDirection.Y > 0f ? -12 : -4), 6, 1, Palette.White);
        else
            Raylib.DrawRectangle(cx + (facing > 0 ? 4 : -6), cy - 7, 2, 4, Palette.White);

        if (visual.Pose == PlayerPose.Throwing)
        {
            float followThrough = Math.Clamp(visual.ActionTime / 0.30f, 0f, 1f);
            Vector2 hand = new(cx + facing * (10f + followThrough * 3f), cy - 12f + followThrough * 14f);
            DrawArm(new(cx + facing * 7, cy - 3), hand, shade);
        }
        else if (visual.Pose == PlayerPose.Catching)
        {
            Vector2 hands = new Vector2(cx, cy) + GetBallOffset(velocity, visual);
            DrawArm(new(cx - 7, cy - 2), hands + new Vector2(-3, 0), shade);
            DrawArm(new(cx + 7, cy - 2), hands + new Vector2(3, 0), shade);
        }

        int fontSize = glyph.Length > 2 ? 8 : 9;
        int textWidth = Raylib.MeasureText(glyph, fontSize);
        int labelX = cx - textWidth / 2;
        int labelY = cy - 3;
        Raylib.DrawText(glyph, labelX + 1, labelY + 1, fontSize, new Color(0, 0, 0, 180));
        Raylib.DrawText(glyph, labelX, labelY, fontSize, Palette.White);
    }

    public static Vector2 GetBallOffset(Vector2 velocity, PlayerVisualFrame visual)
    {
        int facing = GetFacing(velocity, visual).X < -0.15f ? -1 : 1;
        if (visual.Pose == PlayerPose.Tackled && visual.ActionTime >= 0.12f)
            return new Vector2(facing * 5f, 6f);
        Vector2 tucked = new(facing * 9f, 1f);
        return visual.Pose == PlayerPose.Catching
            ? Vector2.Lerp(new Vector2(0, -14), tucked, Math.Clamp((visual.ActionTime - 0.10f) / 0.18f, 0f, 1f))
            : tucked;
    }

    private static Vector2 GetFacing(Vector2 velocity, PlayerVisualFrame visual) =>
        visual.Facing.LengthSquared() > 0.01f ? visual.Facing :
        velocity.LengthSquared() > 0.01f ? Vector2.Normalize(velocity) : Vector2.UnitY;

    private static void DrawArm(Vector2 shoulder, Vector2 hand, Color sleeve)
    {
        Raylib.DrawLineEx(shoulder, hand, 3f, sleeve);
        Raylib.DrawRectangle((int)hand.X - 1, (int)hand.Y - 1, 3, 3, Palette.White);
    }

    private static void DrawFallen(int x, int y, int facing, string glyph, Color color, Color shade, Color highlight)
    {
        Raylib.DrawEllipse(x, y + 7, 15f, 4f, new Color(0, 0, 0, 95));
        Raylib.DrawRectangle(x - 9, y + 1, 18, 9, Palette.Ink);
        Raylib.DrawRectangle(x - 8, y + 2, 16, 7, color);
        Raylib.DrawRectangle(x - 7, y + 2, 14, 2, highlight);
        Raylib.DrawRectangle(x - facing * 13 - 2, y + 2, 5, 3, shade);
        Raylib.DrawRectangle(x - facing * 13 - 2, y + 7, 5, 3, shade);
        Raylib.DrawCircle(x + facing * 11, y + 3, 5f, Palette.Ink);
        Raylib.DrawCircle(x + facing * 11, y + 3, 4f, color);
        Raylib.DrawRectangle(x + facing * 14 - 1, y + 2, 2, 3, Palette.White);
        Raylib.DrawText(glyph, x - Raylib.MeasureText(glyph, 8) / 2, y + 2, 8, Palette.White);
    }

    private static Color Adjust(Color color, int amount) => new(
        (byte)Math.Clamp(color.R + amount, 0, 255),
        (byte)Math.Clamp(color.G + amount, 0, 255),
        (byte)Math.Clamp(color.B + amount, 0, 255),
        color.A);
}
