using System.Text.Json;

namespace RetroQB.Stats;

public sealed class PlayerRecordStore
{
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

    public LeaderboardSummary BuildSummary(string playerName, float seasonRating)
    {
        string normalizedName = NormalizeName(playerName);
        var sortedRecords = GetSortedRecords();
        int index = sortedRecords.FindIndex(record => NamesMatch(record.Name, normalizedName));
        float savedBest = index >= 0 ? sortedRecords[index].BestQbRating : seasonRating;
        int? rank = index >= 0 ? index + 1 : null;

        var entries = sortedRecords
            .Select((record, entryIndex) => new LeaderboardEntry(
                record.Name,
                record.BestQbRating,
                entryIndex + 1,
                NamesMatch(record.Name, normalizedName)))
            .ToArray();

        return new LeaderboardSummary(
            normalizedName,
            seasonRating,
            savedBest,
            false,
            rank,
            entries);
    }

    public LeaderboardSummary SaveSeasonResult(string playerName, float seasonRating)
    {
        string normalizedName = NormalizeName(playerName);
        int existingIndex = FindRecordIndex(normalizedName);
        bool isPersonalBest = true;
        float savedBest = seasonRating;

        if (existingIndex >= 0)
        {
            PlayerRecord existing = _records[existingIndex];
            savedBest = MathF.Max(existing.BestQbRating, seasonRating);
            isPersonalBest = seasonRating >= existing.BestQbRating;
            _records[existingIndex] = existing with
            {
                BestQbRating = savedBest,
                LastUpdatedUtc = DateTime.UtcNow
            };
        }
        else
        {
            _records.Add(new PlayerRecord(normalizedName, seasonRating, DateTime.UtcNow));
        }

        Save();

        var summary = BuildSummary(normalizedName, seasonRating);
        return new LeaderboardSummary(
            summary.PlayerName,
            summary.SeasonRating,
            savedBest,
            isPersonalBest,
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

                _records.Add(new PlayerRecord(normalizedName, record.BestQbRating, record.LastUpdatedUtc));
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
                    BestQbRating = record.BestQbRating,
                    LastUpdatedUtc = record.LastUpdatedUtc
                })
                .ToList()
        };

        string json = JsonSerializer.Serialize(model, JsonOptions);
        File.WriteAllText(_savePath, json);
    }

    private int FindRecordIndex(string normalizedName)
        => _records.FindIndex(record => NamesMatch(record.Name, normalizedName));

    private List<PlayerRecord> GetSortedRecords()
    {
        return _records
            .OrderByDescending(record => record.BestQbRating)
            .ThenBy(record => record.LastUpdatedUtc)
            .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        public float BestQbRating { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}