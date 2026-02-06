using System;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;

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
    /// </summary>
    public void DrawChampionBanner(int finalScore, int awayScore, GameStatsSnapshot stats)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int bannerWidth = Math.Min(560, screenW - 80);
        int bannerHeight = 380;
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        // Outer frame with gold border
        Raylib.DrawRectangle(x - 6, y - 6, bannerWidth + 12, bannerHeight + 12, Palette.Gold);
        Raylib.DrawRectangle(x - 2, y - 2, bannerWidth + 4, bannerHeight + 4, new Color(40, 30, 10, 250));
        Raylib.DrawRectangle(x, y, bannerWidth, bannerHeight, new Color(10, 14, 22, 250));
        Raylib.DrawRectangle(x + 4, y + 4, bannerWidth - 8, bannerHeight - 8, new Color(18, 24, 36, 250));

        // Champion header
        string title = "CHAMPION!";
        int titleSize = 52;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, x + (bannerWidth - titleWidth) / 2, y + 16, titleSize, Palette.Gold);

        string subtitle = "SUPER BOWL WINNER";
        int subSize = 20;
        int subWidth = Raylib.MeasureText(subtitle, subSize);
        Raylib.DrawText(subtitle, x + (bannerWidth - subWidth) / 2, y + 72, subSize, Palette.Lime);

        // Final score
        string scoreText = $"FINAL: {finalScore} - {awayScore}";
        int scoreSize = 22;
        int scoreWidth = Raylib.MeasureText(scoreText, scoreSize);
        Raylib.DrawText(scoreText, x + (bannerWidth - scoreWidth) / 2, y + 100, scoreSize, Palette.White);

        // Divider
        int divY = y + 130;
        Raylib.DrawLine(x + 30, divY, x + bannerWidth - 30, divY, Palette.Gold);

        // QB Stats
        int contentY = divY + 12;
        string qbHeader = "SUPER BOWL QB STATS";
        int headerSize = 18;
        int headerWidth = Raylib.MeasureText(qbHeader, headerSize);
        Raylib.DrawText(qbHeader, x + (bannerWidth - headerWidth) / 2, contentY, headerSize, Palette.Blue);
        contentY += 28;

        int leftCol = x + 40;
        int rightCol = x + bannerWidth / 2 + 20;
        int labelSize = 16;
        int valueSize = 20;
        Color labelColor = new(140, 160, 180, 255);
        Color valueColor = Palette.White;

        // Passing stats
        Raylib.DrawText("PASSING", leftCol, contentY, labelSize, labelColor);
        contentY += 20;
        string compAtt = $"{stats.Qb.Completions}/{stats.Qb.Attempts}";
        Raylib.DrawText("CMP/ATT:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText(compAtt, leftCol + 90, contentY, valueSize, valueColor);
        Raylib.DrawText("YARDS:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.PassYards}", rightCol + 70, contentY, valueSize, valueColor);
        contentY += 24;

        Raylib.DrawText("TD:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.PassTds}", leftCol + 90, contentY, valueSize, stats.Qb.PassTds > 0 ? Palette.Gold : valueColor);
        Raylib.DrawText("INT:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.Interceptions}", rightCol + 70, contentY, valueSize, stats.Qb.Interceptions > 0 ? Palette.Red : valueColor);
        contentY += 28;

        // Rushing
        Raylib.DrawText("RUSHING", leftCol, contentY, labelSize, labelColor);
        contentY += 20;
        Raylib.DrawText("YARDS:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.RushYards}", leftCol + 90, contentY, valueSize, valueColor);
        Raylib.DrawText("TD:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.RushTds}", rightCol + 70, contentY, valueSize, stats.Qb.RushTds > 0 ? Palette.Gold : valueColor);
        contentY += 30;

        // Stage progress - all 3 complete
        int barWidth = bannerWidth - 100;
        int barX = x + 50;
        int barHeight = 18;
        Raylib.DrawRectangle(barX, contentY, barWidth, barHeight, new Color(30, 30, 40, 220));
        Raylib.DrawRectangleLines(barX, contentY, barWidth, barHeight, Palette.Gold);
        Raylib.DrawRectangle(barX + 1, contentY + 1, barWidth - 2, barHeight - 2, Palette.Gold);
        string progressText = "3/3 COMPLETE - CHAMPION!";
        int progSize = 12;
        int progWidth = Raylib.MeasureText(progressText, progSize);
        Raylib.DrawText(progressText, barX + (barWidth - progWidth) / 2, contentY + 3, progSize, new Color(18, 24, 36, 255));

        // Continue prompt
        string prompt = "PRESS ENTER TO CHOOSE TEAM";
        int promptSize = 16;
        int promptWidth = Raylib.MeasureText(prompt, promptSize);
        Raylib.DrawText(prompt, x + (bannerWidth - promptWidth) / 2, y + bannerHeight - 28, promptSize, Palette.Yellow);
    }

    /// <summary>
    /// Draws a banner when the player is eliminated (away team wins).
    /// </summary>
    public void DrawEliminationBanner(int finalScore, int awayScore, SeasonStage stage, GameStatsSnapshot stats)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int bannerWidth = Math.Min(520, screenW - 80);
        int bannerHeight = 260;
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
        Raylib.DrawText(title, x + (bannerWidth - titleWidth) / 2, y + 18, titleSize, Palette.Red);

        // Stage info
        string stageText = $"Fell in the {stage.GetDisplayName()}";
        int stageSize = 18;
        int stageWidth = Raylib.MeasureText(stageText, stageSize);
        Raylib.DrawText(stageText, x + (bannerWidth - stageWidth) / 2, y + 68, stageSize, Palette.Orange);

        // Final score
        string scoreText = $"FINAL: {finalScore} - {awayScore}";
        int scoreSize = 22;
        int scoreWidth = Raylib.MeasureText(scoreText, scoreSize);
        Raylib.DrawText(scoreText, x + (bannerWidth - scoreWidth) / 2, y + 98, scoreSize, Palette.White);

        // Divider
        int divY = y + 128;
        Raylib.DrawLine(x + 30, divY, x + bannerWidth - 30, divY, Palette.Red);

        // Brief stats
        int contentY = divY + 12;
        string compAtt = $"QB: {stats.Qb.Completions}/{stats.Qb.Attempts}, {stats.Qb.PassYards} yds, {stats.Qb.PassTds} TD, {stats.Qb.Interceptions} INT";
        int statSize = 14;
        int statWidth = Raylib.MeasureText(compAtt, statSize);
        Raylib.DrawText(compAtt, x + (bannerWidth - statWidth) / 2, contentY, statSize, new Color(160, 170, 180, 255));
        contentY += 24;

        // Stage progress
        int stageNum = stage.GetStageNumber() - 1; // didn't complete this one
        int barWidth = bannerWidth - 100;
        int barX = x + 50;
        int barHeight = 18;
        Raylib.DrawRectangle(barX, contentY, barWidth, barHeight, new Color(30, 30, 40, 220));
        Raylib.DrawRectangleLines(barX, contentY, barWidth, barHeight, Palette.Red);
        int filledWidth = stageNum > 0 ? (int)(barWidth * stageNum / 3f) : 0;
        if (filledWidth > 2)
            Raylib.DrawRectangle(barX + 1, contentY + 1, filledWidth - 2, barHeight - 2, Palette.Orange);
        string progressText = $"{stageNum}/3 COMPLETE";
        int progSize = 12;
        int progWidth = Raylib.MeasureText(progressText, progSize);
        Raylib.DrawText(progressText, barX + (barWidth - progWidth) / 2, contentY + 3, progSize, Palette.White);

        // Continue prompt
        string prompt = "PRESS ENTER TO CHOOSE TEAM";
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
