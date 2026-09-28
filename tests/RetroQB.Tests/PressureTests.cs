using System.Numerics;
using System.Text.Json;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Tests;

public sealed class PressureTests
{
    [Theory] [InlineData(FieldSide.Left)] [InlineData(FieldSide.Right)]
    public void FixedPackagesCompileAgainstEveryCatalogSurfaceWithExactBudgets(FieldSide side)
    {
        foreach (var play in new PlayManager().Catalog.Plays)
        foreach (bool flip in new[] { false, true })
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var kind in new[] { PressureKind.Mike, PressureKind.Edge, PressureKind.Nickel, PressureKind.Zero })
        {
            var formation = new FormationFactory().CreateFormation(PlayResolver.Resolve(play, flip), 60);
            var offense = OffensiveAlignmentSnapshot.From(formation, 60);
            var call = DefensiveCallDefinition.Get(kind == PressureKind.Zero ? "def.zero" : "def.edge");
            if (kind == PressureKind.Nickel && package == DefensePackage.Base)
            { Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(offense, call, package, pressure: new(kind, side))); continue; }
            var plan = DefensivePlanCompiler.Compile(offense, call, package, pressure: new(kind, side));
            Assert.Empty(DefensivePlanValidator.Validate(plan));
            Assert.Equal(kind == PressureKind.Zero ? 6 : 5, plan.Pressure.Entries.Count);
            Assert.Equal(offense.Skills.Count, plan.Assignments.Count(a => a.Pass is ManResponsibility));
            Assert.Equal(kind == PressureKind.Zero ? 0 : 1, plan.Assignments.Count(a => a.Pass is HelpResponsibility { Role: HelperRole.DeepMiddle }));
            Assert.Equal(plan.Pressure.Entries.Count, plan.Pressure.Entries.Select(e => e.Gap).Distinct().Count());
            Assert.Equal(2, plan.Pressure.Entries.Count(e => e.Contain != ContainRole.None));
            if (kind == PressureKind.Nickel) Assert.Contains(plan.Pressure.Entries, e => e.Actor == DefenderSlot.NB && e.Contain != ContainRole.None);
        }
    }

    [Fact]
    public void PressureFlipRecompilesCoverageAgainstSameOffenseAndTwiceRestoresPlan()
    {
        var (original, _, _) = DefensiveRuntimeTests.Setup("def.edge", "pass.pistol-twins.scissors");
        var cache = new DefensivePlanCache(); var selection = original.Pressure.Selection;
        var first = cache.Resolve(original.Offense, original.Call, original.Package, pressure: selection);
        var flipped = cache.Resolve(original.Offense, original.Call, original.Package, pressure: selection.Flip());
        var restored = cache.Resolve(original.Offense, original.Call, original.Package, pressure: selection.Flip().Flip());
        Assert.Same(first.Offense, flipped.Offense); Assert.Equal(first.ControlledActor, flipped.ControlledActor);
        Assert.NotEqual(first.Assignments, flipped.Assignments);
        Assert.Equal(JsonSerializer.Serialize(first, DefenseRedesignMatrix.Json), JsonSerializer.Serialize(restored, DefenseRedesignMatrix.Json));
        Assert.DoesNotContain(PressureVariants.Available(first), v => v.Kind == PressureKind.Nickel && first.Package == DefensePackage.Base);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void StalledRushersReserveOnlyFreeAlternatesAndKeepBothEdges(int hz)
    {
        var (plan, offense, defenders) = DefensiveRuntimeTests.Setup(); var runtime = new DefensiveRuntime(plan);
        foreach (var actor in defenders.Where(d => plan.Pressure.Entries.Any(e => e.Actor == d.Slot))) actor.IsBeingBlocked = true;
        bool countered = false;
        for (int i = 0; i < hz; i++)
        {
            runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / hz);
            countered |= runtime.Current!.Pressure!.Jobs.Any(j => j.Stage == PressureStage.Counter);
        }
        var board = runtime.Current!.Pressure!;
        Assert.True(countered);
        Assert.Equal(board.Jobs.Count, board.Jobs.Select(j => j.Gap).Distinct().Count());
        Assert.Equal(2, board.Jobs.Count(j => j.Contain != ContainRole.None));
        foreach (var j in board.Jobs.Where(j => j.Contain == ContainRole.None && j.Gap != plan.Pressure.Entries.Single(e => e.Actor == j.Actor).Gap))
            Assert.Equal(plan.Pressure.Entries.Single(e => e.Actor == j.Actor).AlternateGap, j.Gap);
        var held = board.Jobs.Select(j => j.Gap).ToArray();
        for (int i = 0; i < hz; i++) runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / hz);
        Assert.Equal(held, runtime.Current.Pressure!.Jobs.Select(j => j.Gap));
        Assert.All(defenders, d => Assert.Equal(plan.Assignments.Single(a => a.ActorId == d.Slot).Alignment, d.Position));
    }

    [Fact]
    public void ManualIdleRusherDoesNotCountOrLaunchAndDoesNotInventReplacement()
    {
        var (plan, offense, defenders) = DefensiveRuntimeTests.Setup("def.edge"); var runtime = new DefensiveRuntime(plan);
        var manual = plan.Pressure.Entries.First(e => e.Actor.IsLinebacker()).Actor;
        var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .1f, human: manual)!;
        Assert.Equal(5, frame.Pressure!.PlannedCount); Assert.Equal(4, frame.Pressure.ActualCount);
        Assert.False(frame.Decisions.Single(i => i.Actor == manual).ActiveRusher);
        Assert.Equal(5, frame.Decisions.Count(i => i.Primary != null));
        var actor = defenders.Single(d => d.Slot == manual);
        actor.Velocity = Vector2.Normalize(frame.Decisions.Single(i => i.Actor == manual).Target - actor.Position) * actor.Speed;
        frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .1f, human: manual)!;
        Assert.Equal(5, frame.Pressure!.ActualCount);
        offense.Ball.SetInAir(offense.Qb.Position, new(0, 10), 20, 30, 2);
        frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .2f, human: manual)!;
        Assert.Equal(0, frame.Pressure!.ActualCount); Assert.DoesNotContain(frame.Decisions, i => i.ActiveRusher);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void AllFixedVariantsExecuteThroughProductionControllers(int hz)
    {
        foreach (var kind in new[] { PressureKind.Mike, PressureKind.Edge, PressureKind.Nickel, PressureKind.Zero })
        foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
        {
            var input = new ScriptedInput(); var drive = new DefensiveDrive(input, 101, fixedCall: "pass.mesh", engine: DefensiveEngineMode.ZoneDevelopment);
            Assert.True(drive.SelectDefense(kind == PressureKind.Zero ? "def.zero" : "def.edge"));
            Assert.True(drive.SelectPressure(new(kind, side)), drive.SelectionRejection);
            drive.Execution.Control = new(true) { AutomateDefense = true };
            Assert.True(drive.Snap()); bool observed = false;
            for (int i = 0; drive.Live && i < hz * 30; i++)
            {
                drive.Update(1f / hz); var frame = drive.Execution.DefenseRuntime!.Current!;
                observed |= frame.Pressure!.ActualCount == (kind == PressureKind.Zero ? 6 : 5);
                Assert.Equal(11, frame.Decisions.Count);
                Assert.All(drive.Actors.Defenders, d => Assert.True(d.Velocity.Length() <= d.Speed * drive.Plays.DefenderSpeedMultiplier + .001f));
            }
            Assert.True(observed); Assert.False(drive.Live); Assert.Single(drive.Match.History);
        }
    }
}
