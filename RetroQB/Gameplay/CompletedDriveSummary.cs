using RetroQB.Data;

namespace RetroQB.Gameplay;

public sealed record CompletedDriveSummary(string TeamName, string Outcome, int Plays, float Yards, int Points, string Next)
{
    public static CompletedDriveSummary? From(FullMatchSession session)
    {
        var last = session.Drive.LastResult;
        if (last?.DriveEnded != true) return null;
        var m = session.Match;
        var plays = m.History.Reverse().Skip(1)
            .TakeWhile(p => !p.DriveEnded && p.Event.OffenseId == last.Event.OffenseId).Prepend(last).ToArray();
        string outcome = last.TurnoverOnDowns ? "TURNOVER ON DOWNS" : last.Event.Reason switch
        {
            PlayEndReason.Kickoff => "KICKOFF", PlayEndReason.Punt => "PUNT", PlayEndReason.Interception => "INTERCEPTED",
            PlayEndReason.FieldGoalMissed => "FIELD GOAL MISSED", PlayEndReason.FieldGoalGood => "FIELD GOAL GOOD",
            PlayEndReason.Touchdown => "TOUCHDOWN", PlayEndReason.Safety => "SAFETY", _ => "DRIVE OVER"
        };
        if (last.Event.KickReturn is { } kick) outcome += kick.Safety ? " RETURN SAFETY" : kick.Touchdown ? " RETURN TOUCHDOWN" : kick.Touchback ? " TOUCHBACK" : $" | {kick.ReturnYards} YD RETURN";
        string next = session.Timed.Finished ? "FINAL" : session.Clock.Phase == ClockPhase.PeriodBreak ? session.Status
            : session.Timed.KickoffReceiverId is { } receiver ? $"Kickoff to {m.Team(receiver).Definition.Name}"
            : m.PendingPossession is { } pending
                ? $"{m.Team(pending.TeamId).Definition.Name} ball | {(pending.Series.OwnYardLine <= 50 ? $"own {pending.Series.OwnYardLine:0}" : $"opponent {100 - pending.Series.OwnYardLine:0}")}" : "Next possession";
        return new(m.Team(last.Event.KickReturn?.Touchdown == true ? last.ScoringTeamId! : last.Event.OffenseId).Definition.Name, outcome, plays.Length, plays.Sum(p => p.Gain),
            last.Event.KickReturn != null ? last.Points : plays.Where(p => p.ScoringTeamId == last.Event.OffenseId).Sum(p => p.Points), next);
    }
}
