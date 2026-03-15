using System.Text.Json;

namespace RetroQB.Stats;

public sealed class PlayerRecordStore
{
    private const int MaxLeaderboardEntries = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _savePath;
    private readonly List<PlayerRecord> _records = new();

    public PlayerRecordStore()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RetroQB");
        _savePath = Path.Combine(root, "player-records.json");
        Load();
    }

    public bool HasPlayer(string playerName)
        => FindRecordIndex(NormalizeName(playerName)) >= 0;

    public LeaderboardSummary BuildSummary(string playerName, float seasonScore, bool isLatestSeason = false)
    {
        string normalizedName = NormalizeName(playerName);
        var sortedRecords = GetSortedRecords();
        PlayerRecord? latestPlayerRecord = GetLatestRecordForPlayer(normalizedName);
        int? rank = latestPlayerRecord is null ? null : FindRecordRank(sortedRecords, latestPlayerRecord);
        float savedScore = latestPlayerRecord?.DominanceScore ?? seasonScore;
        int? currentPlayerRank = rank;

        var entries = sortedRecords
            .Select((record, entryIndex) => new LeaderboardEntry(
                record.Name,
                record.TeamName,
                record.ScoreHistory,
                record.ScoreDetails,
                record.DominanceScore,
                entryIndex + 1,
                currentPlayerRank.HasValue && entryIndex + 1 == currentPlayerRank.Value))
            .ToArray();

        return new LeaderboardSummary(
            normalizedName,
            seasonScore,
            savedScore,
            isLatestSeason,
            rank,
            entries);
    }

    public LeaderboardSummary SaveSeasonResult(string playerName, string teamName, string scoreHistory, string scoreDetails, float seasonScore)
    {
        string normalizedName = NormalizeName(playerName);
        string normalizedTeamName = string.IsNullOrWhiteSpace(teamName) ? "Unknown" : teamName.Trim();
        string normalizedScoreHistory = NormalizeScoreHistory(scoreHistory);
        string normalizedScoreDetails = NormalizeScoreDetails(scoreDetails);
        DateTime savedAtUtc = DateTime.UtcNow;

        _records.Add(new PlayerRecord(normalizedName, normalizedTeamName, normalizedScoreHistory, normalizedScoreDetails, seasonScore, savedAtUtc));

        Save();

        var summary = BuildSummary(normalizedName, seasonScore, isLatestSeason: true);
        return new LeaderboardSummary(
            summary.PlayerName,
            summary.SeasonScore,
            seasonScore,
            true,
            summary.PlayerRank,
            summary.Entries);
    }

    public IReadOnlyList<PlayerRecord> GetLeaderboard()
        => GetSortedRecords();

    public static string NormalizeName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        string[] parts = rawName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', parts);
    }

    private void Load()
    {
        _records.Clear();

        if (!File.Exists(_savePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(_savePath);
            StorageModel? model = JsonSerializer.Deserialize<StorageModel>(json, JsonOptions);
            if (model?.Records is null)
            {
                return;
            }

            foreach (StorageRecord record in model.Records)
            {
                string normalizedName = NormalizeName(record.Name);
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    continue;
                }

                float score = record.DominanceScore != 0f
                    ? record.DominanceScore
                    : record.QbRating != 0f
                        ? record.QbRating
                        : record.BestQbRating;
                string teamName = string.IsNullOrWhiteSpace(record.TeamName) ? "Unknown" : record.TeamName.Trim();
                string scoreHistory = NormalizeScoreHistory(record.ScoreHistory);
                string scoreDetails = NormalizeScoreDetails(record.ScoreDetails);
                _records.Add(new PlayerRecord(normalizedName, teamName, scoreHistory, scoreDetails, score, record.LastUpdatedUtc));
            }
        }
        catch
        {
            _records.Clear();
        }
    }

    private void Save()
    {
        string? directory = Path.GetDirectoryName(_savePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var model = new StorageModel
        {
            Records = _records
                .Select(record => new StorageRecord
                {
                    Name = record.Name,
                    TeamName = record.TeamName,
                    ScoreHistory = record.ScoreHistory,
                    ScoreDetails = record.ScoreDetails,
                    DominanceScore = record.DominanceScore,
                    LastUpdatedUtc = record.LastUpdatedUtc
                })
                .ToList()
        };

        string json = JsonSerializer.Serialize(model, JsonOptions);
        File.WriteAllText(_savePath, json);
    }

    private int FindRecordIndex(string normalizedName)
        => _records.FindIndex(record => NamesMatch(record.Name, normalizedName));

    private static int? FindRecordRank(IReadOnlyList<PlayerRecord> sortedRecords, PlayerRecord target)
    {
        int index = -1;
        for (int i = 0; i < sortedRecords.Count; i++)
        {
            if (sortedRecords[i] == target)
            {
                index = i;
                break;
            }
        }

        return index >= 0 && index < sortedRecords.Count ? index + 1 : null;
    }

    private PlayerRecord? GetLatestRecordForPlayer(string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        return _records
            .Where(record => NamesMatch(record.Name, normalizedName))
            .OrderByDescending(record => record.LastUpdatedUtc)
            .FirstOrDefault();
    }

    private List<PlayerRecord> GetSortedRecords()
    {
        return _records
            .OrderByDescending(record => record.DominanceScore)
            .ThenBy(record => record.LastUpdatedUtc)
            .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxLeaderboardEntries)
            .ToList();
    }

    private static string NormalizeScoreHistory(string rawHistory)
    {
        if (string.IsNullOrWhiteSpace(rawHistory))
        {
            return "REG -- | PLAYOFF -- | SB --";
        }

        return rawHistory.Trim();
    }

    private static string NormalizeScoreDetails(string rawDetails)
    {
        if (string.IsNullOrWhiteSpace(rawDetails))
        {
            return "STG REG | QB -- | CMP -- | INT -- | SK -- | YPC -- | XPL --";
        }

        return rawDetails.Trim();
    }

    private static bool NamesMatch(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed class StorageModel
    {
        public List<StorageRecord> Records { get; set; } = new();
    }

    private sealed class StorageRecord
    {
        public string Name { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string ScoreHistory { get; set; } = string.Empty;
        public string ScoreDetails { get; set; } = string.Empty;
        public float DominanceScore { get; set; }
        public float QbRating { get; set; }
        public float BestQbRating { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}