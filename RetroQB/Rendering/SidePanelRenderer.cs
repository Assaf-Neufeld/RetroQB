using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

/// <summary>
/// Renders the side panel with play selection, controls, and game info.
/// </summary>
public sealed class SidePanelRenderer
{
    private static int PanelX => (int)Constants.OuterMargin;
    private static int PanelWidth => (int)Constants.SidePanelWidth;

    public void Draw(PlayManager play, string resultText, string selectedReceiverLabel, GameState state, SeasonStage stage)
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
            string[] passKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
            for (int i = 0; i < passPlays.Count && i < 10; i++)
            {
                bool isSelected = play.SelectedPlayType == PlayType.Pass && play.SelectedPlayIndex == i;
                Raylib.DrawText($"{passKeys[i]}) {passPlays[i].Name}", x, y, 14, isSelected ? Palette.Gold : Palette.White);
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
        y = screenH - 180;
        Raylib.DrawRectangle(x - 4, y - 4, PanelWidth - 22, 42, new Color(30, 50, 30, 200));
        string stageLabel = stage.GetDisplayName();
        int stageNum = stage.GetStageNumber();
        Color stageColor = stage switch
        {
            SeasonStage.RegularSeason => Palette.Lime,
            SeasonStage.Playoff => Palette.Yellow,
            SeasonStage.SuperBowl => Palette.Gold,
            _ => Palette.White
        };
        Raylib.DrawText($"STAGE {stageNum}/3: {stageLabel}", x, y, 14, stageColor);
        y += 18;
        Raylib.DrawText("Score 21 to advance!", x, y, 14, Palette.Gold);
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
}
