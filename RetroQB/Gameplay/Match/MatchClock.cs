namespace RetroQB.Gameplay;

public enum ClockPhase { PreSnap, LivePlay, Result, PeriodBreak, Finished }
public enum ClockStopReason { UntilSnap, Running, ResultPresentation, PeriodExpired, Timeout, DelayOfGame, Finished }
[Flags]
public enum ClockSuspension { None = 0, Pause = 1, Replay = 2, FocusLoss = 4, Statistics = 8 }
public readonly record struct ClockTick(bool PeriodExpired = false, bool PlayClockExpired = false);
public sealed record MatchClockSnapshot(int Quarter, double RemainingSeconds, double PlaySeconds,
    ClockPhase Phase, ClockStopReason Reason, ClockSuspension Suspension, bool IsOvertime,
    bool PendingPeriodEnd, int UserTimeouts, int OpponentTimeouts, int ExpiryCount, int RegulationPeriods = 4);

/// <summary>One owner of simulation time. UI, replay and callers never subtract wall time themselves.</summary>
public sealed class MatchClock
{
    private readonly string _userId, _opponentId;
    private readonly double _quarterLength, _playLength;
    private int _userTimeouts = 3, _opponentTimeouts = 3;
    private bool _runAfterResult, _timeoutUsed, _delayPending;
    public int RegulationPeriods { get; }
    public int HalftimePeriod => RegulationPeriods / 2 + 1;
    public string PeriodLabel => IsOvertime ? "OT" : RegulationPeriods == 2 ? $"H{Quarter}" : $"Q{Quarter}";
    public int Quarter { get; private set; } = 1;
    public double RemainingSeconds { get; private set; }
    public double PlaySeconds { get; private set; }
    public ClockPhase Phase { get; private set; } = ClockPhase.PreSnap;
    public ClockStopReason StopReason { get; private set; } = ClockStopReason.UntilSnap;
    public ClockSuspension Suspension { get; private set; }
    public bool PendingPeriodEnd { get; private set; }
    public bool IsOvertime { get; private set; }
    public int ExpiryCount { get; private set; }

