using System;
using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Entities;
using RetroQB.Core;

namespace RetroQB.Rendering;

public sealed class HudRenderer
{
    private static int PanelX => (int)Constants.OuterMargin;
    private static int PanelWidth => (int)Constants.SidePanelWidth;
    private static int ScoreboardY => (int)Constants.OuterMargin;
    private static int ScoreboardWidth => (int)Constants.ScoreboardPanelWidth;
    private static int ScoreboardHeight => Raylib.GetScreenHeight() - (int)(Constants.OuterMargin * 2);
    private GameStatsSnapshot _stats = new(
        new QbStatsSnapshot(0, 0, 0, 0, 0, 0, 0),
        Array.Empty<ReceiverStatsSnapshot>(),
        new RbStatsSnapshot(0, 0));

    public void SetStatsSnapshot(GameStatsSnapshot stats)
    {
        _stats = stats;
    }

    public void DrawScoreboard(PlayManager play, string resultText, GameState state)
    {
        GameStatsSnapshot stats = _stats;
        int x = Raylib.GetScreenWidth() - ScoreboardWidth - (int)Constants.OuterMargin;
        int y = ScoreboardY;

        Color panelBg = new(10, 16, 28, 235);
        Color panelBorder = new(36, 90, 150, 255);
        Color panelHeader = new(20, 28, 45, 255);
        Color panelAccent = new(30, 110, 170, 255);
        Color panelText = Palette.EndZoneText;

        // Outer shell
        Raylib.DrawRectangle(x, y, ScoreboardWidth, ScoreboardHeight, panelBg);
        Raylib.DrawRectangleLines(x, y, ScoreboardWidth, ScoreboardHeight, panelBorder);

        // Header bar
        Raylib.DrawRectangle(x + 2, y + 2, ScoreboardWidth - 4, 22, panelHeader);
        Raylib.DrawText("STATS BOARD", x + 10, y + 5, 14, Palette.Blue);

        int contentX = x + 12;
        int contentY = y + 30;
        int innerWidth = ScoreboardWidth - 24;

        // Score block
        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 48, panelAccent);
        Raylib.DrawText("HOME", contentX + 6, contentY + 2, 14, panelText);
        Raylib.DrawText($"{play.Score}", x + ScoreboardWidth - 54, contentY - 2, 22, Palette.Blue);
        Raylib.DrawText("AWAY", contentX + 6, contentY + 24, 14, panelText);
        Raylib.DrawText($"{play.AwayScore}", x + ScoreboardWidth - 54, contentY + 20, 22, Palette.Red);
        contentY += 54;

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
        string distanceText = toGoal <= play.Distance
            ? "Goal"
            : play.Distance < 1f
                ? "Inches"
                : $"{play.Distance:F0}";

        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 28, panelAccent);
        Raylib.DrawText($"{downOrdinal} & {distanceText}", contentX + 6, contentY, 18, Palette.Lime);
        contentY += 30;

        // Ball position block
        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 24, panelAccent);
        Raylib.DrawText($"BALL ON {yardLine:F0}", contentX + 6, contentY - 1, 14, panelText);
        contentY += 28;

        // Passing
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 8;
        Raylib.DrawText("PASSING", contentX + 2, contentY, 14, Palette.Blue);
        contentY += 18;

        int qbCol1 = contentX + (int)(innerWidth * 0.30f);
        int qbCol2 = contentX + (int)(innerWidth * 0.52f);
        int qbCol3 = contentX + (int)(innerWidth * 0.72f);
        int qbCol4 = contentX + (int)(innerWidth * 0.86f);
        Raylib.DrawText("QB", contentX + 2, contentY, 14, panelText);
        Raylib.DrawText("CMP/ATT", qbCol1, contentY - 2, 12, panelAccent);
        Raylib.DrawText("YDS", qbCol2, contentY - 2, 12, panelAccent);
        Raylib.DrawText("TD", qbCol3, contentY - 2, 12, panelAccent);
        Raylib.DrawText("INT", qbCol4, contentY - 2, 12, panelAccent);
        contentY += 16;
        Raylib.DrawText($"{stats.Qb.Completions}/{stats.Qb.Attempts}", qbCol1, contentY, 14, panelText);
        Raylib.DrawText($"{stats.Qb.PassYards}", qbCol2, contentY, 14, panelText);
        Raylib.DrawText($"{stats.Qb.PassTds}", qbCol3, contentY, 14, panelText);
        Raylib.DrawText($"{stats.Qb.Interceptions}", qbCol4, contentY, 14, panelText);
        contentY += 22;

        // Receiving
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 8;
        Raylib.DrawText("RECEIVING", contentX + 2, contentY, 14, Palette.Blue);
        contentY += 18;

        int recCol1 = contentX + (int)(innerWidth * 0.22f);
        int recCol2 = contentX + (int)(innerWidth * 0.52f);
        int recCol3 = contentX + (int)(innerWidth * 0.72f);
        Raylib.DrawText("REC", contentX + 2, contentY, 12, panelAccent);
        Raylib.DrawText("YDS", recCol2, contentY, 12, panelAccent);
        Raylib.DrawText("TD", recCol3, contentY, 12, panelAccent);
        contentY += 14;

        foreach (var receiver in stats.Receivers)
        {
            Raylib.DrawText($"{receiver.Label}", contentX + 6, contentY, 14, panelText);
            Raylib.DrawText($"{receiver.Receptions}", recCol1, contentY, 14, panelText);
            Raylib.DrawText($"{receiver.Yards}", recCol2, contentY, 14, panelText);
            Raylib.DrawText($"{receiver.Tds}", recCol3, contentY, 14, panelText);
            contentY += 16;
        }

        // Running
        contentY += 4;
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 8;
        Raylib.DrawText("RUNNING", contentX + 2, contentY, 14, Palette.Blue);
        contentY += 18;
        Raylib.DrawText("QB", contentX + 2, contentY, 14, panelText);
        Raylib.DrawText("YDS", recCol2, contentY - 2, 12, panelAccent);
        Raylib.DrawText("TD", recCol3, contentY - 2, 12, panelAccent);
        contentY += 16;
        Raylib.DrawText($"{stats.Qb.RushYards}", recCol2, contentY, 14, panelText);
        Raylib.DrawText($"{stats.Qb.RushTds}", recCol3, contentY, 14, panelText);
        contentY += 16;
        Raylib.DrawText("RB", contentX + 2, contentY, 14, panelText);
        Raylib.DrawText($"{stats.Rb.Yards}", recCol2, contentY, 14, panelText);
        Raylib.DrawText($"{stats.Rb.Tds}", recCol3, contentY, 14, panelText);
        contentY += 22;

        // Drive Summary Section
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 8;
        Raylib.DrawText("DRIVE SUMMARY", contentX + 2, contentY, 16, Palette.Blue);
        contentY += 22;

        // Show all plays in chronological order (oldest first)
        if (play.PlayRecords.Count == 0)
        {
            Raylib.DrawText("No plays yet", contentX + 6, contentY, 14, panelText);
        }
        else
        {
            for (int i = 0; i < play.PlayRecords.Count; i++)
            {
                var record = play.PlayRecords[i];
                
                // Play number and situation (e.g., "#1: OWN 25 | 1st & 10")
                string situationLine = $"#{record.PlayNumber}: {record.GetSituationText()}";
                Raylib.DrawText(situationLine, contentX + 4, contentY, 14, Palette.Yellow);
                contentY += 16;
                
                // Play call (e.g., "Quick: Slants vs Zone (LB blitz)")
                string playCallLine = record.GetPlayCallText();
                string? blitzLine = null;
                int blitzIndex = playCallLine.IndexOf(" (");
                if (blitzIndex >= 0)
                {
                    blitzLine = playCallLine.Substring(blitzIndex + 2).TrimEnd(')');
                    if (blitzLine.EndsWith(" blitz", StringComparison.OrdinalIgnoreCase))
                        blitzLine = blitzLine[..^6];
                    playCallLine = playCallLine.Substring(0, blitzIndex);
                }

                // Truncate if too long
                if (playCallLine.Length > 32)
                    playCallLine = playCallLine.Substring(0, 29) + "...";
                Raylib.DrawText(playCallLine, contentX + 8, contentY, 12, panelText);
                contentY += 14;

                if (!string.IsNullOrWhiteSpace(blitzLine))
                {
                    string blitzText = $"Blitz: {blitzLine}";
                    if (blitzText.Length > 32)
                        blitzText = blitzText.Substring(0, 29) + "...";
                    Raylib.DrawText(blitzText, contentX + 8, contentY, 12, Palette.Orange);
                    contentY += 14;
                }
                
                // Result (e.g., "+14 yd pass to WR2 (Go)")
                string resultLine = record.GetResultText();
                Color resultColor = record.Outcome switch
                {
                    PlayOutcome.Touchdown => Palette.Gold,
                    PlayOutcome.Interception => Palette.Red,
                    PlayOutcome.Incomplete => Palette.Orange,
                    PlayOutcome.Turnover => Palette.Red,
                    _ when record.Gain >= 10 => Palette.Lime,
                    _ when record.Gain < 0 => Palette.Orange,
                    _ => panelText
                };
                // Truncate if too long
                if (resultLine.Length > 35)
                    resultLine = resultLine.Substring(0, 32) + "...";
                Raylib.DrawText(resultLine, contentX + 8, contentY, 12, resultColor);
                contentY += 18;
            }
        }

        // Last play result ticker (at the bottom)
        if (!string.IsNullOrWhiteSpace(resultText))
        {
            contentY += 4;
            Color resultColor = resultText.Contains("TD") || resultText.Contains("1ST") ? Palette.Gold :
                               resultText.Contains("INT") || resultText.Contains("TURN") ? Palette.Red :
                               resultText.Contains("Incomplete") ? Palette.Orange :
                               panelText;

            Raylib.DrawRectangle(contentX - 4, contentY - 4, ScoreboardWidth - 24, 26, panelHeader);
            Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 26, panelAccent);
            Raylib.DrawText(resultText, contentX + 6, contentY - 1, 14, resultColor);
        }
    }

    public void DrawSidePanel(PlayManager play, string resultText, string selectedReceiverLabel, GameState state)
    {
        int screenH = Raylib.GetScreenHeight();
        int panelHeight = screenH - (int)(Constants.OuterMargin * 2);

        // Draw panel background
        Raylib.DrawRectangle(PanelX, (int)Constants.OuterMargin, PanelWidth, panelHeight, new Color(20, 20, 25, 220));
        Raylib.DrawRectangleLines(PanelX, (int)Constants.OuterMargin, PanelWidth, panelHeight, Palette.DarkGreen);

        int y = (int)Constants.OuterMargin + 15;
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

        // Goal
        y = screenH - 160;
        Raylib.DrawRectangle(x - 4, y - 4, PanelWidth - 22, 22, new Color(30, 50, 30, 200));
        Raylib.DrawText("GOAL: First to 21 wins!", x, y, 14, Palette.Gold);
        y += 26;

        // Controls at bottom
        Raylib.DrawLine(x, y, x + PanelWidth - 30, y, Palette.DarkGreen);
        y += 10;
        Raylib.DrawText("CONTROLS", x, y, 14, Palette.Yellow);
        y += 18;
        Raylib.DrawText("Move QB: WASD or Arrows", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Sprint: Hold Shift", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Select Play: 1-9, 0", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Snap Ball: Space", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Throw to Receiver: 1-5", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Restart Drive: R", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Pause: Esc", x, y, 12, Palette.White);
    }

    public void DrawMainMenu()
    {
        var field = Constants.FieldRect;
        int centerX = (int)(field.X + field.Width / 2f);
        int centerY = (int)(field.Y + field.Height / 2f);

        Raylib.DrawText("RetroQB", centerX - 100, centerY - 50, 48, Palette.Gold);
        Raylib.DrawText("Press ENTER to start", centerX - 110, centerY + 30, 20, Palette.White);
    }

    public void DrawPause()
    {
        int screenW = Raylib.GetScreenWidth();
        Raylib.DrawText("PAUSED", screenW / 2 - 60, 40, 24, Palette.Yellow);
    }

    public void DrawVictoryBanner(int finalScore, int awayScore)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int bannerWidth = Math.Min(520, screenW - 80);
        int bannerHeight = 340;
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        // Outer frame with gold border
        Raylib.DrawRectangle(x - 4, y - 4, bannerWidth + 8, bannerHeight + 8, Palette.Gold);
        Raylib.DrawRectangle(x, y, bannerWidth, bannerHeight, new Color(10, 14, 22, 250));
        Raylib.DrawRectangle(x + 4, y + 4, bannerWidth - 8, bannerHeight - 8, new Color(18, 24, 36, 250));
        
        // Victory header
        string title = "VICTORY!";
        int titleSize = 48;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, x + (bannerWidth - titleWidth) / 2, y + 16, titleSize, Palette.Gold);

        // Final score
        string scoreText = $"FINAL: {finalScore} - {awayScore}";
        int scoreSize = 24;
        int scoreWidth = Raylib.MeasureText(scoreText, scoreSize);
        Raylib.DrawText(scoreText, x + (bannerWidth - scoreWidth) / 2, y + 70, scoreSize, Palette.Lime);

        // Divider
        int divY = y + 102;
        Raylib.DrawLine(x + 30, divY, x + bannerWidth - 30, divY, Palette.Gold);

        // QB Stats header
        int contentY = divY + 12;
        string qbHeader = "QB GAME STATS";
        int headerSize = 20;
        int headerWidth = Raylib.MeasureText(qbHeader, headerSize);
        Raylib.DrawText(qbHeader, x + (bannerWidth - headerWidth) / 2, contentY, headerSize, Palette.Blue);
        contentY += 32;

        // Stats layout
        int leftCol = x + 40;
        int rightCol = x + bannerWidth / 2 + 20;
        int labelSize = 16;
        int valueSize = 22;
        Color labelColor = new(140, 160, 180, 255);
        Color valueColor = Palette.White;

        // Passing stats
        Raylib.DrawText("PASSING", leftCol, contentY, labelSize, labelColor);
        contentY += 20;
        
        string compAtt = $"{_stats.Qb.Completions}/{_stats.Qb.Attempts}";
        Raylib.DrawText("CMP/ATT:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText(compAtt, leftCol + 90, contentY, valueSize, valueColor);
        
        Raylib.DrawText("YARDS:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{_stats.Qb.PassYards}", rightCol + 70, contentY, valueSize, valueColor);
        contentY += 26;

        Raylib.DrawText("TD:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{_stats.Qb.PassTds}", leftCol + 90, contentY, valueSize, _stats.Qb.PassTds > 0 ? Palette.Gold : valueColor);
        
        Raylib.DrawText("INT:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{_stats.Qb.Interceptions}", rightCol + 70, contentY, valueSize, _stats.Qb.Interceptions > 0 ? Palette.Red : valueColor);
        contentY += 30;

        // Passer rating calculation (simplified)
        float compPct = _stats.Qb.Attempts > 0 ? (float)_stats.Qb.Completions / _stats.Qb.Attempts * 100f : 0f;
        Raylib.DrawText("CMP%:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{compPct:F1}%", leftCol + 90, contentY, valueSize, compPct >= 65f ? Palette.Lime : valueColor);
        
        float ypa = _stats.Qb.Attempts > 0 ? (float)_stats.Qb.PassYards / _stats.Qb.Attempts : 0f;
        Raylib.DrawText("Y/A:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{ypa:F1}", rightCol + 70, contentY, valueSize, ypa >= 8f ? Palette.Lime : valueColor);
        contentY += 34;

        // Rushing stats
        Raylib.DrawText("RUSHING", leftCol, contentY, labelSize, labelColor);
        contentY += 20;
        
        Raylib.DrawText("YARDS:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{_stats.Qb.RushYards}", leftCol + 90, contentY, valueSize, valueColor);
        
        Raylib.DrawText("TD:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{_stats.Qb.RushTds}", rightCol + 70, contentY, valueSize, _stats.Qb.RushTds > 0 ? Palette.Gold : valueColor);
        contentY += 34;

        // Continue prompt
        string prompt = "PRESS ENTER FOR NEW GAME";
        int promptSize = 16;
        int promptWidth = Raylib.MeasureText(prompt, promptSize);
        Raylib.DrawText(prompt, x + (bannerWidth - promptWidth) / 2, y + bannerHeight - 28, promptSize, Palette.Yellow);
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
