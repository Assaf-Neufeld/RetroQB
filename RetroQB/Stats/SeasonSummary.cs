namespace RetroQB.Stats;

using RetroQB.Gameplay;

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
    private readonly RushStatLine _cumulativeRb = new();
    private readonly Dictionary<string, SkillStatLine> _cumulativeReceivers = new(StringComparer.OrdinalIgnoreCase);
    private int _offensivePlays;
    private int _successfulPlays;
    private int _explosivePlays;
    private int _runPlays;
    private int _successfulRuns;
    private int _explosiveRuns;

    public IReadOnlyList<GameResult> Games => _games;

    /// <summary>Cumulative QB stats across all games.</summary>
    public QbStatsSnapshot CumulativeQbStats => new(
        _cumulativeQb.Completions,
        _cumulativeQb.Attempts,
        _cumulativeQb.PassYards,
        _cumulativeQb.PassTds,
        _cumulativeQb.Interceptions,
        _cumulativeQb.Sacks,
        _cumulativeQb.SackYardsLost,
        _cumulativeQb.RushAttempts,
        _cumulativeQb.RushYards,
        _cumulativeQb.RushTds);

    /// <summary>Cumulative RB stats across all games.</summary>
    public RbStatsSnapshot CumulativeRbStats => new(
        _cumulativeRb.Attempts,
        _cumulativeRb.Yards,
        _cumulativeRb.Tds);

    /// <summary>The furthest stage the player reached.</summary>
    public SeasonStage FurthestStage =>
        _games.Count > 0 ? _games[^1].Stage : SeasonStage.RegularSeason;

    /// <summary>True if the player won the Super Bowl.</summary>
    public bool IsChampion =>
        _games.Any(g => g.Stage == SeasonStage.SuperBowl && g.Won);

    /// <summary>
    /// Records a completed game and accumulates QB stats.
    /// </summary>
    public void RecordGame(SeasonStage stage, int playerScore, int awayScore, GameStatsSnapshot stats)
    {
        _games.Add(new GameResult(stage, playerScore, awayScore, playerScore > awayScore));
        ApplySeasonSnapshot(stats);
    }

    /// <summary>
    /// Records a completed offensive play for season-level dominance scoring.
    /// </summary>
    public void RecordPlay(PlayRecord play)
    {
        _offensivePlays++;

        bool isSuccessful = IsSuccessfulPlay(play);
        bool isExplosive = IsExplosivePlay(play);

        if (isSuccessful)
        {
            _successfulPlays++;
        }

        if (isExplosive)
        {
            _explosivePlays++;
        }

        if (!play.WasRun)
        {
            return;
        }

        _runPlays++;
        if (isSuccessful)
        {
            _successfulRuns++;
        }

        if (isExplosive)
        {
            _explosiveRuns++;
        }
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

        float baseRating = ((a + b + c + d) / 6f) * 100f;

        // Sack penalty (house rule): frequent sacks and larger losses reduce rating.
        float sackRatePenalty = (qb.Sacks / att) * 20f;
        float sackYardPenalty = (qb.SackYardsLost / att) * 0.5f;

        return Math.Clamp(baseRating - sackRatePenalty - sackYardPenalty, 0f, 158.3f);
    }

    /// <summary>
    /// Computes a 0-100 dominance score.
    /// Stage progression (Playoff/Super Bowl/Champion) is the largest component,
    /// then the remaining score is a weighted mix of core efficiency stats.
    /// </summary>
    public float ComputeDominanceScore()
    {
        float stageScore = ComputeStageProgressionScore();

        QbStatsSnapshot qb = CumulativeQbStats;
        float attempts = qb.Attempts;
        float completions = qb.Completions;
        float interceptions = qb.Interceptions;
        float sacks = qb.Sacks;

        float qbRatingScore = Math.Clamp((ComputeQbRating() / 158.3f) * 100f, 0f, 100f);
        float completionRate = attempts > 0f ? completions / attempts : 0f;
        float completionScore = NormalizeRate(completionRate, 0.72f);

        float interceptionRate = attempts > 0f ? interceptions / attempts : 1f;
        float interceptionScore = Math.Clamp((1f - (interceptionRate / 0.08f)) * 100f, 0f, 100f);

        float sackRate = attempts > 0f ? sacks / attempts : 1f;
        float sackScore = Math.Clamp((1f - (sackRate / 0.12f)) * 100f, 0f, 100f);

        float ypc = _cumulativeRb.Attempts > 0
            ? _cumulativeRb.Yards / (float)_cumulativeRb.Attempts
            : 0f;
        float ypcScore = Math.Clamp(((ypc - 2.5f) / 3.5f) * 100f, 0f, 100f);

        float explosiveRate = _offensivePlays > 0 ? _explosivePlays / (float)_offensivePlays : 0f;
        float explosiveScore = NormalizeRate(explosiveRate, 0.22f);

        float weightedScore =
            (stageScore * 0.50f) +
            (qbRatingScore * 0.12f) +
            (completionScore * 0.10f) +
            (interceptionScore * 0.08f) +
            (sackScore * 0.08f) +
            (ypcScore * 0.06f) +
            (explosiveScore * 0.06f);

        return Math.Clamp(weightedScore, 0f, 100f);
    }

    /// <summary>
    /// Builds a compact 3-stage score history string.
    /// Example: "REG 21-14 | PLAYOFF 17-21 | SB --"
    /// </summary>
    public string BuildThreeStageScoreHistory()
    {
        var parts = new List<string>();

        string regularSeason = GetStageScoreText(SeasonStage.RegularSeason);
        if (!string.IsNullOrWhiteSpace(regularSeason))
        {
            parts.Add($"REG {regularSeason}");
        }

        string playoff = GetStageScoreText(SeasonStage.Playoff);
        if (!string.IsNullOrWhiteSpace(playoff))
        {
            parts.Add($"PLAYOFF {playoff}");
        }

        string superBowl = GetStageScoreText(SeasonStage.SuperBowl);
        if (!string.IsNullOrWhiteSpace(superBowl))
        {
            parts.Add($"SB {superBowl}");
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : "REG --";
    }

    /// <summary>
    /// Builds a compact breakdown of the main stats that feed dominance scoring.
    /// Example: "STG PLAYOFF | QBRT 96.5 | CMP 64% | INT 3 | SACK 5 | YPC 4.2 | XPL 15%"
    /// </summary>
    public string BuildDominanceScoreDetails()
    {
        if (_games.Count == 0)
        {
            return "STG REG | QBRT -- | CMP -- | INT -- | SACK -- | YPC -- | XPL --";
        }

        QbStatsSnapshot qb = CumulativeQbStats;
        float attempts = qb.Attempts;
        float completionRate = attempts > 0f ? qb.Completions / attempts : 0f;
        float explosiveRate = _explosivePlays / (float)_offensivePlays;
        float yardsPerCarry = _cumulativeRb.Attempts > 0
            ? _cumulativeRb.Yards / (float)_cumulativeRb.Attempts
            : 0f;
        string stageLabel = GetStageProgressionLabel();
        string completionText = attempts > 0f ? $"{completionRate * 100f:0}%" : "--";
        string explosiveText = _offensivePlays > 0 ? $"{explosiveRate * 100f:0}%" : "--";
        string ypcText = _cumulativeRb.Attempts > 0 ? $"{yardsPerCarry:0.0}" : "--";
        string qbRatingText = attempts > 0f ? $"{ComputeQbRating():0.0}" : "--";
        string intText = attempts > 0f ? $"{qb.Interceptions}" : "--";
        string sackText = attempts > 0f ? $"{qb.Sacks}" : "--";

        return $"STG {stageLabel} | QB {qbRatingText} | CMP {completionText} | INT {intText} | SK {sackText} | YPC {ypcText} | XPL {explosiveText}";
    }

    private float ComputeStageProgressionScore()
    {
        if (IsChampion)
        {
            return 100f;
        }

        if (_games.Any(game => game.Stage == SeasonStage.SuperBowl))
        {
            return 78f;
        }

        if (_games.Any(game => game.Stage == SeasonStage.Playoff))
        {
            return 55f;
        }

        return 20f;
    }

    private string GetStageProgressionLabel()
    {
        if (IsChampion)
        {
            return "CHAMP";
        }

        if (_games.Any(game => game.Stage == SeasonStage.SuperBowl))
        {
            return "SB";
        }

        if (_games.Any(game => game.Stage == SeasonStage.Playoff))
        {
            return "PLAYOFF";
        }

        return "REG";
    }

    /// <summary>Resets the summary for a new season.</summary>
    public void Reset()
    {
        _games.Clear();
        _cumulativeQb.Reset();
        _cumulativeRb.Reset();
        _cumulativeReceivers.Clear();
        _offensivePlays = 0;
        _successfulPlays = 0;
        _explosivePlays = 0;
        _runPlays = 0;
        _successfulRuns = 0;
        _explosiveRuns = 0;
    }

    private void ApplySeasonSnapshot(GameStatsSnapshot stats)
    {
        QbStatsSnapshot qbSnap = stats.Qb;
        _cumulativeQb.Completions = qbSnap.Completions;
        _cumulativeQb.Attempts = qbSnap.Attempts;
        _cumulativeQb.PassYards = qbSnap.PassYards;
        _cumulativeQb.PassTds = qbSnap.PassTds;
        _cumulativeQb.Interceptions = qbSnap.Interceptions;
        _cumulativeQb.Sacks = qbSnap.Sacks;
        _cumulativeQb.SackYardsLost = qbSnap.SackYardsLost;
        _cumulativeQb.RushAttempts = qbSnap.RushAttempts;
        _cumulativeQb.RushYards = qbSnap.RushYards;
        _cumulativeQb.RushTds = qbSnap.RushTds;

        _cumulativeRb.Attempts = stats.Rb.Attempts;
        _cumulativeRb.Yards = stats.Rb.Yards;
        _cumulativeRb.Tds = stats.Rb.Tds;

        _cumulativeReceivers.Clear();
        foreach (ReceiverStatsSnapshot receiver in stats.Receivers)
        {
            if (receiver.Targets <= 0 && receiver.Receptions <= 0 && receiver.Yards <= 0 && receiver.Tds <= 0)
            {
                continue;
            }

            _cumulativeReceivers[receiver.Label] = new SkillStatLine
            {
                Targets = receiver.Targets,
                Receptions = receiver.Receptions,
                Yards = receiver.Yards,
                Tds = receiver.Tds
            };
        }
    }

    private float ComputeRbDominanceScore()
    {
        if (_runPlays == 0 || _cumulativeRb.Attempts == 0)
        {
            return 0f;
        }

        float yardsPerCarry = _cumulativeRb.Yards / (float)_cumulativeRb.Attempts;
        float yardsPerCarryScore = Math.Clamp(((yardsPerCarry - 2.5f) / 3.5f) * 100f, 0f, 100f);
        float runSuccessRate = _successfulRuns / (float)_runPlays;
        float runSuccessScore = NormalizeRate(runSuccessRate, 0.60f);
        float runExplosiveRate = _explosiveRuns / (float)_runPlays;
        float runExplosiveScore = NormalizeRate(runExplosiveRate, 0.18f);

        return (yardsPerCarryScore * 0.50f)
            + (runSuccessScore * 0.35f)
            + (runExplosiveScore * 0.15f);
    }

    private float ComputeDistributionScore()
    {
        int totalTargets = _cumulativeReceivers.Values.Sum(receiver => receiver.Targets);
        if (totalTargets == 0)
        {
            return 0f;
        }

        var targetedReceivers = _cumulativeReceivers.Values
            .Where(receiver => receiver.Targets > 0)
            .ToArray();

        float activeScore = Math.Clamp((targetedReceivers.Length / 5f) * 100f, 0f, 100f);
        if (targetedReceivers.Length == 1)
        {
            return activeScore * 0.4f;
        }

        float entropy = 0f;
        foreach (SkillStatLine receiver in targetedReceivers)
        {
            float share = receiver.Targets / (float)totalTargets;
            entropy -= share * MathF.Log(share);
        }

        float maxEntropy = MathF.Log(targetedReceivers.Length);
        float balanceScore = maxEntropy > 0f ? (entropy / maxEntropy) * 100f : 0f;
        return (activeScore * 0.40f) + (balanceScore * 0.60f);
    }

    private static bool IsExplosivePlay(PlayRecord play)
    {
        if (play.Outcome == PlayOutcome.Touchdown)
        {
            return true;
        }

        float threshold = play.WasRun ? 12f : 16f;
        return play.Gain >= threshold;
    }

    private static bool IsSuccessfulPlay(PlayRecord play)
    {
        if (play.Outcome == PlayOutcome.Touchdown)
        {
            return true;
        }

        if (play.Outcome is PlayOutcome.Interception or PlayOutcome.Incomplete || play.IsSack)
        {
            return false;
        }

        float requiredGain = play.Down switch
        {
            1 => play.Distance * 0.45f,
            2 => play.Distance * 0.60f,
            _ => play.Distance
        };

        return play.Gain >= requiredGain;
    }

    private static float NormalizeRate(float actualRate, float eliteRate)
        => Math.Clamp((actualRate / eliteRate) * 100f, 0f, 100f);

    private string GetStageScoreText(SeasonStage stage)
    {
        GameResult? game = _games.FirstOrDefault(result => result.Stage == stage);
        if (!game.HasValue)
        {
            return string.Empty;
        }

        if (game.Value.PlayerScore == 0 && game.Value.AwayScore == 0)
        {
            return string.Empty;
        }

        return $"{game.Value.PlayerScore}-{game.Value.AwayScore}";
    }
}
