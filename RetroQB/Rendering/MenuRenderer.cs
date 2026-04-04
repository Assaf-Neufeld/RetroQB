using Raylib_cs;
using RetroQB.Core;
using RetroQB.Stats;

namespace RetroQB.Rendering;

/// <summary>
/// Renders the main menu with title, team selection, and game instructions.
/// </summary>
public sealed class MenuRenderer
{
    public void Draw(
        int selectedTeamIndex,
        IReadOnlyList<OffensiveTeamAttributes> teams,
        LeaderboardSummary leaderboardSummary,
        bool showLeaderboard,
        bool showSecretTeamPrompt,
        string secretPasswordInput,
        string secretPasswordMessage)
    {
        DrawBaseMenu(selectedTeamIndex, teams);

        if (showLeaderboard)
        {
            DrawLeaderboardOverlay(leaderboardSummary);
        }

        if (showSecretTeamPrompt)
        {
            DrawSecretTeamPrompt(secretPasswordInput, secretPasswordMessage);
        }
    }

    public void DrawNameEntry(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams, string currentName, string message, LeaderboardSummary leaderboardSummary, bool isPostSeasonSaveMode)
    {
        DrawBaseMenu(selectedTeamIndex, teams);
        OverlayFrame nameFrame = OverlayChromeRenderer.DrawWindowCentered(620, 380, Palette.Cyan, OverlayVariant.Modal, horizontalMargin: 80, verticalMargin: 80);
        int panelX = nameFrame.X;
        int panelY = nameFrame.Y;
        int panelWidth = nameFrame.Width;
        int panelHeight = nameFrame.Height;

        int contentX = panelX + 28;
        int contentY = panelY + 22;

        string title = isPostSeasonSaveMode ? "SAVE SEASON SCORE" : "ENTER YOUR NAME";
        DrawCenteredText(title, panelX, panelWidth, contentY, 28, Palette.Cyan);
        contentY += 42;

        string help = isPostSeasonSaveMode
            ? "Type a name, then press ENTER to save your season score."
            : "Type a name, then press ENTER to start the season.";
        DrawCenteredText(help, panelX, panelWidth, contentY, 16, new Color(180, 200, 220, 255));
        contentY += 34;

        Raylib.DrawRectangle(contentX, contentY, panelWidth - 56, 54, new Color(8, 14, 22, 235));
        Raylib.DrawRectangleLines(contentX, contentY, panelWidth - 56, 54, Palette.Gold);

        string displayName = string.IsNullOrWhiteSpace(currentName) ? "_" : currentName;
        Raylib.DrawText(displayName, contentX + 16, contentY + 14, 26, Palette.White);

        string counter = $"{currentName.Length}/{Input.InputManager.MaxPlayerNameLength}";
        int counterWidth = Raylib.MeasureText(counter, 14);
        Raylib.DrawText(counter, panelX + panelWidth - 40 - counterWidth, contentY + 18, 14, new Color(130, 150, 170, 255));
        contentY += 72;

        if (!string.IsNullOrWhiteSpace(message))
        {
            DrawCenteredText(message, panelX, panelWidth, contentY, 16, Palette.Orange);
            contentY += 30;
        }
        else
        {
            contentY += 10;
        }

        DrawCenteredText("TOP DOMINANCE SCORES", panelX, panelWidth, contentY, 18, Palette.Yellow);
        contentY += 28;

        DrawLeaderboardPreview(panelX + 34, contentY, panelWidth - 68, leaderboardSummary.Entries);

    }

