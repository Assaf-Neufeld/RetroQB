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
    /// <summary>
    /// Draws a banner when the player wins a non-final stage (Regular Season or Playoff).
    /// </summary>
    public void DrawStageCompleteBanner(int finalScore, int awayScore, SeasonStage completedStage, GameStatsSnapshot stats)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int bannerWidth = Math.Min(540, screenW - 80);
        int bannerHeight = 300;
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        Color stageColor = completedStage switch
        {
            SeasonStage.RegularSeason => Palette.Lime,
            SeasonStage.Playoff => Palette.Yellow,
            _ => Palette.Gold
        };

        // Outer frame
        Raylib.DrawRectangle(x - 4, y - 4, bannerWidth + 8, bannerHeight + 8, stageColor);
        Raylib.DrawRectangle(x, y, bannerWidth, bannerHeight, new Color(10, 14, 22, 250));
        Raylib.DrawRectangle(x + 4, y + 4, bannerWidth - 8, bannerHeight - 8, new Color(18, 24, 36, 250));

        // Stage won header
        string title = $"{completedStage.GetDisplayName()} WON!";
        int titleSize = 36;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, x + (bannerWidth - titleWidth) / 2, y + 20, titleSize, stageColor);

        // Score
        string scoreText = $"FINAL: {finalScore} - {awayScore}";
        int scoreSize = 22;
        int scoreWidth = Raylib.MeasureText(scoreText, scoreSize);
        Raylib.DrawText(scoreText, x + (bannerWidth - scoreWidth) / 2, y + 64, scoreSize, Palette.White);

        // Divider
        int divY = y + 94;
        Raylib.DrawLine(x + 30, divY, x + bannerWidth - 30, divY, stageColor);

        // Next stage preview
        int contentY = divY + 16;
        var nextStage = completedStage.GetNextStage();
        string nextLabel = nextStage?.GetDisplayName() ?? "???";
        string nextText = $"NEXT: {nextLabel}";
        int nextSize = 22;
        int nextWidth = Raylib.MeasureText(nextText, nextSize);
        Raylib.DrawText(nextText, x + (bannerWidth - nextWidth) / 2, contentY, nextSize, Palette.Cyan);
        contentY += 30;

        // Difficulty warning
        string diffText = nextStage switch
        {
            SeasonStage.Playoff => "Tougher defense awaits!",
            SeasonStage.SuperBowl => "Elite defense - The ultimate test!",
            _ => "Get ready!"
        };
        int diffSize = 16;
        int diffWidth = Raylib.MeasureText(diffText, diffSize);
        Raylib.DrawText(diffText, x + (bannerWidth - diffWidth) / 2, contentY, diffSize, Palette.Orange);
        contentY += 28;

        // Stage progress bar
        int stageNum = completedStage.GetStageNumber();
        int barWidth = bannerWidth - 100;
        int barX = x + 50;
        int barHeight = 20;
        Raylib.DrawRectangle(barX, contentY, barWidth, barHeight, new Color(30, 30, 40, 220));
        Raylib.DrawRectangleLines(barX, contentY, barWidth, barHeight, stageColor);
        int filledWidth = (int)(barWidth * stageNum / 3f);
        Raylib.DrawRectangle(barX + 1, contentY + 1, filledWidth - 2, barHeight - 2, stageColor);
        string progressText = $"{stageNum}/3 COMPLETE";
        int progSize = 12;
        int progWidth = Raylib.MeasureText(progressText, progSize);
        Raylib.DrawText(progressText, barX + (barWidth - progWidth) / 2, contentY + 4, progSize, Palette.White);
        contentY += 34;

        // Continue prompt
        string prompt = $"PRESS ENTER FOR {nextLabel}";
        int promptSize = 16;
        int promptWidth = Raylib.MeasureText(prompt, promptSize);
        Raylib.DrawText(prompt, x + (bannerWidth - promptWidth) / 2, y + bannerHeight - 30, promptSize, Palette.Yellow);
    }

    /// <summary>
    /// Draws the ultimate champion banner when all 3 stages are won.
    /// Shows season summary: QB rating, stage reached, all game scores.
    /// </summary>
    public void DrawChampionBanner(int finalScore, int awayScore, GameStatsSnapshot stats, SeasonSummary seasonSummary)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int bannerWidth = Math.Min(600, screenW - 60);
        int bannerHeight = Math.Min(520, screenH - 40);
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        // Outer frame with gold border
        Raylib.DrawRectangle(x - 6, y - 6, bannerWidth + 12, bannerHeight + 12, Palette.Gold);
        Raylib.DrawRectangle(x - 2, y - 2, bannerWidth + 4, bannerHeight + 4, new Color(40, 30, 10, 250));
        Raylib.DrawRectangle(x, y, bannerWidth, bannerHeight, new Color(10, 14, 22, 250));
        Raylib.DrawRectangle(x + 4, y + 4, bannerWidth - 8, bannerHeight - 8, new Color(18, 24, 36, 250));

        // Champion header
        string title = "CHAMPION!";
        int titleSize = 48;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, x + (bannerWidth - titleWidth) / 2, y + 14, titleSize, Palette.Gold);

        string subtitle = "SUPER BOWL WINNER";
        int subSize = 18;
        int subWidth = Raylib.MeasureText(subtitle, subSize);
        Raylib.DrawText(subtitle, x + (bannerWidth - subWidth) / 2, y + 66, subSize, Palette.Lime);

        // Divider
        int divY = y + 92;
        Raylib.DrawLine(x + 30, divY, x + bannerWidth - 30, divY, Palette.Gold);

        // Draw shared season summary content
        int contentY = divY + 10;
        DrawSeasonSummaryContent(x, contentY, bannerWidth, seasonSummary, Palette.Gold);

        // Continue prompt
        string prompt = "PRESS ENTER TO CHOOSE TEAM";
        int promptSize = 16;
        int promptWidth = Raylib.MeasureText(prompt, promptSize);
        Raylib.DrawText(prompt, x + (bannerWidth - promptWidth) / 2, y + bannerHeight - 28, promptSize, Palette.Yellow);
    }

    /// <summary>
    /// Draws a banner when the player is eliminated (away team wins).
    /// Shows season summary: QB rating, stage reached, all game scores.
    /// </summary>
    public void DrawEliminationBanner(int finalScore, int awayScore, SeasonStage stage, GameStatsSnapshot stats, SeasonSummary seasonSummary)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int bannerWidth = Math.Min(600, screenW - 60);
        int bannerHeight = Math.Min(480, screenH - 40);
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        // Outer frame with red border
        Raylib.DrawRectangle(x - 4, y - 4, bannerWidth + 8, bannerHeight + 8, Palette.Red);
        Raylib.DrawRectangle(x, y, bannerWidth, bannerHeight, new Color(10, 14, 22, 250));
        Raylib.DrawRectangle(x + 4, y + 4, bannerWidth - 8, bannerHeight - 8, new Color(18, 24, 36, 250));

        // Elimination header
        string title = "ELIMINATED";
        int titleSize = 44;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, x + (bannerWidth - titleWidth) / 2, y + 14, titleSize, Palette.Red);

        // Stage info
        string stageText = $"Fell in the {stage.GetDisplayName()}";
        int stageSize = 18;
        int stageWidth = Raylib.MeasureText(stageText, stageSize);
        Raylib.DrawText(stageText, x + (bannerWidth - stageWidth) / 2, y + 62, stageSize, Palette.Orange);

        // Divider
        int divY = y + 88;
        Raylib.DrawLine(x + 30, divY, x + bannerWidth - 30, divY, Palette.Red);

        // Draw shared season summary content
        int contentY = divY + 10;
        DrawSeasonSummaryContent(x, contentY, bannerWidth, seasonSummary, Palette.Red);

        // Continue prompt
        string prompt = "PRESS ENTER TO CHOOSE TEAM";
        int promptSize = 16;
        int promptWidth = Raylib.MeasureText(prompt, promptSize);
        Raylib.DrawText(prompt, x + (bannerWidth - promptWidth) / 2, y + bannerHeight - 28, promptSize, Palette.Yellow);
    }

    /// <summary>
    /// Draws the shared season summary content: QB rating, game scores, cumulative stats.
    /// </summary>
    private void DrawSeasonSummaryContent(int bannerX, int startY, int bannerWidth, SeasonSummary summary, Color accentColor)
    {
        int contentY = startY;
        int leftCol = bannerX + 30;
        Color labelColor = new(140, 160, 180, 255);
        Color valueColor = Palette.White;

        // ── QB RATING ──
        float qbRating = summary.ComputeQbRating();
        string ratingHeader = "QB RATING";
        int ratingHeaderSize = 16;
        int ratingHeaderWidth = Raylib.MeasureText(ratingHeader, ratingHeaderSize);
        Raylib.DrawText(ratingHeader, bannerX + (bannerWidth - ratingHeaderWidth) / 2, contentY, ratingHeaderSize, Palette.Blue);
        contentY += 22;

        string ratingValue = $"{qbRating:F1}";
        int ratingValueSize = 36;
        int ratingValueWidth = Raylib.MeasureText(ratingValue, ratingValueSize);
        Color ratingColor = qbRating >= 100f ? Palette.Gold
                          : qbRating >= 80f ? Palette.Lime
                          : qbRating >= 60f ? Palette.White
                          : Palette.Orange;
        Raylib.DrawText(ratingValue, bannerX + (bannerWidth - ratingValueWidth) / 2, contentY, ratingValueSize, ratingColor);
        contentY += 42;

        // ── CUMULATIVE PASSING STATS ──
        var qb = summary.CumulativeQbStats;
        string passLine = $"{qb.Completions}/{qb.Attempts}  {qb.PassYards} YDS  {qb.PassTds} TD  {qb.Interceptions} INT";
        int passLineSize = 14;
        int passLineWidth = Raylib.MeasureText(passLine, passLineSize);
        Raylib.DrawText(passLine, bannerX + (bannerWidth - passLineWidth) / 2, contentY, passLineSize, labelColor);
        contentY += 18;

        if (qb.RushYards != 0 || qb.RushTds != 0)
        {
            string rushLine = $"RUSH: {qb.RushYards} YDS  {qb.RushTds} TD";
            int rushLineWidth = Raylib.MeasureText(rushLine, passLineSize);
            Raylib.DrawText(rushLine, bannerX + (bannerWidth - rushLineWidth) / 2, contentY, passLineSize, labelColor);
            contentY += 18;
        }

        contentY += 6;

        // ── DIVIDER ──
        Raylib.DrawLine(bannerX + 40, contentY, bannerX + bannerWidth - 40, contentY, new Color(60, 70, 80, 180));
        contentY += 10;

        // ── GAME SCORES ──
        string scoresHeader = "GAME SCORES";
        int scoresHeaderSize = 16;
        int scoresHeaderWidth = Raylib.MeasureText(scoresHeader, scoresHeaderSize);
        Raylib.DrawText(scoresHeader, bannerX + (bannerWidth - scoresHeaderWidth) / 2, contentY, scoresHeaderSize, Palette.Cyan);
        contentY += 24;

        foreach (var game in summary.Games)
        {
            string stageLabel = game.Stage.GetShortLabel();
            string result = game.Won ? "W" : "L";
            Color resultColor = game.Won ? Palette.Lime : Palette.Red;

            string scoreLine = $"{stageLabel}:  {game.PlayerScore} - {game.AwayScore}";
            int scoreLineSize = 16;

            // Draw stage label + score
            Raylib.DrawText(scoreLine, leftCol, contentY, scoreLineSize, valueColor);

            // Draw W/L badge
            int scoreLineWidth = Raylib.MeasureText(scoreLine, scoreLineSize);
            Raylib.DrawText($"  {result}", leftCol + scoreLineWidth, contentY, scoreLineSize, resultColor);

            contentY += 22;
        }

        // ── STAGE PROGRESS BAR ──
        contentY += 4;
        int stagesWon = summary.Games.Count(g => g.Won);
        int totalGames = summary.Games.Count;
        int barWidth = bannerWidth - 100;
        int barX = bannerX + 50;
        int barHeight = 18;
        Raylib.DrawRectangle(barX, contentY, barWidth, barHeight, new Color(30, 30, 40, 220));
        Raylib.DrawRectangleLines(barX, contentY, barWidth, barHeight, accentColor);
        int filledWidth = totalGames > 0 ? (int)(barWidth * stagesWon / 3f) : 0;
        if (filledWidth > 2)
            Raylib.DrawRectangle(barX + 1, contentY + 1, filledWidth - 2, barHeight - 2, accentColor);
        string progressText = $"{stagesWon}/3 COMPLETE";
        int progSize = 12;
        int progWidth = Raylib.MeasureText(progressText, progSize);
        // Text color: dark on filled bar for champion, white otherwise
        Color progTextColor = stagesWon >= 3 ? new Color(18, 24, 36, 255) : Palette.White;
        Raylib.DrawText(progressText, barX + (barWidth - progWidth) / 2, contentY + 3, progSize, progTextColor);
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
