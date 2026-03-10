namespace RetroQB.Stats;

public sealed record PlayerRecord(
    string Name,
    float BestQbRating,
    DateTime LastUpdatedUtc);

public readonly record struct LeaderboardEntry(
    string Name,
    float Rating,
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
        float seasonRating,
        float savedBestRating,
        bool isPersonalBest,
        int? playerRank,
        IReadOnlyList<LeaderboardEntry> entries)
    {
        PlayerName = playerName;
        SeasonRating = seasonRating;
        SavedBestRating = savedBestRating;
        IsPersonalBest = isPersonalBest;
        PlayerRank = playerRank;
        Entries = entries;
    }

    public string PlayerName { get; }
    public float SeasonRating { get; }
    public float SavedBestRating { get; }
    public bool IsPersonalBest { get; }
    public int? PlayerRank { get; }
    public IReadOnlyList<LeaderboardEntry> Entries { get; }

    public bool HasPlayerRank => PlayerRank.HasValue;
    public bool IsOnPodium => PlayerRank is >= 1 and <= 3;
    public bool IsFirstPlace => PlayerRank == 1;
}