    public void DrawNameConflict(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams, string duplicateName)
    {
        DrawBaseMenu(selectedTeamIndex, teams);
        OverlayFrame conflictFrame = OverlayChromeRenderer.DrawWindowCentered(580, 260, Palette.Orange, OverlayVariant.Modal, horizontalMargin: 80, verticalMargin: 80);
        int panelX = conflictFrame.X;
        int panelY = conflictFrame.Y;
        int panelWidth = conflictFrame.Width;
        int panelHeight = conflictFrame.Height;

        int contentY = panelY + 24;
        DrawCenteredText("NAME ALREADY EXISTS", panelX, panelWidth, contentY, 28, Palette.Orange);
        contentY += 46;

        string line1 = $"\"{duplicateName}\" is already saved.";
        DrawCenteredText(line1, panelX, panelWidth, contentY, 18, Palette.White);
        contentY += 34;

        DrawCenteredText("[1] Use existing player", panelX, panelWidth, contentY, 20, Palette.Lime);
        contentY += 28;
        DrawCenteredText("[2] Go back and change the name", panelX, panelWidth, contentY, 20, Palette.Yellow);
        contentY += 40;

        DrawCenteredText("Press 1 or 2", panelX, panelWidth, contentY, 16, new Color(170, 190, 210, 255));
    }

    private void DrawBaseMenu(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams)
    {
        int screenH = Raylib.GetScreenHeight();
        bool denseTeamLayout = teams.Count > 5;
        int panelWidth = denseTeamLayout ? 860 : 560;
        int panelHeight = Math.Min(denseTeamLayout ? 690 : 600, screenH - 24);

        OverlayFrame frame = OverlayChromeRenderer.DrawWindowCentered(
            panelWidth,
            panelHeight,
            Palette.Gold,
            OverlayVariant.Hero,
            horizontalMargin: denseTeamLayout ? 44 : 56,
            verticalMargin: denseTeamLayout ? 20 : 40,
            drawScrim: true);
        int panelX = frame.X;
        int panelY = frame.Y;
        panelWidth = frame.Width;
        panelHeight = frame.Height;
        bool compactLayout = panelHeight < 585 || denseTeamLayout;

        int contentX = panelX + 30;
        int contentY = panelY + (denseTeamLayout ? 16 : compactLayout ? 18 : 24);

        // TITLE SECTION
        string title = "RETRO QB";
        int titleSize = denseTeamLayout ? 40 : compactLayout ? 46 : 52;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, panelX + (panelWidth - titleWidth) / 2, contentY, titleSize, Palette.Gold);
        contentY += titleSize + (denseTeamLayout ? 2 : compactLayout ? 4 : 8);

        // Subtitle
        string subtitle = "2D Football Simulation";
        int subtitleSize = denseTeamLayout ? 14 : compactLayout ? 15 : 16;
        int subtitleWidth = Raylib.MeasureText(subtitle, subtitleSize);
        Raylib.DrawText(subtitle, panelX + (panelWidth - subtitleWidth) / 2, contentY, subtitleSize, new Color(140, 160, 180, 255));
        contentY += subtitleSize + (denseTeamLayout ? 8 : compactLayout ? 10 : 16);

        // Decorative divider
        int dividerPadding = denseTeamLayout ? 28 : compactLayout ? 32 : 40;
        Raylib.DrawLine(panelX + dividerPadding, contentY, panelX + panelWidth - dividerPadding, contentY, new Color(60, 80, 100, 180));
        Raylib.DrawLine(panelX + dividerPadding, contentY + 1, panelX + panelWidth - dividerPadding, contentY + 1, Palette.Gold);
        Raylib.DrawLine(panelX + dividerPadding, contentY + 2, panelX + panelWidth - dividerPadding, contentY + 2, new Color(60, 80, 100, 180));
        contentY += denseTeamLayout ? 10 : compactLayout ? 14 : 20;

        // GAME RULES SECTION
        string goalText = "* WIN 3 STAGES TO BECOME CHAMPION *";
        int goalSize = denseTeamLayout ? 15 : compactLayout ? 16 : 18;
        int goalWidth = Raylib.MeasureText(goalText, goalSize);
        Raylib.DrawText(goalText, panelX + (panelWidth - goalWidth) / 2, contentY, goalSize, Palette.Lime);
        contentY += goalSize + (denseTeamLayout ? 2 : compactLayout ? 4 : 6);

