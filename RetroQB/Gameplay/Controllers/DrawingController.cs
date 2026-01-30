using System.Numerics;
using Raylib_cs;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Rendering;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles all drawing/rendering during gameplay.
/// </summary>
public sealed class DrawingController
{
    private readonly FieldRenderer _fieldRenderer;
    private readonly HudRenderer _hudRenderer;
    private readonly FireworksEffect _fireworks;
    private readonly ReceiverPriorityManager _priorityManager;

    public DrawingController(
        FieldRenderer fieldRenderer,
        HudRenderer hudRenderer,
        FireworksEffect fireworks,
        ReceiverPriorityManager priorityManager)
    {
        _fieldRenderer = fieldRenderer;
        _hudRenderer = hudRenderer;
        _fireworks = fireworks;
        _priorityManager = priorityManager;
    }

    public FireworksEffect Fireworks => _fireworks;

    /// <summary>
    /// Updates the fireworks effect.
    /// </summary>
    public void UpdateFireworks(float dt)
    {
        _fireworks.Update(dt);
    }

    /// <summary>
    /// Draws the complete game scene.
    /// </summary>
    public void Draw(
        PlayManager playManager,
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Defender> defenders,
        GameState gameState,
        string lastPlayText,
        string driveOverText,
        OffensiveTeamAttributes offensiveTeam,
        int selectedTeamIndex,
        bool isPaused)
    {
        _fieldRenderer.DrawField(playManager.LineOfScrimmage, playManager.FirstDownLine);

        _fireworks.Draw();

        if (gameState == GameState.PreSnap)
        {
            DrawRouteOverlay(receivers, blockers, playManager);
        }

        foreach (var receiver in receivers)
        {
            receiver.Draw();
        }

        DrawReceiverPriorityLabels(receivers);

        foreach (var blocker in blockers)
        {
            blocker.Draw();
        }

        foreach (var defender in defenders)
        {
            defender.Draw();
        }

        qb.Draw();
        ball.Draw();

        // Draw scoreboard and side panel HUD
        string targetLabel = GetSelectedReceiverPriorityLabel(playManager.SelectedReceiver, receivers);
        _hudRenderer.DrawScoreboard(playManager, lastPlayText, gameState, offensiveTeam);
        _hudRenderer.DrawSidePanel(playManager, lastPlayText, targetLabel, gameState);

        if (gameState == GameState.DriveOver)
        {
            DrawDriveOverBanner(driveOverText, "PRESS ENTER FOR NEXT DRIVE");
        }

        if (gameState == GameState.GameOver)
        {
            if (playManager.Score >= 21)
            {
                _hudRenderer.DrawVictoryBanner(playManager.Score, playManager.AwayScore);
            }
            else
            {
                DrawDriveOverBanner(driveOverText, "PRESS ENTER FOR NEW GAME");
            }
        }

        if (gameState == GameState.MainMenu)
        {
            _hudRenderer.DrawMainMenu(selectedTeamIndex, OffensiveTeamPresets.All);
        }

        if (isPaused)
        {
            _hudRenderer.DrawPause();
        }
    }

    /// <summary>
    /// Sets the stats snapshot on the HUD renderer.
    /// </summary>
    public void SetStatsSnapshot(GameStatsSnapshot snapshot)
    {
        _hudRenderer.SetStatsSnapshot(snapshot);
    }

    private void DrawRouteOverlay(IReadOnlyList<Receiver> receivers, IReadOnlyList<Blocker> blockers, PlayManager playManager)
    {
        foreach (var receiver in receivers)
        {
            if (!receiver.Eligible) continue;

            var points = ReceiverAI.GetRouteWaypoints(receiver);
            if (points.Count < 2) continue;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = Constants.WorldToScreen(points[i]);
                Vector2 b = Constants.WorldToScreen(points[i + 1]);
                Raylib.DrawLineEx(a, b, 2.0f, Palette.Yellow);
            }
        }

        OffensiveLinemanAI.DrawRoutes(
            blockers.ToList(),
            playManager.SelectedPlayType,
            playManager.SelectedPlay.Formation,
            playManager.LineOfScrimmage,
            playManager.SelectedPlay.RunningBackSide);
    }

    private void DrawReceiverPriorityLabels(IReadOnlyList<Receiver> receivers)
    {
        if (receivers.Count == 0) return;

        foreach (var receiver in receivers)
        {
            if (!receiver.Eligible) continue;

            string priorityLabel = _priorityManager.GetPriorityLabel(receiver.Index);
            if (priorityLabel == "-") continue;

            Vector2 labelPos = Constants.WorldToScreen(receiver.Position);
            int fontSize = 20;
            Color shadow = new Color(10, 10, 12, 220);
            int textWidth = Raylib.MeasureText(priorityLabel, fontSize);
            int drawX = (int)labelPos.X - textWidth / 2;
            int drawY = (int)labelPos.Y - 20;
            Raylib.DrawText(priorityLabel, drawX + 1, drawY + 1, fontSize, shadow);
            Raylib.DrawText(priorityLabel, drawX, drawY, fontSize, Palette.Lime);
        }
    }

    private string GetSelectedReceiverPriorityLabel(int selectedReceiver, IReadOnlyList<Receiver> receivers)
    {
        if (selectedReceiver < 0 || selectedReceiver >= receivers.Count)
        {
            return "-";
        }

        return _priorityManager.GetPriorityLabel(selectedReceiver);
    }

    public static void DrawDriveOverBanner(string titleText, string subText)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int bannerWidth = Math.Min(760, screenW - 120);
        int bannerHeight = 130;
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        Raylib.DrawRectangle(x, y, bannerWidth, bannerHeight, new Color(10, 10, 14, 235));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, bannerWidth, bannerHeight), 3, Palette.Gold);
        Raylib.DrawRectangle(x + 6, y + 6, bannerWidth - 12, bannerHeight - 12, new Color(20, 20, 28, 235));

        string title = string.IsNullOrWhiteSpace(titleText) ? "DRIVE OVER" : titleText.ToUpperInvariant();
        int titleSize = 40;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        int titleX = x + (bannerWidth - titleWidth) / 2;
        int titleY = y + 18;
        Raylib.DrawText(title, titleX, titleY, titleSize, Palette.Gold);

        string sub = string.IsNullOrWhiteSpace(subText) ? "PRESS ENTER" : subText.ToUpperInvariant();
        int subSize = 18;
        int subWidth = Raylib.MeasureText(sub, subSize);
        Raylib.DrawText(sub, x + (bannerWidth - subWidth) / 2, y + bannerHeight - subSize - 14, subSize, Palette.White);
    }
}
