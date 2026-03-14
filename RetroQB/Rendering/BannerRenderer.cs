using System;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;
using RetroQB.Stats;

namespace RetroQB.Rendering;

/// <summary>
/// Renders victory banners and touchdown popups.
/// </summary>
public sealed class BannerRenderer
{
    public void DrawStageCompleteBanner(int finalScore, int awayScore, SeasonStage completedStage, GameStatsSnapshot stats, SeasonSummary seasonSummary, LeaderboardSummary leaderboardSummary)
    {
        var nextStage = completedStage.GetNextStage();
        string subtitle = nextStage.HasValue
            ? $"NEXT: {nextStage.Value.GetDisplayName()}"
            : "SEASON CONTINUES";

        DrawSeasonOutcomeBanner(
            $"{completedStage.GetDisplayName()} WON!",
            subtitle,
            $"PRESS ENTER FOR {(nextStage?.GetDisplayName() ?? "NEXT GAME").ToUpperInvariant()}",
            finalScore,
            awayScore,
            seasonSummary,
            leaderboardSummary,
            completedStage switch
            {
                SeasonStage.RegularSeason => Palette.Lime,
                SeasonStage.Playoff => Palette.Yellow,
                _ => Palette.Gold
            },
            stats,
            false,
            false,
                false,
            false);
    }

    public void DrawChampionBanner(int finalScore, int awayScore, GameStatsSnapshot stats, SeasonSummary seasonSummary, LeaderboardSummary leaderboardSummary)
    {
        DrawSeasonOutcomeBanner(
            "CHAMPION!",
            "SUPER BOWL WINNER",
            "PRESS ENTER TO CHOOSE TEAM",
            finalScore,
            awayScore,
            seasonSummary,
            leaderboardSummary,
            Palette.Gold,
            stats,
            true,
            true,
                true,
            true);
    }

    public void DrawEliminationBanner(int finalScore, int awayScore, SeasonStage stage, GameStatsSnapshot stats, SeasonSummary seasonSummary, LeaderboardSummary leaderboardSummary)
    {
        DrawSeasonOutcomeBanner(
            "ELIMINATED",
            $"FELL IN THE {stage.GetDisplayName().ToUpperInvariant()}",
            "PRESS ENTER TO CHOOSE TEAM",
            finalScore,
            awayScore,
            seasonSummary,
            leaderboardSummary,
            Palette.Red,
            stats,
            true,
                stage == SeasonStage.SuperBowl,
                stage == SeasonStage.SuperBowl,
            stage == SeasonStage.SuperBowl);
    }

    private void DrawSeasonOutcomeBanner(
        string title,
        string subtitle,
        string prompt,
        int finalScore,
        int awayScore,
        SeasonSummary seasonSummary,
        LeaderboardSummary leaderboardSummary,
        Color accentColor,
        GameStatsSnapshot stats,
        bool showQbRating,
        bool showRankingDetails,
        bool showFinalRank,
        bool showPodium)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int targetBannerWidth = showRankingDetails ? 860 : 640;
        int targetBannerHeight = showRankingDetails ? 780 : 500;
        int bannerWidth = Math.Min(targetBannerWidth, screenW - 48);
        int bannerHeight = Math.Min(targetBannerHeight, screenH - 24);
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        Raylib.DrawRectangle(x - 5, y - 5, bannerWidth + 10, bannerHeight + 10, accentColor);
        Raylib.DrawRectangle(x, y, bannerWidth, bannerHeight, new Color(10, 14, 22, 248));
        Raylib.DrawRectangle(x + 4, y + 4, bannerWidth - 8, bannerHeight - 8, new Color(18, 24, 36, 248));

        int contentY = y + 16;
        int titleFontSize = showRankingDetails ? 44 : 38;
        int subtitleFontSize = showRankingDetails ? 20 : 17;
        DrawCenteredText(title, x, bannerWidth, contentY, titleFontSize, accentColor, 18, 20);
        contentY += titleFontSize + 4;
        DrawCenteredText(subtitle, x, bannerWidth, contentY, subtitleFontSize, Palette.Cyan, 18, 12);
        contentY += subtitleFontSize + 10;

