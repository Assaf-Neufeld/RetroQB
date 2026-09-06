using RetroQB.Stats;

namespace RetroQB.Tests;

public sealed class PlayerRecordStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RetroQB.Tests", Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(_directory, "player-records.json");

    public PlayerRecordStoreTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void SaveRoundTripsAndBacksUpPreviousRecords()
    {
        var store = new PlayerRecordStore(SavePath);
        Assert.Empty(store.StatusMessage);
        Save(store, "Alice", 20);
        string firstSave = File.ReadAllText(SavePath);
        Save(store, "Bob", 30);
        Assert.Equal(firstSave, File.ReadAllText(SavePath + ".bak"));
        Assert.Equal(new[] { "Bob", "Alice" }, new PlayerRecordStore(SavePath).GetLeaderboard().Select(r => r.Name));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void CorruptPrimaryRecoversBackupAndPreservesItDuringNextSave()
    {
        var store = new PlayerRecordStore(SavePath);
        Save(store, "Alice", 20);
        Save(store, "Bob", 30);
        string backup = File.ReadAllText(SavePath + ".bak");
        File.WriteAllText(SavePath, "broken json");

        var recovered = new PlayerRecordStore(SavePath);
        Assert.True(recovered.HasPlayer("Alice"));
        Assert.Contains("backup", recovered.BuildSummary("", 0).StorageMessage);
        Save(recovered, "Carol", 40);
        Assert.Equal(backup, File.ReadAllText(SavePath + ".bak"));
        Assert.Equal("broken json", File.ReadAllText(Assert.Single(Directory.GetFiles(_directory, "*.corrupt-*"))));
        Assert.Equal(2, new PlayerRecordStore(SavePath).GetLeaderboard().Count);
    }

    [Fact]
    public void MissingPrimaryRecoversExistingBackup()
    {
        var store = new PlayerRecordStore(SavePath);
        Save(store, "Alice", 20);
        File.Move(SavePath, SavePath + ".bak");
        var recovered = new PlayerRecordStore(SavePath);
        Assert.True(recovered.HasPlayer("Alice"));
        Save(recovered, "Bob", 30);
        Assert.Equal(2, new PlayerRecordStore(SavePath).GetLeaderboard().Count);
    }

    [Theory]
    [InlineData("broken json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"Records\":null}")]
    [InlineData("{\"Records\":[null]}")]
    public void InvalidSaveWithoutBackupIsNeverOverwritten(string invalidJson)
    {
        File.WriteAllText(SavePath, invalidJson);
        var store = new PlayerRecordStore(SavePath);
        Assert.False(TrySave(store, "Alice", 20, out var summary));
        Assert.NotEmpty(summary.StorageMessage);
        Assert.False(summary.IsLatestSeason);
        Assert.Equal(invalidJson, File.ReadAllText(SavePath));
        Assert.False(store.HasPlayer("Alice"));
    }

    [Fact]
    public void BothUnreadableFilesArePreserved()
    {
        File.WriteAllText(SavePath, "bad primary");
        File.WriteAllText(SavePath + ".bak", "bad backup");
        var store = new PlayerRecordStore(SavePath);
        Assert.False(TrySave(store, "Alice", 20, out _));
        Assert.Equal("bad primary", File.ReadAllText(SavePath));
        Assert.Equal("bad backup", File.ReadAllText(SavePath + ".bak"));
    }

    [Fact]
    public void FailedWriteKeepsOldSaveAndRetryDoesNotDuplicateScore()
    {
        var store = new PlayerRecordStore(SavePath);
        Save(store, "Alice", 20);
        string original = File.ReadAllText(SavePath);
        using (var locked = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(TrySave(store, "Bob", 30, out var summary));
            Assert.False(summary.IsLatestSeason);
            Assert.Equal(30, summary.SeasonScore);
            Assert.Contains("Could not save", summary.StorageMessage);
            Assert.False(store.HasPlayer("Bob"));
        }
        Assert.Equal(original, File.ReadAllText(SavePath));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Save(store, "Bob", 30);
        Assert.Equal(2, new PlayerRecordStore(SavePath).GetLeaderboard().Count);
    }

    [Fact]
    public void RetryReloadsRepairedFileBeforeAddingPendingResult()
    {
        var store = new PlayerRecordStore(SavePath);
        Save(store, "Alice", 20);
        string original = File.ReadAllText(SavePath);
        File.WriteAllText(SavePath, "broken");
        var failed = new PlayerRecordStore(SavePath);
        Assert.False(TrySave(failed, "Bob", 30, out _));
        File.WriteAllText(SavePath, original);
        Save(failed, "Bob", 30);
        Assert.Equal(2, new PlayerRecordStore(SavePath).GetLeaderboard().Count);
    }

    [Theory]
    [InlineData("QbRating")]
    [InlineData("BestQbRating")]
    public void LegacyScoreFilesRemainReadable(string scoreProperty)
    {
        File.WriteAllText(SavePath, $$"""{"Records":[{"Name":"Alice","{{scoreProperty}}":42}]}""");
        var store = new PlayerRecordStore(SavePath);
        Assert.Equal(42, Assert.Single(store.GetLeaderboard()).DominanceScore);
        Save(store, "Bob", 30);
        Assert.Equal(2, new PlayerRecordStore(SavePath).GetLeaderboard().Count);
    }

    private static bool TrySave(PlayerRecordStore store, string name, float score, out LeaderboardSummary summary) =>
        store.TrySaveSeasonResult(name, "Test Team", "REG 21-7", "QB 100", score, out summary);

    private static void Save(PlayerRecordStore store, string name, float score)
    {
        Assert.True(TrySave(store, name, score, out var summary), store.StatusMessage);
        Assert.True(summary.IsLatestSeason);
        Assert.Empty(summary.StorageMessage);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
