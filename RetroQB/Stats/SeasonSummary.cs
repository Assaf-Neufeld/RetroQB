namespace RetroQB.Stats;

/// <summary>
/// Records the result of a single game (one stage).
/// </summary>
public readonly record struct GameResult(
    SeasonStage Stage,
    int PlayerScore,
    int AwayScore,
    bool Won);

/// <summary>
/// Aggregated season summary shown at the end of a run.
/// </summary>
public sealed class SeasonSummary
{
    private readonly List<GameResult> _games = new();
    private readonly QbStatLine _cumulativeQb = new();

    public IReadOnlyList<GameResult> Games => _games;

    /// <summary>Cumulative QB stats across all games.</summary>
    public QbStatsSnapshot CumulativeQbStats => new(
        _cumulativeQb.Completions,
        _cumulativeQb.Attempts,
        _cumulativeQb.PassYards,
        _cumulativeQb.PassTds,
        _cumulativeQb.Interceptions,
        _cumulativeQb.RushYards,
        _cumulativeQb.RushTds);

    /// <summary>The furthest stage the player reached.</summary>
    public SeasonStage FurthestStage =>
        _games.Count > 0 ? _games[^1].Stage : SeasonStage.RegularSeason;

    /// <summary>True if the player won the Super Bowl.</summary>
    public bool IsChampion =>
        _games.Any(g => g.Stage == SeasonStage.SuperBowl && g.Won);

    /// <summary>
    /// Records a completed game and accumulates QB stats.
    /// </summary>
    public void RecordGame(SeasonStage stage, int playerScore, int awayScore, QbStatsSnapshot qbSnap)
    {
        _games.Add(new GameResult(stage, playerScore, awayScore, playerScore > awayScore));
        _cumulativeQb.Completions += qbSnap.Completions;
        _cumulativeQb.Attempts += qbSnap.Attempts;
        _cumulativeQb.PassYards += qbSnap.PassYards;
        _cumulativeQb.PassTds += qbSnap.PassTds;
        _cumulativeQb.Interceptions += qbSnap.Interceptions;
        _cumulativeQb.RushYards += qbSnap.RushYards;
        _cumulativeQb.RushTds += qbSnap.RushTds;
    }

    /// <summary>
    /// Computes the NFL passer rating (scale 0–158.3).
    /// </summary>
    public float ComputeQbRating()
    {
        var qb = CumulativeQbStats;
        if (qb.Attempts == 0) return 0f;

        float att = qb.Attempts;

        // a = ((Comp/Att) - 0.3) * 5
        float a = ((qb.Completions / att) - 0.3f) * 5f;
        // b = ((Yards/Att) - 3) * 0.25
        float b = ((qb.PassYards / att) - 3f) * 0.25f;
        // c = (TD/Att) * 20
        float c = (qb.PassTds / att) * 20f;
        // d = 2.375 - ((INT/Att) * 25)
        float d = 2.375f - ((qb.Interceptions / att) * 25f);

        // Clamp each to [0, 2.375]
        a = Math.Clamp(a, 0f, 2.375f);
        b = Math.Clamp(b, 0f, 2.375f);
        c = Math.Clamp(c, 0f, 2.375f);
        d = Math.Clamp(d, 0f, 2.375f);

        return ((a + b + c + d) / 6f) * 100f;
    }

    /// <summary>Resets the summary for a new season.</summary>
    public void Reset()
    {
        _games.Clear();
        _cumulativeQb.Reset();
    }
}
