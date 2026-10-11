using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;
using RetroQB.AI;

namespace RetroQB.Rendering;

internal static class MatchPlayPanelRenderer
{
    public static void Draw(FullMatchSession session, bool defending, bool acceptResult)
    {
        var layout = HudLayout.Current;
        GameplayHud.Panel(layout.Left, "YOUR PLAY");
        string controls = Controls(session, defending, acceptResult);
        var lines = GameplayHud.Wrap(controls, layout.PanelWidth - 28, layout.Hint);
        int controlsTop = layout.ScreenHeight - 24 - lines.Count * (layout.Hint + 4) - 26;
        var footer = new HudContent(layout.Left, layout) { Y = controlsTop };
        footer.Section("CONTROLS");
        foreach (string line in lines) footer.Line(line, Palette.Muted, layout.Hint);
        var content = new HudContent(layout.Left, layout, controlsTop - 4);
        var drive = session.Drive;
        var frame = session.Replay.CurrentFrame;
        var recorded = session.Replay.Clip?.MatchContext;
        bool selecting = MatchHudPresentation.Selecting(session);

        if (frame == null && drive.LastResult is { } last)
        {
            content.Line("LAST PLAY", Palette.Muted, layout.Hint);
            content.Wrapped(defending ? drive.SelectedDefense.Definition.Name : drive.Plays.SelectedPlay.Name, Palette.Gold);
            content.Wrapped("Review the result and recent plays on the game status panel.", Palette.Muted);
            if (!last.DriveEnded) content.Wrapped("The next call follows after the play settles.", Palette.Muted);
        }
        else if (frame == null && session.SpecialTeams is { } special)
        {
            content.Line(special.IsKickoff ? "KICKOFF" : "PUNT", Palette.Muted, layout.Hint);
            content.Wrapped(session.UserReceivingKick ? "YOU: KICK RETURNER" : "YOU: COVERAGE LINEBACKER", Palette.Gold);
            content.Section("YOUR JOB");
            content.Wrapped(session.UserReceivingKick ? "Follow your blockers and find the open return lane." : "Beat the blockers, contain the return, and make the tackle.");
            if (selecting && !special.IsKickoff && !session.UserReceivingKick)
                content.Wrapped("Select a pass or run to cancel punt.", Palette.Muted, size: layout.Hint);
        }
        else if (frame == null && session.Kick is { } kick)
        {
            content.Line(session.HumanOnDefense ? "CPU FIELD GOAL" : "FIELD GOAL", Palette.Muted, layout.Hint);
            content.Line($"{kick.Distance:0} YARDS | 3 POINTS", Palette.Gold);
            content.Wrapped(kick.Difficulty, Palette.Muted);
            if (!session.HumanOnDefense)
            {
                content.Section("KICK TIMING");
                content.Wrapped("Aim for the center green on both meters.");
                content.Wrapped("Longer kicks have smaller timing windows. Low power leaves the kick short.", Palette.Muted);
                content.Wrapped("Accuracy: early goes right; late goes left.", Palette.Muted);
                if (selecting) content.Wrapped("Select a pass or run to cancel.", Palette.Muted);
                FieldGoalRenderer.DrawMeter(kick, timedMatch: true);
            }
        }
        else if (defending)
        {
            var assignment = recorded?.Assignments ?? drive.SelectedDefense;
            var plan = frame != null ? recorded?.CompiledPlan : drive.CompiledPlan;
            var decision = frame?.DefensiveDecision ?? (frame == null ? drive.Execution.DefenseRuntime?.Current : null);
            content.Line(assignment.Definition.Name, Palette.Gold);
            content.Line($"YOU: {assignment.ControlledSlot}");
            var cue = decision?.Decisions.Single(i => i.Actor == assignment.ControlledSlot);
            content.Wrapped(cue == null ? DefensivePlanPresentation.DescribeAssignment(plan, assignment.ControlledSlot,
                assignment.Assignments.Single(a => a.Slot == assignment.ControlledSlot).Responsibility)
                : DefensivePlanPresentation.Describe(cue), maxLines: 3);
            if (plan != null) content.Wrapped(DefensivePlanPresentation.PressureSummary(plan, decision), Palette.Muted, 3, layout.Hint);
            if (selecting)
            {
                content.Section("DEFENSIVE CALLS | 1-9 / 0");
                string selected = plan?.RequestedCall.Id ?? assignment.Definition.Id;
                for (int i = 0; i < drive.Calls.Count; i++)
                    content.Row(((i + 1) % 10).ToString(), drive.Calls[i].Name, selected == drive.Calls[i].Id, drive.SupportsDefense(drive.Calls[i].Id));
                if (drive.Calls.Any(call => !drive.SupportsDefense(call.Id)))
                    content.Wrapped("Muted calls are unavailable. Select one to see why.", Palette.Muted, 2, layout.Hint);
                if (drive.SelectionRejection is { } rejection) content.Wrapped(rejection, Palette.Orange, 3, layout.Hint);
            }
            else if (decision != null && (decision.Observation.ConfirmedCarrier && decision.Run != null || decision.Coverage != null))
            {
                content.Section("LIVE ASSIGNMENT");
                // Run guidance supersedes coverage guidance once the carrier is confirmed.
                if (decision.Observation.ConfirmedCarrier && decision.Run is { } run) content.Wrapped(DefensivePlayRenderer.RunCue(run));
                else if (decision.Coverage is { } board) content.Wrapped(DefensivePlayRenderer.CoverageCue(board, assignment.ControlledSlot));
            }
        }
        else if (frame != null)
        {
            content.Wrapped(recorded?.OffensiveCall ?? "OFFENSIVE REPLAY", Palette.Gold);
            content.Wrapped("Recorded call and players. The scoreboard shows the recorded match context.", Palette.Muted);
        }
        else if (session.Action == MatchAction.Kneel)
        {
            content.Line("QB KNEEL", Palette.Gold);
            content.Wrapped("Protect the ball and use the clock.");
            if (selecting) content.Wrapped("Select a pass or run to cancel.", Palette.Muted);
        }
        else
        {
            var call = drive.Plays.SelectedPlay;
            content.Wrapped(call.Name, Palette.Gold, 2);
            content.Line(call.Definition.Info.Formation + (call.IsFlipped ? " | FLIPPED" : ""), Palette.Muted, layout.Hint);
            if (call.Family == PlayType.Pass)
            {
                content.Line(PassingGuidance.Reads(call), size: layout.Hint);
                if (!layout.Compact || GameplayHud.Wrap(call.Name, content.Width, layout.Body).Count == 1)
                    content.Line(PassingGuidance.Protection(call), Palette.Muted, layout.Hint);
            }
            else content.Line(call.Definition.Info.CategoryLabel, Palette.Muted, layout.Hint);

            if (selecting)
            {
                Bank(drive.Plays.PassPlays, "PASS | 1-9 / 0", "1234567890");
                Bank(drive.Plays.RunPlays, "RUN | Q-P", "QWERTYUIOP");
                content.Wrapped(drive.Plays.GetSuggestedPlayLabel(), Palette.Cyan, 2, layout.Hint);
            }
            else
            {
                content.Section("YOUR JOB");
                content.Line("YOU: " + (drive.Actors.Ball.Holder?.Glyph ?? "QB"), Palette.Gold);
                content.Wrapped(call.Family == PlayType.Pass ? "Move in the pocket. Press a receiver key to throw." : "Follow the highlighted blocking lane and find space.");
            }

            void Bank(IReadOnlyList<ResolvedPlay> calls, string title, string keys)
            {
                content.Section(title);
                for (int i = 0; i < calls.Count; i++)
                {
                    bool duplicate = calls.Count(c => c.Name == calls[i].Name) > 1;
                    string name = calls[i].Name + (duplicate ? " / " + calls[i].Definition.Info.Formation : "");
                    content.Row(keys[i].ToString(), name, call.Id == calls[i].Id);
                }
            }
        }
    }

