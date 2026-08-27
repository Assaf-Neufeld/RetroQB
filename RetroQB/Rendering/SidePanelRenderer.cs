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

    public void Draw(PlayManager play, string resultText, string selectedReceiverLabel, GameState state, SeasonStage stage, bool replayAvailable)
    {
        int screenH = Raylib.GetScreenHeight();
        int panelHeight = screenH - (int)(Constants.OuterMargin * 2);

        // Arcade cabinet panel: stepped shadow, double edge, and restrained scan bands.
        int panelY = (int)Constants.OuterMargin;
        Raylib.DrawRectangle(PanelX + 4, panelY + 4, PanelWidth, panelHeight, new Color(0, 0, 0, 110));
        Raylib.DrawRectangle(PanelX, panelY, PanelWidth, panelHeight, Palette.Panel);
        Raylib.DrawRectangleLinesEx(new Rectangle(PanelX, panelY, PanelWidth, panelHeight), 2f, new Color(32, 92, 66, 255));
        Raylib.DrawRectangleLines(PanelX + 5, panelY + 5, PanelWidth - 10, panelHeight - 10, Palette.PanelLine);
        for (int bandY = panelY + 8; bandY < panelY + panelHeight - 8; bandY += 10)
        {
            Raylib.DrawRectangle(PanelX + 7, bandY, PanelWidth - 14, 1, new Color(255, 255, 255, 5));
        }

        int y = (int)Constants.OuterMargin + 15;
        int x = PanelX + 15;

        // Title
        Raylib.DrawRectangle(x - 5, y - 5, PanelWidth - 20, 42, new Color(29, 39, 38, 230));
        Raylib.DrawRectangle(x - 5, y - 5, 5, 42, Palette.Gold);
        Raylib.DrawText("RETRO QB", x + 7, y, 34, Palette.Gold);
        Raylib.DrawText("'86", x + PanelWidth - 73, y + 16, 12, Palette.Muted);
        y += 46;

        // Divider
        Raylib.DrawLine(x, y, x + PanelWidth - 30, y, Palette.DarkGreen);
        y += 15;

        // Play selection (pre-snap)
        if (state == GameState.PreSnap)
        {
            Raylib.DrawText("SELECT PLAY:", x, y, 18, Palette.Yellow);
            y += 24;
            
            Raylib.DrawText($"Suggested: {play.GetSuggestedPlayLabel()}", x, y, 16, Palette.Lime);
            y += 22;

            // Pass plays header
            Raylib.DrawText("PASS (1-9, 0):", x, y, 14, Palette.Cyan);
            y += 18;

            var passPlays = play.PassPlays;
            string[] passKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
            for (int i = 0; i < passPlays.Count && i < 10; i++)
            {
                bool isSelected = play.SelectedPlayType == PlayType.Pass && play.SelectedPlayIndex == i;
                if (isSelected)
                {
                    Raylib.DrawRectangle(x - 4, y - 1, PanelWidth - 28, 16, new Color(52, 46, 18, 210));
                    Raylib.DrawRectangle(x - 4, y - 1, 3, 16, Palette.Gold);
                }
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
                if (isSelected)
                {
                    Raylib.DrawRectangle(x - 4, y - 1, PanelWidth - 28, 16, new Color(52, 35, 18, 210));
                    Raylib.DrawRectangle(x - 4, y - 1, 3, 16, Palette.Orange);
                }
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

        bool showReplayHint = replayAvailable && state is GameState.PreSnap or GameState.PlayOver or GameState.DriveOver or GameState.StageComplete or GameState.GameOver;

        // Bottom-anchored controls layout to avoid overflow at smaller heights
        int controlsLineSpacing = 14;
        int controlsLines = 9;
        int controlsBlockHeight = 28 + (controlsLines * controlsLineSpacing);
        int controlsStartY = screenH - (int)Constants.OuterMargin - controlsBlockHeight;

        // Goal section sits above controls and grows slightly when replay hint is shown
        int goalSectionHeight = showReplayHint ? 64 : 46;
        int goalY = controlsStartY - goalSectionHeight - 10;
        int minGoalY = (int)Constants.OuterMargin + 120;
        if (goalY < minGoalY)
        {
            goalY = minGoalY;
        }

        // Goal
        y = goalY;
        string stageLabel = stage.GetDisplayName();
        int stageNum = stage.GetStageNumber();
        Color stageColor = stage switch
        {
            SeasonStage.RegularSeason => Palette.Lime,
            SeasonStage.Playoff => Palette.Yellow,
            SeasonStage.SuperBowl => Palette.Gold,
            _ => Palette.White
        };
        Raylib.DrawRectangle(x - 4, y - 4, PanelWidth - 22, 42, new Color(18, 48, 29, 235));
        Raylib.DrawRectangle(x - 4, y - 4, 4, 42, stageColor);
        Raylib.DrawText($"STAGE {stageNum}/3: {stageLabel}", x, y, 14, stageColor);
        y += 18;
        Raylib.DrawText("Score 21 to advance!", x, y, 14, Palette.Gold);
        y += 26;

        if (showReplayHint)
        {
            Raylib.DrawText("Replay Last Play: F", x, y, 14, Palette.Cyan);
            y += 18;
        }

        // Controls at bottom
        y = controlsStartY;
        Raylib.DrawLine(x, y, x + PanelWidth - 30, y, Palette.DarkGreen);
        y += 10;
        Raylib.DrawText("CONTROLS", x, y, 14, Palette.Yellow);
        y += 18;
        Raylib.DrawText("Move: Arrow Keys", x, y, 12, Palette.White);
        y += controlsLineSpacing;
        Raylib.DrawText("Sprint: Hold Shift", x, y, 12, Palette.White);
        y += controlsLineSpacing;
        Raylib.DrawText("Pass Plays: 1-9, 0", x, y, 12, Palette.White);
        y += controlsLineSpacing;
        Raylib.DrawText("Run Plays: Q-P", x, y, 12, Palette.White);
        y += controlsLineSpacing;
        Raylib.DrawText("Snap Ball: Space", x, y, 12, Palette.White);
        y += controlsLineSpacing;
        Raylib.DrawText("Throw: 1-5", x, y, 12, Palette.White);
        y += controlsLineSpacing;
        Raylib.DrawText("Replay (dead-ball): F", x, y, 12, Palette.White);
        y += controlsLineSpacing;
        Raylib.DrawText("Restart Season: Z", x, y, 12, Palette.White);
        y += controlsLineSpacing;
        Raylib.DrawText("Pause: Esc", x, y, 12, Palette.White);
    }
}
