using RetroQB.AI;
using RetroQB.Input;
using RetroQB.Gameplay.Replay;

namespace RetroQB.Gameplay;

public sealed record MatchInput(int? Call = null, int? Run = null, bool Ready = false, bool Kick = false,
    bool Punt = false, bool Kneel = false, bool Flip = false, bool Timeout = false, bool Pause = false,
    bool Replay = false, bool Restart = false, bool Focused = true, bool Statistics = false, int SummaryScroll = 0);

/// <summary>Both possessions share the same actors, contact detection, clock and exactly-once resolver.</summary>
public sealed class FullMatchSession
{
    private readonly Random _random;
    private readonly IGameInput _input;
    private double _presentation, _actionSeconds;
    private bool _cpuKickGood;
    public DefensiveDrive Drive { get; }
    public TimedMatch Timed => Drive.Timed;
    public MatchClock Clock => Timed.Clock;
    public MatchState Match => Timed.Match;
    public ReplayPlayer Replay { get; } = new();
    public MatchAction Action { get; private set; }
    public FieldGoalAttempt? Kick { get; private set; }
    public SpecialTeamsPlay? SpecialTeams { get; private set; }
    public bool UserReceivingKick => Drive.PreparedOffenseId != Match.User.Definition.Id;
    public double SnapRemaining { get; private set; }
    public bool HumanOnDefense => Drive.HumanOnDefense;
    public bool ShowStatistics => (Clock.Suspension & ClockSuspension.Statistics) != 0;
    public int SummaryOffset { get; private set; }
    public double PresentationSeconds => _presentation;
    public string Status => Timed.Finished ? $"FINAL: {Match.Team(Timed.WinnerId!).Definition.Name} wins"
        : Clock.IsOvertime ? $"OT {Timed.OvertimePair} | attempt {Timed.OvertimeAttempt}/2"
        : Clock.Phase == ClockPhase.PeriodBreak ? Clock.Quarter == Clock.HalftimePeriod ? "HALFTIME" : Clock.PeriodLabel : "";

    public FullMatchSession(IGameInput input, int seed, TimedMatch match)
    {
        _input = input; _random = new(seed ^ 0x42319); Drive = new(input, seed, match: match); Prepare();
    }

    public StrategyContext Context()
        => new(Clock.RegulationPeriods == 2 ? Clock.Quarter * 2 : Clock.Quarter, Clock.RemainingSeconds, Clock.IsOvertime, Match.Offense.Score - Match.Defense.Score,
            Match.Series.Down, Match.Series.OwnYardLine, Match.Series.Distance, Clock.Timeouts(Match.Defense.Definition.Id),
            Clock.PlaySeconds, Clock.StopReason == ClockStopReason.Running
                || Drive.LastResult is { StopsClock: false } && Clock.StopReason == ClockStopReason.ResultPresentation);

    private void Prepare()
    {
        _presentation = _actionSeconds = 0; Kick = null; SpecialTeams = null;
        if (Timed.KickoffReceiverId != null && Clock.Phase == ClockPhase.PreSnap)
        {
            Timed.PrepareKickoff(); Drive.PrepareSpecialTeams(); Action = MatchAction.Kickoff;
            SpecialTeams = new(true, 35, _random); SnapRemaining = 6; return;
        }
        Action = HumanOnDefense ? MatchStrategy.Choose(Context()) : MatchAction.Scrimmage;
        SnapRemaining = Math.Max(6, MatchStrategy.Cadence(Context(), 6 + _random.NextDouble() * 2));
        if (Action == MatchAction.FieldGoal) Kick = new(Drive.Plays.LineOfScrimmage);
        if (Action == MatchAction.Punt) SpecialTeams = new(false, Match.Series.OwnYardLine, _random);
    }

    public bool SelectAction(MatchAction action)
    {
        if (Action == MatchAction.Kickoff || HumanOnDefense || Drive.Live || Drive.LastResult != null || Clock.Phase != ClockPhase.PreSnap) return false;
        if (action == MatchAction.Punt && (Clock.IsOvertime || Match.Series.Down != 4)) return false;
        if (action == MatchAction.FieldGoal && FieldGoalAttempt.DistanceFrom(Drive.Plays.LineOfScrimmage) > FieldGoalAttempt.MaxDistance) return false;
        if (action == MatchAction.Kickoff) return false;
        Action = action; Kick = action == MatchAction.FieldGoal ? new(Drive.Plays.LineOfScrimmage) : null;
        SpecialTeams = action == MatchAction.Punt ? new(false, Match.Series.OwnYardLine, _random) : null;
        return true;
    }

    public void Restart()
    {
        Replay.Unload(); Drive.Restart(); SummaryOffset = 0; Prepare();
    }

