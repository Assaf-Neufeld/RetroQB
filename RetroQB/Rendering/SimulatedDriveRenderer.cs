using System.Numerics;
using Raylib_cs;
using RetroQB.Gameplay.Controllers;

namespace RetroQB.Rendering;

public sealed class SimulatedDriveRenderer
{
    public void Draw(SimulatedDriveController controller)
    {
        Vector2 markerPos = Constants.WorldToScreen(new Vector2(Constants.FieldWidth * 0.5f, controller.BallWorldY));
        float pulse = 10f + (MathF.Sin((float)Raylib.GetTime() * 6f) * 1.5f);

        Raylib.DrawCircleV(markerPos, pulse + 2f, new Color(10, 10, 14, 180));
        Raylib.DrawCircleV(markerPos, pulse, Palette.Red);
        Raylib.DrawCircleLines((int)markerPos.X, (int)markerPos.Y, pulse, Palette.White);

        DrawOverlayHeader(controller.CurrentPlayText);
        DrawResultBanner(controller.ResultBanner, controller.IsComplete);
    }

    private static void DrawOverlayHeader(string playText)
    {
        Rectangle field = Constants.FieldRect;
        int boxX = (int)field.X + 20;
        int boxY = (int)field.Y + 12;
        int boxW = (int)field.Width - 40;

        Raylib.DrawRectangle(boxX, boxY, boxW, 34, new Color(10, 10, 14, 190));
        Raylib.DrawRectangleLines(boxX, boxY, boxW, 34, Palette.Gold);

        string text = string.IsNullOrWhiteSpace(playText) ? "OPPONENT DRIVE" : playText.ToUpperInvariant();
        int size = 16;
        int width = Raylib.MeasureText(text, size);
        Raylib.DrawText(text, boxX + ((boxW - width) / 2), boxY + 9, size, Palette.White);
    }

    private static void DrawResultBanner(string text, bool isComplete)
    {
        if (!isComplete || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        int screenW = Raylib.GetScreenWidth();
        int y = (int)(Constants.FieldRect.Y + Constants.FieldRect.Height * 0.5f) - 25;
        int w = Math.Min(520, screenW - 240);
        int x = (screenW - w) / 2;
        Raylib.DrawRectangle(x, y, w, 56, new Color(10, 10, 14, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, w, 56), 2f, Palette.Gold);

        string upper = text.ToUpperInvariant();
        int size = 24;
        int textWidth = Raylib.MeasureText(upper, size);
        Raylib.DrawText(upper, x + ((w - textWidth) / 2), y + 16, size, Palette.Gold);
    }
}
