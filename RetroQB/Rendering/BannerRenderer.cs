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
    public void DrawVictoryBanner(int finalScore, int awayScore, GameStatsSnapshot stats)
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
        
        string compAtt = $"{stats.Qb.Completions}/{stats.Qb.Attempts}";
        Raylib.DrawText("CMP/ATT:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText(compAtt, leftCol + 90, contentY, valueSize, valueColor);
        
        Raylib.DrawText("YARDS:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.PassYards}", rightCol + 70, contentY, valueSize, valueColor);
        contentY += 26;

        Raylib.DrawText("TD:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.PassTds}", leftCol + 90, contentY, valueSize, stats.Qb.PassTds > 0 ? Palette.Gold : valueColor);
        
        Raylib.DrawText("INT:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.Interceptions}", rightCol + 70, contentY, valueSize, stats.Qb.Interceptions > 0 ? Palette.Red : valueColor);
        contentY += 30;

        // Passer rating calculation (simplified)
        float compPct = stats.Qb.Attempts > 0 ? (float)stats.Qb.Completions / stats.Qb.Attempts * 100f : 0f;
        Raylib.DrawText("CMP%:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{compPct:F1}%", leftCol + 90, contentY, valueSize, compPct >= 65f ? Palette.Lime : valueColor);
        
        float ypa = stats.Qb.Attempts > 0 ? (float)stats.Qb.PassYards / stats.Qb.Attempts : 0f;
        Raylib.DrawText("Y/A:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{ypa:F1}", rightCol + 70, contentY, valueSize, ypa >= 8f ? Palette.Lime : valueColor);
        contentY += 34;

        // Rushing stats
        Raylib.DrawText("RUSHING", leftCol, contentY, labelSize, labelColor);
        contentY += 20;
        
        Raylib.DrawText("YARDS:", leftCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.RushYards}", leftCol + 90, contentY, valueSize, valueColor);
        
        Raylib.DrawText("TD:", rightCol, contentY, labelSize, labelColor);
        Raylib.DrawText($"{stats.Qb.RushTds}", rightCol + 70, contentY, valueSize, stats.Qb.RushTds > 0 ? Palette.Gold : valueColor);
        contentY += 34;

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