        string scoreLine = $"FINAL: {finalScore} - {awayScore}";
        DrawCenteredText(scoreLine, x, bannerWidth, contentY, showRankingDetails ? 22 : 19, Palette.White);
        contentY += showRankingDetails ? 36 : 30;

        if (showQbRating)
        {
            float dominanceScore = seasonSummary.ComputeDominanceScore();
            DrawCenteredText("DOMINANCE SCORE", x, bannerWidth, contentY, showRankingDetails ? 18 : 15, Palette.Blue);
            contentY += 20;
            DrawCenteredText($"{dominanceScore:F1}", x, bannerWidth, contentY, showRankingDetails ? 42 : 34, GetRatingColor(dominanceScore));
            contentY += showRankingDetails ? 50 : 40;
        }

        int currentPlayerRank = leaderboardSummary.CurrentPlayerRank ?? 0;

        if (showRankingDetails && showFinalRank && leaderboardSummary.HasPlayerRank)
        {
            string finalRankText = leaderboardSummary.IsFirstPlace
                ? $"DOMINANCE RANK: #{currentPlayerRank}  TROPHY HOLDER"
                : leaderboardSummary.IsOnPodium
                    ? $"DOMINANCE RANK: #{currentPlayerRank}  PODIUM"
                    : $"DOMINANCE RANK: #{currentPlayerRank}";
            DrawCenteredText(finalRankText, x, bannerWidth, contentY, 20, leaderboardSummary.IsOnPodium ? Palette.Gold : Palette.Orange);
            contentY += 28;
        }

        string passLine = BuildPassingLine(seasonSummary.CumulativeQbStats);
        DrawCenteredText(passLine, x, bannerWidth, contentY, showRankingDetails ? 15 : 14, new Color(160, 180, 200, 255));
        contentY += showRankingDetails ? 28 : 24;

        int sectionTop = contentY;
        int rankingPanelX = x + bannerWidth - 24 - 460;
        int rankingPanelWidth = 460;
        if (showRankingDetails)
        {
            int leftX = x + 24;
            int leftWidth = 320;
            int rightX = rankingPanelX;
            int rightWidth = rankingPanelWidth;

            DrawGameScoresPanel(leftX, sectionTop, leftWidth, seasonSummary, accentColor, stats);
            DrawLeaderboardPanel(rightX, sectionTop, rightWidth, leaderboardSummary, accentColor);
        }
        else
        {
            DrawGameScoresPanel(x + 24, sectionTop, bannerWidth - 48, seasonSummary, accentColor, stats);
        }

        if (showRankingDetails && showPodium)
        {
            int podiumPanelHeight = 248;
            int bottomY = y + bannerHeight - 322;
            DrawPodiumPanel(rankingPanelX, bottomY, rankingPanelWidth, podiumPanelHeight, leaderboardSummary, accentColor);
        }

