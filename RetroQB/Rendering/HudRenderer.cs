using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Entities;
using RetroQB.Core;

namespace RetroQB.Rendering;

public sealed class HudRenderer
{
    private const int PanelX = 10;
    private const int PanelWidth = 260;
    
    public void DrawSidePanel(PlayManager play, string resultText, int selectedReceiver, GameState state, PlayType playType)
    {
        int screenH = Raylib.GetScreenHeight();
        
        // Draw panel background
        Raylib.DrawRectangle(PanelX, 10, PanelWidth, screenH - 20, new Color(20, 20, 25, 220));
        Raylib.DrawRectangleLines(PanelX, 10, PanelWidth, screenH - 20, Palette.DarkGreen);
        
        int y = 25;
        int x = PanelX + 15;
        
        // Title
        Raylib.DrawText("RETRO QB", x, y, 28, Palette.Gold);
        y += 40;
        
        // Score
        Raylib.DrawText($"SCORE: {play.Score}", x, y, 24, Palette.White);
        y += 40;
        
        // Divider
        Raylib.DrawLine(x, y, x + PanelWidth - 30, y, Palette.DarkGreen);
        y += 15;
        
        // Down and Distance
        string downOrdinal = play.Down switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            4 => "4th",
            _ => $"{play.Down}th"
        };
        
        float yardLine = play.GetYardLineDisplay(play.LineOfScrimmage);
        float toGoal = 100f - yardLine;
        string distanceText = toGoal <= play.Distance ? "Goal" : $"{play.Distance:F0}";
        
        Raylib.DrawText($"{downOrdinal} & {distanceText}", x, y, 22, Palette.Gold);
        y += 28;
        
        Raylib.DrawText($"Ball on {yardLine:F0} yd line", x, y, 18, Palette.White);
        y += 28;
        
        Raylib.DrawText($"Target: WR {selectedReceiver + 1}", x, y, 18, Palette.Lime);
        y += 35;
        
        // Divider
        Raylib.DrawLine(x, y, x + PanelWidth - 30, y, Palette.DarkGreen);
        y += 15;
        
        // Play selection (pre-snap) or last result
        if (state == GameState.PreSnap)
        {
            Raylib.DrawText("SELECT PLAY:", x, y, 16, Palette.Yellow);
            y += 22;
            Raylib.DrawText("1) Quick Pass", x, y, 16, playType == PlayType.QuickPass ? Palette.Gold : Palette.White);
            y += 20;
            Raylib.DrawText("2) Long Pass", x, y, 16, playType == PlayType.LongPass ? Palette.Gold : Palette.White);
            y += 20;
            Raylib.DrawText("3) QB Run", x, y, 16, playType == PlayType.QbRunFocus ? Palette.Gold : Palette.White);
            y += 28;
            Raylib.DrawText("SPACE to snap", x, y, 16, Palette.Lime);
            y += 30;
        }
        else if (state == GameState.PlayOver)
        {
            Raylib.DrawText("PLAY OVER", x, y, 18, Palette.Yellow);
            y += 24;
            Raylib.DrawText("ENTER to continue", x, y, 16, Palette.Lime);
            y += 30;
        }
        else if (!string.IsNullOrWhiteSpace(resultText))
        {
            Raylib.DrawText(resultText, x, y, 16, Palette.Gold);
            y += 30;
        }
        else
        {
            y += 30;
        }
        
        // Divider
        Raylib.DrawLine(x, y, x + PanelWidth - 30, y, Palette.DarkGreen);
        y += 15;
        
        // Drive Summary
        Raylib.DrawText("DRIVE SUMMARY", x, y, 16, Palette.Yellow);
        y += 24;
        
        // Show last 8 plays of drive history (most recent at top)
        int maxPlays = Math.Min(play.DriveHistory.Count, 8);
        if (maxPlays == 0)
        {
            Raylib.DrawText("No plays yet", x, y, 14, Palette.White);
        }
        else
        {
            for (int i = play.DriveHistory.Count - 1; i >= Math.Max(0, play.DriveHistory.Count - maxPlays); i--)
            {
                string playResult = play.DriveHistory[i];
                Color textColor = playResult.Contains("TD") || playResult.Contains("1ST") ? Palette.Gold :
                                  playResult.Contains("INT") || playResult.Contains("TURN") ? Palette.Red :
                                  Palette.White;
                Raylib.DrawText(playResult, x, y, 14, textColor);
                y += 18;
            }
        }
        
        // Controls at bottom
        y = screenH - 100;
        Raylib.DrawLine(x, y, x + PanelWidth - 30, y, Palette.DarkGreen);
        y += 10;
        Raylib.DrawText("CONTROLS", x, y, 14, Palette.Yellow);
        y += 18;
        Raylib.DrawText("Move: WASD/Arrows", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Sprint: Shift", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Throw: Space", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Cycle WR: Tab", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Restart: R  Pause: Esc", x, y, 12, Palette.White);
    }

    public void DrawMainMenu()
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();
        int centerX = screenW / 2;
        int centerY = screenH / 2;
        
        Raylib.DrawText("RetroQB", centerX - 100, centerY - 50, 48, Palette.Gold);
        Raylib.DrawText("Press ENTER to start", centerX - 110, centerY + 30, 20, Palette.White);
    }

    public void DrawPause()
    {
        int screenW = Raylib.GetScreenWidth();
        Raylib.DrawText("PAUSED", screenW / 2 - 60, 40, 24, Palette.Yellow);
    }
}
