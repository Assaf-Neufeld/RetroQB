using System.Numerics;
using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Entities;
using RetroQB.Gameplay.Replay;
using RetroQB.AI;

namespace RetroQB.Rendering;

public sealed class FullMatchRenderer
{
    private readonly FieldRenderer _field = new();
    private readonly TimedScoreboardRenderer _scoreboard = new();
    public void Draw(FullMatchSession session, TimedSeason? season = null, bool returnToMenu = false)
    {
        bool previous = Constants.OffenseDownScreen;
        Constants.OffenseDownScreen = session.Replay.Clip?.MatchContext is { } replay
            ? replay.OffenseId != replay.UserId : session.Drive.PreparedOffenseId != session.Match.User.Definition.Id;
        try { DrawMatch(session, season, returnToMenu); }
        finally { Constants.OffenseDownScreen = previous; }
    }

    private void DrawMatch(FullMatchSession session, TimedSeason? season, bool returnToMenu)
    {
        var d = session.Drive; var m = session.Match; var c = session.Clock;
        var frame = session.Replay.CurrentFrame; var recorded = session.Replay.Clip?.MatchContext;
        var offense = TeamCatalog.Get(recorded?.OffenseId ?? d.PreparedOffenseId);
        var defense = TeamCatalog.Get(recorded?.DefenseId ?? m.Other(d.PreparedOffenseId).Definition.Id);
        bool defending = recorded != null ? recorded.ControlledDefender != null : d.Execution.Control.HumanOnDefense;
        var assignment = recorded?.Assignments ?? d.SelectedDefense;
        Constants.UpdateFieldRect(); Raylib.ClearBackground(Palette.Background);
        var scored = d.LastResult is { Points: > 0 } result ? result : null;
        bool homeCheers = scored?.ScoringTeamId == m.User.Definition.Id;
        float celebration = scored == null ? 0 : Math.Clamp(1 - (float)session.PresentationSeconds / 3, 0, 1);
        var crowd = new CrowdBackdropState(homeCheers ? celebration : .25f, homeCheers ? .25f : celebration, .35f + celebration * .6f,
            homeCheers ? m.User.Definition.Name.ToUpperInvariant() : "", celebration, .5f, .5f,
            (float)session.PresentationSeconds, celebration, .5f, homeCheers, (float)(180 - c.RemainingSeconds));
        _field.DrawField(frame?.LineOfScrimmage ?? d.Plays.LineOfScrimmage, frame?.FirstDownLine ?? d.Plays.FirstDownLine,
            m.User.Definition.Name, m.User.Definition.PrimaryColor, m.Opponent.Definition.Name, m.Opponent.Definition.PrimaryColor, recorded?.Stage ?? m.Stage,
            frame != null ? default : crowd, frame?.Down ?? m.Series.Down, showPlayMarkers: frame != null || session.SpecialTeams == null);
        if (frame != null)
        {
            foreach (var actor in new[] { frame.Quarterback }.Concat(frame.Receivers).Concat(frame.Blockers).Concat(frame.Defenders))
                PixelPlayerRenderer.Draw(Constants.WorldToScreen(actor.Position), actor.Velocity, actor.Glyph, actor.Color, actor.Visual);
            ReplayActorFrame? holder = FindBallHolder(frame);
            float ballHeight = GetBallArcHeight(frame.Ball);
            FootballRenderer.DrawGroundShadow(frame.Ball.Position, frame.Ball.State, ballHeight);
            FootballRenderer.Draw(frame.Ball.Position, frame.Ball.Velocity, frame.Ball.State,
                ballHeight, frame.Ball.AirTime, holder?.Position, holder?.Velocity, holder?.Visual ?? default);
            Vector2 controlled = recorded?.ControlledIndex is >= 0 and var index && index < frame.Defenders.Count
                ? frame.Defenders[index].Position : frame.Receivers.Where(r => r.Id == frame.Ball.HolderId)
                    .Select(r => r.Position).DefaultIfEmpty(frame.Quarterback.Position).First();
            Ring(controlled);
        }
        else if (session.SpecialTeams is { } teams)
            SpecialTeamsRenderer.Draw(teams, offense.PrimaryColor, defense.PrimaryColor, session.UserReceivingKick);
        else if (session.Kick != null)
            FieldGoalRenderer.DrawOnField(session.Kick, d.Plays.LineOfScrimmage, offense.PrimaryColor, defense.PrimaryColor);
        else if (session.Browser.IsOpen && session.Browser.Actors is { } preview)
        {
            var rect = Constants.FieldRect;
            Raylib.BeginScissorMode((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
            Gameplay.Controllers.DrawingController.DrawRouteOverlay(preview.Receivers, preview.Blockers, session.Browser.Preview!, d.Plays.LineOfScrimmage);
            foreach (var actor in preview.Receivers.Cast<Entity>().Concat(preview.Blockers).Append(preview.Qb)) actor.Draw();
            foreach (var defender in d.Actors.Defenders) defender.Draw();
            preview.Ball.Draw();
            Raylib.EndScissorMode();
        }
        else
        {
            if (!d.Live && d.LastResult == null && defending) DefensivePlayRenderer.DrawAssignments(assignment);
            if (!d.Live && d.LastResult == null && !defending)
            {
                var rect = Constants.FieldRect;
                Raylib.BeginScissorMode((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                RetroQB.Gameplay.Controllers.DrawingController.DrawRouteOverlay(d.Actors.Receivers, d.Actors.Blockers, d.Plays);
                Raylib.EndScissorMode();
            }
            foreach (var actor in d.Players) actor.Draw();
            d.Actors.Ball.Draw();
            Ring(defending ? d.Linebacker.Position : d.Actors.Ball.Holder?.Position ?? d.Actors.Qb.Position);
            if (!defending) DrawReceiverLabels(d);
        }
        if (defending && ShowsScrimmageCoverage(session)) DefensivePlayRenderer.DrawCoverage(recorded?.CompiledPlan ?? (frame == null ? d.CompiledPlan : null),
            frame?.DefensiveDecision ?? (frame == null ? d.Execution.DefenseRuntime?.Current : null));
        MatchPlayPanelRenderer.Draw(session, defending, season != null);
        _scoreboard.Draw(session);
        bool superBowlVictory = season?.SuperBowlVictory == true && frame == null && c.Suspension == ClockSuspension.None;
        bool stageVictory = season?.StageVictory == true && frame == null && c.Suspension == ClockSuspension.None;
        if (superBowlVictory)
            new BannerRenderer().DrawSuperBowlVictoryBanner(m.User.Score, m.Opponent.Score,
                m.User.Stats, season!.PreviewResult());
        else if (stageVictory)
            new BannerRenderer().DrawStageCompleteBanner(m.User.Score, m.Opponent.Score, season!.Stage,
                m.User.Stats, season.PreviewResult(), season.Leaderboard);
        else if (frame == null && d.LastResult?.DriveEnded == true && !session.ShowStatistics) DrawDriveSummary(session);
        if (season?.Pregame == true || season?.Complete == true) DrawSeason(season, returnToMenu);
        if (session.ShowStatistics) DrawStatistics(session, season);
        else if (session.Browser.IsOpen && frame == null) PlaybookBrowserRenderer.Draw(session.Browser, d.Plays.SelectedPlay);
        if (!session.ShowStatistics && (c.Suspension & (ClockSuspension.Pause | ClockSuspension.FocusLoss)) != 0)
        {
            var pause = OverlayChromeRenderer.DrawWindowCentered(460, 190, Palette.PanelLine, OverlayVariant.Modal, 40, 40);
            var text = new HudContent(new(pause.X + 8, pause.Y + 8, pause.Width - 16, pause.Height - 16), HudLayout.Current);
            text.Y = pause.Y + 30;
            text.Line("PAUSED", size: 26);
            text.Wrapped((c.Suspension & ClockSuspension.FocusLoss) != 0 ? "Return to the game window to continue." : "Esc: resume | Tab: statistics", Palette.Muted);
            text.Line("Game and preparation clocks are suspended.", Palette.Muted, HudLayout.Current.Hint);
        }
    }

    internal static bool ShowsScrimmageCoverage(FullMatchSession session)
        => session.Replay.CurrentFrame != null || session.Action == MatchAction.Scrimmage
            && session.SpecialTeams == null && session.Kick == null;

    internal static string SidebarControls(FullMatchSession session, bool defending)
        => MatchPlayPanelRenderer.Controls(session, defending);

    private static void DrawDriveSummary(FullMatchSession session)
    {
        var summary = CompletedDriveSummary.From(session)!;
        var panel = OverlayChromeRenderer.DrawWindowCentered(620, 290, Palette.Gold, OverlayVariant.Modal, 40, 40);
        int x = panel.X, y = panel.Y, width = panel.Width;
        void Line(string text, int offset, int size, Color color)
        {
            while (size > 12 && Raylib.MeasureText(text, size) > width - 40) size--;
            Raylib.DrawText(text, x + 20, y + offset, size, color);
        }
        Line(summary.TeamName.ToUpperInvariant() + " | DRIVE RESULT", 20, 19, Palette.Cyan);
        Line(summary.Outcome, 62, 28, Palette.Gold);
        Line($"{summary.Plays} {(summary.Plays == 1 ? "PLAY" : "PLAYS")}   {summary.Yards:0} YARDS   {summary.Points} POINTS", 108, 21, Palette.White);
        Line(summary.Next, 153, 20, Palette.White);
        string controls = session.Drive.LastReplay != null
            ? session.Timed.Finished ? "Enter: accept result | F: replay | Tab: stats" : "Space: continue | F: replay | Tab: stats"
            : session.Timed.Finished ? "Enter: accept result | Tab: stats" : "Space: continue | Tab: stats";
        Line(controls, 218, 17, Palette.Gold);
    }

    private static void Ring(Vector2 world)
    {
        var p = Constants.WorldToScreen(world); Raylib.DrawCircleLines((int)p.X, (int)p.Y, 13, Palette.Gold);
    }

    private static void DrawReceiverLabels(DefensiveDrive drive)
    {
        var field = Constants.FieldRect;
        var labels = new List<Rectangle>();
        foreach (var receiver in drive.Actors.Receivers.Where(r => r.Eligible))
        {
            var point = Constants.WorldToScreen(receiver.Position);
            string label = drive.ReceiverLabel(receiver.Index);
            int width = Raylib.MeasureText(label, 14) + 6;
            Rectangle box = default;
            foreach (var offset in new[] { new Vector2(9, -23), new Vector2(-width - 9, -23), new Vector2(9, 12), new Vector2(-width - 9, 12), new Vector2(9, -43) })
            {
                box = new(Math.Clamp(point.X + offset.X, field.X + 2, field.X + field.Width - width - 2),
                    Math.Clamp(point.Y + offset.Y, field.Y + 2, field.Y + field.Height - 20), width, 18);
                if (!labels.Any(r => Raylib.CheckCollisionRecs(r, box))) break;
            }
            labels.Add(box);
            Raylib.DrawRectangleRec(box, Palette.HudSurface);
            Raylib.DrawText(label, (int)box.X + 3, (int)box.Y + 2, 14, Palette.Gold);
        }
    }

    private static ReplayActorFrame? FindBallHolder(ReplayFrame frame)
    {
        if (frame.Ball.State == BallState.HeldByQB && frame.Quarterback.Id == frame.Ball.HolderId)
            return frame.Quarterback;
        if (frame.Ball.State == BallState.HeldByReceiver)
            foreach (var receiver in frame.Receivers)
                if (receiver.Id == frame.Ball.HolderId) return receiver;
        return null;
    }

    private static float GetBallArcHeight(ReplayBallFrame ball)
    {
        if (ball.State != BallState.InAir || ball.ArcApexHeight <= 0f) return 0f;
        float progress = ball.IntendedDistance > 0.01f
            ? Math.Clamp(Vector2.Distance(ball.ThrowStart, ball.Position) / ball.IntendedDistance, 0f, 1f)
            : 1f;
        return ball.ArcApexHeight * 4f * progress * (1f - progress);
    }

    private static void Panel(string heading)
    {
        OverlayChromeRenderer.DrawWindowCentered(Raylib.GetScreenWidth() - 30, Raylib.GetScreenHeight() - 30,
            Palette.PanelLine, OverlayVariant.Modal, 30, 30);
        Raylib.DrawText(heading, 40, 40, 25, Palette.Gold);
    }

    private static void DrawStatistics(FullMatchSession session, TimedSeason? season)
    {
        var layout = HudLayout.Current;
        var panel = OverlayChromeRenderer.DrawWindowCentered(Math.Min(1200, layout.ScreenWidth - 40),
            Math.Min(800, layout.ScreenHeight - 40), Palette.PanelLine, OverlayVariant.Modal, 40, 40);
        GameplayHud.Text("MATCH STATISTICS", panel.X + 24, panel.Y + 22, panel.Width - 48, 22);
        int width = (panel.Width - 64) / 2;
        int x = panel.X + 24;
        foreach (var team in new[] { session.Match.User, session.Match.Opponent })
        {
            var offense = team.Stats; var defense = team.DefenseStats;
            Raylib.DrawRectangle(x, panel.Y + 62, width, 3, team.Definition.PrimaryColor);
            var text = new HudContent(new(x - 14, panel.Y + 62, width + 28, panel.Height - 120), layout) { Y = panel.Y + 76 };
            text.Line($"{team.Definition.Name}  {team.Score}", size: 20);
            text.Section("OFFENSE");
            text.Line($"Passing: {offense.Qb.Completions}/{offense.Qb.Attempts}, {offense.Qb.PassYards} yd");
            text.Line($"Pass TD: {offense.Qb.PassTds} | INT: {offense.Qb.Interceptions}");
            text.Line($"Sacks allowed: {offense.Qb.Sacks}");
            text.Line($"QB rush: {offense.Qb.RushAttempts} for {offense.Qb.RushYards} yd");
            text.Line($"RB rush: {offense.Rb.Attempts} for {offense.Rb.Yards} yd");
            text.Section("DEFENSE");
            text.Line($"Tackles: {defense.Tackles} | Controlled LB: {defense.ControlledTackles}");
            text.Line($"Sacks: {defense.Sacks} | INT: {defense.Interceptions} | PD: {defense.PassesDefended}");
            text.Line($"Yards allowed: {defense.YardsAllowed}");
            text.Line($"Points allowed: {defense.PointsAllowed}");
            text.Line($"3rd / 4th stops: {defense.ThirdDownStops} / {defense.FourthDownStops}");
            text.Section("CONTROLLED LINEBACKER");
            text.Line($"{defense.ControlledSacks} SK | {defense.ControlledInterceptions} INT | {defense.ControlledPassesDefended} PD", Palette.Gold);
            foreach (var player in defense.Players.Where(p => p.Tackles + p.Sacks + p.Interceptions + p.PassesDefended > 0)
                .OrderByDescending(p => p.Tackles + p.Sacks + p.Interceptions + p.PassesDefended).Take(5))
                text.Line($"{player.Slot}: {player.Tackles} TKL | {player.Sacks} SK | {player.Interceptions} INT | {player.PassesDefended} PD", Palette.Muted, layout.Hint);
            x += width + 16;
        }
        if (season != null)
            GameplayHud.Text($"Accepted games: {season.Completed.Count} | {season.Summary.BuildThreeStageScoreHistory()}", panel.X + 24, panel.Y + panel.Height - 58, panel.Width - 48, layout.Hint, Palette.Muted);
        GameplayHud.Text("Tab: return", panel.X + 24, panel.Y + panel.Height - 34, panel.Width - 48, layout.Body, Palette.Gold);
    }

    private static void DrawSeason(TimedSeason season, bool returnToMenu)
    {
        if (season.Pregame)
        {
            PregameRenderer.Draw(season.Team.Offense, season.Current.Match.Opponent.DefensiveAttributes, season.Stage,
                season.Current.Clock.RegulationPeriods == 2
                    ? "TWO 2-MINUTE HALVES  /  YOU RECEIVE FIRST  /  NAME ENTRY AFTER THE SEASON"
                    : "FOUR TIMED QUARTERS  /  PLAY OFFENSE AND DEFENSE");
            return;
        }
        if (season.Complete && !season.Saved)
        {
            var teams = OffensiveTeamPresets.GetMenuTeams(true);
            new MenuRenderer().DrawNameEntry(teams.ToList().FindIndex(t => t.Name == season.Team.Name), teams, season.PlayerName,
                season.StorageMessage, season.Leaderboard, true);
            return;
        }
        Panel(season.Complete ? season.Summary.IsChampion ? "SUPER BOWL CHAMPION" : "SEASON COMPLETE" : season.Stage.GetDisplayName());
        int y = 100;
        void Line(string text, Color? color = null)
        {
            int size = 20;
            while (size > 12 && Raylib.MeasureText(text, size) > Raylib.GetScreenWidth() - 80) size--;
            Raylib.DrawText(text, 40, y, size, color ?? Palette.White); y += 38;
        }
        Line($"{season.PlayerName} | {season.Team.Name}");
        Line($"Offensive dominance score: {season.Summary.ComputeDominanceScore():0.0}", Palette.Gold);
        Line("Saved to the timed-season leaderboard", Palette.Gold);
        foreach (var entry in season.Leaderboard.Entries.Take(5)) Line($"#{entry.Rank} {entry.Name} | {entry.TeamName} | {entry.Score:0.0}");
        Line(season.Summary.BuildThreeStageScoreHistory(), Palette.Gold);
        if (season.Completed.Count > 0)
        {
            Line($"Season defense: {season.Completed.Sum(g => g.Defense.Tackles)} tackles, {season.Completed.Sum(g => g.Defense.Sacks)} sacks, {season.Completed.Sum(g => g.Defense.Interceptions)} interceptions");
            Line($"Controlled linebacker tackles: {season.Completed.Sum(g => g.Defense.ControlledTackles)}");
        }
        if (season.Complete) Line("Tab: inspect final match statistics");
        if (season is { Complete: true, Saved: true } && returnToMenu) Line("Enter: return to team selection");
    }
}
