using System.Text.Json;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Stats;

namespace RetroQB.Tests;

public sealed class TimedSeasonTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RetroQB-Phase5-" + Guid.NewGuid());
    private string SavePath => Path.Combine(_directory, "records.json");
    public TimedSeasonTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);
    private TimedSeason NewSeason() => new(new ScriptedInput(), 101, TeamCatalog.Get("ballers"), "Same Player",
        new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed), 1);
    private static void Finish(TimedSeason season, bool won)
    {
        var s = season.Current;
        s.Match.User.Score = won ? 7 : 0; s.Match.Opponent.Score = won ? 0 : 7;
        s.Clock.StartPeriod(4); s.Timed.Continue();
        Assert.NotNull(s.Timed.Advance(0, "fixture.kneel")); s.Timed.Advance(2);
        s.Drive.ResolveSpecial(PlayEndReason.Kneel, 19);
        Assert.True(s.Timed.Finished);
    }

    [Fact]
    public void StageVictoryWaitsForAcceptanceAndPreviewDoesNotCommitResults()
    {
        var season = NewSeason(); season.Accept();
        Finish(season, true);
        Assert.True(season.StageVictory);
        var preview = season.PreviewResult();
        Assert.Single(preview.Games);
        Assert.Empty(season.Completed); Assert.Empty(season.Summary.Games);
        Assert.Equal(preview.ComputeDominanceScore(), season.PreviewResult().ComputeDominanceScore());
        season.Update(10, new());
        Assert.True(season.StageVictory); Assert.Equal(SeasonStage.RegularSeason, season.Stage);
        season.Accept();
        Assert.False(season.StageVictory); Assert.True(season.Pregame);
        Assert.Single(season.Completed);
        Assert.Equal(preview.ComputeDominanceScore(), season.Summary.ComputeDominanceScore());
        season.Accept(); Finish(season, true);
        Assert.True(season.StageVictory);
        Assert.Equal(2, season.PreviewResult().Games.Count);
        season.Current.Restart();
        Assert.False(season.StageVictory); Assert.Single(season.Completed);
    }

    [Fact]
    public void STATS01_CpuOffenseAndUserDefenseShareOneEventWithoutPollutingUserOffense()
    {
        var u = TeamCatalog.Get("ballers"); var o = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        var m = new MatchState(u, o, o.Id);
        var p = m.BeginPlay("pass");
        var e = new PlayEnded(p.Id, o.Id, PlayEndReason.Tackle, 26,
            new(PassAttempt: true, Completion: true, Target: ReceiverSlot.WR1), DefenderSlot.MLB, ControlledDefender: DefenderSlot.MLB);
        m.Resolve(e); m.Resolve(e);
        Assert.Equal(1, m.Opponent.Stats.Qb.Completions); Assert.Equal(6, m.Opponent.Stats.Qb.PassYards);
        Assert.Equal(0, m.User.Stats.Qb.Attempts); Assert.Equal(0, m.User.Stats.Rb.Attempts);
        Assert.Equal(1, m.User.DefenseStats.Tackles); Assert.Equal(1, m.User.DefenseStats.ControlledTackles);
        p = m.BeginPlay("run");
        m.Resolve(new(p.Id, o.Id, PlayEndReason.Tackle, 28, new(Rush: RushingRole.RunningBack), DefenderSlot.FS, ControlledDefender: DefenderSlot.MLB));
        Assert.Equal(2, m.User.DefenseStats.Tackles); Assert.Equal(1, m.User.DefenseStats.ControlledTackles);
        Assert.Equal(2, m.User.DefenseStats.Players.Count); Assert.Equal(2, m.Opponent.Stats.Rb.Yards);
        m.Reset(); Assert.Equal(0, m.User.DefenseStats.Tackles); Assert.Empty(m.User.DefenseStats.Players);
    }

    [Theory]
    [InlineData(PlayEndReason.Sack)] [InlineData(PlayEndReason.Interception)] [InlineData(PlayEndReason.PassDefended)]
    public void STATS01_DefensiveAttributionAndThirdDownStops(PlayEndReason reason)
    {
        var u = TeamCatalog.Get("ballers"); var o = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        var m = new MatchState(u, o, o.Id, start: new(20, 3, 10)); var p = m.BeginPlay("fixture");
        m.Resolve(new(p.Id, o.Id, reason, reason == PlayEndReason.Sack ? 15 : 25,
            reason == PlayEndReason.Sack ? null : new(PassAttempt: true), DefenderSlot.OLB1, ControlledDefender: DefenderSlot.OLB1));
        var stats = m.User.DefenseStats;
        Assert.Equal(1, stats.ThirdDownStops);
        Assert.Equal(reason == PlayEndReason.Sack ? 1 : 0, stats.Sacks);
        Assert.Equal(reason == PlayEndReason.Interception ? 1 : 0, stats.Interceptions);
        Assert.Equal(reason == PlayEndReason.PassDefended ? 1 : 0, stats.PassesDefended);
        Assert.Equal(stats.Sacks, stats.ControlledSacks); Assert.Equal(stats.Interceptions, stats.ControlledInterceptions);
        Assert.Equal(stats.PassesDefended, stats.ControlledPassesDefended);
    }

    [Fact]
    public void REPLAY01_ReplayAfterPossessionChangeKeepsRecordedTeamClockAndLinebacker()
    {
        var u = TeamCatalog.Get("ballers"); var o = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        var s = new FullMatchSession(new ScriptedInput(), 101, new(u, o, o.Id, start: new(99, 1, 1)));
        s.Drive.Snap(); var slot = s.Drive.Linebacker.Slot;
        s.Drive.Actors.Qb.Position = new(25, 111); s.Drive.Update(.01f);
        var clip = s.Drive.LastReplay!; Assert.NotNull(clip);
        s.Update(0, new(Ready: true)); Assert.False(s.HumanOnDefense);
        Assert.Equal(o.Id, clip.MatchContext!.OffenseId); Assert.Equal(u.Id, clip.MatchContext.DefenseId);
        Assert.Equal(slot, clip.MatchContext.ControlledDefender); Assert.True(clip.MatchContext.ControlledIndex >= 0);
        Assert.Equal(0, clip.MatchContext.OpponentScore); Assert.Equal(1, clip.MatchContext.Clock.Quarter);
        string before = JsonSerializer.Serialize(new { s.Match.User.Stats, Cpu = s.Match.Opponent.Stats, s.Match.History });
        var clock = s.Clock.Snapshot();
        for (int i = 0; i < 2; i++)
        {
            s.Update(0, new(Replay: true)); Assert.Same(clip, s.Replay.Clip);
            s.Update(.1f); s.Update(0, new(Ready: true));
        }
        Assert.Equal(clock, s.Clock.Snapshot());
        Assert.Equal(before, JsonSerializer.Serialize(new { s.Match.User.Stats, Cpu = s.Match.Opponent.Stats, s.Match.History }));
    }

    [Fact]
    public void SAVE01_LegacyAndTimedRecordsShareFileButNeverRankTogether()
    {
        File.WriteAllText(SavePath, """{"Records":[{"Name":"Same Player","TeamName":"Ballers","DominanceScore":90}]}""");
        var timed = new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed);
        Assert.False(timed.HasPlayer("Same Player"));
        Guid id = Guid.NewGuid();
        Assert.True(timed.TrySaveSeasonResult("Same Player", "Ballers", "REG 7-0", "test", 30, out _, id));
        Assert.True(timed.TrySaveSeasonResult("Same Player", "Ballers", "REG 7-0", "test", 30, out _, id));
        var legacy = new PlayerRecordStore(SavePath); var reloaded = new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed);
        Assert.Equal(90, Assert.Single(legacy.GetLeaderboard()).DominanceScore);
        Assert.Equal(30, Assert.Single(reloaded.GetLeaderboard()).DominanceScore);
        Assert.Equal(1, reloaded.BuildSummary("Same Player", 30).PlayerRank);
        using var json = JsonDocument.Parse(File.ReadAllText(SavePath)); Assert.Equal(2, json.RootElement.GetProperty("Version").GetInt32());
        Assert.Contains("Same Player", File.ReadAllText(SavePath + ".bak"));
    }

    [Fact]
    public void SAVE01_TimedBackupRecoveryRetainsBothRulesets()
    {
        var legacy = new PlayerRecordStore(SavePath); legacy.TrySaveSeasonResult("Legacy", "Ballers", "REG", "test", 20, out _);
        var timed = new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed); timed.TrySaveSeasonResult("Timed", "Ballers", "REG", "test", 30, out _);
        timed.TrySaveSeasonResult("Second", "Ballers", "REG", "test", 40, out _); // Backup now has both rulesets.
        File.WriteAllText(SavePath, "broken");
        var recovered = new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed);
        Assert.Contains("Recovered", recovered.StatusMessage); Assert.Single(recovered.GetLeaderboard());
        Assert.True(recovered.TrySaveSeasonResult("Retry", "Ballers", "REG", "test", 50, out _));
        Assert.Single(new PlayerRecordStore(SavePath).GetLeaderboard());
        Assert.Equal(2, new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed).GetLeaderboard().Count);
    }

    [Fact]
    public void SAVE01_FailedSaveRetryDoesNotDuplicateSeason()
    {
        Directory.CreateDirectory(SavePath); // A directory cannot be replaced with the save file.
        var timed = new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed); Guid id = Guid.NewGuid();
        Assert.False(timed.TrySaveSeasonResult("Retry", "Ballers", "REG", "test", 20, out _, id));
        Directory.Delete(SavePath);
        Assert.True(timed.TrySaveSeasonResult("Retry", "Ballers", "REG", "test", 20, out _, id));
        Assert.True(timed.TrySaveSeasonResult("Retry", "Ballers", "REG", "test", 20, out _, id));
        Assert.Single(new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed).GetLeaderboard());
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void SEASON01_ChampionOrEachStageLossSavesExactlyOneCorrectSeason(int lossStage)
    {
        var s = NewSeason();
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal((SeasonStage)i, s.Stage); Assert.True(s.Pregame); s.Accept();
            Finish(s, i != lossStage); s.Accept();
            if (s.Complete) break;
        }
        Assert.True(s.Complete); Assert.True(s.Saved); Assert.Equal(lossStage < 0, s.Summary.IsChampion);
        Assert.Equal(lossStage < 0 ? 3 : lossStage + 1, s.Completed.Count);
        var snapshot = s.Summary.BuildThreeStageScoreHistory(); s.Accept(); s.Accept();
        Assert.Equal(snapshot, s.Summary.BuildThreeStageScoreHistory());
        Assert.Single(new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed).GetLeaderboard());
        var expected = new SeasonSummary();
        var saved = new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed).GetLeaderboard().Single();
        Assert.Equal(JsonSerializer.Serialize(s.Completed), JsonSerializer.Serialize(saved.Games));
        foreach (var game in s.Completed)
        {
            expected.RecordPlay(new() { Down = 1, Distance = 10, Gain = -1, WasRun = true, Outcome = PlayOutcome.Tackle });
            expected.RecordGame(game.Result.Stage, game.Result.PlayerScore, game.Result.AwayScore,
                StatsAggregation.Sum(s.Completed.Take((int)game.Result.Stage + 1).Select(g => g.Offense)));
        }
        Assert.Equal(expected.ComputeDominanceScore(), s.Summary.ComputeDominanceScore());
    }

    [Fact]
    public void SEASON02_RestartTwiceLeavesAcceptedStageAndItsStatsIntact()
    {
        var s = NewSeason(); s.Accept(); Finish(s, true); s.Accept();
        Assert.Equal(SeasonStage.Playoff, s.Stage); string previous = JsonSerializer.Serialize(s.Completed);
        s.Accept(); Finish(s, false);
        s.Update(0, new(Restart: true)); s.Update(0, new(Restart: true));
        Assert.Equal(previous, JsonSerializer.Serialize(s.Completed)); Assert.Single(s.Summary.Games);
        Assert.False(s.Current.Timed.Finished); Assert.Empty(s.Current.Match.History);
        Assert.Equal(0, s.Current.Match.User.Stats.Qb.RushAttempts); Assert.Equal(0, s.Current.Match.User.DefenseStats.Tackles);
        Assert.False(File.Exists(SavePath));
        Finish(s, true); s.Accept(); Assert.Equal(2, s.Summary.Games.Count);
    }

    [Fact]
    public void StatisticsOverlayFreezesClockAndKeepsIndependentPause()
    {
        var s = NewSeason(); s.Accept(); s.Current.Drive.Snap();
        s.Update(0, new(Statistics: true, Pause: true)); var clock = s.Current.Clock.Snapshot();
        s.Update(10, new()); Assert.Equal(clock, s.Current.Clock.Snapshot());
        s.Update(0, new(Statistics: true)); Assert.Equal(ClockSuspension.Pause, s.Current.Clock.Suspension);
    }

    [Fact]
    public void SAVE01_StoresOpenedBeforeEitherSaveDoNotEraseOtherRuleset()
    {
        var legacy = new PlayerRecordStore(SavePath);
        var timed = new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed);
        legacy.TrySaveSeasonResult("Same", "Ballers", "REG", "test", 20, out _);
        timed.TrySaveSeasonResult("Same", "Ballers", "REG", "test", 30, out _);
        legacy.TrySaveSeasonResult("Later", "Ballers", "REG", "test", 40, out _);
        Assert.Equal(2, new PlayerRecordStore(SavePath).GetLeaderboard().Count);
        Assert.Single(new PlayerRecordStore(SavePath, MatchRuleset.TwoSidedTimed).GetLeaderboard());
    }
}
