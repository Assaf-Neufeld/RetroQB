using RetroQB.Input;

namespace RetroQB.Gameplay;

public sealed record CompletedTimedGame(GameResult Result, GameStatsSnapshot Offense, DefenseStatsSnapshot Defense,
    GameStatsSnapshot OpponentOffense, DefenseStatsSnapshot OpponentDefense);

/// <summary>Commits a matchup only when its result is accepted; restarting never rolls back an earlier stage.</summary>
public sealed class TimedSeason
{
    private readonly IGameInput _input;
    private readonly int _seed;
    private readonly double _quarterSeconds;
    private readonly PlayerRecordStore _store;
    private readonly List<CompletedTimedGame> _completed = new();
    private readonly Guid _seasonId = Guid.NewGuid();
    public TeamDefinition Team { get; }
    public string PlayerName { get; private set; }
    public SeasonStage Stage { get; private set; }
    public FullMatchSession Current { get; private set; }
    public SeasonSummary Summary { get; } = new();
    public IReadOnlyList<CompletedTimedGame> Completed => _completed.AsReadOnly();
    public bool Pregame { get; private set; } = true;
    public bool Complete { get; private set; }
    public bool Saved { get; private set; }
    public LeaderboardSummary Leaderboard { get; private set; } = LeaderboardSummary.Empty;
    public string StorageMessage => _store.StatusMessage;

    public TimedSeason(IGameInput input, int seed, TeamDefinition team, string playerName, PlayerRecordStore store, double quarterSeconds = 180)
    {
        if (store.Ruleset != MatchRuleset.TwoSidedTimed) throw new ArgumentException("Timed seasons need a timed leaderboard.");
        _input = input; _seed = seed; Team = team; PlayerName = PlayerRecordStore.NormalizeName(playerName);
        _store = store; _quarterSeconds = quarterSeconds; Current = CreateMatch();
    }

    private FullMatchSession CreateMatch()
        => new(_input, unchecked(_seed + (int)Stage * 7919), new(Team, TeamCatalog.ForStage(Stage), Team.Id, Stage, quarterSeconds: _quarterSeconds));

    public void EditName(string text, bool backspace)
    {
        if (!Pregame || _completed.Count != 0) return;
        if (backspace && PlayerName.Length > 0) PlayerName = PlayerName[..^1];
        PlayerName = (PlayerName + text)[..Math.Min(18, PlayerName.Length + text.Length)];
    }

    public void Update(float dt, MatchInput input, bool accept = false)
    {
        if (accept) { Accept(); return; }
        if (Pregame) return;
        if (Complete) { Current.Update(dt, input with { Restart = false }); return; }
        Current.Update(dt, input);
    }

    public void Accept()
    {
        if (Complete) { if (!Saved) Save(); return; }
        if (Pregame)
        {
            PlayerName = PlayerRecordStore.NormalizeName(PlayerName);
            if (PlayerName.Length > 0) Pregame = false;
            return;
        }
        if (!Current.Timed.Finished || Current.Replay.IsPlaying || Current.Clock.Suspension != ClockSuspension.None) return;
        var match = Current.Match;
        foreach (var result in match.History.Where(r => r.Event.OffenseId == Team.Id))
        {
            if (result.Event.Reason is PlayEndReason.FieldGoalGood or PlayEndReason.FieldGoalMissed or PlayEndReason.Punt) continue;
            var play = match.StartOf(result.Event.PlayId);
            Summary.RecordPlay(new() { Down = play.Series.Down, Distance = play.Series.Distance, Gain = result.Gain,
                WasRun = result.Event.Stats?.Rush is RushingRole.Quarterback or RushingRole.RunningBack,
                Outcome = result.Points == 7 && result.ScoringTeamId == Team.Id ? PlayOutcome.Touchdown
                    : result.Event.Reason == PlayEndReason.Interception ? PlayOutcome.Interception
                    : result.TurnoverOnDowns ? PlayOutcome.Turnover : PlayOutcome.Tackle });
        }
        bool won = Current.Timed.WinnerId == Team.Id;
        _completed.Add(new(new(Stage, match.User.Score, match.Opponent.Score, won), match.User.Stats,
            match.User.DefenseStats, match.Opponent.Stats, match.Opponent.DefenseStats));
        Summary.RecordGame(Stage, match.User.Score, match.Opponent.Score, StatsAggregation.Sum(_completed.Select(g => g.Offense)));
        if (!won || Stage == SeasonStage.SuperBowl) { Complete = true; Save(); }
        else { Stage = Stage.GetNextStage()!.Value; Current = CreateMatch(); Pregame = true; }
    }

    private void Save()
    {
        Saved = _store.TrySaveSeasonResult(PlayerName, Team.Name, Summary.BuildThreeStageScoreHistory(),
            Summary.BuildDominanceScoreDetails(), Summary.ComputeDominanceScore(), out var leaderboard, _seasonId, Completed);
        Leaderboard = leaderboard;
    }
}
