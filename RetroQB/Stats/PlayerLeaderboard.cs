namespace RetroQB.Stats;

public sealed record PlayerRecord(
    string Name,
    string TeamName,
    string ScoreHistory,
    string ScoreDetails,
    float DominanceScore,
    DateTime LastUpdatedUtc);

public readonly record struct LeaderboardEntry(
    string Name,
    string TeamName,
    string ScoreHistory,
    string ScoreDetails,
    float Score,
    int Rank,
    bool IsCurrentPlayer)
{
    public bool HasTrophy => Rank == 1;
    public bool IsOnPodium => Rank <= 3;
}

public sealed class LeaderboardSummary
{
    public static LeaderboardSummary Empty { get; } = new(
        string.Empty,
        0f,
        0f,
        false,
        null,
        Array.Empty<LeaderboardEntry>());

    public LeaderboardSummary(
        string playerName,
        float seasonScore,
        float savedScore,
        bool isLatestSeason,
        int? playerRank,
        IReadOnlyList<LeaderboardEntry> entries)
    {
        PlayerName = playerName;
        SeasonScore = seasonScore;
        SavedScore = savedScore;
        IsLatestSeason = isLatestSeason;
        PlayerRank = playerRank;
        Entries = entries;
    }

    public string PlayerName { get; }
    public float SeasonScore { get; }
    public float SavedScore { get; }
    public bool IsLatestSeason { get; }
    public int? PlayerRank { get; }
    public IReadOnlyList<LeaderboardEntry> Entries { get; }

    public LeaderboardEntry CurrentPlayerEntry => Entries.FirstOrDefault(entry => entry.IsCurrentPlayer);
    public int? CurrentPlayerRank => CurrentPlayerEntry.Rank > 0 ? CurrentPlayerEntry.Rank : PlayerRank;

    public bool HasPlayerRank => CurrentPlayerRank.HasValue;
    public bool IsOnPodium => CurrentPlayerRank is >= 1 and <= 3;
    public bool IsFirstPlace => CurrentPlayerRank == 1;
}