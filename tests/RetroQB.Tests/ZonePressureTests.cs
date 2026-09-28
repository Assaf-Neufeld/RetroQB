using System.Numerics;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Tests;

public sealed class ZonePressureTests
{
    [Theory] [InlineData(PressureKind.ZoneFive, 5, 3)] [InlineData(PressureKind.Simulated, 4, 4)]
    public void CatalogCompilesExplicitDropsWithExactShellAndRushBudgets(PressureKind kind, int rushers, int underneath)
    {
        foreach (var play in new PlayManager().Catalog.Plays)
        foreach (bool flipped in new[] { false, true })
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
        {
            var f = new FormationFactory().CreateFormation(PlayResolver.Resolve(play, flipped), 60);
            var plan = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(f, 60), DefensiveCallDefinition.Get("def.cover3"), package, pressure: new(kind, side));
            Assert.Empty(DefensivePlanValidator.Validate(plan));
            Assert.Equal(rushers, plan.Pressure.Entries.Count);
            Assert.Equal(3, plan.Assignments.Count(a => a.Pass is ZoneResponsibility { IsDeep: true }));
            Assert.Equal(underneath, plan.Assignments.Count(a => a.Pass is ZoneResponsibility { IsDeep: false }));
            var drop = Assert.Single(plan.Pressure.Drops);
            Assert.Equal(side == FieldSide.Left ? DefenderSlot.DE1 : DefenderSlot.DE2, drop.Actor);
            Assert.DoesNotContain(plan.Pressure.Entries, e => e.Actor == drop.Actor);
            Assert.NotEqual(plan.Assignments.Single(a => a.ActorId == drop.Actor).Alignment, drop.Home);
        }
    }

    [Fact]
    public void IncapableDroppingRosterIsFilteredAndCannotReachPreview()
    {
        var (basePlan, offense, _) = DefensiveRuntimeTests.Setup("def.cover3");
        var team = new DefensiveTeamAttributes { DeSpeed = .25f };
        Assert.DoesNotContain(PressureVariants.Available(basePlan, team), p => p.Kind is PressureKind.ZoneFive or PressureKind.Simulated);
        var plan = DefensivePlanCompiler.Compile(basePlan.Offense, basePlan.Call, pressure: new(PressureKind.Simulated));
        Assert.Throws<InvalidOperationException>(() => new DefensivePlanPreview(plan, offense, team));
        Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(basePlan.Offense, DefensiveCallDefinition.Get("def.cover2-zone"), pressure: new(PressureKind.ZoneFive)));
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void DropTravelsFromActualAlignmentAndProtectionUsesOnlyCurrentRushers(int hz)
    {
        foreach (var kind in new[] { PressureKind.ZoneFive, PressureKind.Simulated })
        foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
        foreach (string play in new[] { "pass.mesh", "pass.four-verts", "pass.gun-doubles.pa-cross" })
        {
            var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: play, engine: DefensiveEngineMode.ZoneDevelopment);
            drive.SelectDefense("def.cover3"); Assert.True(drive.SelectPressure(new(kind, side)), drive.SelectionRejection);
            var plan = drive.CompiledPlan!; var drop = Assert.Single(plan.Pressure.Drops);
            var actor = drive.Actors.Defenders.Single(d => d.Slot == drop.Actor); var start = actor.Position;
            drive.Execution.Control = new(true) { AutomateDefense = true }; drive.Snap();
            bool liveRush = false, moved = false;
            for (int i = 0; drive.Live && i < hz * 30; i++)
            {
                var before = actor.Position; drive.Update(1f / hz);
                var frame = drive.Execution.DefenseRuntime!.Current!;
                Assert.False(actor.IsRusher);
                Assert.DoesNotContain(PassProtectionPlanner.Assign(drive.Actors.Blockers, drive.Actors.Defenders, drive.Execution.Backfield.OpeningComplete).Values, d => d.Slot == drop.Actor);
                if (!frame.Observation.ConfirmedCarrier)
                {
                    Assert.NotNull(frame.Coverage);
                    Assert.Contains(frame.Coverage.Commitments, c => c.Actor == drop.Actor);
                }
                liveRush |= frame.Pressure!.ActualCount == (kind == PressureKind.ZoneFive ? 5 : 4);
                moved |= Vector2.Distance(start, actor.Position) > 1;
                // Motor speed remains bounded; contact/separation uses the unchanged production pipeline.
                Assert.True(actor.Velocity.Length() <= actor.Speed * drive.Plays.DefenderSpeedMultiplier + .001f);
            }
            Assert.True(liveRush); Assert.True(moved); Assert.False(drive.Live); Assert.Single(drive.Match.History);
            Assert.Contains(drive.LastReplay!.Frames, f => f.DefensiveDecision?.Pressure?.PlannedCount == plan.Pressure.Entries.Count);
        }
    }
}
