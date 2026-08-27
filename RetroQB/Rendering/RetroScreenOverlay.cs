using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

/// <summary>
/// Lightweight final-pass treatment that gives every state the same arcade-display finish.
/// </summary>
public static class RetroScreenOverlay
{
    public static void Draw()
    {
        int width = Raylib.GetScreenWidth();
        int height = Raylib.GetScreenHeight();

        // Sparse scanlines preserve readability while gently tying the vector primitives together.
        for (int y = 1; y < height; y += 4)
        {
            Raylib.DrawRectangle(0, y, width, 1, new Color(0, 0, 0, 20));
        }

        // Pixel-stepped vignette, intentionally squared off instead of using a modern soft shader.
        int band = Math.Clamp(Math.Min(width, height) / 48, 8, 22);
        for (int i = 0; i < 4; i++)
        {
            int alpha = 30 - (i * 6);
            int inset = i * band;
            int thickness = Math.Max(3, band - (i * 2));
            Color shade = new(0, 0, 0, alpha);
            Raylib.DrawRectangle(inset, inset, width - (inset * 2), thickness, shade);
            Raylib.DrawRectangle(inset, height - inset - thickness, width - (inset * 2), thickness, shade);
            Raylib.DrawRectangle(inset, inset, thickness, height - (inset * 2), shade);
            Raylib.DrawRectangle(width - inset - thickness, inset, thickness, height - (inset * 2), shade);
        }

        // Tiny corner screws make the whole screen feel like one cabinet faceplate.
        DrawScrew(7, 7);
        DrawScrew(width - 8, 7);
        DrawScrew(7, height - 8);
        DrawScrew(width - 8, height - 8);
    }

    private static void DrawScrew(int x, int y)
    {
        Raylib.DrawCircle(x, y, 3, new Color(70, 84, 94, 150));
        Raylib.DrawLine(x - 1, y, x + 1, y, new Color(12, 16, 22, 210));
    }
}
