using System.Numerics;
using System.Text.Json;
using RetroQB.Core;
using RetroQB.Development;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Stats;

namespace RetroQB.Tests;

public sealed class DevelopmentScenarioTests
{
    [Fact]
    public void FixtureAcceptsExplicitTeamsStageAndStartingSeries()
    {
        var definition = ScenarioDefinition.Get("offense-pass") with
        {
            Start = new DriveStart(45, 3, 7),
            Offense = OffensiveTeamPresets.GetMenuTeams(false)[1],
            Defense = DefensiveTeamPresets.CrimsonRush,
            Stage = SeasonStage.Playoff
        };
        using var run = new ScenarioRun(definition, 101);
        Assert.Equal(definition.Offense.Name, run.Current.Offense);
        Assert.Equal(definition.Defense.Name, run.Current.Defense);
        Assert.Equal(SeasonStage.Playoff, run.Session.CurrentStage);
        Assert.Equal(55, run.Current.LineOfScrimmage);
        Assert.Equal(3, run.Current.Down);
        Assert.Equal(7, run.Current.Distance);
        Assert.Equal(62, run.Current.FirstDownLine);
        run.Step(new InputFrame());
        Assert.Equal(GameState.PreSnap, run.Current.State);
        run.Step(new InputFrame(Space: true));
        Assert.Equal(GameState.PlayActive, run.Current.State);
    }

    [Theory]
    [InlineData("offense-pass")]
    [InlineData("offense-run")]
    [InlineData("offense-play-action")]
    [InlineData("offense-field-goal")]
    [InlineData("offense-replay")]
    public void SameSeedAndInputsReproduceTraceAndTerminalRulesState(string name)
    {
        using var first = Run(name, 101);
        using var second = Run(name, 101);
        Assert.Equal(JsonSerializer.Serialize(first.Trace, ScenarioRun.JsonOptions),
            JsonSerializer.Serialize(second.Trace, ScenarioRun.JsonOptions));
        Assert.NotNull(first.Current.Outcome);
        Assert.Equal(first.Tick, second.Tick);
        Assert.True(first.Current.Actors.All(a => float.IsFinite(a.Position.X) && float.IsFinite(a.Position.Y)));
    }

    [Fact]
    public void SeedChangesDefensiveSetupRatherThanOnlyReportMetadata()
    {
        using var first = new ScenarioRun(ScenarioDefinition.Get("offense-pass"), 101);
        using var second = new ScenarioRun(ScenarioDefinition.Get("offense-pass"), 102);
        Assert.NotEqual(JsonSerializer.Serialize(first.Current, ScenarioRun.JsonOptions),
            JsonSerializer.Serialize(second.Current, ScenarioRun.JsonOptions));
    }

    [Fact]
    public void BaselinePassRunAndPlayActionExerciseActualBallAndExchangeStates()
    {
        using var pass = Run("offense-pass", 101);
        Assert.Contains(pass.Trace, t => t.State.BallState == BallState.InAir);
        Assert.Contains(pass.Trace, t => t.State.BallState == BallState.HeldByReceiver);
        Assert.Equal(1, pass.Current.Stats.Qb.Attempts);
        Assert.Equal(1, pass.Current.Stats.Qb.Completions);
        Assert.Equal(PlayOutcome.Touchdown, pass.Current.Outcome);
        using var run = Run("offense-run", 101);
        Assert.Contains(run.Trace, t => t.State.BallState == BallState.HeldByReceiver);
        Assert.Equal(0, run.Current.Stats.Qb.Attempts);
        Assert.Equal(4, run.Current.Gain);
        using var fake = Run("offense-play-action", 101);
        Assert.Contains(fake.Trace, t => t.State.Exchange == "FAKE");
        Assert.Contains(fake.Trace, t => t.State.BallState == BallState.InAir);
        Assert.Equal(1, fake.Current.Stats.Qb.Attempts);
    }

    [Fact]
    public void BaselineKickRecordsCurrentScoringWithoutChangingRules()
    {
        using var kick = Run("offense-field-goal", 101);
        Assert.Equal(4, kick.Trace[0].State.Down);
        Assert.Equal(80, kick.Trace[0].State.LineOfScrimmage);
        Assert.Equal(5, kick.Trace[0].State.Distance);
        Assert.Equal(PlayOutcome.FieldGoalGood, kick.Current.Outcome);
        Assert.Equal(3, kick.Current.Score);
        Assert.Equal(3, kick.Current.AwayScore); // Intentionally replaced when M1/M8 activates real possession rules.
        Assert.Contains(kick.Trace, t => t.State.KickPhase == KickPhase.Power);
        Assert.Contains(kick.Trace, t => t.State.KickPhase == KickPhase.Accuracy);
    }

    [Fact]
    public void ReplayEntersProductionPlaybackAndDoesNotScoreOrRecordTwice()
    {
        using var run = Run("offense-replay", 101);
        Assert.Contains(run.Trace, t => t.State.State == GameState.Replay);
        var terminal = run.Trace.First(t => t.State.Outcome.HasValue).State;
        Assert.True(run.Current.ReplayFrameCount > 0);
        Assert.NotEqual(GameState.Replay, run.Current.State);
        Assert.Equal(terminal.Score, run.Current.Score);
        Assert.Equal(terminal.AwayScore, run.Current.AwayScore);
        Assert.Equal(JsonSerializer.Serialize(terminal.Stats), JsonSerializer.Serialize(run.Current.Stats));
        int ticks = run.Tick;
        run.Step();
        Assert.Equal(ticks, run.Tick);
    }

