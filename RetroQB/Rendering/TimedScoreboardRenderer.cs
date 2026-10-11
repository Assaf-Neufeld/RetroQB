using Raylib_cs;
using RetroQB.Gameplay;
using System.Runtime.Versioning;

namespace RetroQB.Rendering;

/// <summary>Stable match facts, clocks, and results; actions belong to YOUR PLAY.</summary>
public sealed class TimedScoreboardRenderer
{
    private int _lastBeepSecond = -1;
    public void Draw(FullMatchSession session)
    {
        var layout = HudLayout.Current;
        GameplayHud.Panel(layout.Right, "GAME STATUS");
        var content = new HudContent(layout.Right, layout, layout.ScreenHeight - 44);
        var match = session.Match;
        var frame = session.Replay.CurrentFrame;
        var replay = frame != null ? session.Replay.Clip?.MatchContext : null;
        var clock = replay?.Clock ?? session.Clock.Snapshot();
        content.Line((replay?.Stage ?? match.Stage).GetDisplayName().ToUpperInvariant(), Palette.Muted, layout.Hint);
        foreach (var team in new[] { match.User, match.Opponent })
        {
            int height = layout.Compact ? 40 : 46;
            Raylib.DrawRectangle(content.X - 4, content.Y, content.Width + 8, height - 4, Palette.PanelRaised);
            Raylib.DrawRectangle(content.X - 4, content.Y, 3, height - 4, team.Definition.PrimaryColor);
            GameplayHud.Text(team.Definition.Name.ToUpperInvariant(), content.X + 6, content.Y + 9, content.Width - 62, layout.Body);
            string score = (team == match.User ? replay?.UserScore ?? team.Score : replay?.OpponentScore ?? team.Score).ToString();
            Raylib.DrawText(score, content.X + content.Width - Raylib.MeasureText(score, 26), content.Y + 4, 26, Palette.White);
            content.Y += height;
        }
        int seconds = (int)Math.Ceiling(clock.RemainingSeconds);
        bool running = clock.Reason == ClockStopReason.Running && clock.Suspension == ClockSuspension.None
            && clock.Phase is ClockPhase.PreSnap or ClockPhase.LivePlay && !clock.IsOvertime;
        bool lowTime = running && seconds is > 0 and <= 30;
        bool criticalTime = lowTime && seconds <= 10;
        if (!running || seconds > 10 || frame != null) _lastBeepSecond = -1;
        else if (seconds is 10 or 5 or 3 or 2 or 1 && seconds != _lastBeepSecond)
        {
            _lastBeepSecond = seconds;
            if (OperatingSystem.IsWindows()) _ = Task.Run(BeepOnWindows);
        }
        Color clockColor = criticalTime ? Palette.Red : lowTime ? Palette.Orange : Palette.White;
        content.Section(frame != null ? "RECORDED CLOCK" : "CLOCKS");
        content.Line(clock.IsOvertime ? "OVERTIME" : $"{(clock.RegulationPeriods == 2 ? "H" : "Q")}{clock.Quarter}  {seconds / 60:00}:{seconds % 60:00}", clockColor, 28);
        string clockState = frame != null ? "RECORDED" : clock.Suspension != ClockSuspension.None ? "PAUSED"
            : clock.IsOvertime ? "PAIRED POSSESSIONS" : criticalTime ? "HURRY! 10 SECONDS OR LESS"
            : lowTime ? "LOW TIME" : running ? "RUNNING" : "STOPPED";
        content.Line(clockState, lowTime ? clockColor : Palette.Muted, layout.Hint);
        var timers = MatchHudPresentation.Timers(session);
        if (timers.PlayClock) Value("PLAY CLOCK", $"{Math.Ceiling(clock.PlaySeconds):00}s", clock.PlaySeconds <= 5 ? Palette.Red : Palette.White);
        if (timers.CpuLabel is { } cpuLabel) Value(cpuLabel, $"{timers.CpuSeconds:0.0}s", Palette.White);

        string offenseId = replay?.OffenseId ?? session.Drive.PreparedOffenseId;
        content.Section("POSSESSION & FIELD");
        var special = frame == null ? session.SpecialTeams : null;
        content.Wrapped(MatchHudPresentation.Possession(session), maxLines: 2);
        if (special != null)
        {
            content.Line((special.IsKickoff ? "KICKOFF" : "PUNT") + " | " + special.Phase.ToString().ToUpperInvariant(), size: layout.Hint);
            content.Line(special.Phase == SpecialTeamsPhase.Setup ? "KICK POSITION: " + YardSpot(match, offenseId, match.Series.OwnYardLine)
                : special.Result is { } result ? result.Touchback ? "TOUCHBACK" : $"RETURN: {result.ReturnYards} YARDS" : "BALL IN PLAY", Palette.Muted, layout.Hint);
        }
        else
        {
            int down = frame?.Down ?? match.Series.Down;
            float distance = frame != null ? frame.FirstDownLine - frame.LineOfScrimmage : match.Series.Distance;
            float yard = frame != null ? frame.LineOfScrimmage - 10 : match.Series.OwnYardLine;
            content.Line($"DOWN {down} | {distance:0.#} TO GO");
            content.Line("BALL: " + YardSpot(match, offenseId, yard), Palette.Muted, layout.Hint);
        }
        Value("TIMEOUTS", $"YOU {clock.UserTimeouts} | CPU {clock.OpponentTimeouts}", Palette.Muted);
        content.Section("MATCH STATE");
        content.Wrapped(MatchHudPresentation.Status(session), Palette.White, 2, layout.Hint);
        if (frame != null)
        {
            content.Wrapped("Score, clock and field position are from this recorded play.", Palette.Muted, 3, layout.Hint);
            return;
        }
        content.Section("RECENT PLAYS");
        var history = match.History.Reverse().Skip(session.SummaryOffset).Take(layout.Compact ? 2 : 3).ToList();
        if (history.Count == 0) content.Line("No completed plays yet.", Palette.Muted, layout.Hint);
        foreach (var play in history)
        {
            content.Line(match.Team(play.Event.OffenseId).Definition.Name, Palette.Muted, layout.Hint);
            content.Line($"{(play.TurnoverOnDowns ? "Turnover on downs" : play.Event.Reason)}  {play.Gain:+0;-0;0} yd", size: layout.Body);
            content.Y += 4;
        }
        var stats = match.User.Stats; var defense = match.User.DefenseStats;
        string statsTeam = match.User.Definition.Name.ToUpperInvariant();
        if (content.Y + 150 <= content.Bottom)
        {
            content.Section(statsTeam + " OFFENSE");
            Value("PASS", $"{stats.Qb.Completions}/{stats.Qb.Attempts} | {stats.Qb.PassYards} YD", Palette.White);
            Value("RUSH", $"{stats.Rb.Attempts + stats.Qb.RushAttempts} ATT | {stats.Rb.Yards + stats.Qb.RushYards} YD", Palette.White);
            content.Section(statsTeam + " DEFENSE");
            Value("TACKLES", $"{defense.Tackles} | YOUR LB {defense.ControlledTackles}", Palette.White);
            Value("SK / INT / PD", $"{defense.Sacks} / {defense.Interceptions} / {defense.PassesDefended}", Palette.White);
        }
        else if (content.Y + 66 <= content.Bottom)
        {
            content.Section(statsTeam + " STATS");
            content.Line($"PASS {stats.Qb.PassYards} YD | RUSH {stats.Rb.Yards + stats.Qb.RushYards} YD", size: layout.Hint);
            content.Line($"DEF: {defense.Sacks} SK | {defense.Interceptions} INT | {defense.Tackles} TKL", size: layout.Hint);
        }
        GameplayHud.Text("PgUp/PgDn: history", content.X, layout.ScreenHeight - 30, content.Width, layout.Hint, Palette.Muted);

        void Value(string label, string value, Color color)
        {
            int font = layout.Hint;
            if (content.Y + font > content.Bottom) return;
            int valueWidth = Raylib.MeasureText(value, font);
            GameplayHud.Text(label, content.X, content.Y, content.Width - valueWidth - 8, font, Palette.Muted);
            Raylib.DrawText(value, content.X + content.Width - valueWidth, content.Y, font, color);
            content.Y += font + 4;
        }
    }
    private static string YardSpot(MatchState match, string offenseId, float yard)
        => $"{match.Team(yard <= 50 ? offenseId : match.Other(offenseId).Definition.Id).Definition.Name} {(yard <= 50 ? yard : 100 - yard):0}";

    [SupportedOSPlatform("windows")]
    private static void BeepOnWindows()
    {
        try { Console.Beep(880, 85); }
        catch (Exception) { /* Audio may be unavailable; the visual warning remains active. */ }
    }
}
