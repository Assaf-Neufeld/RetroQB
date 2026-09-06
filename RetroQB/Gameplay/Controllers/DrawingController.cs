using System.Numerics;
using Raylib_cs;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay.Replay;
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
    private readonly ScreenEffects _screenEffects;
    private readonly ReplayOverlayRenderer _replayOverlayRenderer;

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
        _screenEffects = new ScreenEffects();
        _replayOverlayRenderer = new ReplayOverlayRenderer();
    }

    public FireworksEffect Fireworks => _fireworks;
    public ScreenEffects ScreenEffects => _screenEffects;

    /// <summary>
    /// Updates the fireworks and screen effects.
    /// </summary>
    public void UpdateFireworks(float dt)
    {
        _fireworks.Update(dt);
        _screenEffects.Update(dt);
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
        IReadOnlyList<OffensiveTeamAttributes> menuTeams,
        GameState gameState,
        string lastPlayText,
        string driveOverText,
        string driveOverDetailText,
        string driveOverPlayText,
        int driveSummaryScrollOffsetFromLatest,
        OffensiveTeamAttributes offensiveTeam,
        DefensiveTeamAttributes defensiveTeam,
        int selectedTeamIndex,
        string playerName,
        string nameInput,
        string pendingPlayerName,
        string nameEntryMessage,
        bool isPostSeasonNameEntry,
        LeaderboardSummary leaderboardSummary,
        bool showMenuLeaderboard,
        bool showSecretTeamPrompt,
        string secretPasswordInput,
        string secretPasswordMessage,
        bool isPaused,
        SeasonStage currentStage,
        SeasonSummary seasonSummary,
        bool replayAvailable,
        CrowdBackdropState crowdState)
    {
        // Apply camera shake offset
        Vector2 shake = _screenEffects.ShakeOffset;
        if (shake != Vector2.Zero)
        {
            Rlgl.PushMatrix();
            Rlgl.Translatef(shake.X, shake.Y, 0f);
        }

        _fieldRenderer.DrawField(
            playManager.LineOfScrimmage,
            playManager.FirstDownLine,
            offensiveTeam.Name,
            offensiveTeam.PrimaryColor,
            defensiveTeam.Name,
            defensiveTeam.PrimaryColor,
            currentStage,
            crowdState, playManager.Down);

        _fireworks.Draw();
        FootballRenderer.DrawGroundShadow(ball.Position, ball.State, ball.GetArcHeight());

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
        _hudRenderer.DrawScoreboard(playManager, lastPlayText, gameState, offensiveTeam, defensiveTeam, currentStage, driveSummaryScrollOffsetFromLatest);
        _hudRenderer.DrawSidePanel(playManager, lastPlayText, targetLabel, gameState, currentStage, replayAvailable);

        if (gameState == GameState.DriveOver)
        {
            DrawDriveOverBanner(driveOverText, driveOverDetailText, driveOverPlayText, "PRESS ENTER FOR NEXT DRIVE");
        }

        if (gameState == GameState.StageComplete)
        {
            _hudRenderer.DrawStageCompleteBanner(playManager.Score, playManager.AwayScore, currentStage, seasonSummary, leaderboardSummary);
        }

        if (gameState == GameState.GameOver)
        {
            if (playManager.Score >= 21)
            {
                _hudRenderer.DrawChampionBanner(playManager.Score, playManager.AwayScore, currentStage, seasonSummary, leaderboardSummary);
            }
            else
            {
                _hudRenderer.DrawEliminationBanner(playManager.Score, playManager.AwayScore, currentStage, seasonSummary, leaderboardSummary);
            }
        }

        if (gameState == GameState.MainMenu)
        {
            _hudRenderer.DrawMainMenu(selectedTeamIndex, menuTeams, leaderboardSummary, showMenuLeaderboard, showSecretTeamPrompt, secretPasswordInput, secretPasswordMessage);
        }

        if (gameState == GameState.PlayerNameEntry)
        {
            _hudRenderer.DrawPlayerNameEntry(selectedTeamIndex, menuTeams, nameInput, nameEntryMessage, leaderboardSummary, isPostSeasonNameEntry);
        }

        if (gameState == GameState.NameConflict)
        {
            _hudRenderer.DrawNameConflict(selectedTeamIndex, menuTeams, pendingPlayerName);
        }

        if (isPaused)
        {
            _hudRenderer.DrawPause();
        }

        // Pop shake transform before drawing flash overlay
        if (shake != Vector2.Zero)
        {
            Rlgl.PopMatrix();
        }

        // Draw flash overlay on top of everything
        _screenEffects.DrawFlash();
        RetroScreenOverlay.Draw();
    }

    public void DrawReplay(
        PlayManager playManager,
        ReplayFrame replayFrame,
        string lastPlayText,
        string driveOverText,
        int driveSummaryScrollOffsetFromLatest,
        OffensiveTeamAttributes offensiveTeam,
        DefensiveTeamAttributes defensiveTeam,
        int selectedTeamIndex,
        string playerName,
        string nameInput,
        string pendingPlayerName,
        string nameEntryMessage,
        LeaderboardSummary leaderboardSummary,
        bool isPaused,
        SeasonStage currentStage,
        SeasonSummary seasonSummary,
        bool replayAvailable,
        CrowdBackdropState crowdState)
    {
        _fieldRenderer.DrawField(
            replayFrame.LineOfScrimmage,
            replayFrame.FirstDownLine,
            offensiveTeam.Name,
            offensiveTeam.PrimaryColor,
            defensiveTeam.Name,
            defensiveTeam.PrimaryColor,
            currentStage,
            crowdState, replayFrame.Down);

        FootballRenderer.DrawGroundShadow(replayFrame.Ball.Position, replayFrame.Ball.State,
            GetBallArcHeight(replayFrame.Ball, replayFrame.Ball.Position));

        foreach (var receiver in replayFrame.Receivers)
        {
            DrawReplayActor(receiver);
        }

        foreach (var blocker in replayFrame.Blockers)
        {
            DrawReplayActor(blocker);
        }

        foreach (var defender in replayFrame.Defenders)
        {
            DrawReplayActor(defender);
        }

        DrawReplayActor(replayFrame.Quarterback);
        DrawReplayBall(replayFrame.Ball, replayFrame);

        _hudRenderer.DrawScoreboard(playManager, lastPlayText, GameState.Replay, offensiveTeam, defensiveTeam, currentStage, driveSummaryScrollOffsetFromLatest);
        _hudRenderer.DrawSidePanel(playManager, lastPlayText, "-", GameState.Replay, currentStage, replayAvailable);
        _replayOverlayRenderer.DrawReplayBadge(isPaused);
        RetroScreenOverlay.Draw();
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

            var points = RouteVisualizer.GetRouteWaypoints(receiver);
            if (points.Count < 2) continue;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = Constants.WorldToScreen(points[i]);
                Vector2 b = Constants.WorldToScreen(points[i + 1]);
                DrawRetroRouteSegment(a, b, i == points.Count - 2);
            }
        }

        OffensiveLinemanAI.DrawRoutes(
            blockers.ToList(),
            playManager.SelectedPlay,
            playManager.LineOfScrimmage);
    }

    private static void DrawRetroRouteSegment(Vector2 a, Vector2 b, bool drawArrow)
    {
        Vector2 delta = b - a;
        float length = delta.Length();
        if (length < 0.5f) return;

        Vector2 dir = delta / length;
        Raylib.DrawLineEx(a + new Vector2(1, 2), b + new Vector2(1, 2), 4f, new Color(2, 20, 10, 130));

        const float dash = 7f;
        const float gap = 3f;
        for (float start = 0; start < length; start += dash + gap)
        {
            Vector2 p1 = a + dir * start;
            Vector2 p2 = a + dir * MathF.Min(start + dash, length);
            Raylib.DrawLineEx(p1, p2, 2f, Palette.Yellow);
        }

        if (!drawArrow) return;
        Vector2 side = new(-dir.Y, dir.X);
        Vector2 tip = b;
        Vector2 back = b - dir * 9f;
        Raylib.DrawTriangle(tip, back + side * 5f, back - side * 5f, Palette.Yellow);
        Raylib.DrawTriangle(tip - dir * 2f, back - side * 3f, back + side * 3f, new Color(255, 236, 145, 255));
    }

    private void DrawReceiverPriorityLabels(IReadOnlyList<Receiver> receivers)
    {
        if (receivers.Count == 0) return;

        foreach (var receiver in receivers)
        {
            if (!receiver.Eligible) continue;

            string priorityLabel = _priorityManager.GetPriorityLabel(receiver.Index);
            if (priorityLabel == "-") continue;

            Vector2 center = Constants.WorldToScreen(receiver.Position);
            int fontSize = 16;
            int textWidth = Raylib.MeasureText(priorityLabel, fontSize);

            // Position at upper-right of the player circle
            int offsetX = 8;
            int offsetY = -18;
            int drawX = (int)center.X + offsetX;
            int drawY = (int)center.Y + offsetY;

            // Arcade keycap: clearer than floating text when routes and players overlap.
            int keyWidth = textWidth + 8;
            Raylib.DrawRectangle(drawX + 2, drawY + 2, keyWidth, fontSize + 5, new Color(0, 0, 0, 120));
            Raylib.DrawRectangle(drawX, drawY, keyWidth, fontSize + 5, new Color(18, 24, 34, 245));
            Raylib.DrawRectangleLines(drawX, drawY, keyWidth, fontSize + 5, Palette.Gold);
            Raylib.DrawText(priorityLabel, drawX + 4, drawY + 2, fontSize, Palette.Gold);
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

    public static void DrawDriveOverBanner(string titleText, string detailText, string playText, string subText)
    {
        OverlayFrame frame = OverlayChromeRenderer.DrawWindowCentered(
            preferredWidth: 820,
            preferredHeight: 210,
            accent: Palette.Gold,
            variant: OverlayVariant.Modal,
            horizontalMargin: 96,
            verticalMargin: 120,
            drawScrim: true);

        int bannerWidth = frame.Width;
        int bannerHeight = frame.Height;
        int x = frame.X;
        int y = frame.Y;

        string title = string.IsNullOrWhiteSpace(titleText) ? "DRIVE OVER" : titleText.ToUpperInvariant();
        int titleSize = GetFittedFontSize(title, 40, bannerWidth - 48, 20);
        int titleWidth = Raylib.MeasureText(title, titleSize);
        int titleX = x + (bannerWidth - titleWidth) / 2;
        int titleY = y + 18;
        Raylib.DrawText(title, titleX, titleY, titleSize, Palette.Gold);

        int textX = x + 28;
        int maxTextWidth = bannerWidth - 56;
        int detailY = titleY + titleSize + 22;

        if (!string.IsNullOrWhiteSpace(detailText))
        {
            foreach (string line in WrapTextToWidth(detailText, 20, maxTextWidth))
            {
                Raylib.DrawText(line, textX, detailY, 20, Palette.White);
                detailY += 24;
            }
        }

        if (!string.IsNullOrWhiteSpace(playText))
        {
            detailY += 4;
            foreach (string line in WrapTextToWidth(playText, 16, maxTextWidth))
            {
                Raylib.DrawText(line, textX, detailY, 16, new Color(210, 210, 218, 255));
                detailY += 20;
            }
        }

        string sub = string.IsNullOrWhiteSpace(subText) ? "PRESS ENTER" : subText.ToUpperInvariant();
        int subSize = GetFittedFontSize(sub, 18, bannerWidth - 40, 12);
        int subWidth = Raylib.MeasureText(sub, subSize);
        Raylib.DrawText(sub, x + (bannerWidth - subWidth) / 2, y + bannerHeight - subSize - 14, subSize, Palette.White);
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

    private static List<string> WrapTextToWidth(string text, int fontSize, int maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return lines;
        }

        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return lines;
        }

        string currentLine = words[0];
        for (int i = 1; i < words.Length; i++)
        {
            string candidate = $"{currentLine} {words[i]}";
            if (Raylib.MeasureText(candidate, fontSize) <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            lines.Add(currentLine);
            currentLine = words[i];
        }

        lines.Add(currentLine);
        return lines;
    }

    private static void DrawReplayActor(ReplayActorFrame actor)
    {
        Vector2 screen = Constants.WorldToScreen(actor.Position);
        PixelPlayerRenderer.Draw(screen, actor.Velocity, actor.Glyph, actor.Color, actor.Visual);
    }

    private static void DrawReplayBall(ReplayBallFrame ball, ReplayFrame frame)
    {
        ReplayActorFrame? holder = ball.State is BallState.HeldByQB or BallState.HeldByReceiver
            ? FindActorById(frame, ball.HolderId) : null;
        FootballRenderer.Draw(ball.Position, ball.Velocity, ball.State,
            GetBallArcHeight(ball, ball.Position), ball.AirTime, holder?.Position, holder?.Velocity, holder?.Visual ?? default);
    }

    private static ReplayActorFrame? FindActorById(ReplayFrame frame, int actorId)
    {
        if (frame.Quarterback.Id == actorId)
        {
            return frame.Quarterback;
        }

        foreach (var receiver in frame.Receivers)
        {
            if (receiver.Id == actorId)
            {
                return receiver;
            }
        }

        foreach (var blocker in frame.Blockers)
        {
            if (blocker.Id == actorId)
            {
                return blocker;
            }
        }

        foreach (var defender in frame.Defenders)
        {
            if (defender.Id == actorId)
            {
                return defender;
            }
        }

        return null;
    }

    private static float GetBallArcHeight(ReplayBallFrame ball, Vector2 drawPos)
    {
        if (ball.State != BallState.InAir || ball.ArcApexHeight <= 0f)
        {
            return 0f;
        }

        float progress = 1f;
        if (ball.IntendedDistance > 0.01f)
        {
            progress = Math.Clamp(Vector2.Distance(ball.ThrowStart, drawPos) / ball.IntendedDistance, 0f, 1f);
        }

        return ball.ArcApexHeight * 4f * progress * (1f - progress);
    }

}
