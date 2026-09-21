using Raylib_cs;
using RetroQB.Gameplay;
using System.Runtime.Versioning;

namespace RetroQB.Rendering;

/// <summary>The original stats-board presentation, adapted to team-owned timed match data.</summary>
public sealed class TimedScoreboardRenderer
{
    private int _lastBeepSecond = -1;

    public void Draw(FullMatchSession session)
    {
        var m = session.Match; var d = session.Drive;
        var replay = session.Replay.Clip?.MatchContext;
        var frame = session.Replay.CurrentFrame;
        var clock = replay?.Clock ?? session.Clock.Snapshot();
        int x = Raylib.GetScreenWidth() - 330, width = 320;
        float scale = Math.Min(1, (Raylib.GetScreenHeight() - 20) / 860f);
        int Y(int y) => 10 + (int)(y * scale);
        int Font(int size) => Math.Max(10, (int)(size * scale));
        void Text(string text, int y, int size = 16, Color? color = null, int offset = 14)
            {
            int font = Font(size);
            while (font > 9 && Raylib.MeasureText(text, font) > width - offset - 12) font--;
            Raylib.DrawText(text, x + offset, Y(y), font, color ?? Palette.White);
        }
        void Box(int y, int height, Color color)
        {
            Raylib.DrawRectangle(x + 8, Y(y), width - 16, (int)(height * scale), Palette.PanelRaised);
            Raylib.DrawRectangleLinesEx(new(x + 8, Y(y), width - 16, height * scale), 1, color);
        }
        Raylib.DrawRectangle(x + 4, 14, width, Raylib.GetScreenHeight() - 20, new Color(0, 0, 0, 110));
        Raylib.DrawRectangle(x, 10, width, Raylib.GetScreenHeight() - 20, Palette.Panel);
        Raylib.DrawRectangleLinesEx(new(x, 10, width, Raylib.GetScreenHeight() - 20), 2, Palette.PanelLine);
        Box(8, 28, Palette.Gold); Text("GAME DAY | STATS BOARD", 14, 16, Palette.Gold);
        Text((replay?.Stage ?? m.Stage).GetDisplayName().ToUpperInvariant(), 47, 16, Palette.Lime);
        int seconds = (int)Math.Ceiling(clock.RemainingSeconds);
        bool clockRunning = clock.Reason == ClockStopReason.Running
            && clock.Suspension == ClockSuspension.None && clock.Phase is ClockPhase.PreSnap or ClockPhase.LivePlay;
        bool lowTime = clockRunning && seconds is > 0 and <= 30;
        bool criticalTime = lowTime && seconds <= 10;
        if (!clockRunning || seconds > 10) _lastBeepSecond = -1;
        else if (seconds is 10 or 5 or 3 or 2 or 1 && seconds != _lastBeepSecond)
        {
            _lastBeepSecond = seconds;
            if (OperatingSystem.IsWindows()) _ = Task.Run(BeepOnWindows);
        }
        float pulse = .5f + .5f * MathF.Sin((float)Raylib.GetTime() * 8);
        Color clockColor = criticalTime
            ? new Color((byte)255, (byte)(35 + pulse * 75), (byte)(35 + pulse * 75), (byte)255)
            : lowTime ? new Color(255, 183, 45, 255) : Palette.Gold;
        Box(77, 53, criticalTime ? clockColor : Palette.Gold);
        Text(clock.IsOvertime ? "OVERTIME" : $"{(clock.RegulationPeriods == 2 ? "H" : "Q")}{clock.Quarter}  {seconds / 60:00}:{seconds % 60:00}", 85, 27, clockColor);
        if (lowTime)
            Text(criticalTime ? $"HURRY! {seconds} SECONDS" : seconds == 30 ? "30 SECONDS LEFT" : "LOW TIME", 119, 13,
                criticalTime ? clockColor : new Color(255, 183, 45, 255), 76);
        Text($"PLAY {Math.Ceiling(clock.PlaySeconds):00}", 95, 16, clock.PlaySeconds <= 5 ? Palette.Red : Palette.White, 222);
        int row = 141;
        foreach (var team in new[] { m.User, m.Opponent })
        {
            Box(row, 43, team.Definition.PrimaryColor);
            Raylib.DrawRectangle(x + 10, Y(row + 2), 5, (int)(39 * scale), team.Definition.PrimaryColor);
            Text(team.Definition.Name.ToUpperInvariant(), row + 8, 17, Palette.White, 24);
            int score = team == m.User ? replay?.UserScore ?? team.Score : replay?.OpponentScore ?? team.Score;
            Text(score.ToString(), row + 4, 27, team.Definition.PrimaryColor, 265);
            row += 48;
        }
        string offenseId = replay?.OffenseId ?? d.PreparedOffenseId;
        bool defending = offenseId != m.User.Definition.Id;
        Text(session.SpecialTeams != null && frame == null ? session.UserReceivingKick ? "YOUR KICK RETURN  ^" : "YOUR KICK COVERAGE  ^" : defending ? "CPU POSSESSION  v" : "YOUR POSSESSION  ^", 245, 17, Palette.Gold);
        int down = frame?.Down ?? m.Series.Down;
        float distance = frame != null ? frame.FirstDownLine - frame.LineOfScrimmage : m.Series.Distance;
        float yard = frame != null ? frame.LineOfScrimmage - 10 : m.Series.OwnYardLine;
        Box(273, 53, Palette.Cyan);
        Text(session.SpecialTeams is { } special && frame == null ? $"{(special.IsKickoff ? "KICKOFF" : "PUNT")} | {special.Phase.ToString().ToUpperInvariant()}" : $"DOWN {down}  |  {distance:0.#} TO GO", 280, 18, Palette.Lime);
        Text(yard <= 50 ? $"BALL: OWN {yard:0}" : $"BALL: OPP {100-yard:0}", 304, 14);
        Text($"TIMEOUTS   YOU {clock.UserTimeouts}  |  CPU {clock.OpponentTimeouts}", 341, 14);
        var stats = m.Team(offenseId).Stats;
        Box(372, 25, Palette.PanelLine); Text(defending ? "CPU OFFENSE" : "YOUR OFFENSE", 377, 15, Palette.Gold);
        Text("PASS       CMP/ATT    YDS   TD  INT", 410, 12, Palette.Cyan);
        Text($"QB          {stats.Qb.Completions}/{stats.Qb.Attempts}         {stats.Qb.PassYards}     {stats.Qb.PassTds}    {stats.Qb.Interceptions}", 433, 14);
        Text($"RUSH  {stats.Rb.Attempts + stats.Qb.RushAttempts} ATT   {stats.Rb.Yards + stats.Qb.RushYards} YD", 461, 15);
        Text($"SACKS ALLOWED  {stats.Qb.Sacks}", 487, 14);
        var defense = m.User.DefenseStats;
        Box(519, 25, Palette.PanelLine); Text("YOUR DEFENSE", 524, 15, Palette.Gold);
        Text($"TACKLES {defense.Tackles}  |  YOUR LB {defense.ControlledTackles}", 556, 14);
        Text($"SACKS {defense.Sacks}  INT {defense.Interceptions}  PD {defense.PassesDefended}", 581, 14);
        Box(615, 25, Palette.PanelLine); Text(frame != null ? "REPLAY | RECORDED SCORE / CLOCK" : "RECENT PLAYS", 620, 13, Palette.Gold);
        if (frame == null)
        {
            int y = 651;
            foreach (var play in m.History.Reverse().Skip(session.SummaryOffset).Take(3))
            {
                Text(m.Team(play.Event.OffenseId).Definition.Name, y, 13, Palette.Cyan);
                Text($"{(play.TurnoverOnDowns ? "Turnover on downs" : play.Event.Reason)}  {play.Gain:+0;-0;0} yd", y + 17, 13);
                y += 45;
            }
        }
        Text(frame != null ? "F / SPACE: RETURN" : session.Clock.Suspension != 0 ? "PAUSED" : session.Status != "" ? session.Status : d.Live ? "LIVE" : "SELECT YOUR CALL", 804, 14, Palette.Gold);
        Text("TAB: FULL STATS   PGUP/PGDN: HISTORY", 831, 11, Palette.Cyan);
    }

    [SupportedOSPlatform("windows")]
    private static void BeepOnWindows()
    {
        try { Console.Beep(880, 85); }
        catch (Exception) { /* Audio may be unavailable; the visual warning remains active. */ }
    }
}