        // Stage descriptions
        int stageLineFontSize = denseTeamLayout ? 12 : compactLayout ? 13 : 14;
        int stageLineSpacing = denseTeamLayout ? 14 : compactLayout ? 16 : 18;
        string[] stageLines = {
            "1. Regular Season  ->  2. Playoff  ->  3. Super Bowl",
            "Score 21 each round. Defense gets harder!"
        };
        foreach (var line in stageLines)
        {
            int lineWidth = Raylib.MeasureText(line, stageLineFontSize);
            Raylib.DrawText(line, panelX + (panelWidth - lineWidth) / 2, contentY, stageLineFontSize, new Color(180, 200, 220, 255));
            contentY += stageLineSpacing;
        }
        contentY += denseTeamLayout ? 2 : compactLayout ? 4 : 8;

        // TEAM SELECTION SECTION
        string selectHeader = "SELECT YOUR TEAM";
        int headerSize = denseTeamLayout ? 16 : compactLayout ? 17 : 18;
        int headerWidth = Raylib.MeasureText(selectHeader, headerSize);
        Raylib.DrawText(selectHeader, panelX + (panelWidth - headerWidth) / 2, contentY, headerSize, Palette.Yellow);
        contentY += headerSize + (denseTeamLayout ? 8 : compactLayout ? 10 : 14);

        string sortHint = "Ordered by score, strongest to weakest";
        int sortHintSize = denseTeamLayout ? 11 : 12;
        int sortHintWidth = Raylib.MeasureText(sortHint, sortHintSize);
        Raylib.DrawText(sortHint, panelX + (panelWidth - sortHintWidth) / 2, contentY, sortHintSize, new Color(150, 172, 194, 255));
        contentY += sortHintSize + (denseTeamLayout ? 8 : 10);

        // Team selection area background
        int teamAreaWidth = panelWidth - (denseTeamLayout ? 42 : 50);
        int teamLineHeight = denseTeamLayout ? 52 : compactLayout ? 60 : 68;
        int teamAreaHeight = teamLineHeight * teams.Count + 12;
        int teamAreaX = panelX + (denseTeamLayout ? 21 : 25);
        Raylib.DrawRectangle(teamAreaX, contentY, teamAreaWidth, teamAreaHeight, new Color(8, 12, 18, 220));
        Raylib.DrawRectangleLines(teamAreaX, contentY, teamAreaWidth, teamAreaHeight, new Color(50, 70, 100, 180));

        int teamY = contentY + 8;

