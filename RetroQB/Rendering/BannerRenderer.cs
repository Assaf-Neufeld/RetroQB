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
        string subtitle = string.Empty;

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

        int targetBannerWidth = showRankingDetails ? 1040 : 760;
        int targetBannerHeight = showRankingDetails ? 792 : 700;
        int bannerWidth = Math.Min(targetBannerWidth, screenW - 48);
        int bannerHeight = Math.Min(targetBannerHeight, screenH - 24);
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(4, 7, 12, 150));
        DrawOutcomeBackdrop(x, y, bannerWidth, bannerHeight, accentColor);

        int innerX = x + 22;
        int innerY = y + 22;
        int innerWidth = bannerWidth - 44;
        int innerHeight = bannerHeight - 44;
        int headerHeight = showRankingDetails ? 148 : 134;

        DrawOutcomeHero(innerX, innerY, innerWidth, headerHeight, title, subtitle, finalScore, awayScore, accentColor, seasonSummary);

        int bodyY = innerY + headerHeight + 18;
        int bodyHeight = innerHeight - headerHeight - 86;
        int currentPlayerRank = leaderboardSummary.CurrentPlayerRank ?? 0;

        if (showRankingDetails)
        {
            int gap = 18;
            int rightWidth = Math.Min(460, Math.Max(380, innerWidth - 340 - gap));
            int leftWidth = innerWidth - rightWidth - gap;
            if (leftWidth < 340)
            {
                leftWidth = 340;
                rightWidth = innerWidth - leftWidth - gap;
            }

            int leftX = innerX;
            int rightX = leftX + leftWidth + gap;
            int leaderboardHeight = showPodium ? 290 : bodyHeight;
            int podiumHeight = showPodium ? bodyHeight - leaderboardHeight - 16 : 0;

            DrawPerformanceDashboard(leftX, bodyY, leftWidth, bodyHeight, seasonSummary, accentColor, stats, showFinalRank ? currentPlayerRank : null, showFinalRank && leaderboardSummary.HasPlayerRank ? leaderboardSummary.IsOnPodium : false);
            DrawLeaderboardPanel(rightX, bodyY, rightWidth, leaderboardHeight, leaderboardSummary, accentColor);

            if (showPodium && podiumHeight >= 180)
            {
                DrawPodiumPanel(rightX, bodyY + leaderboardHeight + 16, rightWidth, podiumHeight, leaderboardSummary, accentColor);
            }
        }
        else
        {
            DrawPerformanceDashboard(innerX, bodyY, innerWidth, bodyHeight, seasonSummary, accentColor, stats, null, false);
        }

        DrawActionButtons(x, y + bannerHeight - 62, bannerWidth, prompt, "Z TO RESTART");
    }

    private static void DrawOutcomeBackdrop(int x, int y, int width, int height, Color accentColor)
    {
        Raylib.DrawRectangle(x + 8, y + 10, width, height, new Color(0, 0, 0, 80));
        Raylib.DrawRectangle(x - 2, y - 2, width + 4, height + 4, new Color((int)accentColor.R, (int)accentColor.G, (int)accentColor.B, 70));
        Raylib.DrawRectangle(x, y, width, height, new Color(11, 16, 24, 244));
        Raylib.DrawRectangle(x, y, width, 8, new Color((int)accentColor.R, (int)accentColor.G, (int)accentColor.B, 110));
        Raylib.DrawRectangleLines(x, y, width, height, accentColor);
    }

    private static void DrawPanelSurface(int x, int y, int width, int height, Color accentColor, Color fillColor)
    {
        Raylib.DrawRectangle(x, y, width, height, fillColor);
        Raylib.DrawRectangle(x, y, width, 3, new Color((int)accentColor.R, (int)accentColor.G, (int)accentColor.B, 120));
        Raylib.DrawRectangleLines(x, y, width, height, new Color((int)accentColor.R, (int)accentColor.G, (int)accentColor.B, 165));
    }

    private void DrawOutcomeHero(
        int x,
        int y,
        int width,
        int height,
        string title,
        string subtitle,
        int finalScore,
        int awayScore,
        Color accentColor,
        SeasonSummary seasonSummary)
    {
        DrawPanelSurface(x, y, width, height, accentColor, new Color(16, 24, 38, 228));

        int scoreBlockWidth = Math.Min(240, width / 3);
        int scoreX = x + width - scoreBlockWidth - 26;
        int leftX = x + 26;
        int leftWidth = Math.Max(220, scoreX - leftX - 16);
        bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
        int titleY = hasSubtitle ? y + 22 : y + 32;
        DrawFittedLeftText(title, leftX, titleY, 46, leftWidth, accentColor, 24);
        if (hasSubtitle)
        {
            DrawFittedLeftText(subtitle, leftX, y + 72, 20, leftWidth, Palette.Cyan, 12);
        }

        string scoreLabel = "FINAL SCORE";
        DrawCenteredText(scoreLabel, scoreX, scoreBlockWidth, y + 22, 14, new Color(160, 180, 205, 255));
        DrawCenteredText($"{finalScore} - {awayScore}", scoreX, scoreBlockWidth, y + 48, 42, Palette.White);

        float dominanceScore = seasonSummary.ComputeDominanceScore();

        int chipY = hasSubtitle ? y + height - 46 : y + height - 40;
        int chipWidth = Math.Min(220, Math.Max(150, leftWidth));
        DrawMetricChip(leftX, chipY, chipWidth, 30, "DOMINANCE SCORE", $"{dominanceScore:F1}", GetRatingColor(dominanceScore), new Color(24, 36, 54, 240));
    }

    private void DrawPerformanceDashboard(
        int x,
        int y,
        int width,
        int height,
        SeasonSummary summary,
        Color accentColor,
        GameStatsSnapshot stats,
        int? finalRank,
        bool isPodiumFinish)
    {
        DrawPanelSurface(x, y, width, height, accentColor, new Color(10, 15, 24, 226));

        bool compact = height < 400;
        DrawFittedLeftText("SEASON BREAKDOWN", x + 18, y + 14, compact ? 18 : 22, width - 36, Palette.White, 12);

        int contentX = x + 18;
        int contentY = y + 48;
        int contentWidth = width - 36;

        DrawFittedLeftText("Season totals", contentX, contentY, compact ? 12 : 14, contentWidth, new Color(170, 190, 210, 255), 9);
        contentY += compact ? 20 : 24;

        int stageTrackHeight = compact ? 78 : 94;
        DrawStageTrack(contentX, contentY, contentWidth, stageTrackHeight, summary, accentColor);
        contentY += stageTrackHeight + 14;

        int cardsGap = 12;
        int cardWidth = (contentWidth - cardsGap) / 2;
        int cardHeight = compact ? 84 : 112;

        QbStatsSnapshot qb = summary.CumulativeQbStats;
        float ypa = qb.Attempts > 0 ? qb.PassYards / (float)qb.Attempts : 0f;
        float qbRating = summary.ComputeQbRating();
        string[] qbLines =
        [
            $"QBR: {qbRating:0.0}",
            $"CMP: {qb.Completions}/{qb.Attempts}",
            $"YDS: {qb.PassYards}  YPA: {ypa:0.0}",
            $"TD-INT: {qb.PassTds}-{qb.Interceptions}"
        ];
        DrawStatCard(contentX, contentY, cardWidth, cardHeight, "QB TOTAL", qbLines, Palette.QB, new Color(18, 30, 45, 230));

        float ypc = stats.Rb.Attempts > 0 ? stats.Rb.Yards / (float)stats.Rb.Attempts : 0f;
        string[] rushLines =
        [
            $"ATT: {stats.Rb.Attempts}",
            $"YDS: {stats.Rb.Yards}   YPC: {ypc:0.0}",
            $"TD: {stats.Rb.Tds}"
        ];
        DrawStatCard(contentX + cardWidth + cardsGap, contentY, cardWidth, cardHeight, "GROUND GAME", rushLines, Palette.Lime, new Color(18, 33, 28, 230));
        contentY += cardHeight + cardsGap;

        string[] pressureLines =
        [
            $"SACKS: {qb.Sacks}",
            $"LOST: {qb.SackYardsLost} YDS",
            $"QB RUN: {qb.RushAttempts} ATT / {qb.RushYards} YDS"
        ];
        DrawStatCard(contentX, contentY, cardWidth, cardHeight, "PRESSURE", pressureLines, Palette.Orange, new Color(34, 25, 18, 230));

        DrawReceivingCard(contentX + cardWidth + cardsGap, contentY, cardWidth, cardHeight, stats, accentColor);
        contentY += cardHeight + 14;

        if (finalRank.HasValue)
        {
            string footer = isPodiumFinish
                ? $"FINAL DOMINANCE RANK #{finalRank.Value}  |  PODIUM FINISH"
                : $"FINAL DOMINANCE RANK #{finalRank.Value}";
            DrawFittedLeftText(footer, contentX, contentY, 15, contentWidth, isPodiumFinish ? Palette.Gold : Palette.Orange, 10);
            contentY += 22;
        }

        if (contentY <= y + height - 16)
        {
            DrawFittedLeftText(BuildDominanceDetailLine(summary), contentX, contentY, 12, contentWidth, new Color(142, 165, 188, 255), 9);
        }
    }

    private void DrawStageTrack(int x, int y, int width, int height, SeasonSummary summary, Color accentColor)
    {
        bool compact = height < 90;
        DrawPanelSurface(x, y, width, height, accentColor, new Color(14, 20, 30, 235));

        int headerY = y + 10;
        int stagesWon = summary.Games.Count(game => game.Won);
        DrawFittedLeftText($"STAGE PROGRESS  {stagesWon}/3", x + 14, headerY, compact ? 14 : 16, width - 28, Palette.White, 10);

        int barX = x + 14;
        int barY = y + (compact ? 30 : 34);
        int barWidth = width - 28;
        int barHeight = compact ? 12 : 14;
        Raylib.DrawRectangle(barX, barY, barWidth, barHeight, new Color(26, 32, 44, 255));
        Raylib.DrawRectangleLines(barX, barY, barWidth, barHeight, new Color(74, 92, 118, 255));
        int filledWidth = (int)(barWidth * (stagesWon / 3f));
        if (filledWidth > 0)
        {
            Raylib.DrawRectangle(barX + 1, barY + 1, Math.Max(0, filledWidth - 2), barHeight - 2, accentColor);
        }

        SeasonStage[] stages = [SeasonStage.RegularSeason, SeasonStage.Playoff, SeasonStage.SuperBowl];
        int rowY = y + (compact ? 48 : 56);
        int rowWidth = (width - 28 - 16) / 3;
        for (int i = 0; i < stages.Length; i++)
        {
            DrawStageTrackCell(x + 14 + i * (rowWidth + 8), rowY, rowWidth, compact ? 20 : 28, stages[i], summary);
        }
    }

    private static void DrawStageTrackCell(int x, int y, int width, int height, SeasonStage stage, SeasonSummary summary)
    {
        bool compact = height <= 20;
        GameResult? result = summary.Games.FirstOrDefault(game => game.Stage == stage);
        bool played = result.HasValue && (result.Value.PlayerScore > 0 || result.Value.AwayScore > 0);
        Color border = !played
            ? new Color(72, 84, 100, 255)
            : result.Value.Won ? Palette.Lime : Palette.Red;
        Color fill = !played
            ? new Color(20, 25, 34, 255)
            : result.Value.Won ? new Color(18, 44, 28, 255) : new Color(52, 20, 20, 255);

        Raylib.DrawRectangle(x, y, width, height, fill);
        Raylib.DrawRectangleLines(x, y, width, height, border);

        string score = played ? $"{result.Value.PlayerScore}-{result.Value.AwayScore}" : "N/A";
        DrawCenteredText(stage.GetShortLabel(), x, width, y + (compact ? 2 : 4), compact ? 9 : 11, Palette.White, 4, 8);
        DrawCenteredText(score, x, width, y + (compact ? 10 : 16), compact ? 9 : 11, played ? Palette.White : new Color(140, 160, 180, 255), 4, 8);
    }

    private static void DrawStatCard(int x, int y, int width, int height, string title, IReadOnlyList<string> lines, Color borderColor, Color fillColor)
    {
        bool compact = height < 90;
        DrawPanelSurface(x, y, width, height, borderColor, fillColor);
        DrawFittedLeftText(title, x + 12, y + 10, compact ? 13 : 15, width - 24, borderColor, 10);

        int lineY = y + (compact ? 24 : 36);
        int bottomLimit = y + height - 10;
        foreach (string line in lines)
        {
            if (lineY > bottomLimit)
            {
                break;
            }

            DrawFittedLeftText(line, x + 12, lineY, compact ? 10 : 13, width - 24, Palette.White, 9);
            lineY += compact ? 14 : 18;
        }
    }

    private void DrawReceivingCard(int x, int y, int width, int height, GameStatsSnapshot stats, Color accentColor)
    {
        bool compact = height < 90;
        DrawPanelSurface(x, y, width, height, accentColor, new Color(20, 27, 41, 230));
        DrawFittedLeftText("RECEIVING", x + 12, y + 10, compact ? 13 : 15, width - 24, Palette.Cyan, 10);

        var leaders = stats.Receivers
            .OrderByDescending(receiver => receiver.Yards)
            .ThenByDescending(receiver => receiver.Receptions)
            .ThenByDescending(receiver => receiver.Targets)
            .Where(receiver => receiver.Targets > 0 || receiver.Receptions > 0 || receiver.Yards > 0 || receiver.Tds > 0)
            .Take(6)
            .ToArray();

        if (leaders.Length == 0 || leaders.All(receiver => receiver.Targets == 0 && receiver.Receptions == 0 && receiver.Yards == 0 && receiver.Tds == 0))
        {
            DrawCenteredText("NO RECEIVING PRODUCTION", x, width, y + (compact ? 34 : 50), compact ? 11 : 13, new Color(160, 180, 200, 255));
            return;
        }

        int rowY = y + (compact ? 28 : 34);
        int rowHeight = compact ? 10 : 12;
        int bottomLimit = y + height - 10;
        foreach (ReceiverStatsSnapshot receiver in leaders)
        {
            if (rowY > bottomLimit)
            {
                break;
            }

            string rowText = $"{receiver.Label}  {receiver.Receptions}/{receiver.Targets}  {receiver.Yards}Y  {receiver.Tds}TD";
            DrawFittedLeftText(rowText, x + 12, rowY, compact ? 9 : 10, width - 24, Palette.White, 8);
            rowY += rowHeight;
        }
    }

    private static void DrawMetricChip(int x, int y, int width, int height, string label, string value, Color valueColor, Color fillColor)
    {
        Raylib.DrawRectangle(x, y, width, height, fillColor);
        Raylib.DrawRectangleLines(x, y, width, height, new Color((int)valueColor.R, (int)valueColor.G, (int)valueColor.B, 180));
        DrawFittedLeftText(label, x + 10, y + 8, 10, Math.Max(26, width - 64), new Color(160, 180, 205, 255), 8);

        int fittedFontSize = GetFittedFontSize(value, 15, Math.Max(28, width - 74), 9);
        int textWidth = Raylib.MeasureText(value, fittedFontSize);
        Raylib.DrawText(value, x + width - 10 - textWidth, y + (height - fittedFontSize) / 2, fittedFontSize, valueColor);
    }

    private void DrawLeaderboardPanel(int x, int y, int width, int height, LeaderboardSummary summary, Color accentColor)
    {
        const int rowHeight = 42;
        DrawPanelSurface(x, y, width, height, accentColor, new Color(8, 12, 18, 220));

        DrawCenteredText("DOMINANCE RANKINGS", x, width, y + 12, 18, Palette.Yellow);

        string savedLine = $"SAVED SCORE: {summary.SavedScore:F1}";
        DrawCenteredText(savedLine, x, width, y + 36, 14, new Color(170, 190, 210, 255));

        int drawY = y + 58;
        int rows = Math.Min(Math.Max(1, (height - 92) / rowHeight), summary.Entries.Count);
        for (int i = 0; i < rows; i++)
        {
            LeaderboardEntry entry = summary.Entries[i];
            bool highlight = entry.IsCurrentPlayer;

            if (highlight)
            {
                Raylib.DrawRectangle(x + 10, drawY - 3, width - 20, rowHeight - 2, new Color(40, 60, 82, 215));
                Raylib.DrawRectangleLines(x + 10, drawY - 3, width - 20, rowHeight - 2, Palette.Cyan);
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
            DrawFittedLeftText(entry.ScoreHistory, x + 18, drawY + 17, 10, width - 132, new Color(175, 195, 215, 255), 8);
            DrawFittedLeftText(entry.ScoreDetails, x + 18, drawY + 29, 9, width - 132, new Color(145, 170, 195, 255), 7);
            if (entry.HasTrophy)
            {
                DrawTrophyIcon(x + width - 108, drawY + 2, 0.58f, Palette.Gold);
            }

            int scoreWidth = Raylib.MeasureText(score, 18);
            Raylib.DrawText(score, x + width - 18 - scoreWidth, drawY + 10, 18, highlight ? Palette.Cyan : Palette.White);
            drawY += rowHeight;
        }

        if (!summary.HasPlayerRank)
        {
            DrawCenteredText("Save a game to enter the rankings", x, width, y + height - 22, 14, Palette.Orange);
            return;
        }

        int currentPlayerRank = summary.CurrentPlayerRank ?? 0;
        string playerLine = $"#{currentPlayerRank} CURRENT PLACE";
        DrawCenteredText(playerLine, x, width, y + height - 20, 15, summary.IsOnPodium ? Palette.Lime : Palette.Orange);
    }

    private void DrawPodiumPanel(int x, int y, int width, int height, LeaderboardSummary summary, Color accentColor)
    {
        DrawPanelSurface(x, y, width, height, accentColor, new Color(8, 12, 18, 220));

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
        string displayDetails = TruncatePodiumName(entry.ScoreDetails, width - 12);
        int nameFontSize = displayName.Length > 10 ? 14 : 16;
        int nameY = baseY - Math.Max(50, height - 16);
        DrawCenteredText(displayName, x, width, nameY, nameFontSize, isCurrentPlayer ? Palette.Cyan : Palette.White);
        DrawCenteredText(displayTeam, x, width, nameY + 16, 12, new Color(165, 185, 205, 255));
        DrawCenteredText(displayHistory, x, width, nameY + 30, 10, new Color(150, 170, 190, 255));
        DrawCenteredText(displayDetails, x, width, nameY + 42, 10, new Color(140, 165, 190, 255));

        int ratingY = Math.Min(baseY - 22, nameY + 56);
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

    private static void DrawFittedLeftText(string text, int x, int y, int fontSize, int maxWidth, Color color, int minFontSize)
    {
        int fittedFontSize = GetFittedFontSize(text, fontSize, maxWidth, minFontSize);
        Raylib.DrawText(text, x, y, fittedFontSize, color);
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

    private static string BuildDominanceDetailLine(SeasonSummary summary)
    {
        QbStatsSnapshot qb = summary.CumulativeQbStats;
        float attempts = Math.Max(1f, qb.Attempts);
        float completionRate = qb.Completions / attempts;
        float ypa = qb.PassYards / attempts;
        float qbRating = summary.ComputeQbRating();
        string completionText = $"{completionRate * 100f:0}%";

        return $"QBRT {qbRating:0.0} | CMP {completionText} | YPA {ypa:0.0} | INT {qb.Interceptions} | SACK {qb.Sacks}";
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
