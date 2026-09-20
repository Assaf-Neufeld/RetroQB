using System.Numerics;
using Raylib_cs;
using RetroQB.Gameplay;
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
            Raylib.DrawCircleV(Constants.WorldToScreen(frame.Ball.Position), 3, Palette.White);
            Vector2 controlled = recorded?.ControlledIndex is >= 0 and var index && index < frame.Defenders.Count
                ? frame.Defenders[index].Position : frame.Receivers.Where(r => r.Id == frame.Ball.HolderId)
                    .Select(r => r.Position).DefaultIfEmpty(frame.Quarterback.Position).First();
            Ring(controlled);
        }
        else if (session.SpecialTeams is { } teams)
            SpecialTeamsRenderer.Draw(teams, offense.PrimaryColor, defense.PrimaryColor, session.UserReceivingKick);
        else if (session.Kick != null)
            FieldGoalRenderer.DrawOnField(session.Kick, d.Plays.LineOfScrimmage, offense.PrimaryColor, defense.PrimaryColor);
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
            if (!defending) foreach (var receiver in d.Actors.Receivers.Where(r => r.Eligible && !r.IsBlocking))
            {
                var p = Constants.WorldToScreen(receiver.Position);
                Raylib.DrawText(d.ReceiverLabel(receiver.Index), (int)p.X + 8, (int)p.Y - 12, 15, Palette.Gold);
            }
        }
        float scale = Math.Min(1, Raylib.GetScreenHeight() / 900f);
        int right = Raylib.GetScreenWidth() - 310;
        void Text(string text, int x, int y, int size = 17, Color? color = null)
        {
            int fontSize = Math.Max(11, (int)(size * scale));
            while (fontSize > 11 && text.Split('\n').Any(line => Raylib.MeasureText(line, fontSize) > 290)) fontSize--;
            Raylib.DrawText(text, x, (int)(y * scale), fontSize, color ?? Palette.White);
        }
        Text(frame != null ? "REPLAY" : session.SpecialTeams is { } st ? st.IsKickoff ? "KICKOFF" : "PUNT" : defending ? "CALL DEFENSE" : "CALL OFFENSE", 20, 28, 25, Palette.Gold);
        if (session.SpecialTeams is { } special && frame == null)
        {
            Text(session.UserReceivingKick ? "YOU: KICK RETURNER" : "YOU: COVERAGE LINEBACKER", 20, 100, 18, Palette.Gold);
            Text(special.Phase.ToString().ToUpperInvariant(), 20, 155, 22);
            Text("Space: snap / kick / ready\nWASD / arrows: move\nShift: sprint\nReturn the kick or make the tackle", 20, 215, 16);
            if (special.Phase == SpecialTeamsPhase.Setup && !special.IsKickoff && !session.UserReceivingKick)
                Text("Choose a pass/run to cancel punt", 20, 335, 15);
            if (special.Result is { } kr) Text(kr.Touchback ? "TOUCHBACK" : $"RETURN: {kr.ReturnYards} YARDS", 20, 390, 20, Palette.Gold);
        }
        else if (session.Kick != null && frame == null)
        {
            if (!session.HumanOnDefense) FieldGoalRenderer.DrawHud(session.Kick, timedMatch: true);
            else Text($"CPU FIELD GOAL\n{session.Kick.Distance:0} YARDS\n{session.Kick.Result}", 20, 100);
        }
        else if (defending)
        {
            for (int i = 0; i < 10; i++)
                Text($"{(i + 1) % 10} {DefensivePlaybook.All[i].Name}", 20, 95 + i * 29, 16,
                    assignment.Definition == DefensivePlaybook.All[i] ? Palette.Gold : Palette.White);
            Text($"YOU: {assignment.ControlledSlot}", 20, 415, 20, Palette.Gold);
            Text(assignment.Assignments.Single(a => a.Slot == assignment.ControlledSlot).Responsibility, 20, 450);
            if (frame == null) Text($"CPU snap in {Math.Max(0, session.SnapRemaining):0.0}s", 20, 520);
        }
        else if (frame != null)
        {
            Text(recorded?.OffensiveCall ?? "OFFENSIVE REPLAY", 20, 100);
            Text("Recorded teams and clock\nSpace / F: return", 20, 170);
        }
        else
        {
            for (int i = 0; i < d.Plays.PassPlays.Count; i++) Text($"{(i + 1) % 10} {d.Plays.PassPlays[i].Name}", 20, 85 + i * 22, 13);
            const string keys = "QWERTYUIOP";
            for (int i = 0; i < d.Plays.RunPlays.Count; i++) Text($"{keys[i]} {d.Plays.RunPlays[i].Name}", 20, 320 + i * 22, 13);
            Text(session.Action == MatchAction.Scrimmage ? d.Plays.SelectedPlay.Name : session.Action.ToString(), 20, 570, 16, Palette.Gold);
        }
        Text("Space: snap / continue\nWASD / arrows: move | Shift: sprint\n1-5 live: throw | X: flip\nK: kick  B: punt  V: kneel\nC: timeout  Esc: pause\nF: replay  Z: restart\nTab: statistics | PgUp/PgDn: history", 20, 695, 15);
        _scoreboard.Draw(session);
        if (frame == null && d.LastResult?.DriveEnded == true && !session.ShowStatistics) DrawDriveSummary(session);
        if (season?.Pregame == true || season?.Complete == true) DrawSeason(season, returnToMenu);
        else if (season != null && session.Timed.Finished) Text("Enter: accept result\nZ: restart this matchup\nTab: inspect statistics", right, 820, 15, Palette.Gold);
        if (session.ShowStatistics) DrawStatistics(session, season);
    }

    private static void DrawDriveSummary(FullMatchSession session)
    {
        var summary = CompletedDriveSummary.From(session)!;
        int width = Math.Min(600, Raylib.GetScreenWidth() - 40), height = 274;
        int x = (Raylib.GetScreenWidth() - width) / 2, y = (Raylib.GetScreenHeight() - height) / 2;
        Raylib.DrawRectangle(x + 5, y + 5, width, height, new Color(0, 0, 0, 150));
        Raylib.DrawRectangle(x, y, width, height, new Color(12, 20, 30, 248));
        Raylib.DrawRectangleLinesEx(new(x, y, width, height), 2, Palette.Gold);
        void Line(string text, int offset, int size, Color color)
        {
            while (size > 12 && Raylib.MeasureText(text, size) > width - 40) size--;
            Raylib.DrawText(text, x + 20, y + offset, size, color);
        }
        Line(summary.TeamName.ToUpperInvariant() + " | DRIVE RESULT", 20, 19, Palette.Cyan);
        Line(summary.Outcome, 62, 28, Palette.Gold);
        Line($"{summary.Plays} {(summary.Plays == 1 ? "PLAY" : "PLAYS")}   {summary.Yards:0} YARDS   {summary.Points} POINTS", 108, 21, Palette.White);
        Line(summary.Next, 153, 20, Palette.White);
        Line(session.Timed.Finished ? "Enter: accept result | F: replay | Tab: stats" : "Space: continue | F: replay | Tab: stats", 218, 17, Palette.Gold);
    }

    private static void Ring(Vector2 world)
    {
        var p = Constants.WorldToScreen(world); Raylib.DrawCircleLines((int)p.X, (int)p.Y, 13, Palette.Gold);
    }

    private static void Panel(string heading)
    {
        Raylib.DrawRectangle(15, 15, Raylib.GetScreenWidth() - 30, Raylib.GetScreenHeight() - 30, new Color(12, 20, 30, 248));
        Raylib.DrawText(heading, 40, 40, 25, Palette.Gold);
    }

    private static void DrawStatistics(FullMatchSession session, TimedSeason? season)
    {
        Panel("MATCH STATISTICS | TAB: RETURN");
        var m = session.Match;
        int x = 40;
        foreach (var team in new[] { m.User, m.Opponent })
        {
            var o = team.Stats; var d = team.DefenseStats;
            Raylib.DrawText($"{team.Definition.Name}  {team.Score}\n\nOFFENSE\nPassing: {o.Qb.Completions}/{o.Qb.Attempts}, {o.Qb.PassYards} yd\nPass TD: {o.Qb.PassTds} | INT: {o.Qb.Interceptions}\nSacks allowed: {o.Qb.Sacks}\nQB rush: {o.Qb.RushAttempts} for {o.Qb.RushYards} yd\nRB rush: {o.Rb.Attempts} for {o.Rb.Yards} yd\n\nDEFENSE\nTackles: {d.Tackles} | Controlled LB: {d.ControlledTackles}\nSacks: {d.Sacks} | INT: {d.Interceptions}\nPass breakups: {d.PassesDefended}\nYards allowed: {d.YardsAllowed}\nPoints allowed: {d.PointsAllowed}\n3rd / 4th stops: {d.ThirdDownStops} / {d.FourthDownStops}", x, 100, 19, Palette.White);
            Raylib.DrawText($"Controlled LB: {d.ControlledSacks} SK {d.ControlledInterceptions} INT {d.ControlledPassesDefended} PD", x, 460, 15, Palette.Gold);
            int y = 500;
            foreach (var player in d.Players.Where(p => p.Tackles + p.Sacks + p.Interceptions + p.PassesDefended > 0)
                .OrderByDescending(p => p.Tackles + p.Sacks + p.Interceptions + p.PassesDefended).Take(5))
            {
                Raylib.DrawText($"{player.Slot}: {player.Tackles} TKL {player.Sacks} SK {player.Interceptions} INT {player.PassesDefended} PD", x, y, 15, Palette.White); y += 22;
            }
            x += (Raylib.GetScreenWidth() - 80) / 2;
        }
        if (season != null)
            Raylib.DrawText($"Accepted games: {season.Completed.Count} | {season.Summary.BuildThreeStageScoreHistory()}", 40, Raylib.GetScreenHeight() - 62, 17, Palette.Gold);
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