        for (int i = 0; i < teams.Count; i++)
        {
            OffensiveTeamAttributes team = teams[i];
            bool isSelected = i == selectedTeamIndex;
            Color textColor = isSelected ? Palette.Gold : Palette.White;

            int rowX = teamAreaX + (denseTeamLayout ? 10 : 12);
            int rowWidth = teamAreaWidth - 24;
            int infoWidth = rowWidth;
            int chartX = rowX + 18;
            int chartWidth = rowWidth - 30;
            int scoreWidth = denseTeamLayout ? 68 : compactLayout ? 74 : 82;
            if (denseTeamLayout)
            {
                const int denseChartMinWidth = 260;
                int preferredInfoWidth = (int)(rowWidth * 0.52f);
                int maxInfoWidth = rowWidth - denseChartMinWidth - 12;
                infoWidth = Math.Clamp(preferredInfoWidth, 280, Math.Max(280, maxInfoWidth));
                chartX = rowX + infoWidth;
                chartWidth = rowWidth - infoWidth - 12;
            }

            if (isSelected)
            {
                // Selection highlight
                Raylib.DrawRectangle(rowX - 4, teamY - 2, rowWidth + 8, teamLineHeight - 6, new Color(40, 50, 70, 200));
                Raylib.DrawRectangleLines(rowX - 4, teamY - 2, rowWidth + 8, teamLineHeight - 6, team.PrimaryColor);
                
                // Selection indicator arrow
                Raylib.DrawText(">", rowX - 2, teamY + (denseTeamLayout ? 0 : compactLayout ? 1 : 4), denseTeamLayout ? 14 : compactLayout ? 16 : 18, Palette.Gold);
            }

            // Team color swatches
            int swatchX = rowX + 20;
            int swatchSize = denseTeamLayout ? 15 : compactLayout ? 18 : 20;
            int swatchTop = teamY + (denseTeamLayout ? 4 : 5);
            Raylib.DrawRectangle(swatchX, swatchTop, swatchSize, swatchSize, team.PrimaryColor);
            Raylib.DrawRectangle(swatchX + swatchSize + 4, swatchTop, swatchSize, swatchSize, team.SecondaryColor);
            Raylib.DrawRectangleLines(swatchX, swatchTop, swatchSize, swatchSize, new Color(80, 90, 100, 180));
            Raylib.DrawRectangleLines(swatchX + swatchSize + 4, swatchTop, swatchSize, swatchSize, new Color(80, 90, 100, 180));

            // Team info
            string keyHint = string.Equals(team.Name, OffensiveTeamPresets.GoldenLegion.Name, StringComparison.OrdinalIgnoreCase)
                ? "[0]"
                : $"[{i + 1}]";
            int keyX = swatchX + (denseTeamLayout ? 44 : compactLayout ? 50 : 56);
            int teamInfoY = teamY + (denseTeamLayout ? 2 : compactLayout ? 5 : 6);
            int titleFontSize = denseTeamLayout ? 15 : compactLayout ? 16 : 18;
            int subtitleFontSize = denseTeamLayout ? 11 : 12;
            Color subtitleColor = isSelected ? new Color(168, 190, 210, 255) : new Color(110, 130, 150, 255);
            Raylib.DrawText(keyHint, keyX, teamInfoY, denseTeamLayout ? 13 : compactLayout ? 15 : 16, isSelected ? Palette.Cyan : new Color(100, 120, 140, 255));

            int teamLineX = swatchX + (denseTeamLayout ? 78 : compactLayout ? 82 : 92);
            int teamLineWidth = denseTeamLayout ? infoWidth - (teamLineX - rowX) - 10 : rowWidth - (teamLineX - rowX);
            int fittedTitleFont = denseTeamLayout
                ? GetFittedFontSize(team.Name, titleFontSize, teamLineWidth, 10)
                : titleFontSize;
            int fittedSubtitleFont = denseTeamLayout
                ? GetFittedFontSize(team.Description, subtitleFontSize, teamLineWidth, 9)
                : subtitleFontSize;
            Raylib.DrawText(team.Name, teamLineX, teamInfoY, fittedTitleFont, textColor);
            Raylib.DrawText(team.Description, teamLineX, teamInfoY + (denseTeamLayout ? 16 : 18), fittedSubtitleFont, subtitleColor);

            int statWidth = Math.Max(116, chartWidth - scoreWidth - 8);
            int scoreX = chartX + statWidth + 8;

            DrawTeamStatBars(team, chartX, teamY + (denseTeamLayout ? 8 : compactLayout ? 26 : 32), statWidth, isSelected, denseTeamLayout);
            DrawTeamScore(team, scoreX, teamY + (denseTeamLayout ? 2 : compactLayout ? 6 : 8), scoreWidth, teamLineHeight - (denseTeamLayout ? 6 : 10), isSelected, denseTeamLayout);

            teamY += teamLineHeight;
        }

        contentY += teamAreaHeight + (denseTeamLayout ? 10 : compactLayout ? 16 : 24);

        // INSTRUCTIONS FOOTER
        Raylib.DrawLine(panelX + dividerPadding, contentY, panelX + panelWidth - dividerPadding, contentY, new Color(60, 80, 100, 180));
        contentY += denseTeamLayout ? 8 : compactLayout ? 10 : 16;

        string controls1 = $"Press 1-{Math.Min(teams.Count, OffensiveTeamPresets.StandardTeamCount)} to select team";
        string controls2 = "Press ENTER to start";
        string controls3 = "Press 0 for secret team";
        string controls4 = "Press L for leaderboard";
        int ctrlSize = denseTeamLayout ? 13 : compactLayout ? 14 : 16;
        int ctrlGap = denseTeamLayout ? 4 : compactLayout ? 6 : 8;

        int ctrl1Width = Raylib.MeasureText(controls1, ctrlSize);
        int ctrl2Width = Raylib.MeasureText(controls2, ctrlSize);
        int ctrl3Width = Raylib.MeasureText(controls3, ctrlSize);
        int ctrl4Width = Raylib.MeasureText(controls4, ctrlSize);

