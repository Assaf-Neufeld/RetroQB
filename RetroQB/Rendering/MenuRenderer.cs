using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

/// <summary>
/// Renders the main menu with title, team selection, and game instructions.
/// </summary>
public sealed class MenuRenderer
{
    public void Draw(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams)
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

        // TITLE SECTION
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

        // GAME RULES SECTION
        string goalText = "★ FIRST TO 21 WINS ★";
        int goalSize = 20;
        int goalWidth = Raylib.MeasureText(goalText, goalSize);
        Raylib.DrawText(goalText, panelX + (panelWidth - goalWidth) / 2, contentY, goalSize, Palette.Lime);
        contentY += goalSize + 20;

        // TEAM SELECTION SECTION
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

        // INSTRUCTIONS FOOTER
        Raylib.DrawLine(panelX + dividerPadding, contentY, panelX + panelWidth - dividerPadding, contentY, new Color(60, 80, 100, 180));
        contentY += 16;

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
}