    internal static string Controls(FullMatchSession session, bool defending, bool acceptResult = false)
    {
        if (session.Replay.CurrentFrame != null) return "Space / F: return\nEsc: pause | Tab: statistics";
        const string common = "WASD/arrows: move | Shift: sprint\nC: timeout | Esc: pause\nTab: stats | F: replay | Z: restart";
        if (session.Timed.Finished) return (acceptResult ? "Enter: accept result\n" : "") + "Tab: stats | F: replay | Z: restart";
        if (session.Drive.LastResult != null || session.Clock.Phase == ClockPhase.PeriodBreak)
            return "Space: continue | F: replay\nTab: statistics | Esc: pause\nZ: restart";
        if (session.SpecialTeams is { } special)
            return (special.Phase == SpecialTeamsPhase.Setup ? "Space: kick / ready\n" : "") + common;
        if (session.Kick is { } kick)
            return (session.HumanOnDefense ? MatchHudPresentation.Selecting(session) ? "Space: ready\n" : "" : kick.Phase switch
            {
                KickPhase.Setup => "Space: snap\n",
                KickPhase.Ready => "Space: start power\n",
                KickPhase.Power => "Space: lock power\n",
                KickPhase.Accuracy => "Space: lock accuracy / kick\n",
                _ => ""
            }) + "C: timeout | Esc: pause\nTab: stats | Z: restart";
        if (session.Drive.Live) return (!defending && session.Action == MatchAction.Scrimmage
            && session.Drive.Plays.SelectedPlay.Family == PlayType.Pass ? "1-5: throw to receiver\n" : "") + common;
        if (session.Action == MatchAction.Kneel) return "Space: kneel\n1-9/0 or Q-P: cancel kneel\n" + common;
        if (defending) return "1-9 / 0: switch defense\nQ/E: pressure | X: flip side\nH: personnel | J: front\nSpace: ready\n" + common;
        return "Space: snap | X: flip\nL: playbook | K: field goal\nB: punt | V: kneel\n" + common;
    }
}
