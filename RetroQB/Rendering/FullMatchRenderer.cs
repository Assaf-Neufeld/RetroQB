using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.AI;

namespace RetroQB.Rendering;

public sealed class FullMatchRenderer
{
    private readonly FieldRenderer _field = new();
    public void Draw(FullMatchSession session)
    {
        var d = session.Drive; var m = session.Match; var c = session.Clock;
        var offense = m.Team(d.PreparedOffenseId); var defense = m.Other(d.PreparedOffenseId);
        Constants.UpdateFieldRect(); Raylib.ClearBackground(Palette.Background);
        var frame = session.Replay.CurrentFrame;
        _field.DrawField(frame?.LineOfScrimmage ?? d.Plays.LineOfScrimmage, frame?.FirstDownLine ?? d.Plays.FirstDownLine,
            offense.Definition.Name, offense.Definition.PrimaryColor, defense.Definition.Name,
            defense.Definition.PrimaryColor, SeasonStage.RegularSeason, default, frame?.Down ?? m.Series.Down);
        if (frame != null)
        {
            foreach (var actor in new[] { frame.Quarterback }.Concat(frame.Receivers).Concat(frame.Blockers).Concat(frame.Defenders))
                PixelPlayerRenderer.Draw(Constants.WorldToScreen(actor.Position), actor.Velocity, actor.Glyph, actor.Color, actor.Visual);
            Raylib.DrawCircleV(Constants.WorldToScreen(frame.Ball.Position), 3, Palette.White);
        }
        else if (session.Kick != null)
        {
            FieldGoalRenderer.DrawOnField(session.Kick, d.Plays.LineOfScrimmage, m.Offense.Definition.PrimaryColor, m.Defense.Definition.PrimaryColor);
        }
        else
        {
            if (!d.Live && d.LastResult == null && session.HumanOnDefense) DefensivePlayRenderer.DrawAssignments(d.SelectedDefense);
            foreach (var actor in d.Players) actor.Draw();
            d.Actors.Ball.Draw();
            var controlled = d.Execution.Control.HumanOnDefense ? d.Linebacker.Position : d.Actors.Ball.Holder?.Position ?? d.Actors.Qb.Position;
            var center = Constants.WorldToScreen(controlled);
            Raylib.DrawCircleLines((int)center.X, (int)center.Y, 13, Palette.Gold);
        }
        void Left(string s, int y, int size = 17) => Raylib.DrawText(s, 20, y, size, Palette.White);
        Left(session.HumanOnDefense ? "CALL DEFENSE" : "CALL OFFENSE", 28, 25);
        if (session.Kick != null && !session.HumanOnDefense) FieldGoalRenderer.DrawHud(session.Kick, timedMatch: true);
        else if (session.Kick != null) Left($"CPU FIELD GOAL\n{session.Kick.Distance:0} YARDS\n{session.Kick.Result}", 100);
        else if (session.HumanOnDefense)
        {
            for (int i = 0; i < 10; i++)
                Raylib.DrawText($"{(i + 1) % 10} {DefensivePlaybook.All[i].Name}", 20, 95 + i * 29, 16,
                    d.SelectedDefense.Definition == DefensivePlaybook.All[i] ? Palette.Gold : Palette.White);
            Left($"YOU: {d.SelectedDefense.ControlledSlot}", 415, 20);
            DefensivePlayRenderer.Text(d.SelectedDefense.Assignments.Single(a => a.Slot == d.SelectedDefense.ControlledSlot).Responsibility, 20, 450);
            Left($"CPU snap in {session.SnapRemaining:0.0}s", 520);
        }
        else
        {
            for (int i = 0; i < d.Plays.PassPlays.Count; i++) Left($"{(i + 1) % 10} {d.Plays.PassPlays[i].Name}", 85 + i * 22, 13);
            const string keys = "QWERTYUIOP";
            for (int i = 0; i < d.Plays.RunPlays.Count; i++) Left($"{keys[i]} {d.Plays.RunPlays[i].Name}", 320 + i * 22, 13);
            DefensivePlayRenderer.Text(session.Action == MatchAction.Scrimmage ? d.Plays.SelectedPlay.Name : session.Action.ToString(), 20, 555);
            if (d.Live) foreach (var receiver in d.Actors.Receivers.Where(r => r.Eligible && !r.IsBlocking))
            {
                var p = Constants.WorldToScreen(receiver.Position);
                Raylib.DrawText(d.ReceiverLabel(receiver.Index), (int)p.X + 8, (int)p.Y - 12, 16, Palette.Gold);
            }
        }
        Left("Space: snap / continue\nWASD: move | Shift: sprint\n1-5 live: throw | X: flip\nK: kick  B: punt  V: kneel\nC: timeout  Esc: pause\nF: replay  Z: restart", 665, 15);
        int right = Raylib.GetScreenWidth() - 310;
        int seconds = (int)Math.Ceiling(c.RemainingSeconds);
        Raylib.DrawText(c.IsOvertime ? $"OT {session.Timed.OvertimePair}" : $"Q{c.Quarter} {seconds / 60:00}:{seconds % 60:00}", right, 35, 27, Palette.Gold);
        Raylib.DrawText($"Play clock: {Math.Ceiling(c.PlaySeconds)}\n\n{m.User.Definition.Name} {m.User.Score}\n{m.Opponent.Definition.Name} {m.Opponent.Score}\n\n{(session.HumanOnDefense ? "CPU" : "YOUR")} BALL\nDown {m.Series.Down} | {m.Series.Distance:0.#} to go\nOwn {m.Series.OwnYardLine:0.#}\nTimeouts: {c.Timeouts(m.User.Definition.Id)} / {c.Timeouts(m.Opponent.Definition.Id)}", right, 95, 20, Palette.White);
        DefensivePlayRenderer.Text(session.Status, right, 390);
        DefensivePlayRenderer.Text(session.Replay.IsPlaying ? "REPLAY" : c.Suspension != 0 ? "PAUSED" : d.Live ? "LIVE"
            : d.LastResult != null ? $"{d.LastResult.Event.Reason} | {d.LastResult.Gain:0.#} yards" : "CHOOSE YOUR CALL", right, 480);
        if (!d.Live && (d.LastResult?.DriveEnded == true || c.Phase == ClockPhase.PeriodBreak))
            DefensivePlayRenderer.Text("Space: continue", right, 550);
    }
}
