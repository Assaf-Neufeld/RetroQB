using RetroQB.Data;

namespace RetroQB.Gameplay;

/// <summary>Orders timeout, clock, snap, play result and period transitions. GameSession wiring is M7–M9.</summary>
public sealed class TimedMatch
{
    public MatchState Match { get; }
    public MatchClock Clock { get; }
    public bool SpecialTeamsEnabled { get; }
    public string? KickoffReceiverId { get; private set; }
    public string? WinnerId { get; private set; }
    public bool Finished => Clock.Phase == ClockPhase.Finished;
    public int OvertimePair { get; private set; }
    public int OvertimeAttempt { get; private set; }
    public string? OvertimeOpenerId { get; private set; }

    public TimedMatch(TeamDefinition user, TeamDefinition opponent, string openingReceiverId,
        SeasonStage stage = SeasonStage.RegularSeason, DriveStart? start = null,
        double quarterSeconds = 180, double playSeconds = 20, int regulationPeriods = 4, bool specialTeams = false)
    {
        Match = new(user, opponent, openingReceiverId, stage, start);
        Clock = new(user.Id, opponent.Id, quarterSeconds, playSeconds, regulationPeriods);
        SpecialTeamsEnabled = specialTeams;
        if (specialTeams) KickoffReceiverId = openingReceiverId;
    }

    public PlayStart? Advance(double dt, string? snapCallId = null, string? timeoutTeamId = null)
    {
        if (!double.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        if (timeoutTeamId != null) Clock.TryTimeout(timeoutTeamId);
        var tick = Clock.Advance(dt);
        if (tick.PeriodExpired && Clock.Phase != ClockPhase.LivePlay)
        {
            CompletePeriod();
            return null; // Never reinterpret a last-second snap as a snap in the new quarter.
        }
        if (tick.PlayClockExpired)
        {
            Match.ApplyDelayPenalty();
            Clock.ResetAfterDelay();
            return null;
        }
        if (snapCallId == null || Finished || Clock.Phase != ClockPhase.PreSnap
            || Clock.Suspension != ClockSuspension.None) return null;
        if (string.IsNullOrWhiteSpace(snapCallId)) throw new ArgumentException("Call ID is required.");
        if (Match.ActivePlay != null || Match.PendingPossession != null) return null;
        return Clock.TrySnap() ? Match.BeginPlay(snapCallId) : null;
    }

    public PlayResolution Resolve(PlayEnded ended)
    {
        bool active = Match.ActivePlay?.Id == ended.PlayId;
        if (active && (Clock.Phase != ClockPhase.LivePlay || Clock.Suspension != ClockSuspension.None))
            throw new InvalidOperationException("Cannot resolve a non-live/suspended play.");
        if (active && Clock.IsOvertime && ended.Reason == PlayEndReason.Punt)
            throw new ArgumentException("Punting is unavailable in overtime.");
        var result = Match.Resolve(ended);
        if (!active) return result; // Exactly-once result also means exactly-once period advancement.
        Clock.EndPlay(result.StopsClock);
        if (Clock.PendingPeriodEnd) CompletePeriod();
        else if (Clock.IsOvertime && result.DriveEnded) CompleteOvertimeAttempt();
        else if (SpecialTeamsEnabled && result.Points > 0 && !Finished && !Clock.IsOvertime)
            KickoffReceiverId = result.NextPossession!.TeamId;
        return result;
    }

    public bool Continue()
    {
        if (Finished || Clock.Suspension != ClockSuspension.None
            || Clock.Phase is not (ClockPhase.Result or ClockPhase.PeriodBreak)) return false;
        Match.ContinuePossession();
        return Clock.ReadyForNextPlay();
    }

    private void CompletePeriod()
    {
        if (Clock.Quarter == Clock.RegulationPeriods)
        {
            if (!TryFinish())
            {
                KickoffReceiverId = null;
                OvertimePair = OvertimeAttempt = 1;
                OvertimeOpenerId = Match.OpeningReceiverId;
                Match.StartPossession(OvertimeOpenerId, new(75));
                Clock.StartOvertime(true);
            }
            return;
        }
        int next = Clock.Quarter + 1;
        if (next == Clock.HalftimePeriod)
        {
            Match.StartPossession(Match.SecondHalfReceiverId, new());
            if (SpecialTeamsEnabled) KickoffReceiverId = Match.SecondHalfReceiverId;
        }
        Clock.StartPeriod(next);
    }

    internal void PrepareKickoff()
    {
        if (KickoffReceiverId is not { } receiver || Clock.Phase != ClockPhase.PreSnap) return;
        Match.StartPossession(Match.Other(receiver).Definition.Id, new(35));
        KickoffReceiverId = null;
    }
    private void CompleteOvertimeAttempt()
    {
        if (OvertimeAttempt == 1)
        {
            OvertimeAttempt = 2;
            Match.StartPossession(Match.Other(OvertimeOpenerId!).Definition.Id, new(75));
            Clock.StartOvertime(false);
        }
        else if (!TryFinish())
        {
            OvertimePair++; OvertimeAttempt = 1;
            OvertimeOpenerId = Match.Other(OvertimeOpenerId!).Definition.Id;
            Match.StartPossession(OvertimeOpenerId, new(75));
            Clock.StartOvertime(true);
        }
    }

    private bool TryFinish()
    {
        if (Match.User.Score == Match.Opponent.Score) return false;
        WinnerId = Match.User.Score > Match.Opponent.Score ? Match.User.Definition.Id : Match.Opponent.Definition.Id;
        Match.CancelPendingPossession();
        KickoffReceiverId = null; Clock.Finish();
        return true;
    }

    public void Restart()
    {
        Match.Reset(); Clock.Reset(); WinnerId = OvertimeOpenerId = null;
        OvertimePair = OvertimeAttempt = 0;
        KickoffReceiverId = SpecialTeamsEnabled ? Match.OpeningReceiverId : null;
    }
}