        Raylib.DrawText(controls1, panelX + (panelWidth - ctrl1Width) / 2, contentY, ctrlSize, new Color(160, 180, 200, 255));
        contentY += ctrlSize + ctrlGap;
        Raylib.DrawText(controls2, panelX + (panelWidth - ctrl2Width) / 2, contentY, ctrlSize, Palette.Yellow);
        contentY += ctrlSize + ctrlGap;
        Raylib.DrawText(controls3, panelX + (panelWidth - ctrl3Width) / 2, contentY, ctrlSize, Palette.Gold);
        contentY += ctrlSize + ctrlGap;
        Raylib.DrawText(controls4, panelX + (panelWidth - ctrl4Width) / 2, contentY, ctrlSize, Palette.Cyan);
    }

    private void DrawSecretTeamPrompt(string passwordInput, string message)
    {
        OverlayFrame secretFrame = OverlayChromeRenderer.DrawWindowCentered(560, 280, Palette.Gold, OverlayVariant.Modal, horizontalMargin: 80, verticalMargin: 80);
        int panelX = secretFrame.X;
        int panelY = secretFrame.Y;
        int panelWidth = secretFrame.Width;
        int panelHeight = secretFrame.Height;

        int contentX = panelX + 28;
        int contentY = panelY + 24;

        DrawCenteredText("SECRET TEAM", panelX, panelWidth, contentY, 28, Palette.Gold);
        contentY += 42;

        DrawCenteredText("Enter the 4-digit password to unlock Golden Legion.", panelX, panelWidth, contentY, 16, new Color(180, 200, 220, 255));
        contentY += 34;

        Raylib.DrawRectangle(contentX, contentY, panelWidth - 56, 54, new Color(8, 14, 22, 235));
        Raylib.DrawRectangleLines(contentX, contentY, panelWidth - 56, 54, Palette.Red);

        string displayPassword = string.IsNullOrEmpty(passwordInput)
            ? "_ _ _ _"
            : string.Join(" ", new string('*', passwordInput.Length).ToCharArray());
        Raylib.DrawText(displayPassword, contentX + 18, contentY + 14, 26, Palette.White);
        contentY += 72;

        if (!string.IsNullOrWhiteSpace(message))
        {
            DrawCenteredText(message, panelX, panelWidth, contentY, 16, Palette.Orange);
            contentY += 28;
        }

        DrawCenteredText("ENTER = unlock    ESC = cancel", panelX, panelWidth, contentY, 16, Palette.Cyan);
    }

    private void DrawLeaderboardOverlay(LeaderboardSummary leaderboardSummary)
    {
        OverlayFrame lbFrame = OverlayChromeRenderer.DrawWindowCentered(760, 620, Palette.Gold, OverlayVariant.Modal, horizontalMargin: 80, verticalMargin: 80);
        int panelX = lbFrame.X;
        int panelY = lbFrame.Y;
        int panelWidth = lbFrame.Width;
        int panelHeight = lbFrame.Height;

        int contentY = panelY + 22;
        DrawCenteredText("DOMINANCE LEADERBOARD", panelX, panelWidth, contentY, 30, Palette.Gold);
        contentY += 42;

        string subtitle = leaderboardSummary.Entries.Count == 0
            ? "No saved seasons yet"
            : "Saved dominance scores";
        DrawCenteredText(subtitle, panelX, panelWidth, contentY, 16, new Color(180, 200, 220, 255));
        contentY += 30;

        int boardX = panelX + 36;
        int boardY = contentY;
        int boardWidth = panelWidth - 72;
        int boardHeight = panelHeight - 132;

        Raylib.DrawRectangle(boardX, boardY, boardWidth, boardHeight, new Color(8, 12, 18, 220));
        Raylib.DrawRectangleLines(boardX, boardY, boardWidth, boardHeight, new Color(55, 75, 100, 180));

        if (leaderboardSummary.Entries.Count == 0)
        {
            DrawCenteredText("Finish a season to create the first record", panelX, panelWidth, boardY + boardHeight / 2 - 10, 18, new Color(150, 170, 190, 255));
            DrawCenteredText("Press L or ESC to close", panelX, panelWidth, panelY + panelHeight - 34, 15, Palette.Cyan);
            return;
        }

        const int overlayRowHeight = 46;
        int rowY = boardY + 16;
        int maxRows = Math.Min(10, leaderboardSummary.Entries.Count);
        for (int i = 0; i < maxRows; i++)
        {
            LeaderboardEntry entry = leaderboardSummary.Entries[i];
            bool highlight = entry.IsCurrentPlayer;

            if (highlight)
            {
                Raylib.DrawRectangle(boardX + 10, rowY - 4, boardWidth - 20, overlayRowHeight - 2, new Color(40, 60, 82, 215));
                Raylib.DrawRectangleLines(boardX + 10, rowY - 4, boardWidth - 20, overlayRowHeight - 2, Palette.Cyan);
            }

            Color rankColor = entry.Rank switch
            {
                1 => Palette.Gold,
                2 => new Color(205, 215, 225, 255),
                3 => new Color(214, 140, 72, 255),
                _ => Palette.White
            };

            string line1 = TruncateTextToWidth(
                $"#{entry.Rank}  {entry.Name} - {entry.TeamName}",
                16,
                boardWidth - 170);
            Raylib.DrawText(line1, boardX + 18, rowY, 16, rankColor);

            DrawFittedText(entry.ScoreHistory, boardX + 18, rowY + 17, 10, boardWidth - 170, new Color(175, 195, 215, 255), 8);
            DrawFittedText(entry.ScoreDetails, boardX + 18, rowY + 29, 9, boardWidth - 170, new Color(145, 170, 195, 255), 7);

            if (entry.HasTrophy)
            {
                DrawTrophyIcon(boardX + boardWidth - 134, rowY + 2, 0.62f, Palette.Gold);
            }

            string score = entry.Score.ToString("F1");
            int scoreWidth = Raylib.MeasureText(score, 20);
            Raylib.DrawText(score, boardX + boardWidth - 18 - scoreWidth, rowY + 10, 20, highlight ? Palette.Cyan : Palette.White);
            rowY += overlayRowHeight;
        }

        DrawCenteredText("Press L or ESC to close", panelX, panelWidth, panelY + panelHeight - 34, 15, Palette.Cyan);
    }

    private static void DrawTrophyIcon(int x, int y, float scale, Color color)
    {
        int cupWidth = Math.Max(8, (int)(20 * scale));
        int cupHeight = Math.Max(8, (int)(14 * scale));
        int stemWidth = Math.Max(4, (int)(6 * scale));
        int stemHeight = Math.Max(4, (int)(8 * scale));
        int baseWidth = Math.Max(10, (int)(18 * scale));
        int baseHeight = Math.Max(4, (int)(5 * scale));

        Raylib.DrawRectangle(x, y, cupWidth, cupHeight, color);
        Raylib.DrawRectangle(x + (cupWidth - stemWidth) / 2, y + cupHeight, stemWidth, stemHeight, color);
        Raylib.DrawRectangle(x + (cupWidth - baseWidth) / 2, y + cupHeight + stemHeight, baseWidth, baseHeight, color);
        Raylib.DrawRectangleLines(x, y, cupWidth, cupHeight, new Color(60, 42, 10, 255));
        Raylib.DrawRectangleLines(x + (cupWidth - baseWidth) / 2, y + cupHeight + stemHeight, baseWidth, baseHeight, new Color(60, 42, 10, 255));
        Raylib.DrawCircleLines(x - Math.Max(2, (int)(2 * scale)), y + Math.Max(4, (int)(6 * scale)), Math.Max(4, (int)(5 * scale)), color);
        Raylib.DrawCircleLines(x + cupWidth + Math.Max(2, (int)(2 * scale)), y + Math.Max(4, (int)(6 * scale)), Math.Max(4, (int)(5 * scale)), color);
    }

    private static void DrawCenteredText(string text, int panelX, int panelWidth, int y, int fontSize, Color color)
    {
        int fittedFontSize = GetFittedFontSize(text, fontSize, panelWidth - 24, 10);
        int textWidth = Raylib.MeasureText(text, fittedFontSize);
        Raylib.DrawText(text, panelX + (panelWidth - textWidth) / 2, y, fittedFontSize, color);
    }

    private static int GetFittedFontSize(string text, int preferredFontSize, int maxWidth, int minFontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return preferredFontSize;
        }

        int fontSize = preferredFontSize;
        while (fontSize > minFontSize && Raylib.MeasureText(text, fontSize) > maxWidth)
        {
            fontSize--;
        }

        return fontSize;
    }

    private static void DrawLeaderboardPreview(int x, int y, int width, IReadOnlyList<LeaderboardEntry> entries)
    {
        const int previewHeight = 142;
        const int previewRowHeight = 26;

        Raylib.DrawRectangle(x, y, width, previewHeight, new Color(8, 12, 18, 220));
        Raylib.DrawRectangleLines(x, y, width, previewHeight, new Color(55, 75, 100, 180));

        if (entries.Count == 0)
        {
            string empty = "No saved players yet";
            int emptyWidth = Raylib.MeasureText(empty, 18);
            Raylib.DrawText(empty, x + (width - emptyWidth) / 2, y + 48, 18, new Color(150, 170, 190, 255));
            return;
        }

        int drawY = y + 10;
        int maxRows = Math.Min(5, entries.Count);
        for (int i = 0; i < maxRows; i++)
        {
            LeaderboardEntry entry = entries[i];
            Color rankColor = entry.Rank switch
            {
                1 => Palette.Gold,
                2 => new Color(205, 215, 225, 255),
                3 => new Color(214, 140, 72, 255),
                _ => Palette.White
            };

            string left = TruncateTextToWidth($"#{entry.Rank}  {entry.Name} - {entry.TeamName}", 14, width - 106);
            string right = entry.Score.ToString("F1");
            Raylib.DrawText(left, x + 14, drawY + 2, 14, rankColor);
            DrawFittedText(entry.ScoreDetails, x + 14, drawY + 16, 9, width - 106, new Color(165, 185, 205, 255), 7);
            int rightWidth = Raylib.MeasureText(right, 18);
            Raylib.DrawText(right, x + width - 14 - rightWidth, drawY + 1, 18, Palette.White);
            drawY += previewRowHeight;
        }
    }

    private static void DrawFittedText(string text, int x, int y, int preferredFontSize, int maxWidth, Color color, int minFontSize)
    {
        int fittedFontSize = GetFittedFontSize(text, preferredFontSize, maxWidth, minFontSize);
        Raylib.DrawText(text, x, y, fittedFontSize, color);
    }

    private static string TruncateTextToWidth(string text, int fontSize, int maxWidth)
    {
        if (Raylib.MeasureText(text, fontSize) <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        for (int length = text.Length - 1; length > 1; length--)
        {
            string candidate = text[..length] + ellipsis;
            if (Raylib.MeasureText(candidate, fontSize) <= maxWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
    }

    /// <summary>
    /// Draws a compact stat grid showing four grouped offensive team categories.
    /// </summary>
    private static void DrawTeamStatBars(OffensiveTeamAttributes team, int x, int y, int availableWidth, bool highlighted, bool denseLayout)
    {
        var (labels, values) = GetTeamSkillMetrics(team);
        int metricCount = labels.Length;
        int groupSpacing = denseLayout ? 6 : 8;
        int cellWidth = (availableWidth - groupSpacing * (metricCount - 1)) / metricCount;
        int barHeight = denseLayout ? 6 : 8;
        int labelFontSize = denseLayout ? 9 : 12;
        int barY = y + (denseLayout ? 12 : 14);

        Color labelColor = highlighted ? new Color(180, 200, 220, 255) : new Color(120, 140, 160, 255);
        Color barBg = new Color(30, 35, 45, 200);
        Color barBorder = new Color(60, 70, 85, 180);

        for (int j = 0; j < labels.Length; j++)
        {
            int cellX = x + j * (cellWidth + groupSpacing);
            int labelWidth = Raylib.MeasureText(labels[j], labelFontSize);
            int labelX = cellX + (cellWidth - labelWidth) / 2;
            int barWidth = Math.Max(20, cellWidth - 4);
            int barX = cellX + (cellWidth - barWidth) / 2;

            Raylib.DrawText(labels[j], labelX, y, labelFontSize, labelColor);
            Raylib.DrawRectangle(barX, barY, barWidth, barHeight, barBg);

            int fillWidth = (int)(barWidth * Math.Clamp(values[j], 0f, 1f));
            if (fillWidth > 0)
            {
                Color barColor = GetRatingBarColor(values[j]);
                Raylib.DrawRectangle(barX, barY, fillWidth, barHeight, barColor);
            }

            Raylib.DrawRectangleLines(barX, barY, barWidth, barHeight, barBorder);
        }
    }

    private static void DrawTeamScore(OffensiveTeamAttributes team, int x, int y, int width, int height, bool highlighted, bool denseLayout)
    {
        Color borderColor = highlighted ? Palette.Gold : new Color(74, 92, 112, 220);
        Color backgroundColor = highlighted ? new Color(30, 44, 62, 235) : new Color(14, 20, 28, 225);
        Color scoreColor = GetScoreColor(team.TeamScore);
        int labelSize = denseLayout ? 9 : 10;
        int scoreSize = denseLayout ? 22 : 26;

        Raylib.DrawRectangle(x, y, width, height, backgroundColor);
        Raylib.DrawRectangleLines(x, y, width, height, borderColor);

        string label = "OVR";
        int labelWidth = Raylib.MeasureText(label, labelSize);
        Raylib.DrawText(label, x + (width - labelWidth) / 2, y + 4, labelSize, new Color(180, 196, 214, 255));

        string scoreText = team.TeamScore.ToString();
        int scoreWidth = Raylib.MeasureText(scoreText, scoreSize);
        int scoreY = y + (denseLayout ? 16 : 18);
        Raylib.DrawText(scoreText, x + (width - scoreWidth) / 2, scoreY, scoreSize, scoreColor);
    }

    private static (string[] Labels, float[] Values) GetTeamSkillMetrics(OffensiveTeamAttributes team)
    {
        OffensiveTeamSkills skills = team.Skills;
        float qb = (Math.Clamp(skills.QbThrowPower, 0f, 1f) + Math.Clamp(skills.QbThrowAccuracy, 0f, 1f)) / 2f;
        float wr = (Math.Clamp(skills.WrSpeed, 0f, 1f) + Math.Clamp(skills.WrSkill, 0f, 1f)) / 2f;
        float rb = (Math.Clamp(skills.RbPower, 0f, 1f) + Math.Clamp(skills.RbSpeed, 0f, 1f)) / 2f;
        float ol = Math.Clamp(skills.OlStrength, 0f, 1f);
        return
        (
            ["QB", "WR", "RB", "OL"],
            [
                qb,
                wr,
                rb,
                ol
            ]
        );
    }

    /// <summary>
    /// Returns a color interpolated from red (low) through yellow (mid) to green (high).
    /// </summary>
    private static Color GetRatingBarColor(float rating)
    {
        rating = Math.Clamp(rating, 0f, 1f);
        if (rating < 0.5f)
        {
            // Red → Yellow
            float t = rating / 0.5f;
            return new Color((byte)200, (byte)(60 + (int)(140 * t)), (byte)40, (byte)255);
        }
        else
        {
            // Yellow → Green
            float t = (rating - 0.5f) / 0.5f;
            return new Color((byte)(200 - (int)(150 * t)), (byte)200, (byte)(40 + (int)(40 * t)), (byte)255);
        }
    }

    private static Color GetScoreColor(int score)
    {
        if (score >= 90)
        {
            return Palette.Gold;
        }

        if (score >= 80)
        {
            return Palette.Lime;
        }

        if (score >= 70)
        {
            return Palette.Yellow;
        }

        return Palette.Orange;
    }

    public void DrawPause()
    {
        OverlayFrame frame = OverlayChromeRenderer.DrawWindowCentered(
            preferredWidth: 320,
            preferredHeight: 110,
            accent: Palette.Yellow,
            variant: OverlayVariant.Toast,
            horizontalMargin: 120,
            verticalMargin: 120,
            drawScrim: true);

        DrawCenteredText("PAUSED", frame.X, frame.Width, frame.Y + 38, 30, Palette.Yellow);
    }
}
