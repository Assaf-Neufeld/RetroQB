using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Entities;
using RetroQB.Core;

namespace RetroQB.Rendering;

public sealed class HudRenderer
{
    private const int PanelX = 10;
    private const int PanelWidth = 320;
    private const int ScoreboardY = 10;
    private const int ScoreboardWidth = 320;
    private const int ScoreboardHeight = 140;

    public void DrawScoreboard(PlayManager play, string resultText, GameState state)
    {
        int x = Raylib.GetScreenWidth() - ScoreboardWidth - 10;
        int y = ScoreboardY;

        // Outer shell
        Raylib.DrawRectangle(x, y, ScoreboardWidth, ScoreboardHeight, new Color(12, 12, 14, 235));
        Raylib.DrawRectangleLines(x, y, ScoreboardWidth, ScoreboardHeight, Palette.Gold);

        // Header bar
        Raylib.DrawRectangle(x + 2, y + 2, ScoreboardWidth - 4, 22, new Color(30, 30, 36, 255));
        Raylib.DrawText("RETRO STADIUM", x + 10, y + 5, 14, Palette.Gold);

        int contentX = x + 10;
        int contentY = y + 30;

        // Score block
        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 20, 32, Palette.DarkGreen);
        Raylib.DrawText("HOME", contentX + 6, contentY + 2, 14, Palette.White);
        Raylib.DrawText($"{play.Score}", x + ScoreboardWidth - 50, contentY - 1, 26, Palette.White);
        contentY += 38;

        // Down & Distance block
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

        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 20, 28, Palette.DarkGreen);
        Raylib.DrawText($"{downOrdinal} & {distanceText}", contentX + 6, contentY, 18, Palette.Gold);
        contentY += 30;

        // Ball position block
        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 20, 24, Palette.DarkGreen);
        Raylib.DrawText($"BALL ON {yardLine:F0}", contentX + 6, contentY - 1, 14, Palette.White);
        contentY += 28;

        // Last play result ticker
        if (!string.IsNullOrWhiteSpace(resultText))
        {
            Color resultColor = resultText.Contains("TD") || resultText.Contains("1ST") ? Palette.Gold :
                               resultText.Contains("INT") || resultText.Contains("TURN") ? Palette.Red :
                               resultText.Contains("Incomplete") ? Palette.Orange :
                               Palette.White;

            Raylib.DrawRectangle(contentX - 4, contentY - 4, ScoreboardWidth - 20, 26, new Color(25, 25, 30, 255));
            Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 20, 26, Palette.DarkGreen);
            Raylib.DrawText(resultText, contentX + 6, contentY - 1, 14, resultColor);
        }
    }
    
    public void DrawSidePanel(PlayManager play, string resultText, string selectedReceiverLabel, GameState state)
    {
        int screenH = Raylib.GetScreenHeight();
        
        // Draw panel background
        Raylib.DrawRectangle(PanelX, 10, PanelWidth, screenH - 20, new Color(20, 20, 25, 220));
        Raylib.DrawRectangleLines(PanelX, 10, PanelWidth, screenH - 20, Palette.DarkGreen);
        
        int y = 25;
        int x = PanelX + 15;
        
        // Title
        Raylib.DrawText("RETRO QB", x, y, 34, Palette.Gold);
        y += 46;

        // Divider
        Raylib.DrawLine(x, y, x + PanelWidth - 30, y, Palette.DarkGreen);
        y += 15;

        // Play selection (pre-snap)
        if (state == GameState.PreSnap)
        {
            Raylib.DrawText("SELECT PLAY:", x, y, 18, Palette.Yellow);
            y += 24;
            PlayType suggestedFamily = play.GetSuggestedPlayFamily();
            string suggestedName = suggestedFamily switch
            {
                PlayType.QuickPass => "Quick",
                PlayType.LongPass => "Long",
                PlayType.QbRunFocus => "Run",
                _ => "Play"
            };
            Raylib.DrawText($"Suggested: {suggestedName}", x, y, 16, Palette.Lime);
            y += 22;
            var options = play.PlayOptions;
            for (int i = 0; i < options.Count; i++)
            {
                int displayNum = i == 9 ? 0 : i + 1;
                var option = options[i];
                bool isSelected = option.Family == play.SelectedPlayFamily && option.Index == play.SelectedPlayIndex;
                string familyName = option.Family switch
                {
                    PlayType.QuickPass => "Quick",
                    PlayType.LongPass => "Long",
                    PlayType.QbRunFocus => "Run",
                    _ => "Play"
                };

                Raylib.DrawText($"{displayNum}) {familyName}: {option.Name}", x, y, 18, isSelected ? Palette.Gold : Palette.White);
                y += 20;
            }

            y += 10;
            Raylib.DrawText("1-9,0 select | SPACE snap", x, y, 18, Palette.Lime);
            y += 32;
        }
        else
        {
            y += 24;
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
        Raylib.DrawText("Throw: 1-5", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Target: Priority", x, y, 12, Palette.White);
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

    public void DrawTouchdownPopup()
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int bannerWidth = Math.Min(720, screenW - 120);
        int bannerHeight = 120;
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        Raylib.DrawRectangle(x, y, bannerWidth, bannerHeight, new Color(10, 10, 14, 235));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, bannerWidth, bannerHeight), 3, Palette.Gold);
        Raylib.DrawRectangle(x + 6, y + 6, bannerWidth - 12, bannerHeight - 12, new Color(20, 20, 28, 235));

        string text = "TOUCHDOWN!";
        int fontSize = 52;
        int textWidth = Raylib.MeasureText(text, fontSize);
        int textX = x + (bannerWidth - textWidth) / 2;
        int textY = y + (bannerHeight - fontSize) / 2 - 4;
        Raylib.DrawText(text, textX, textY, fontSize, Palette.Gold);

        string subText = "7 POINTS";
        int subSize = 20;
        int subWidth = Raylib.MeasureText(subText, subSize);
        Raylib.DrawText(subText, x + (bannerWidth - subWidth) / 2, y + bannerHeight - subSize - 12, subSize, Palette.White);
    }
}
