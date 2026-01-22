using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Entities;
using RetroQB.Core;

namespace RetroQB.Rendering;

public sealed class HudRenderer
{
    public void DrawHud(PlayManager play, string resultText, int selectedReceiver)
    {
        int x = 20;
        int y = 10;

        Raylib.DrawText($"Down: {play.Down}", x, y, 18, Palette.White);
        Raylib.DrawText($"To Go: {play.Distance:F1}", x + 120, y, 18, Palette.White);
        Raylib.DrawText($"LOS: {play.GetYardLineDisplay(play.LineOfScrimmage):F0}", x + 260, y, 18, Palette.White);
        Raylib.DrawText($"Score: {play.Score}", x + 380, y, 18, Palette.White);
        Raylib.DrawText($"Target: R{selectedReceiver + 1}", x + 520, y, 18, Palette.White);

        if (!string.IsNullOrWhiteSpace(resultText))
        {
            Raylib.DrawText(resultText, x, y + 24, 18, Palette.Gold);
        }

        int bottomY = (int)(Constants.FieldRect.Y + Constants.FieldRect.Height + 8);
        Raylib.DrawText("Move: WASD/Arrows  Sprint: Shift  Throw: Space  Cycle: Tab  Restart: R  Pause: Esc", x, bottomY, 16, Palette.White);
    }

    public void DrawMainMenu()
    {
        Raylib.DrawText("RetroQB", 520, 220, 48, Palette.Gold);
        Raylib.DrawText("Press ENTER to start", 500, 300, 20, Palette.White);
    }

    public void DrawPreSnap(PlayType playType)
    {
        Raylib.DrawText("PRE-SNAP", 560, 60, 20, Palette.White);
        Raylib.DrawText("Choose Play: 1) Quick Pass  2) Long Pass  3) QB Run Focus", 330, 90, 18, Palette.White);
        Raylib.DrawText($"Selected: {playType}", 540, 120, 18, Palette.Gold);
        Raylib.DrawText("Press SPACE to snap", 520, 150, 18, Palette.White);
    }

    public void DrawPlayOver()
    {
        Raylib.DrawText("PLAY OVER", 550, 80, 20, Palette.White);
        Raylib.DrawText("Press ENTER to continue", 500, 110, 18, Palette.White);
    }

    public void DrawPause()
    {
        Raylib.DrawText("PAUSED", 590, 40, 24, Palette.Yellow);
    }
}