        DrawActionButtons(x, y + bannerHeight - 62, bannerWidth, prompt, "Z TO RESTART");
    }

    private static string BuildPassingLine(QbStatsSnapshot qb)
    {
        string line = $"{qb.Completions}/{qb.Attempts} CMP  {qb.PassYards} YDS  {qb.PassTds} TD  {qb.Interceptions} INT";
        if (qb.Sacks > 0)
        {
            line += $"  {qb.Sacks} SACK";
        }

        return line;
    }

    private void DrawGameScoresPanel(int x, int y, int width, SeasonSummary summary, Color accentColor, GameStatsSnapshot stats)
    {
        int height = 210;
        Raylib.DrawRectangle(x, y, width, height, new Color(8, 12, 18, 220));
        Raylib.DrawRectangleLines(x, y, width, height, accentColor);

        DrawCenteredText("RUN SUMMARY", x, width, y + 12, 18, Palette.White);

        int contentY = y + 42;
        int stagesWon = summary.Games.Count(game => game.Won);
        string progress = $"STAGES WON: {stagesWon}/3";
        DrawCenteredText(progress, x, width, contentY, 16, Palette.Lime);
        contentY += 28;

        foreach (var game in summary.Games.Take(3))
        {
            string line = $"{game.Stage.GetShortLabel()}: {game.PlayerScore} - {game.AwayScore}";
            Color lineColor = game.Won ? Palette.White : new Color(255, 170, 170, 255);
            Raylib.DrawText(line, x + 18, contentY, 16, lineColor);
            string result = game.Won ? "W" : "L";
            int resultWidth = Raylib.MeasureText(result, 16);
            Raylib.DrawText(result, x + width - 18 - resultWidth, contentY, 16, game.Won ? Palette.Lime : Palette.Red);
            contentY += 22;
        }

        contentY += 6;
        string rbLine = $"RB: {stats.Rb.Attempts} ATT  {stats.Rb.Yards} YDS  {stats.Rb.Tds} TD";
        Raylib.DrawText(rbLine, x + 18, contentY, 14, new Color(170, 190, 210, 255));
        contentY += 22;

        var topReceiver = stats.Receivers
            .OrderByDescending(receiver => receiver.Yards)
            .ThenByDescending(receiver => receiver.Receptions)
            .ThenByDescending(receiver => receiver.Targets)
            .FirstOrDefault();

        string receiverLine = topReceiver.Targets > 0 || topReceiver.Receptions > 0 || topReceiver.Yards > 0 || topReceiver.Tds > 0
            ? $"TOP REC: {topReceiver.Label}  {topReceiver.Yards} YDS"
            : "TOP REC: NONE";
        Raylib.DrawText(receiverLine, x + 18, contentY, 14, new Color(170, 190, 210, 255));
        contentY += 28;

        int barWidth = width - 36;
        int barX = x + 18;
        int barHeight = 16;
        Raylib.DrawRectangle(barX, contentY, barWidth, barHeight, new Color(28, 32, 42, 220));
        Raylib.DrawRectangleLines(barX, contentY, barWidth, barHeight, accentColor);
        int filledWidth = (int)(barWidth * stagesWon / 3f);
        if (filledWidth > 0)
        {
            Raylib.DrawRectangle(barX + 1, contentY + 1, Math.Max(0, filledWidth - 2), barHeight - 2, accentColor);
        }
    }

    private void DrawLeaderboardPanel(int x, int y, int width, LeaderboardSummary summary, Color accentColor)
    {
        int height = 210;
        Raylib.DrawRectangle(x, y, width, height, new Color(8, 12, 18, 220));
        Raylib.DrawRectangleLines(x, y, width, height, accentColor);

        DrawCenteredText("DOMINANCE RANKINGS", x, width, y + 12, 18, Palette.Yellow);

        string savedLine = $"SAVED SCORE: {summary.SavedScore:F1}";
        DrawCenteredText(savedLine, x, width, y + 36, 14, new Color(170, 190, 210, 255));

        int drawY = y + 62;
        int rows = Math.Min(5, summary.Entries.Count);
        for (int i = 0; i < rows; i++)
        {
            LeaderboardEntry entry = summary.Entries[i];
            bool highlight = entry.IsCurrentPlayer;

            if (highlight)
            {
                Raylib.DrawRectangle(x + 10, drawY - 3, width - 20, 24, new Color(40, 60, 82, 215));
                Raylib.DrawRectangleLines(x + 10, drawY - 3, width - 20, 24, Palette.Cyan);
            }

            string left = TruncateTextToWidth($"#{entry.Rank}  {entry.Name} - {entry.TeamName}", 16, width - 128);
            string score = entry.Score.ToString("F1");
            Color rankColor = entry.Rank switch
            {
                1 => Palette.Gold,
                2 => new Color(205, 215, 225, 255),
                3 => new Color(214, 140, 72, 255),
                _ => Palette.White
            };

            Raylib.DrawText(left, x + 18, drawY + 1, 16, rankColor);
            string history = TruncateTextToWidth(entry.ScoreHistory, 10, width - 132);
            Raylib.DrawText(history, x + 18, drawY + 16, 10, new Color(165, 185, 205, 255));
            if (entry.HasTrophy)
            {
                DrawTrophyIcon(x + width - 108, drawY + 2, 0.58f, Palette.Gold);
            }

            int scoreWidth = Raylib.MeasureText(score, 18);
            Raylib.DrawText(score, x + width - 18 - scoreWidth, drawY, 18, highlight ? Palette.Cyan : Palette.White);
            drawY += 28;
        }

        if (!summary.HasPlayerRank)
        {
            DrawCenteredText("Save a game to enter the rankings", x, width, y + height - 32, 14, Palette.Orange);
            return;
        }

        int currentPlayerRank = summary.CurrentPlayerRank ?? 0;
        string playerLine = $"#{currentPlayerRank} CURRENT PLACE";
        DrawCenteredText(playerLine, x, width, y + height - 30, 15, summary.IsOnPodium ? Palette.Lime : Palette.Orange);
    }

    private void DrawPodiumPanel(int x, int y, int width, int height, LeaderboardSummary summary, Color accentColor)
    {
        Raylib.DrawRectangle(x, y, width, height, new Color(8, 12, 18, 220));
        Raylib.DrawRectangleLines(x, y, width, height, accentColor);

        DrawCenteredText("PODIUM", x, width, y + 12, 22, Palette.White);

        int baseY = y + height - 42;
        int centerX = x + width / 2;
        int placeWidth = 118;
        int gap = 14;

        LeaderboardEntry secondPlaceEntry = GetEntryForRank(summary, 2);
        LeaderboardEntry firstPlaceEntry = GetEntryForRank(summary, 1);
        LeaderboardEntry thirdPlaceEntry = GetEntryForRank(summary, 3);

        DrawPodiumPlace(centerX - placeWidth / 2 - placeWidth - gap, baseY, placeWidth, 74, secondPlaceEntry, 2, secondPlaceEntry.IsCurrentPlayer);
        DrawPodiumPlace(centerX - placeWidth / 2, baseY, placeWidth, 104, firstPlaceEntry, 1, firstPlaceEntry.IsCurrentPlayer);
        DrawPodiumPlace(centerX + placeWidth / 2 + gap, baseY, placeWidth, 58, thirdPlaceEntry, 3, thirdPlaceEntry.IsCurrentPlayer);

        string message = summary.IsOnPodium
            ? summary.IsFirstPlace
                ? "GOOD JOB - YOU ARE FIRST PLACE AND KEEP THE TROPHY"
                : "GOOD JOB - YOU ARE ON THE PODIUM"
            : summary.HasPlayerRank
                ? "YOU ARE NOT ON THE PODIUM"
                : "NO PLAYER RANK YET";

        Color messageColor = summary.IsOnPodium ? Palette.Lime : Palette.Orange;
        DrawCenteredText(message, x, width, y + height - 28, 16, messageColor);
    }

    private void DrawPodiumPlace(int x, int baseY, int width, int height, LeaderboardEntry entry, int place, bool isCurrentPlayer)
    {
        Color blockColor = place switch
        {
            1 => new Color(185, 140, 40, 255),
            2 => new Color(120, 130, 145, 255),
            _ => new Color(145, 92, 52, 255)
        };

        if (height > 0)
        {
            Raylib.DrawRectangle(x, baseY - height, width, height, blockColor);
            Raylib.DrawRectangleLines(x, baseY - height, width, height, isCurrentPlayer ? Palette.Cyan : Palette.White);
        }

        if (entry.Rank == 0)
        {
            string emptyPlaceLabel = place.ToString();
            int emptyPlaceWidth = Raylib.MeasureText(emptyPlaceLabel, 16);
            Raylib.DrawText(emptyPlaceLabel, x + (width - emptyPlaceWidth) / 2, baseY - 22, 16, Palette.White);
            DrawCenteredText("---", x, width, baseY - height - 20, 12, new Color(140, 160, 180, 255));
            return;
        }

        string placeLabel = place.ToString();
        int placeLabelWidth = Raylib.MeasureText(placeLabel, 16);
        int placeLabelY = baseY - 22;
        Raylib.DrawText(placeLabel, x + (width - placeLabelWidth) / 2, placeLabelY, 16, Palette.White);

        string displayName = TruncatePodiumName(entry.Name, width - 12);
        string displayTeam = TruncatePodiumName(entry.TeamName, width - 12);
        string displayHistory = TruncatePodiumName(entry.ScoreHistory, width - 12);
        int nameFontSize = displayName.Length > 10 ? 14 : 16;
        int nameY = baseY - Math.Max(50, height - 16);
        DrawCenteredText(displayName, x, width, nameY, nameFontSize, isCurrentPlayer ? Palette.Cyan : Palette.White);
        DrawCenteredText(displayTeam, x, width, nameY + 16, 12, new Color(165, 185, 205, 255));
        DrawCenteredText(displayHistory, x, width, nameY + 30, 10, new Color(150, 170, 190, 255));

        int ratingY = Math.Min(baseY - 22, nameY + 44);
        DrawCenteredText(entry.Score.ToString("F1"), x, width, ratingY, 16, Palette.Yellow);

        if (place == 1)
        {
            DrawTrophyIcon(x + width / 2 - 10, baseY - height - 60, 0.62f, entry.IsCurrentPlayer ? Palette.Gold : new Color(220, 190, 120, 255));
        }

        if (isCurrentPlayer)
        {
            DrawCenteredText("YOU", x, width, baseY - height - 46, 12, Palette.Cyan);
        }
    }

    private static LeaderboardEntry GetEntryForRank(LeaderboardSummary summary, int rank)
    {
        return summary.Entries.FirstOrDefault(entry => entry.Rank == rank);
    }

    private static string TruncatePodiumName(string name, int maxWidth)
    {
        if (Raylib.MeasureText(name, 14) <= maxWidth)
        {
            return name;
        }

        string ellipsis = "...";
        for (int length = name.Length - 1; length > 1; length--)
        {
            string candidate = name[..length] + ellipsis;
            if (Raylib.MeasureText(candidate, 14) <= maxWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
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

    private static Color GetRatingColor(float score)
    {
        return score switch
        {
            >= 90f => Palette.Gold,
            >= 75f => Palette.Lime,
            >= 55f => Palette.White,
            _ => Palette.Orange
        };
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

    private static void DrawActionButtons(int bannerX, int y, int bannerWidth, string confirmText, string restartText)
    {
        int buttonWidth = Math.Min(300, (bannerWidth - 18) / 2);
        int buttonHeight = 34;
        int gap = 18;
        int totalWidth = (buttonWidth * 2) + gap;
        int startX = bannerX + (bannerWidth - totalWidth) / 2;

        DrawActionButton(startX, y, buttonWidth, buttonHeight, confirmText, Palette.Yellow, new Color(58, 48, 12, 220));
        DrawActionButton(startX + buttonWidth + gap, y, buttonWidth, buttonHeight, restartText, Palette.Cyan, new Color(14, 44, 54, 220));
    }

    private static void DrawActionButton(int x, int y, int width, int height, string text, Color borderColor, Color fillColor)
    {
        Raylib.DrawRectangle(x, y, width, height, fillColor);
        Raylib.DrawRectangleLines(x, y, width, height, borderColor);
        int fontSize = GetFittedFontSize(text, 16, width - 20, 10);
        int textWidth = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, x + (width - textWidth) / 2, y + (height - fontSize) / 2, fontSize, borderColor);
    }

    private static void DrawCenteredText(string text, int x, int width, int y, int fontSize, Color color, int horizontalPadding = 12, int minFontSize = 10)
    {
        int fittedFontSize = GetFittedFontSize(text, fontSize, width - (horizontalPadding * 2), minFontSize);
        int textWidth = Raylib.MeasureText(text, fittedFontSize);
        Raylib.DrawText(text, x + (width - textWidth) / 2, y, fittedFontSize, color);
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