    [Fact]
    public void ReportsAndScenarioSavesCannotOverwriteAnExistingLeaderboard()
    {
        string output = Path.Combine(Path.GetTempPath(), "RetroQB-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        string sentinel = Path.Combine(output, "player-records.json");
        File.WriteAllText(sentinel, "existing leaderboard sentinel");
        using var run = new ScenarioRun(ScenarioDefinition.Get("offense-run"), 101, output);
        Assert.NotEqual(sentinel, run.RecordPath);
        string normalSave = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RetroQB", "player-records.json");
        Assert.NotEqual(Path.GetFullPath(normalSave), run.RecordPath);
        Assert.Empty(run.Records.GetLeaderboard());
        Assert.True(run.Records.TrySaveSeasonResult("Scenario", "Ballers", "REG 21-0", "baseline", 42, out _));
        Assert.True(File.Exists(run.RecordPath));
        while (!run.Complete) run.Step();
        string reportPath = run.WriteReport();
        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal(run.RecordPath, report.RootElement.GetProperty("RecordPath").GetString());
        Assert.Equal("existing leaderboard sentinel", File.ReadAllText(sentinel));
        Assert.True(report.RootElement.GetProperty("Complete").GetBoolean());
    }

    [Fact]
    public void NormalStartupStillUsesMenuPregameAndHumanOffensiveInput()
    {
        var input = new ScriptedInput();
        string path = Path.Combine(Path.GetTempPath(), "RetroQB-tests", Guid.NewGuid().ToString("N"), "records.json");
        using var session = new GameSession(input, new Random(101), new PlayerRecordStore(path));
        Assert.Equal(GameState.MainMenu, session.CaptureSnapshot().State);
        input.Frame = new(Enter: true);
        session.Update(1f / 60);
        Assert.Equal(GameState.Pregame, session.CaptureSnapshot().State);
        session.Update(1f / 60);
        Assert.Equal(GameState.PreSnap, session.CaptureSnapshot().State);
        input.Frame = new(PassSelection: 1, Space: true);
        session.Update(1f / 60);
        Assert.Equal(GameState.PlayActive, session.CaptureSnapshot().State);
        Vector2 before = session.CaptureSnapshot().Actors[0].Position;
        input.Frame = new(Movement: Vector2.UnitX);
        for (int i = 0; i < 5; i++) session.Update(1f / 60);
        Assert.True(session.CaptureSnapshot().Actors[0].Position.X > before.X);
        input.Frame = new(Escape: true);
        session.Update(1f / 60);
        var paused = session.CaptureSnapshot();
        input.Frame = new(Movement: Vector2.UnitY);
        session.Update(1f / 60);
        Assert.True(paused.Paused);
        Assert.Equal(paused.Actors[0].Position, session.CaptureSnapshot().Actors[0].Position);
    }

    [Theory]
    [InlineData(0, 1, 10)]
    [InlineData(100, 1, 10)]
    [InlineData(20, 0, 10)]
    [InlineData(20, 5, 10)]
    [InlineData(20, 1, 0)]
    [InlineData(95, 1, 10)]
    public void InvalidStartingSeriesIsRejectedBeforeMutatingDrive(float yard, int down, float distance)
    {
        var drive = new DriveState();
        Assert.Throws<ArgumentOutOfRangeException>(() => drive.Reset(new(yard, down, distance)));
        Assert.Equal(30, drive.LineOfScrimmage);
        Assert.Equal(1, drive.Down);
    }

    [Fact]
    public void LaunchParserPreservesNormalStartupAndRejectsReleaseScenarios()
    {
        Assert.Null(ScenarioLaunchOptions.Parse([], true));
        Assert.Null(ScenarioLaunchOptions.Parse([], false));
        Assert.Throws<ArgumentException>(() => ScenarioLaunchOptions.Parse(["--scenario", "offense-pass"], false));
        var parsed = ScenarioLaunchOptions.Parse(["--scenario", "offense-pass", "--seed", "101", "--headless"], true)!;
        Assert.Equal(101, parsed.Seed);
        Assert.True(parsed.Headless);
        Assert.Equal("offense-pass", parsed.Definition.Name);
    }

    [Theory]
    [InlineData("--scenario", "unknown-drive")]
    [InlineData("--scenario")]
    [InlineData("--seed", "abc")]
    [InlineData("--unexpected")]
    [InlineData("--headless")]
    public void UnknownOrIncompleteDevelopmentRequestsAreRejected(params string[] args)
        => Assert.Throws<ArgumentException>(() => ScenarioLaunchOptions.Parse(args, true));

    [Fact]
    public void ConflictingAndDuplicateFlagsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => ScenarioLaunchOptions.Parse(
            ["--scenario", "offense-pass", "--capture", "--headless"], true));
        Assert.Throws<ArgumentException>(() => ScenarioLaunchOptions.Parse(
            ["--scenario", "offense-pass", "--seed", "1", "--seed", "2"], true));
    }

    private static ScenarioRun Run(string name, int seed)
    {
        var run = new ScenarioRun(ScenarioDefinition.Get(name), seed);
        while (!run.Complete) run.Step();
        Assert.Null(run.Failure);
        return run;
    }
}
