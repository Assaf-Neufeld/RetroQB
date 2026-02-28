using Raylib_cs;
using RetroQB.Gameplay.Controllers;

namespace RetroQB.Rendering;

public sealed class FieldGoalRenderer
{
    public void Draw(FieldGoalController controller)
    {
        int screenWidth = Raylib.GetScreenWidth();
        int screenHeight = Raylib.GetScreenHeight();

        int panelWidth = Math.Min(560, screenWidth - 140);
        int panelHeight = 124;
        int panelX = (screenWidth - panelWidth) / 2;
        int panelY = screenHeight - panelHeight - 36;

        Raylib.DrawRectangle(panelX, panelY, panelWidth, panelHeight, new Color(10, 10, 14, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(panelX, panelY, panelWidth, panelHeight), 2f, Palette.Gold);

        string title = $"FIELD GOAL ({controller.Distance:F0} YDS)";
        int titleSize = 24;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        Raylib.DrawText(title, panelX + (panelWidth - titleWidth) / 2, panelY + 12, titleSize, Palette.Gold);

        int barX = panelX + 26;
        int barY = panelY + 54;
        int barWidth = panelWidth - 52;
        int barHeight = 20;

        Raylib.DrawText("ACCURACY", barX, barY - 18, 14, Palette.White);
        Raylib.DrawRectangle(barX, barY, barWidth, barHeight, new Color(30, 34, 44, 220));
        Raylib.DrawRectangleLines(barX, barY, barWidth, barHeight, Palette.White);

        float maxAllowedError = controller.MaxAllowedError;

        float leftWindowNorm = (1f - maxAllowedError) * 0.5f;
        float rightWindowNorm = (1f + maxAllowedError) * 0.5f;
        int windowX = barX + (int)(leftWindowNorm * barWidth);
        int windowW = Math.Max(2, (int)((rightWindowNorm - leftWindowNorm) * barWidth));
        Raylib.DrawRectangle(windowX, barY + 1, windowW, barHeight - 2, new Color(50, 95, 50, 200));

        int centerX = barX + (barWidth / 2);
        Raylib.DrawLine(centerX, barY - 2, centerX, barY + barHeight + 2, Palette.Yellow);

        float normalizedAccuracy = (controller.AccuracyNormalized + 1f) * 0.5f;
        int accuracyMarkerX = barX + (int)(normalizedAccuracy * barWidth);
        Raylib.DrawLine(accuracyMarkerX, barY - 2, accuracyMarkerX, barY + barHeight + 2, Palette.Cyan);

        Raylib.DrawText(controller.DifficultyLabel, barX, barY + 28, 12, Palette.White);

        string prompt = controller.Phase switch
        {
            FieldGoalKickPhase.SweepingAccuracy => "SPACE: KICK",
            _ => ""
        };

        if (!string.IsNullOrEmpty(prompt))
        {
            int promptSize = 16;
            int promptWidth = Raylib.MeasureText(prompt, promptSize);
            Raylib.DrawText(prompt, panelX + (panelWidth - promptWidth) / 2, panelY + panelHeight - 22, promptSize, Palette.White);
        }
    }
}
