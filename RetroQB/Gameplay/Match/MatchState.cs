using RetroQB.Data;

namespace RetroQB.Gameplay;

/// <summary>Rules-only two-sided match. The legacy session is bridged later; no rendering or input dependencies.</summary>
public sealed class MatchState
{
    private readonly Dictionary<long, PlayResolution> _resolved = new();
    private readonly List<PlayResolution> _history = new();
    private long _nextPlayId = 1;
    public MatchRuleset Ruleset => MatchRuleset.TwoSidedTimed;
    public TeamMatchState User { get; }
    public TeamMatchState Opponent { get; }
    public string OpeningReceiverId { get; }
    public string SecondHalfReceiverId => Other(OpeningReceiverId).Definition.Id;
    public string PossessionId { get; private set; }
    public TeamMatchState Offense => Team(PossessionId);
    public TeamMatchState Defense => Other(PossessionId);
    public DriveStart Series { get; private set; }
    public PlayStart? ActivePlay { get; private set; }
    public NextPossession? PendingPossession { get; private set; }
    public IReadOnlyList<PlayResolution> History => _history.AsReadOnly();

    public MatchState(TeamDefinition user, TeamDefinition opponent, string openingReceiverId,
        SeasonStage stage = SeasonStage.RegularSeason, DriveStart? start = null)
    {
        if (string.IsNullOrWhiteSpace(user.Id) || string.IsNullOrWhiteSpace(opponent.Id) || user.Id == opponent.Id)
            throw new ArgumentException("A match requires two distinct team IDs.");
        User = new(user, false, stage);
        Opponent = new(opponent, true, stage);
        Team(openingReceiverId);
        OpeningReceiverId = PossessionId = openingReceiverId;
        Series = start ?? new();
        Series.Validate();
    }

    public TeamMatchState Team(string id) => id == User.Definition.Id ? User
        : id == Opponent.Definition.Id ? Opponent : throw new ArgumentException($"Unknown match team: {id}");
    public TeamMatchState Other(string id) => Team(id) == User ? Opponent : User;

    public PlayStart BeginPlay(string callId)
    {
        if (string.IsNullOrWhiteSpace(callId)) throw new ArgumentException("Call ID is required.");
        if (ActivePlay != null || PendingPossession != null) throw new InvalidOperationException("Finish the current play/transition first.");
        ActivePlay = new(_nextPlayId++, PossessionId, callId, Series);
        Offense.RecordCall(callId);
        return ActivePlay;
    }

    public PlayResolution Resolve(PlayEnded ended)
    {
        if (_resolved.TryGetValue(ended.PlayId, out var previous))
            return previous.Event == ended ? previous : throw new InvalidOperationException("Conflicting result for an already resolved play.");
        var play = ActivePlay ?? throw new InvalidOperationException("No active play.");
        if (ended.PlayId != play.Id || ended.OffenseId != play.OffenseId)
            throw new ArgumentException("Result does not belong to the active play.");
        var result = MatchRules.Resolve(play, ended, Defense.Definition.Id);
        // Validation happens before any score, stat, memory, or series mutations.
        ApplyStats(Offense.Statistics, result);
        if (result.ScoringTeamId != null) Team(result.ScoringTeamId).Score += result.Points;
        if (result.NextSeries != null) Series = result.NextSeries;
        PendingPossession = result.NextPossession;
        if (ended.Coverage is { } coverage)
            Defense.DefensiveMemory.RecordOutcome(new PlayRecord
            {
                CoverageScheme = coverage, Down = play.Series.Down, Distance = play.Series.Distance,
                Gain = result.Gain, IsSack = ended.Reason == PlayEndReason.Sack,
                Outcome = ended.Reason switch
                {
                    PlayEndReason.Touchdown => PlayOutcome.Touchdown,
                    PlayEndReason.Interception => PlayOutcome.Interception,
                    PlayEndReason.Incomplete => PlayOutcome.Incomplete,
                    PlayEndReason.PassDefended => PlayOutcome.PassDefended,
                    _ => PlayOutcome.Tackle
                }
            });
        _resolved.Add(ended.PlayId, result);
        _history.Add(result);
        ActivePlay = null;
        return result;
    }

    public bool ContinuePossession()
    {
        if (PendingPossession is not { } next) return false;
        StartPossession(next.TeamId, next.Series);
        return true;
    }

    internal void StartPossession(string teamId, DriveStart series)
    {
        Team(teamId);
        series.Validate();
        if (ActivePlay != null) throw new InvalidOperationException("Cannot replace a live play.");
        PossessionId = teamId;
        Series = series;
        PendingPossession = null;
    }

    internal void CancelPendingPossession() => PendingPossession = null;

    internal void ApplyDelayPenalty()
    {
        if (ActivePlay != null || PendingPossession != null) throw new InvalidOperationException("Penalty requires pre-snap.");
        float loss = MathF.Min(5, Series.OwnYardLine / 2);
        Series = new(Series.OwnYardLine - loss, Series.Down, Series.Distance + loss);
    }

    public void Reset()
    {
        User.Reset(); Opponent.Reset();
        _history.Clear(); _resolved.Clear();
        // IDs never repeat on restart, so a stale event cannot resolve a new play.
        ActivePlay = null; PendingPossession = null;
        PossessionId = OpeningReceiverId; Series = new();
    }

    private static void ApplyStats(StatisticsTracker tracker, PlayResolution result)
    {
        var ended = result.Event;
        var stats = ended.Stats ?? new();
        int yards = (int)MathF.Round(result.Gain);
        bool touchdown = result.Points == 7 && result.ScoringTeamId == ended.OffenseId;
        if (stats.PassAttempt)
        {
            tracker.RecordPassAttempt();
            if (stats.Target is { } target) tracker.RecordTarget(target);
            if (ended.Reason == PlayEndReason.Interception) tracker.RecordInterception();
        }
        if (stats.Completion && stats.Target is { } catcher)
        {
            tracker.RecordCompletion(catcher);
            tracker.RecordPassYards(catcher, yards, touchdown);
        }
        if (ended.Reason == PlayEndReason.Sack) tracker.RecordSack(Math.Max(0, -yards));
        if (stats.Rush == RushingRole.Quarterback) tracker.RecordQbRushYards(yards, touchdown);
        if (stats.Rush == RushingRole.RunningBack) tracker.RecordRushYards(yards, touchdown);
    }
}
