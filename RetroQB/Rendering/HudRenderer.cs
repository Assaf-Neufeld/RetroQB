using System;
using System.Collections.Generic;
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

    public void DrawScoreboard(PlayManager play, string resultText, GameState state, OffensiveTeamAttributes offensiveTeam)
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
        Raylib.DrawText("STATS BOARD", x + 10, y + 5, 14, offensiveTeam.PrimaryColor);

        int contentX = x + 12;
        int contentY = y + 30;
        int innerWidth = ScoreboardWidth - 24;

        void DrawRightText(string text, int rightX, int drawY, int fontSize, Color color)
        {
            int textWidth = Raylib.MeasureText(text, fontSize);
            Raylib.DrawText(text, rightX - textWidth, drawY, fontSize, color);
        }

        // Score block
        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 48, panelAccent);
        string homeLabel = string.IsNullOrWhiteSpace(offensiveTeam.Name) ? "HOME" : offensiveTeam.Name.ToUpperInvariant();
        Raylib.DrawText(homeLabel, contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        Raylib.DrawText($"{play.Score}", x + ScoreboardWidth - 54, contentY - 2, 22, offensiveTeam.PrimaryColor);
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
        contentY += 6;
        Raylib.DrawRectangle(contentX - 2, contentY, innerWidth + 4, 18, new Color(18, 26, 40, 220));
        Raylib.DrawText("PASSING", contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        contentY += 22;

        int qbCol1Right = contentX + (int)(innerWidth * 0.52f);
        int qbCol2Right = contentX + (int)(innerWidth * 0.72f);
        int qbCol3Right = contentX + (int)(innerWidth * 0.86f);
        int qbCol4Right = contentX + innerWidth;
        Raylib.DrawText("QB", contentX + 2, contentY, 14, panelText);
        DrawRightText("CMP/ATT", qbCol1Right, contentY - 2, 12, panelAccent);
        DrawRightText("YDS", qbCol2Right, contentY - 2, 12, panelAccent);
        DrawRightText("TD", qbCol3Right, contentY - 2, 12, panelAccent);
        DrawRightText("INT", qbCol4Right, contentY - 2, 12, panelAccent);
        contentY += 16;
        DrawRightText($"{stats.Qb.Completions}/{stats.Qb.Attempts}", qbCol1Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.PassYards}", qbCol2Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.PassTds}", qbCol3Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.Interceptions}", qbCol4Right, contentY, 14, panelText);
        contentY += 22;

        // Receiving
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 6;
        Raylib.DrawRectangle(contentX - 2, contentY, innerWidth + 4, 18, new Color(18, 26, 40, 220));
        Raylib.DrawText("RECEIVING", contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        contentY += 22;

        int recCol1Right = contentX + (int)(innerWidth * 0.46f);
        int recCol2Right = contentX + (int)(innerWidth * 0.72f);
        int recCol3Right = contentX + innerWidth;
        Raylib.DrawText("REC", contentX + 2, contentY, 12, panelAccent);
        DrawRightText("YDS", recCol2Right, contentY, 12, panelAccent);
        DrawRightText("TD", recCol3Right, contentY, 12, panelAccent);
        contentY += 14;

        foreach (var receiver in stats.Receivers)
        {
            Raylib.DrawText($"{receiver.Label}", contentX + 6, contentY, 14, panelText);
            DrawRightText($"{receiver.Receptions}", recCol1Right, contentY, 14, panelText);
            DrawRightText($"{receiver.Yards}", recCol2Right, contentY, 14, panelText);
            DrawRightText($"{receiver.Tds}", recCol3Right, contentY, 14, panelText);
            contentY += 16;
        }

        // Running
        contentY += 4;
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 6;
        Raylib.DrawRectangle(contentX - 2, contentY, innerWidth + 4, 18, new Color(18, 26, 40, 220));
        Raylib.DrawText("RUNNING", contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        contentY += 22;
        Raylib.DrawText("QB", contentX + 2, contentY, 14, panelText);
        DrawRightText("YDS", recCol2Right, contentY - 2, 12, panelAccent);
        DrawRightText("TD", recCol3Right, contentY - 2, 12, panelAccent);
        contentY += 16;
        DrawRightText($"{stats.Qb.RushYards}", recCol2Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.RushTds}", recCol3Right, contentY, 14, panelText);
        contentY += 16;
        Raylib.DrawText("RB", contentX + 2, contentY, 14, panelText);
        DrawRightText($"{stats.Rb.Yards}", recCol2Right, contentY, 14, panelText);
        DrawRightText($"{stats.Rb.Tds}", recCol3Right, contentY, 14, panelText);
        contentY += 22;

        // Drive Summary Section
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 8;
        Raylib.DrawText("DRIVE SUMMARY", contentX + 2, contentY, 16, offensiveTeam.SecondaryColor);
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
            
            PlayType suggestedType = play.GetSuggestedPlayType();
            string suggestedName = suggestedType == PlayType.Pass ? "Pass" : "Run";
            Raylib.DrawText($"Suggested: {suggestedName}", x, y, 16, Palette.Lime);
            y += 22;

            // Pass plays header
            Raylib.DrawText("PASS (1-9, 0):", x, y, 14, Palette.Cyan);
            y += 18;

            var passPlays = play.PassPlays;
            string[] passKeys = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
            for (int i = 0; i < passPlays.Count && i < 10; i++)
            {
                int displayIndex = i == 0 ? 0 : i;
                string key = passKeys[displayIndex];
                bool isSelected = play.SelectedPlayType == PlayType.Pass && play.SelectedPlayIndex == i;
                Raylib.DrawText($"{key}) {passPlays[i].Name}", x, y, 14, isSelected ? Palette.Gold : Palette.White);
                y += 16;
            }

            y += 8;

            // Run plays header
            Raylib.DrawText("RUN (Q-P):", x, y, 14, Palette.Orange);
            y += 18;

            var runPlays = play.RunPlays;
            string[] runKeys = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
            for (int i = 0; i < runPlays.Count && i < 10; i++)
            {
                bool isSelected = play.SelectedPlayType == PlayType.Run && play.SelectedPlayIndex == i;
                Raylib.DrawText($"{runKeys[i]}) {runPlays[i].Name}", x, y, 14, isSelected ? Palette.Gold : Palette.White);
                y += 16;
            }

            y += 10;
            Raylib.DrawText("SPACE to snap", x, y, 16, Palette.Lime);
            y += 24;
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
        Raylib.DrawText("Move: WASD or Arrows", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Sprint: Hold Shift", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Pass Plays: 1-9, 0", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Run Plays: Q-P", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Snap Ball: Space", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Throw: 1-5", x, y, 12, Palette.White);
        y += 15;
        Raylib.DrawText("Pause: Esc", x, y, 12, Palette.White);
    }

    public void DrawMainMenu(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams)
    {
        var field = Constants.FieldRect;
        int centerX = (int)(field.X + field.Width / 2f);
        int centerY = (int)(field.Y + field.Height / 2f);

        // Calculate panel dimensions for unified window
        int panelWidth = 560;
        int panelHeight = 420;
        int panelX = centerX - panelWidth / 2;
        int panelY = centerY - panelHeight / 2;

        // Outer glow/shadow effect
        Raylib.DrawRectangle(panelX - 8, panelY - 8, panelWidth + 16, panelHeight + 16, new Color(0, 0, 0, 120));
        
        // Main panel background with layered borders
        Raylib.DrawRectangle(panelX - 4, panelY - 4, panelWidth + 8, panelHeight + 8, Palette.Gold);
        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(12, 16, 24, 250));
        Raylib.DrawRectangle(panelX + 3, panelY + 3, panelWidth - 6, panelHeight - 6, new Color(18, 22, 32, 250));

        int contentX = panelX + 30;
        int contentY = panelY + 24;

        // ═══════════════════════════════════════════════════════════════
        // TITLE SECTION
        // ═══════════════════════════════════════════════════════════════
        string title = "RETRO QB";
        int titleSize = 52;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, panelX + (panelWidth - titleWidth) / 2, contentY, titleSize, Palette.Gold);
        contentY += titleSize + 8;

        // Subtitle
        string subtitle = "2D Football Simulation";
        int subtitleSize = 16;
        int subtitleWidth = Raylib.MeasureText(subtitle, subtitleSize);
        Raylib.DrawText(subtitle, panelX + (panelWidth - subtitleWidth) / 2, contentY, subtitleSize, new Color(140, 160, 180, 255));
        contentY += subtitleSize + 16;

        // Decorative divider
        int dividerPadding = 40;
        Raylib.DrawLine(panelX + dividerPadding, contentY, panelX + panelWidth - dividerPadding, contentY, new Color(60, 80, 100, 180));
        Raylib.DrawLine(panelX + dividerPadding, contentY + 1, panelX + panelWidth - dividerPadding, contentY + 1, Palette.Gold);
        Raylib.DrawLine(panelX + dividerPadding, contentY + 2, panelX + panelWidth - dividerPadding, contentY + 2, new Color(60, 80, 100, 180));
        contentY += 20;

        // ═══════════════════════════════════════════════════════════════
        // GAME RULES SECTION
        // ═══════════════════════════════════════════════════════════════
        string goalText = "★ FIRST TO 21 WINS ★";
        int goalSize = 20;
        int goalWidth = Raylib.MeasureText(goalText, goalSize);
        Raylib.DrawText(goalText, panelX + (panelWidth - goalWidth) / 2, contentY, goalSize, Palette.Lime);
        contentY += goalSize + 20;

        // ═══════════════════════════════════════════════════════════════
        // TEAM SELECTION SECTION
        // ═══════════════════════════════════════════════════════════════
        string selectHeader = "SELECT YOUR TEAM";
        int headerSize = 18;
        int headerWidth = Raylib.MeasureText(selectHeader, headerSize);
        Raylib.DrawText(selectHeader, panelX + (panelWidth - headerWidth) / 2, contentY, headerSize, Palette.Yellow);
        contentY += headerSize + 14;

        // Team selection area background
        int teamAreaWidth = panelWidth - 50;
        int teamAreaHeight = 38 * teams.Count + 12;
        int teamAreaX = panelX + 25;
        Raylib.DrawRectangle(teamAreaX, contentY, teamAreaWidth, teamAreaHeight, new Color(8, 12, 18, 220));
        Raylib.DrawRectangleLines(teamAreaX, contentY, teamAreaWidth, teamAreaHeight, new Color(50, 70, 100, 180));

        int teamY = contentY + 8;
        int teamLineHeight = 38;

        for (int i = 0; i < teams.Count; i++)
        {
            OffensiveTeamAttributes team = teams[i];
            bool isSelected = i == selectedTeamIndex;
            Color textColor = isSelected ? Palette.Gold : Palette.White;

            int rowX = teamAreaX + 12;
            int rowWidth = teamAreaWidth - 24;

            if (isSelected)
            {
                // Selection highlight
                Raylib.DrawRectangle(rowX - 4, teamY - 2, rowWidth + 8, 32, new Color(40, 50, 70, 200));
                Raylib.DrawRectangleLines(rowX - 4, teamY - 2, rowWidth + 8, 32, team.PrimaryColor);
                
                // Selection indicator arrow
                Raylib.DrawText("►", rowX - 2, teamY + 4, 18, Palette.Gold);
            }

            // Team color swatches
            int swatchX = rowX + 20;
            Raylib.DrawRectangle(swatchX, teamY + 5, 20, 20, team.PrimaryColor);
            Raylib.DrawRectangle(swatchX + 24, teamY + 5, 20, 20, team.SecondaryColor);
            Raylib.DrawRectangleLines(swatchX, teamY + 5, 20, 20, new Color(80, 90, 100, 180));
            Raylib.DrawRectangleLines(swatchX + 24, teamY + 5, 20, 20, new Color(80, 90, 100, 180));

            // Team info
            string keyHint = $"[{i + 1}]";
            Raylib.DrawText(keyHint, swatchX + 56, teamY + 6, 16, isSelected ? Palette.Cyan : new Color(100, 120, 140, 255));
            
            string teamLine = $"{team.Name} - {team.Description}";
            Raylib.DrawText(teamLine, swatchX + 92, teamY + 6, 18, textColor);

            teamY += teamLineHeight;
        }

        contentY += teamAreaHeight + 24;

        // ═══════════════════════════════════════════════════════════════
        // INSTRUCTIONS FOOTER
        // ═══════════════════════════════════════════════════════════════
        // Decorative divider before footer
        Raylib.DrawLine(panelX + dividerPadding, contentY, panelX + panelWidth - dividerPadding, contentY, new Color(60, 80, 100, 180));
        contentY += 16;

        // Control hints in a row
        string controls1 = "Press 1-3 to select team";
        string controls2 = "Press ENTER to start";
        int ctrlSize = 16;

        int ctrl1Width = Raylib.MeasureText(controls1, ctrlSize);
        int ctrl2Width = Raylib.MeasureText(controls2, ctrlSize);

        Raylib.DrawText(controls1, panelX + (panelWidth - ctrl1Width) / 2, contentY, ctrlSize, new Color(160, 180, 200, 255));
        contentY += ctrlSize + 8;
        Raylib.DrawText(controls2, panelX + (panelWidth - ctrl2Width) / 2, contentY, ctrlSize, Palette.Yellow);
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
