using System.Numerics;
using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Replay;

namespace RetroQB.Rendering;

/// <summary>Defense-only presentation: no offensive call, route, priority, or intended target strings.</summary>
public sealed class DefensivePlayRenderer
{
    private readonly FieldRenderer _field = new();
    public void Draw(DefensivePlaySession session, string? failure = null)
    {
        var drive = session.Drive; var clock = session.Clock; var replay = session.Replay.CurrentFrame;
        var displayedCall = replay != null ? drive.LastReplayDefense ?? drive.SelectedDefense : drive.SelectedDefense;
        Constants.UpdateFieldRect(); Raylib.ClearBackground(Palette.Background);
        _field.DrawField(replay?.LineOfScrimmage ?? drive.Plays.LineOfScrimmage,
            replay?.FirstDownLine ?? drive.Plays.FirstDownLine,
            drive.Match.Opponent.Definition.Name, drive.Match.Opponent.Definition.PrimaryColor,
            drive.Match.User.Definition.Name, drive.Match.User.Definition.PrimaryColor,
            SeasonStage.RegularSeason, default, replay?.Down ?? drive.Match.Series.Down);
        if (!drive.Live && drive.LastResult == null && replay == null) DrawAssignments(drive.SelectedDefense);
        Vector2 controlled = drive.Linebacker.Position;
        if (replay == null)
        {
            foreach (var actor in drive.Players) actor.Draw();
            drive.Actors.Ball.Draw();
        }
        else
        {
            foreach (var actor in new[] { replay.Quarterback }.Concat(replay.Receivers).Concat(replay.Blockers).Concat(replay.Defenders))
                PixelPlayerRenderer.Draw(Constants.WorldToScreen(actor.Position), actor.Velocity, actor.Glyph, actor.Color, actor.Visual);
            int index = drive.LastReplayControlledIndex;
            if (index >= 0 && index < replay.Defenders.Count) controlled = replay.Defenders[index].Position;
            Raylib.DrawCircleV(Constants.WorldToScreen(replay.Ball.Position), 3, Palette.White);
        }
        var center = Constants.WorldToScreen(controlled);
        Raylib.DrawCircleLines((int)center.X, (int)center.Y, 13, Palette.Gold);
        Raylib.DrawText("CALL DEFENSE", 20, 28, 25, Palette.Gold);
        Raylib.DrawText("1-9 / 0: select    Space: ready", 20, 65, 15, Palette.White);
        for (int i = 0; i < DefensivePlaybook.All.Count; i++)
        {
            var call = DefensivePlaybook.All[i];
            bool selected = call.Id == displayedCall.Definition.Id;
            int y = 110 + i * 33;
            if (selected) Raylib.DrawRectangle(14, y - 6, 300, 29, new Color(40, 48, 63, 255));
            Raylib.DrawText($"{(i + 1) % 10}  {call.Name}", 22, y, 17, selected ? Palette.Gold : Palette.White);
        }
        Text(displayedCall.Definition.Coaching, 20, 455);
        var job = displayedCall.Assignments.Single(a => a.Slot == displayedCall.ControlledSlot);
        Raylib.DrawText($"YOU: {displayedCall.ControlledSlot}", 20, 525, 20, Palette.Gold);
        Text(job.Responsibility, 20, 555);
        Raylib.DrawText("WASD / arrows: move\nC: timeout    P: pause\nF: replay     Esc: close", 20, 640, 17, Palette.White);
        int right = Raylib.GetScreenWidth() - 310;
        int seconds = (int)Math.Ceiling(clock.RemainingSeconds);
        Raylib.DrawText($"Q{clock.Quarter}  {seconds / 60:00}:{seconds % 60:00}", right, 35, 25, Palette.Gold);
        Raylib.DrawText($"Play clock: {Math.Ceiling(clock.PlaySeconds):0}\nSnap in: {session.SnapRemaining:0.0}s", right, 85, 20, Palette.White);
        Raylib.DrawText($"{drive.Match.User.Definition.Name}  {drive.Match.User.Score}\n{drive.Match.Opponent.Definition.Name}  {drive.Match.Opponent.Score}", right, 170, 21, Palette.White);
        Raylib.DrawText($"CPU BALL\nDown {drive.Match.Series.Down} | {drive.Match.Series.Distance:0.#} to go\nCPU own {drive.Match.Series.OwnYardLine:0.#}\nTimeouts: {clock.Timeouts(drive.Match.User.Definition.Id)} / {clock.Timeouts(drive.Match.Opponent.Definition.Id)}", right, 270, 20, Palette.White);
        string status = failure ?? (replay != null ? "REPLAY" : clock.Suspension != ClockSuspension.None ? "PAUSED"
            : drive.Complete ? "DRIVE OVER" : drive.Live ? "LIVE" : drive.LastResult != null ? "PLAY OVER"
            : clock.StopReason == ClockStopReason.Timeout ? "TIMEOUT" : clock.StopReason == ClockStopReason.DelayOfGame ? "DELAY OF GAME" : "CHOOSE YOUR CALL");
        Text(status, right, 415);
        if (drive.LastResult != null) Text(drive.LastResult.Event.Reason.ToString(), right, 485);
        Raylib.DrawText("Gold ring: your linebacker\nRed: rush   Blue: zone\nWhite: man assignment", right, 570, 16, Palette.White);
    }

    private static void DrawAssignments(ResolvedDefensivePlay play)
    {
        foreach (var job in play.Assignments)
        {
            var start = Constants.WorldToScreen(job.Position); var end = Constants.WorldToScreen(job.Target);
            Color color = job.Rush ? Palette.Red : job.Zone != RetroQB.Entities.CoverageRole.None ? new Color(90, 190, 255, 180) : Palette.White;
            Raylib.DrawLineEx(start, end, 1.5f, color);
            if (job.Zone != RetroQB.Entities.CoverageRole.None) Raylib.DrawCircleLines((int)end.X, (int)end.Y, 12, color);
            else Raylib.DrawCircleV(end, 3, color);
        }
    }

    private static void Text(string text, int x, int y)
    {
        string line = "";
        foreach (string word in text.Split(' '))
        {
            if (Raylib.MeasureText(line + word, 17) > 285) { Raylib.DrawText(line, x, y, 17, Palette.White); y += 23; line = ""; }
            line += word + " ";
        }
        Raylib.DrawText(line, x, y, 17, Palette.White);
    }
}
