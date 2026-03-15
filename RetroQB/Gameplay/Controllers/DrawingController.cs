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
        GameState gameState,
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
        bool isPostSeasonNameEntry,
        LeaderboardSummary leaderboardSummary,
        bool showMenuLeaderboard,
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
            crowdState);

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
        _hudRenderer.DrawScoreboard(playManager, lastPlayText, gameState, offensiveTeam, defensiveTeam, currentStage, driveSummaryScrollOffsetFromLatest);
        _hudRenderer.DrawSidePanel(playManager, lastPlayText, targetLabel, gameState, currentStage, replayAvailable);

        if (gameState == GameState.DriveOver)
        {
            DrawDriveOverBanner(driveOverText, "PRESS ENTER FOR NEXT DRIVE");
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
            _hudRenderer.DrawMainMenu(selectedTeamIndex, OffensiveTeamPresets.All, leaderboardSummary, showMenuLeaderboard);
        }

        if (gameState == GameState.PlayerNameEntry)
        {
            _hudRenderer.DrawPlayerNameEntry(selectedTeamIndex, OffensiveTeamPresets.All, nameInput, nameEntryMessage, leaderboardSummary, isPostSeasonNameEntry);
        }

        if (gameState == GameState.NameConflict)
        {
            _hudRenderer.DrawNameConflict(selectedTeamIndex, OffensiveTeamPresets.All, pendingPlayerName);
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
            crowdState);

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
                Raylib.DrawLineEx(a, b, 2.0f, Palette.Yellow);
            }
        }

        OffensiveLinemanAI.DrawRoutes(
            blockers.ToList(),
            playManager.SelectedPlay,
            playManager.LineOfScrimmage);
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

            // Drop shadow + gold text
            Raylib.DrawText(priorityLabel, drawX + 1, drawY + 1, fontSize, new Color(10, 10, 14, 180));
            Raylib.DrawText(priorityLabel, drawX, drawY, fontSize, Palette.Gold);
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
        int titleSize = GetFittedFontSize(title, 40, bannerWidth - 48, 20);
        int titleWidth = Raylib.MeasureText(title, titleSize);
        int titleX = x + (bannerWidth - titleWidth) / 2;
        int titleY = y + 18;
        Raylib.DrawText(title, titleX, titleY, titleSize, Palette.Gold);

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

    private static void DrawReplayActor(ReplayActorFrame actor)
    {
        Vector2 screen = Constants.WorldToScreen(actor.Position);

        float baseRadius = actor.Glyph switch
        {
            "OL" or "DL" => 11.5f,
            "DE" or "LB" or "TE" => 10.5f,
            "QB" => 10f,
            "WR" or "RB" or "DB" => 9.5f,
            _ => 10f
        };

        Raylib.DrawCircleV(screen + new Vector2(1f, 1.5f), baseRadius, new Color(10, 10, 12, 120));
        Raylib.DrawCircleV(screen, baseRadius, actor.Color);
        Raylib.DrawCircleLines((int)screen.X, (int)screen.Y, baseRadius, new Color(16, 16, 20, 220));

        int fontSize = 12;
        int textWidth = Raylib.MeasureText(actor.Glyph, fontSize);
        int labelX = (int)screen.X - textWidth / 2;
        int labelY = (int)screen.Y - fontSize / 2;
        Raylib.DrawText(actor.Glyph, labelX + 1, labelY + 1, fontSize, new Color(10, 10, 12, 160));
        Raylib.DrawText(actor.Glyph, labelX, labelY, fontSize, Palette.White);
    }

    private static void DrawReplayBall(ReplayBallFrame ball, ReplayFrame frame)
    {
        Vector2 drawPos = ball.Position;
        if (ball.State is BallState.HeldByQB or BallState.HeldByReceiver)
        {
            ReplayActorFrame? holder = FindActorById(frame, ball.HolderId);
            if (holder.HasValue)
            {
                drawPos = holder.Value.Position + new Vector2(0.8f, 0f);
            }
        }

        Vector2 screen = Constants.WorldToScreen(drawPos);

        float height = GetBallArcHeight(ball, drawPos);
        if (height > 0f)
        {
            float heightPixels = (height / Constants.FieldLength) * Constants.FieldRect.Height;
            screen.Y -= heightPixels;
        }

        float majorRadius = 7f;
        float minorRadius = 4.5f;
        if (height > 0f)
        {
            float scale = 1f + height * 0.06f;
            majorRadius *= scale;
            minorRadius *= scale;
        }

        float angle = 0f;
        if (ball.State == BallState.InAir && ball.Velocity.LengthSquared() > 0.1f)
        {
            Vector2 screenVel = new Vector2(ball.Velocity.X, -ball.Velocity.Y);
            angle = MathF.Atan2(screenVel.Y, screenVel.X);
        }

        Vector2 major = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        Vector2 minor = new Vector2(-major.Y, major.X);

        Color brown = new Color(139, 90, 43, 255);
        Color darkBrown = new Color(100, 60, 25, 255);
        Color laceWhite = new Color(230, 230, 230, 255);

        Vector2 shadowOff = new Vector2(1f, 2f);
        DrawFootballBody(screen + shadowOff, major, minor, majorRadius, minorRadius, new Color(10, 10, 12, 100));
        DrawFootballBody(screen, major, minor, majorRadius, minorRadius, brown);

        Vector2 tipFront = screen + major * majorRadius;
        Vector2 tipBack = screen - major * majorRadius;
        Vector2 tipPerp = minor * (minorRadius * 0.4f);
        Raylib.DrawTriangle(tipFront, screen + major * (majorRadius * 0.6f) - tipPerp, screen + major * (majorRadius * 0.6f) + tipPerp, darkBrown);
        Raylib.DrawTriangle(tipBack, screen - major * (majorRadius * 0.6f) + tipPerp, screen - major * (majorRadius * 0.6f) - tipPerp, darkBrown);

        float laceLen = majorRadius * 0.5f;
        Vector2 laceStart = screen - major * laceLen;
        Vector2 laceEnd = screen + major * laceLen;
        Raylib.DrawLineEx(laceStart, laceEnd, 1f, laceWhite);

        int stitchCount = 3;
        float stitchH = minorRadius * 0.35f;
        for (int i = 0; i < stitchCount; i++)
        {
            float t = (i + 0.5f) / stitchCount;
            Vector2 stitchCenter = Vector2.Lerp(laceStart, laceEnd, t);
            Raylib.DrawLineV(stitchCenter - minor * stitchH, stitchCenter + minor * stitchH, laceWhite);
        }
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

    private static void DrawFootballBody(Vector2 center, Vector2 major, Vector2 minor, float majorR, float minorR, Color color)
    {
        const int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float a1 = (i / (float)segments) * MathF.PI * 2f;
            float a2 = ((i + 1) / (float)segments) * MathF.PI * 2f;

            Vector2 p1 = center + major * (MathF.Cos(a1) * majorR) + minor * (MathF.Sin(a1) * minorR);
            Vector2 p2 = center + major * (MathF.Cos(a2) * majorR) + minor * (MathF.Sin(a2) * minorR);

            Raylib.DrawTriangle(center, p2, p1, color);
        }
    }
}
