using System.Text.Json;
using RetroQB.Development;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class DefenseRedesignMatrixTests
{
    [Fact]
    public void AllTwentyFixturesResolveRealCatalogCallsAndDeclareTheirMissingCapabilities()
    {
        var catalog = new PlayManager().Catalog;
        Assert.Equal(20, DefenseRedesignMatrix.Fixtures.Count);
        Assert.Equal(Enumerable.Range(1, 20).Select(i => $"DM-{i:D2}"), DefenseRedesignMatrix.Fixtures.Select(f => f.Id));
        foreach (var fixture in DefenseRedesignMatrix.Fixtures)
        {
            Assert.NotNull(catalog[fixture.Play]); Assert.Contains(DefensivePlaybook.All, d => d.Id == fixture.Defense);
            Assert.NotEmpty(fixture.Expected); Assert.NotEmpty(fixture.Geometry); Assert.NotEmpty(fixture.RequiredCapability);
        }
        Assert.NotNull(ScenarioLaunchOptions.Parse(["--scenario", "defense-redesign-matrix", "--headless"], true));
        Assert.Throws<ArgumentException>(() => ScenarioLaunchOptions.Parse(["--scenario", "defense-redesign-matrix", "--headless"], false));
        Assert.Equal(120, ScenarioLaunchOptions.Parse(["--scenario", "defense-redesign-matrix", "--headless", "--hz", "120"], true)!.Hz);
        Assert.Throws<ArgumentException>(() => ScenarioLaunchOptions.Parse(["--scenario", "defense-redesign-matrix", "--hz", "0"], true));
        Assert.Throws<ArgumentException>(() => ScenarioLaunchOptions.Parse(["--scenario", "defense-deep", "--hz", "30"], true));
        Assert.NotNull(ScenarioLaunchOptions.Parse(["--scenario", "defense-plan-matrix", "--headless"], true));
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void FixedInputReproducesIdenticalEventsAndTrajectories(int hz)
    {
        var fixture = DefenseRedesignMatrix.Fixtures[0] with { Duration = 2 };
        var a = DefenseRedesignMatrix.Exercise(fixture, 101, hz, DefenseInputMode.AssignmentFollowing, false, out var measurement);
        var b = DefenseRedesignMatrix.Exercise(fixture, 101, hz, DefenseInputMode.AssignmentFollowing, false, out _);
        Assert.Null(a.Failure); Assert.True(measurement.Frames > 0);
        Assert.Equal(JsonSerializer.Serialize(a, DefenseRedesignMatrix.Json), JsonSerializer.Serialize(b, DefenseRedesignMatrix.Json));
        Assert.All(a.Frames, frame => Assert.Equal(22, frame.Actors.Select(x => x.Id).Distinct().Count()));
        Assert.NotEmpty(a.Findings);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void InputModesAreExplicitAndKeepProductionSimulation(int inputMode)
    {
        var mode = (DefenseInputMode)inputMode;
        var sample = DefenseRedesignMatrix.Exercise(DefenseRedesignMatrix.Fixtures[0] with { Duration = 1.2f },
            101, 60, mode, false, out _);
        Assert.Null(sample.Failure); Assert.True(sample.Frames.Count > 1);
        Assert.Equal(mode, sample.InputMode);
        Assert.All(sample.Frames, f => Assert.Equal(mode == DefenseInputMode.FullAI, f.ControlledActor == null));
        if (mode is DefenseInputMode.Stationary or DefenseInputMode.FullAI)
            Assert.All(sample.Frames, f => Assert.Equal(System.Numerics.Vector2.Zero, f.Input));
        if (mode == DefenseInputMode.Abandonment && sample.Frames.Count > 61)
            Assert.Equal(-System.Numerics.Vector2.UnitY, sample.Frames[61].Input);
    }
}