    public MatchClock(string userId, string opponentId, double quarterSeconds = 180, double playSeconds = 20, int regulationPeriods = 4)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(opponentId) || userId == opponentId)
            throw new ArgumentException("Clock requires distinct team IDs.");
        if (!double.IsFinite(quarterSeconds) || quarterSeconds <= 0 || !double.IsFinite(playSeconds) || playSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(quarterSeconds));
        _userId = userId; _opponentId = opponentId;
        if (regulationPeriods is not (2 or 4)) throw new ArgumentOutOfRangeException(nameof(regulationPeriods));
        RegulationPeriods = regulationPeriods;
        _quarterLength = RemainingSeconds = quarterSeconds;
        _playLength = PlaySeconds = playSeconds;
    }

    public int Timeouts(string teamId) => teamId == _userId ? _userTimeouts
        : teamId == _opponentId ? _opponentTimeouts : throw new ArgumentException("Unknown timeout team.");

    public ClockTick Advance(double dt)
    {
        if (!double.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        if (Suspension != ClockSuspension.None || Phase is ClockPhase.Result or ClockPhase.PeriodBreak or ClockPhase.Finished)
            return default;
        bool expiry = false;
        // Stop at the first pre-snap boundary even when a caller supplies a large time step.
        double elapsed = Phase == ClockPhase.PreSnap ? Math.Min(dt, PlaySeconds) : dt;
        if (!IsOvertime && StopReason == ClockStopReason.Running && !PendingPeriodEnd)
        {
            RemainingSeconds = Math.Max(0, RemainingSeconds - elapsed);
            if (RemainingSeconds <= 1e-8)
            {
                RemainingSeconds = 0; PendingPeriodEnd = true; ExpiryCount++; expiry = true;
                StopReason = ClockStopReason.PeriodExpired;
                if (Phase != ClockPhase.LivePlay) Phase = ClockPhase.PeriodBreak;
            }
        }
        if (Phase != ClockPhase.PreSnap || PendingPeriodEnd || _delayPending) return new(expiry);
        PlaySeconds = Math.Max(0, PlaySeconds - dt);
        if (PlaySeconds <= 1e-8)
        {
            PlaySeconds = 0; _delayPending = true;
            return new(expiry, true);
        }
        return new(expiry);
    }

    public bool TrySnap()
    {
        if (Phase != ClockPhase.PreSnap || Suspension != ClockSuspension.None || PendingPeriodEnd || _delayPending)
            return false;
        Phase = ClockPhase.LivePlay;
        StopReason = ClockStopReason.Running;
        _timeoutUsed = false;
        return true;
    }

    public void EndPlay(bool stopsClock)
    {
        if (Phase != ClockPhase.LivePlay) throw new InvalidOperationException("No live play to end.");
        _runAfterResult = !stopsClock && !PendingPeriodEnd;
        Phase = ClockPhase.Result;
        StopReason = PendingPeriodEnd ? ClockStopReason.PeriodExpired : ClockStopReason.ResultPresentation;
    }

    public bool ReadyForNextPlay()
    {
        if (Phase is not (ClockPhase.Result or ClockPhase.PeriodBreak) || PendingPeriodEnd || Suspension != ClockSuspension.None)
            return false;
        Phase = ClockPhase.PreSnap; PlaySeconds = _playLength;
        StopReason = _runAfterResult ? ClockStopReason.Running : ClockStopReason.UntilSnap;
        return true;
    }

    public bool TryTimeout(string teamId)
    {
        int remaining = Timeouts(teamId);
        if (remaining == 0 || _timeoutUsed || PendingPeriodEnd || Suspension != ClockSuspension.None
            || Phase is not (ClockPhase.PreSnap or ClockPhase.Result)) return false;
        if (teamId == _userId) _userTimeouts--; else _opponentTimeouts--;
        _timeoutUsed = true; _runAfterResult = false; _delayPending = false;
        StopReason = ClockStopReason.Timeout; PlaySeconds = _playLength;
        return true;
    }

    public void Suspend(ClockSuspension reason)
    {
        if (reason == ClockSuspension.None || (reason & ~(ClockSuspension.Pause | ClockSuspension.Replay | ClockSuspension.FocusLoss | ClockSuspension.Statistics)) != 0)
            throw new ArgumentOutOfRangeException(nameof(reason));
        Suspension |= reason;
    }
    public void Resume(ClockSuspension reason) => Suspension &= ~reason;

    internal void ResetAfterDelay()
    {
        if (!_delayPending) throw new InvalidOperationException("No pending delay penalty.");
        _delayPending = false; _runAfterResult = false;
        PlaySeconds = _playLength; StopReason = ClockStopReason.DelayOfGame;
    }

    internal void StartPeriod(int quarter)
    {
        if (quarter < 2 || quarter > RegulationPeriods || Phase == ClockPhase.LivePlay)
            throw new InvalidOperationException("Invalid regulation period transition.");
        Quarter = quarter; RemainingSeconds = _quarterLength;
        PrepareBreak();
        if (quarter == HalftimePeriod) _userTimeouts = _opponentTimeouts = 3;
    }

    internal void StartOvertime(bool newPair)
    {
        IsOvertime = true; Quarter = RegulationPeriods + 1; RemainingSeconds = 0;
        PrepareBreak();
        if (newPair) _userTimeouts = _opponentTimeouts = 1;
    }

    private void PrepareBreak()
    {
        PendingPeriodEnd = false; _runAfterResult = _timeoutUsed = _delayPending = false;
        PlaySeconds = _playLength; Phase = ClockPhase.PeriodBreak; StopReason = ClockStopReason.UntilSnap;
    }

    internal void Finish()
    {
        Phase = ClockPhase.Finished; StopReason = ClockStopReason.Finished;
        PendingPeriodEnd = false; _runAfterResult = false;
    }

    public void Reset()
    {
        Quarter = 1; RemainingSeconds = _quarterLength; PlaySeconds = _playLength;
        Phase = ClockPhase.PreSnap; StopReason = ClockStopReason.UntilSnap;
        Suspension = ClockSuspension.None; IsOvertime = PendingPeriodEnd = false;
        _runAfterResult = _timeoutUsed = _delayPending = false;
        _userTimeouts = _opponentTimeouts = 3; ExpiryCount = 0;
    }

    public MatchClockSnapshot Snapshot() => new(Quarter, RemainingSeconds, PlaySeconds, Phase,
        StopReason, Suspension, IsOvertime, PendingPeriodEnd, _userTimeouts, _opponentTimeouts, ExpiryCount, RegulationPeriods);
}
