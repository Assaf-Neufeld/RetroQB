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
        DrawCoverage(session.Replay.Clip?.MatchContext?.CompiledPlan ?? (replay == null ? drive.CompiledPlan : null),
            replay?.DefensiveDecision ?? (replay == null ? drive.Execution.DefenseRuntime?.Current : null));
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
            bool supported = drive.SupportsDefense(call.Id);
            bool selected = call.Id == displayedCall.Definition.Id;
            int y = 110 + i * 33;
            if (selected) Raylib.DrawRectangle(14, y - 6, 300, 29, new Color(40, 48, 63, 255));
            Raylib.DrawText($"{(i + 1) % 10}  {call.Name}{(supported ? "" : " (unavailable)")}", 22, y, supported ? 17 : 13,
                selected ? Palette.Gold : supported ? Palette.White : new Color(110, 110, 110, 255));
        }
        Text(displayedCall.Definition.Coaching, 20, 455);
        var job = displayedCall.Assignments.Single(a => a.Slot == displayedCall.ControlledSlot);
        Raylib.DrawText($"YOU: {displayedCall.ControlledSlot}", 20, 525, 20, Palette.Gold);
        var liveCue = (replay?.DefensiveDecision ?? (replay == null ? drive.Execution.DefenseRuntime?.Current : null))?.Decisions.Single(d => d.Actor == displayedCall.ControlledSlot);
        Text(liveCue == null ? job.Responsibility : Gameplay.Defense.DefensivePlanPresentation.Describe(liveCue), 20, 555);
        var displayedPlan = session.Replay.Clip?.MatchContext?.CompiledPlan ?? (replay == null ? drive.CompiledPlan : null);
        if (displayedPlan != null)
        {
            Text(Gameplay.Defense.DefensivePlanPresentation.PressureSummary(displayedPlan, replay?.DefensiveDecision ?? (replay == null ? drive.Execution.DefenseRuntime?.Current : null)), 20, 590);
            if (!drive.Live && replay == null && (displayedPlan.Pressure.Entries.Count > 4 || displayedPlan.Call.Zone == Gameplay.Defense.SpotZoneFamily.Cover3)) Text("Q/E: variant   X: pressure side", 20, 735);
        }
        Raylib.DrawText("WASD / arrows: move\nC: timeout    P: pause\nF: replay     Esc: close", 20, 640, 17, Palette.White);
        int right = Raylib.GetScreenWidth() - 310;
        int seconds = (int)Math.Ceiling(clock.RemainingSeconds);
        Raylib.DrawText($"Q{clock.Quarter}  {seconds / 60:00}:{seconds % 60:00}", right, 35, 25, Palette.Gold);
        Raylib.DrawText($"Play clock: {Math.Ceiling(clock.PlaySeconds):0}\nSnap in: {session.SnapRemaining:0.0}s", right, 85, 20, Palette.White);
        Raylib.DrawText($"{drive.Match.User.Definition.Name}  {drive.Match.User.Score}\n{drive.Match.Opponent.Definition.Name}  {drive.Match.Opponent.Score}", right, 170, 21, Palette.White);
        Raylib.DrawText($"CPU BALL\nDown {drive.Match.Series.Down} | {drive.Match.Series.Distance:0.#} to go\nCPU own {drive.Match.Series.OwnYardLine:0.#}\nTimeouts: {clock.Timeouts(drive.Match.User.Definition.Id)} / {clock.Timeouts(drive.Match.Opponent.Definition.Id)}", right, 270, 20, Palette.White);
        string status = failure ?? drive.SelectionRejection ?? (replay != null ? "REPLAY" : clock.Suspension != ClockSuspension.None ? "PAUSED"
            : drive.Complete ? "DRIVE OVER" : drive.Live ? "LIVE" : drive.LastResult != null ? "PLAY OVER"
            : clock.StopReason == ClockStopReason.Timeout ? "TIMEOUT" : clock.StopReason == ClockStopReason.DelayOfGame ? "DELAY OF GAME" : "CHOOSE YOUR CALL");
        Text(status, right, 415);
        if (drive.LastResult != null) Text(drive.LastResult.Event.Reason.ToString(), right, 485);
        Raylib.DrawText("Gold ring: your linebacker\nRed: rush   Blue: zone/match\nWhite: man assignment", right, 570, 16, Palette.White);
        var decision = replay?.DefensiveDecision ?? (replay == null ? drive.Execution.DefenseRuntime?.Current : null);
        if (decision != null)
        {
            var cue = decision.Decisions.Single(d => d.Actor == displayedCall.ControlledSlot);
            Text(Gameplay.Defense.DefensivePlanPresentation.Describe(cue), right, 660);
            if (decision.Coverage is { } board) Text(CoverageCue(board, displayedCall.ControlledSlot), right, 715);
            if (decision.Observation.ConfirmedCarrier && decision.Run is { } run) Text(RunCue(run), right, 715);
        }
    }

    internal static void DrawAssignments(ResolvedDefensivePlay play)
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

    internal static string CoverageCue(Gameplay.Defense.CoverageBoard board, DefenderSlot controlled)
    {
        var job = board.Commitments.Single(c => c.Actor == controlled);
        if (job.HumanAbandonment) return "Coverage exposed: return to your receiver";
        var transfer = board.Transfers.LastOrDefault(t => t.Source == controlled || t.Destination == controlled);
        if (transfer != null) return transfer.Stage == "Commit" ? "Handoff accepted" : transfer.Stage == "Decline" ? "Handoff declined: keep coverage" : "Handoff pending";
        return job.Status.Contains("Carry") ? "Carrying receiver; awaiting handoff" : "Blue: home area   Gold: live responsibility";
    }

    internal static void DrawCoverage(Gameplay.Defense.ResolvedDefensivePlan? plan, Gameplay.Defense.DefensiveDecisionFrame? frame)
    {
        if (plan != null && frame?.Pressure?.Jobs.Any(j => j.Actor == plan.ControlledActor) == true && !frame.Observation.ConfirmedCarrier)
        {
            var rushIntent = frame.Decisions.Single(i => i.Actor == plan.ControlledActor);
            var actor = frame.Observation.Visible.Defenders.Single(a => a.Id == plan.ControlledActor);
            Raylib.DrawLineEx(Constants.WorldToScreen(actor.Position), Constants.WorldToScreen(rushIntent.Target), 2, Palette.Gold);
            return;
        }
        if (plan != null && frame?.Observation.ConfirmedCarrier == true)
        {
            var runIntent = frame.Decisions.Single(i => i.Actor == plan.ControlledActor);
            var actor = frame.Observation.Visible.Defenders.Single(a => a.Id == plan.ControlledActor);
            Raylib.DrawLineEx(Constants.WorldToScreen(actor.Position), Constants.WorldToScreen(runIntent.Target), 2, Palette.Gold);
            return;
        }
        if (plan == null || plan.Call.Zone == Gameplay.Defense.SpotZoneFamily.None) return;
        var controlled = plan.Assignments.Single(a => a.ActorId == plan.ControlledActor);
        if (controlled.Pass is not Gameplay.Defense.ZoneResponsibility zone) return;
        var a = Constants.WorldToScreen(new(zone.Area.Left, zone.Area.Shallow));
        var b = Constants.WorldToScreen(new(zone.Area.Right, zone.Area.Deep));
        Raylib.DrawRectangleLines((int)MathF.Min(a.X, b.X), (int)MathF.Min(a.Y, b.Y), (int)MathF.Abs(b.X - a.X), (int)MathF.Abs(b.Y - a.Y), new Color(90, 190, 255, 150));
        var home = Constants.WorldToScreen(zone.Home); Raylib.DrawCircleLines((int)home.X, (int)home.Y, 8, new Color(90, 190, 255, 180));
        if (frame == null) return;
        var intent = frame.Decisions.Single(i => i.Actor == plan.ControlledActor);
        var position = frame.Observation.Visible.Defenders.Single(d => d.Id == plan.ControlledActor).Position;
        Raylib.DrawLineEx(Constants.WorldToScreen(position), Constants.WorldToScreen(intent.Target), 2, Palette.Gold);
    }

    internal static string RunCue(Gameplay.Defense.RunBoard run) => run.Concessions.Any(c => c.Contains("ForceExposed"))
        ? "Outside force unavailable: protect the edge" : run.Cutback ? "Cutback: backside defender responds"
        : run.Tackler == null ? "No clear tackle: maintain pursuit angles" : "Tackler attacks; support protects escape lanes";

    internal static void Text(string text, int x, int y)
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