    public void Update(float dt, MatchInput? input = null)
    {
        if (!float.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        input ??= new();
        if (input.Restart) { Restart(); return; }
        if (input.Statistics)
        {
            if (ShowStatistics) Clock.Resume(ClockSuspension.Statistics); else Clock.Suspend(ClockSuspension.Statistics);
        }
        SummaryOffset = Math.Clamp(SummaryOffset + input.SummaryScroll, 0, Math.Max(0, Match.History.Count - 5));
        if (input.Focused) Clock.Resume(ClockSuspension.FocusLoss); else Clock.Suspend(ClockSuspension.FocusLoss);
        if (input.Pause)
        {
            if ((Clock.Suspension & ClockSuspension.Pause) != 0) Clock.Resume(ClockSuspension.Pause);
            else Clock.Suspend(ClockSuspension.Pause);
        }
        if (input.Replay && !Drive.Live)
        {
            if (Replay.IsPlaying) { Replay.Unload(); Clock.Resume(ClockSuspension.Replay); }
            else if (Drive.LastReplay is { } clip) { Replay.Load(clip); Clock.Suspend(ClockSuspension.Replay); }
        }
        if (Replay.IsPlaying)
        {
            if ((Clock.Suspension & ~ClockSuspension.Replay) == 0) Replay.Update(dt);
            if (Replay.IsComplete || input.Ready) { Replay.Unload(); Clock.Resume(ClockSuspension.Replay); }
            return;
        }
        if (Clock.Suspension != ClockSuspension.None || Timed.Finished) return;
        if (input.Timeout) Clock.TryTimeout(Match.User.Definition.Id);
        if (!HumanOnDefense && MatchStrategy.DefensiveTimeout(Context())) Clock.TryTimeout(Match.Opponent.Definition.Id);
        if (Drive.Live && SpecialTeams is { } special)
        {
            if (!special.IsKickoff || special.Phase == SpecialTeamsPhase.Return) Timed.Advance(dt);
            var movement = Constants.OrientDirection(_input.GetMovementDirection(),
                Drive.Execution.ScreenRelativeDefensiveInput && UserReceivingKick);
            special.Update(dt, movement, _input.IsSprintHeld(), UserReceivingKick);
            if (special.Result is { } result)
                Drive.ResolveSpecial(special.IsKickoff ? PlayEndReason.Kickoff : PlayEndReason.Punt,
                    Math.Clamp(100 - result.ReceivingYard, 0, 100), result);
            return;
        }
        if (Drive.Live)
        {
            if (Action == MatchAction.Scrimmage) { Drive.Update(dt); return; }
            Timed.Advance(dt); _actionSeconds += dt;
            if (Action == MatchAction.FieldGoal && HumanOnDefense) Kick!.Update(dt);
            if (Action == MatchAction.FieldGoal && !HumanOnDefense)
            {
                if (input.Ready) Kick!.PressSpace();
                Kick!.Update(dt);
                if (Kick.Phase != KickPhase.Result) return;
                Drive.ResolveSpecial(Kick.IsGood ? PlayEndReason.FieldGoalGood : PlayEndReason.FieldGoalMissed,
                    Match.ActivePlay!.Series.OwnYardLine - 7);
            }
            else if (_actionSeconds >= (Action == MatchAction.FieldGoal ? 2.05 : .75))
            {
                var play = Match.ActivePlay!;
                Drive.ResolveSpecial(Action switch { MatchAction.Kneel => PlayEndReason.Kneel,
                    MatchAction.Punt => PlayEndReason.Punt, _ => _cpuKickGood ? PlayEndReason.FieldGoalGood : PlayEndReason.FieldGoalMissed },
                    play.Series.OwnYardLine - (Action == MatchAction.Kneel ? 1 : Action == MatchAction.FieldGoal ? 7 : 0));
            }
            return;
        }
        if (Drive.LastResult != null || Clock.Phase == ClockPhase.PeriodBreak)
        {
            _presentation += dt;
            // A change of possession or a period break gives the player time to read the result.
            bool summary = Drive.LastResult?.DriveEnded == true || Clock.Phase == ClockPhase.PeriodBreak;
            if ((summary ? input.Ready : _presentation >= 1.25) && Drive.Continue()) Prepare();
            return;
        }
        if (Action == MatchAction.Kickoff && SpecialTeams != null)
        {
            SnapRemaining -= dt;
            if (input.Ready || UserReceivingKick && SnapRemaining <= 0)
            {
                if (Timed.Advance(0, "special.kickoff") != null) SpecialTeams.Start();
            }
            return;
        }
        if (HumanOnDefense)
        {
            if (input.Call is >= 0 and < 10) Drive.SelectDefense(DefensivePlaybook.All[input.Call.Value].Id);
            if (input.Ready) SnapRemaining = 0;
        }
        else
        {
            if (input.Call is { } pass && Drive.SelectOffense(pass)) SelectAction(MatchAction.Scrimmage);
            if (input.Run is { } run && Drive.SelectOffense(run, true)) SelectAction(MatchAction.Scrimmage);
            if (input.Flip) Drive.SelectOffense(0, flip: true);
            if (input.Kick) SelectAction(MatchAction.FieldGoal);
            if (input.Punt) SelectAction(MatchAction.Punt);
            if (input.Kneel) SelectAction(MatchAction.Kneel);
        }
        int quarter = Clock.Quarter;
        double playSeconds = Clock.PlaySeconds;
        Drive.AdvancePreSnap(dt);
        if (Clock.Quarter != quarter || Clock.Phase != ClockPhase.PreSnap || Clock.PlaySeconds > playSeconds) return;
        if (HumanOnDefense)
        {
            // A timeout can invalidate a previously safe kneel plan.
            Action = MatchStrategy.Choose(Context());
            if (Action == MatchAction.Punt && SpecialTeams == null) SpecialTeams = new(false, Match.Series.OwnYardLine, _random);
            if (Action != MatchAction.Punt) SpecialTeams = null;
            SnapRemaining -= dt;
        }
        if (!(HumanOnDefense ? SnapRemaining <= 0 : input.Ready)) return;
        if (Action == MatchAction.Scrimmage) { Drive.Snap(); return; }
        if (Timed.Advance(0, $"special.{Action.ToString().ToLowerInvariant()}") == null) return;
        _actionSeconds = 0;
        if (Action == MatchAction.Punt) SpecialTeams!.Start();
        if (Action == MatchAction.FieldGoal)
        {
            Kick = new(Drive.Plays.LineOfScrimmage);
            if (HumanOnDefense)
            {
                _cpuKickGood = _random.NextDouble() < MatchStrategy.KickProbability(Kick.Distance);
                Kick.BeginCpuFlight(_cpuKickGood);
            }
            else Kick.PressSpace();
        }
    }
}
