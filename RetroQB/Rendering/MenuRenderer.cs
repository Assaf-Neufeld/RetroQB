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
        string secretPasswordMessage,
        bool secretTeamUnlocked)
    {
        DrawBaseMenu(selectedTeamIndex, teams, secretTeamUnlocked);

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
        DrawBaseMenu(selectedTeamIndex, teams, teams.Count > OffensiveTeamPresets.StandardTeamCount);
        DrawOverlayShell(620, 380, out int panelX, out int panelY, out int panelWidth, out int panelHeight, Palette.Cyan);

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
        DrawBaseMenu(selectedTeamIndex, teams, teams.Count > OffensiveTeamPresets.StandardTeamCount);
        DrawOverlayShell(580, 260, out int panelX, out int panelY, out int panelWidth, out int panelHeight, Palette.Orange);

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

    private void DrawBaseMenu(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams, bool secretTeamUnlocked)
    {
        var field = Constants.FieldRect;
        int centerX = (int)(field.X + field.Width / 2f);
        int centerY = (int)(field.Y + field.Height / 2f);
        int screenH = Raylib.GetScreenHeight();

        // Calculate panel dimensions for unified window
        int panelWidth = 560;
        int panelHeight = Math.Min(600, screenH - 40);
        int panelX = centerX - panelWidth / 2;
        int panelY = centerY - panelHeight / 2;
        bool compactLayout = panelHeight < 585;

        // Outer glow/shadow effect
        Raylib.DrawRectangle(panelX - 8, panelY - 8, panelWidth + 16, panelHeight + 16, new Color(0, 0, 0, 120));
        
        // Main panel background with layered borders
        Raylib.DrawRectangle(panelX - 4, panelY - 4, panelWidth + 8, panelHeight + 8, Palette.Gold);
        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(12, 16, 24, 250));
        Raylib.DrawRectangle(panelX + 3, panelY + 3, panelWidth - 6, panelHeight - 6, new Color(18, 22, 32, 250));

        int contentX = panelX + 30;
        int contentY = panelY + (compactLayout ? 18 : 24);

        // TITLE SECTION
        string title = "RETRO QB";
        int titleSize = compactLayout ? 46 : 52;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, panelX + (panelWidth - titleWidth) / 2, contentY, titleSize, Palette.Gold);
        contentY += titleSize + (compactLayout ? 4 : 8);

        // Subtitle
        string subtitle = "2D Football Simulation";
        int subtitleSize = compactLayout ? 15 : 16;
        int subtitleWidth = Raylib.MeasureText(subtitle, subtitleSize);
        Raylib.DrawText(subtitle, panelX + (panelWidth - subtitleWidth) / 2, contentY, subtitleSize, new Color(140, 160, 180, 255));
        contentY += subtitleSize + (compactLayout ? 10 : 16);

        // Decorative divider
        int dividerPadding = compactLayout ? 32 : 40;
        Raylib.DrawLine(panelX + dividerPadding, contentY, panelX + panelWidth - dividerPadding, contentY, new Color(60, 80, 100, 180));
        Raylib.DrawLine(panelX + dividerPadding, contentY + 1, panelX + panelWidth - dividerPadding, contentY + 1, Palette.Gold);
        Raylib.DrawLine(panelX + dividerPadding, contentY + 2, panelX + panelWidth - dividerPadding, contentY + 2, new Color(60, 80, 100, 180));
        contentY += compactLayout ? 14 : 20;

        // GAME RULES SECTION
        string goalText = "* WIN 3 STAGES TO BECOME CHAMPION *";
        int goalSize = compactLayout ? 16 : 18;
        int goalWidth = Raylib.MeasureText(goalText, goalSize);
        Raylib.DrawText(goalText, panelX + (panelWidth - goalWidth) / 2, contentY, goalSize, Palette.Lime);
        contentY += goalSize + (compactLayout ? 4 : 6);

        // Stage descriptions
        int stageLineFontSize = compactLayout ? 13 : 14;
        int stageLineSpacing = compactLayout ? 16 : 18;
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
        contentY += compactLayout ? 4 : 8;

        // TEAM SELECTION SECTION
        string selectHeader = "SELECT YOUR TEAM";
        int headerSize = compactLayout ? 17 : 18;
        int headerWidth = Raylib.MeasureText(selectHeader, headerSize);
        Raylib.DrawText(selectHeader, panelX + (panelWidth - headerWidth) / 2, contentY, headerSize, Palette.Yellow);
        contentY += headerSize + (compactLayout ? 10 : 14);

        // Team selection area background
        int teamAreaWidth = panelWidth - 50;
        int teamLineHeight = compactLayout ? 60 : 68;
        int teamAreaHeight = teamLineHeight * teams.Count + 12;
        int teamAreaX = panelX + 25;
        Raylib.DrawRectangle(teamAreaX, contentY, teamAreaWidth, teamAreaHeight, new Color(8, 12, 18, 220));
        Raylib.DrawRectangleLines(teamAreaX, contentY, teamAreaWidth, teamAreaHeight, new Color(50, 70, 100, 180));

        int teamY = contentY + 8;

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
                Raylib.DrawRectangle(rowX - 4, teamY - 2, rowWidth + 8, teamLineHeight - 6, new Color(40, 50, 70, 200));
                Raylib.DrawRectangleLines(rowX - 4, teamY - 2, rowWidth + 8, teamLineHeight - 6, team.PrimaryColor);
                
                // Selection indicator arrow
                Raylib.DrawText(">", rowX - 2, teamY + (compactLayout ? 1 : 4), compactLayout ? 16 : 18, Palette.Gold);
            }

            // Team color swatches
            int swatchX = rowX + 20;
            int swatchSize = compactLayout ? 18 : 20;
            int swatchTop = teamY + 5;
            Raylib.DrawRectangle(swatchX, swatchTop, swatchSize, swatchSize, team.PrimaryColor);
            Raylib.DrawRectangle(swatchX + swatchSize + 4, swatchTop, swatchSize, swatchSize, team.SecondaryColor);
            Raylib.DrawRectangleLines(swatchX, swatchTop, swatchSize, swatchSize, new Color(80, 90, 100, 180));
            Raylib.DrawRectangleLines(swatchX + swatchSize + 4, swatchTop, swatchSize, swatchSize, new Color(80, 90, 100, 180));

            // Team info
            string keyHint = $"[{i + 1}]";
            int keyX = swatchX + (compactLayout ? 50 : 56);
            int teamInfoY = teamY + (compactLayout ? 5 : 6);
            Raylib.DrawText(keyHint, keyX, teamInfoY, compactLayout ? 15 : 16, isSelected ? Palette.Cyan : new Color(100, 120, 140, 255));
            
            string teamLine = $"{team.Name} - {team.Description}";
            Raylib.DrawText(teamLine, swatchX + (compactLayout ? 82 : 92), teamInfoY, compactLayout ? 16 : 18, textColor);

            // Stat bar chart
            DrawTeamStatBars(team, rowX + 18, teamY + (compactLayout ? 26 : 32), rowWidth - 30, isSelected);

            teamY += teamLineHeight;
        }

        contentY += teamAreaHeight + (compactLayout ? 16 : 24);

        // INSTRUCTIONS FOOTER
        Raylib.DrawLine(panelX + dividerPadding, contentY, panelX + panelWidth - dividerPadding, contentY, new Color(60, 80, 100, 180));
        contentY += compactLayout ? 10 : 16;

        string controls1 = secretTeamUnlocked
            ? "Press 1-3 or 0 to select team"
            : "Press 1-3 to select team";
        string controls2 = "Press ENTER to start";
        string controls3 = secretTeamUnlocked ? "Press 0 to pick Golden Legion" : "Press 0 for secret team";
        string controls4 = "Press L for leaderboard";
        int ctrlSize = compactLayout ? 14 : 16;
        int ctrlGap = compactLayout ? 6 : 8;

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
        DrawOverlayShell(560, 280, out int panelX, out int panelY, out int panelWidth, out int panelHeight, Palette.Gold);

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
        DrawOverlayShell(760, 620, out int panelX, out int panelY, out int panelWidth, out int panelHeight, Palette.Gold);

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

    private static void DrawOverlayShell(int width, int height, out int panelX, out int panelY, out int panelWidth, out int panelHeight, Color accent)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        panelWidth = Math.Min(width, screenW - 80);
        panelHeight = Math.Min(height, screenH - 80);
        panelX = (screenW - panelWidth) / 2;
        panelY = (screenH - panelHeight) / 2;

        Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(0, 0, 0, 120));
        Raylib.DrawRectangle(panelX - 6, panelY - 6, panelWidth + 12, panelHeight + 12, accent);
        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(10, 14, 22, 250));
        Raylib.DrawRectangle(panelX + 4, panelY + 4, panelWidth - 8, panelHeight - 8, new Color(18, 24, 36, 250));
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
    /// Draws a horizontal row of four stat bars (WR, RB, QB, OL) for a team.
    /// </summary>
    private static void DrawTeamStatBars(OffensiveTeamAttributes team, int x, int y, int availableWidth, bool highlighted)
    {
        var (wrRating, rbRating, qbRating, olRating) = ComputeTeamRatings(team);
        ReadOnlySpan<string> labels = ["WR", "RB", "QB", "OL"];
        ReadOnlySpan<float> values = [wrRating, rbRating, qbRating, olRating];

        int labelWidth = 24;
        int barHeight = 8;
        int groupSpacing = 8;
        int groupWidth = (availableWidth - groupSpacing * 3) / 4;
        int barMaxWidth = groupWidth - labelWidth - 4;

        Color labelColor = highlighted ? new Color(180, 200, 220, 255) : new Color(120, 140, 160, 255);
        Color barBg = new Color(30, 35, 45, 200);
        Color barBorder = new Color(60, 70, 85, 180);

        for (int j = 0; j < 4; j++)
        {
            int bx = x + j * (groupWidth + groupSpacing);

            // Label
            Raylib.DrawText(labels[j], bx, y, 12, labelColor);

            // Bar background
            int barX = bx + labelWidth;
            Raylib.DrawRectangle(barX, y + 1, barMaxWidth, barHeight, barBg);

            // Bar fill
            int fillWidth = (int)(barMaxWidth * Math.Clamp(values[j], 0f, 1f));
            if (fillWidth > 0)
            {
                Color barColor = GetRatingBarColor(values[j]);
                Raylib.DrawRectangle(barX, y + 1, fillWidth, barHeight, barColor);
            }

            // Bar outline
            Raylib.DrawRectangleLines(barX, y + 1, barMaxWidth, barHeight, barBorder);
        }
    }

    /// <summary>
    /// Computes 0-1 ratings for WR, RB, QB, and OL from team roster data.
    /// </summary>
    private static (float WR, float RB, float QB, float OL) ComputeTeamRatings(OffensiveTeamAttributes team)
    {
        var roster = team.Roster;

        // WR: average speed multiplier + average catching ability
        float wrSpeedSum = 0f, wrCatchSum = 0f;
        int wrCount = 0;
        foreach (var wr in roster.WideReceivers.Values)
        {
            wrSpeedSum += wr.Speed / Constants.WrSpeed;
            wrCatchSum += wr.CatchingAbility;
            wrCount++;
        }
        float wrSpeedRating = wrCount > 0 ? Math.Clamp((wrSpeedSum / wrCount - 0.7f) / 0.7f, 0f, 1f) : 0f;
        float wrCatchRating = wrCount > 0 ? Math.Clamp((wrCatchSum / wrCount - 0.4f) / 0.6f, 0f, 1f) : 0f;
        float wrRating = (wrSpeedRating + wrCatchRating) / 2f;

        // RB: speed multiplier + tackle break chance
        var rb = roster.RunningBacks.Values.FirstOrDefault() ?? RbProfile.Default;
        float rbSpeedRating = Math.Clamp((rb.Speed / Constants.RbSpeed - 0.7f) / 0.7f, 0f, 1f);
        float rbTackleRating = Math.Clamp((rb.TackleBreakChance - 0.1f) / 0.4f, 0f, 1f);
        float rbRating = (rbSpeedRating + rbTackleRating) / 2f;

        // QB: accuracy (lower = better, inverted) + arm strength
        var qb = roster.Quarterback;
        float qbAccuracyRating = Math.Clamp((1.0f - qb.Accuracy) / 0.5f, 0f, 1f);
        float qbArmRating = Math.Clamp((qb.ArmStrength - 0.7f) / 0.6f, 0f, 1f);
        float qbRating = (qbAccuracyRating + qbArmRating) / 2f;

        // OL: blocking strength
        float olRating = Math.Clamp((roster.OffensiveLine.BlockingStrength - 0.6f) / 0.8f, 0f, 1f);

        return (wrRating, rbRating, qbRating, olRating);
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

    public void DrawPause()
    {
        int screenW = Raylib.GetScreenWidth();
        Raylib.DrawText("PAUSED", screenW / 2 - 60, 40, 24, Palette.Yellow);
    }
}
