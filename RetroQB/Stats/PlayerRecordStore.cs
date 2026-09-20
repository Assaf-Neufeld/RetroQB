using System.Text.Json;
using RetroQB.Gameplay;

namespace RetroQB.Stats;

public sealed class PlayerRecordStore
{
    private const int MaxLeaderboardEntries = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _savePath;
    public MatchRuleset Ruleset { get; }
    private readonly List<PlayerRecord> _records = new();
    private bool _loadFailed;
    private bool _recoveredFromBackup;

    public string StatusMessage { get; private set; } = string.Empty;

    public PlayerRecordStore() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RetroQB", "player-records.json"))
    {
    }

    public PlayerRecordStore(MatchRuleset ruleset) : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroQB", "player-records.json"), ruleset) { }

    public PlayerRecordStore(string savePath, MatchRuleset ruleset = MatchRuleset.LegacyOffenseOnly)
    {
        if (!Enum.IsDefined(ruleset)) throw new ArgumentOutOfRangeException(nameof(ruleset));
        Ruleset = ruleset;
        _savePath = Path.GetFullPath(savePath);
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
                GetDisplayTeamName(record),
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
            entries) { StorageMessage = StatusMessage };
    }

    public bool TrySaveSeasonResult(string playerName, string teamName, string scoreHistory, string scoreDetails, float seasonScore, out LeaderboardSummary summary, Guid? seasonId = null,
        IReadOnlyList<CompletedTimedGame>? games = null)
    {
        // Refresh both rulesets before writing; another session may have saved since this store was opened.
        Load();
        if (_loadFailed)
        {
            summary = BuildSummary(playerName, seasonScore);
            return false;
        }

        string normalizedName = NormalizeName(playerName);
        if (seasonId.HasValue && _records.Any(r => r.Ruleset == Ruleset && r.SeasonId == seasonId))
        {
            summary = BuildSummary(normalizedName, seasonScore, true);
            return true;
        }
        string normalizedTeamName = NormalizeStoredTeamName(teamName);
        string normalizedScoreHistory = NormalizeScoreHistory(scoreHistory);
        string normalizedScoreDetails = NormalizeScoreDetails(scoreDetails);
        DateTime savedAtUtc = DateTime.UtcNow;

        _records.Add(new PlayerRecord(normalizedName, normalizedTeamName, normalizedScoreHistory, normalizedScoreDetails, seasonScore, savedAtUtc, Ruleset, seasonId,
            games == null ? null : Array.AsReadOnly(games.ToArray())));

        try
        {
            Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A failed attempt must not appear saved or duplicate the score on retry.
            _records.RemoveAt(_records.Count - 1);
            StatusMessage = "Could not save. Check disk access, then press ENTER.";
            summary = BuildSummary(normalizedName, seasonScore);
            return false;
        }

        StatusMessage = string.Empty;
        summary = BuildSummary(normalizedName, seasonScore, isLatestSeason: true);
        return true;
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
        _loadFailed = false;
        _recoveredFromBackup = false;
        StatusMessage = string.Empty;

        if (TryReadRecords(_savePath, out var records, out bool primaryMissing))
        {
            _records.AddRange(records);
            return;
        }

        if (TryReadRecords(_savePath + ".bak", out records, out bool backupMissing))
        {
            _records.AddRange(records);
            _recoveredFromBackup = true;
            StatusMessage = "Recovered leaderboard backup; latest scores may be missing.";
            return;
        }

        // Only two missing files mean a new leaderboard. Never overwrite unreadable data.
        _loadFailed = !primaryMissing || !backupMissing;
        if (_loadFailed)
        {
            StatusMessage = "Cannot load leaderboard. Restore save files, then retry.";
        }
    }

    private static bool TryReadRecords(string path, out List<PlayerRecord> records, out bool missing)
    {
        records = new();
        missing = false;
        try
        {
            string json = File.ReadAllText(path);
            StorageModel? model = JsonSerializer.Deserialize<StorageModel>(json, JsonOptions);
            if (model?.Records is null || model.Version is < 1 or > 2)
            {
                return false;
            }

            foreach (StorageRecord record in model.Records)
            {
                if (record is null)
                {
                    return false;
                }
                if (!Enum.IsDefined(record.Ruleset)) return false;

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
                string teamName = NormalizeStoredTeamName(record.TeamName);
                string scoreHistory = NormalizeScoreHistory(record.ScoreHistory);
                string scoreDetails = NormalizeScoreDetails(record.ScoreDetails);
                records.Add(new PlayerRecord(normalizedName, teamName, scoreHistory, scoreDetails, score, record.LastUpdatedUtc, record.Ruleset, record.SeasonId, record.Games));
            }
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            missing = true;
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
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
            Version = 2,
            Records = _records
                .Select(record => new StorageRecord
                {
                    Name = record.Name,
                    TeamName = record.TeamName,
                    ScoreHistory = record.ScoreHistory,
                    ScoreDetails = record.ScoreDetails,
                    DominanceScore = record.DominanceScore,
                    LastUpdatedUtc = record.LastUpdatedUtc,
                    Ruleset = record.Ruleset, SeasonId = record.SeasonId, Games = record.Games
                })
                .ToList()
        };

        // Write and flush in the same directory before atomically replacing the save.
        string temporaryPath = _savePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, model, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_savePath))
            {
                // Keep the good backup when the primary was corrupt or unreadable.
                string backupPath = _recoveredFromBackup
                    ? _savePath + ".corrupt-" + Guid.NewGuid().ToString("N")
                    : _savePath + ".bak";
                File.Replace(temporaryPath, _savePath, backupPath);
            }
            else
            {
                File.Move(temporaryPath, _savePath);
            }

            _recoveredFromBackup = false;
        }
        finally
        {
            // Cleanup must not turn a committed save into a reported failure.
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private int FindRecordIndex(string normalizedName)
        => _records.FindIndex(record => record.Ruleset == Ruleset && NamesMatch(record.Name, normalizedName));

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
            .Where(record => record.Ruleset == Ruleset && NamesMatch(record.Name, normalizedName))
            .OrderByDescending(record => record.LastUpdatedUtc)
            .FirstOrDefault();
    }

    private List<PlayerRecord> GetSortedRecords()
    {
        return _records
            .Where(record => record.Ruleset == Ruleset)
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

    private static string NormalizeStoredTeamName(string rawTeamName)
    {
        if (string.IsNullOrWhiteSpace(rawTeamName))
        {
            return "Unknown";
        }

        return rawTeamName.Trim();
    }

    private static string GetDisplayTeamName(PlayerRecord record)
    {
        string normalizedTeamName = NormalizeStoredTeamName(record.TeamName);
        return IsSecretTeamName(normalizedTeamName)
            ? GetStablePublicTeamName(record)
            : normalizedTeamName;
    }

    private static bool IsSecretTeamName(string teamName)
    {
        return string.Equals(teamName, OffensiveTeamPresets.GoldenLegion.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(teamName, "Passing Team", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetStablePublicTeamName(PlayerRecord record)
    {
        IReadOnlyList<OffensiveTeamAttributes> publicTeams = OffensiveTeamPresets.All;
        if (publicTeams.Count == 0)
        {
            return "Unknown";
        }

        string seed = string.Concat(
            NormalizeName(record.Name), "|",
            record.ScoreHistory.Trim(), "|",
            record.ScoreDetails.Trim(), "|",
            record.DominanceScore.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), "|",
            record.LastUpdatedUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));

        int teamIndex = (int)(ComputeStableHash(seed) % (uint)publicTeams.Count);
        return publicTeams[teamIndex].Name;
    }

    private static uint ComputeStableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        uint hash = offsetBasis;
        foreach (char ch in value)
        {
            hash ^= ch;
            hash *= prime;
        }

        return hash;
    }

    private static bool NamesMatch(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed class StorageModel
    {
        public int Version { get; set; } = 1;
        public List<StorageRecord>? Records { get; set; }
    }

    private sealed class StorageRecord
    {
        public MatchRuleset Ruleset { get; set; } = MatchRuleset.LegacyOffenseOnly;
        public Guid? SeasonId { get; set; }
        public IReadOnlyList<CompletedTimedGame>? Games { get; set; }
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
