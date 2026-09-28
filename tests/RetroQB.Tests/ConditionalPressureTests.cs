using System.Numerics;
using RetroQB.AI;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Tests;

public sealed class ConditionalPressureTests
{
    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void SimulatedPressurePreservesRecoveryPathWhenForceOwnerIsBlockedAfterHandoff(int hz)
    {
        var sample = DefenseRuntimeMatrix.Exercise("run.hb-dive", "def.cover3", 101, hz, false,
            DefenseInputMode.Stationary, DefensiveEngineMode.ZoneDevelopment, pressure: new(PressureKind.Simulated, FieldSide.Right));
        Assert.Null(sample.Failure);
        Assert.Contains(sample.Decisions, f => f.Run?.Read == RunRead.Handoff);
    }

    private static (ResolvedDefensivePlan Plan, FormationResult Offense, List<Defender> Defense) Setup(PressureKind kind)
    {
        var (basis, offense, _) = DefensiveRuntimeTests.Setup(offense: "pass.gun-doubles.pa-cross");
        var plan = DefensivePlanCompiler.Compile(basis.Offense, basis.Call, pressure: new(kind));
        return (plan, offense, plan.Assignments.Select(a => new Defender(a.Alignment, a.PositionRole, a.ActorId)).ToList());
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void GreenDogWaitsForObservedBlockAndRecoversLateReleaseEvenWhenEngaged(int hz)
    {
        var (plan, offense, actors) = Setup(PressureKind.GreenDog); var runtime = new DefensiveRuntime(plan);
        var job = plan.Pressure.Conditional!; var back = offense.Receivers.Single(r => r.Index == job.ReleaseKey!.Value.Value);
        var contact = actors.Single(a => a.Slot == DefenderSlot.DT2);
        contact.Position = back.Position + Vector2.UnitX; contact.IsBeingBlocked = true;
        var green = actors.Single(a => a.Slot == job.Actor);
        for (int i = 0; i < hz * .45f; i++)
        {
            var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / hz)!;
            if (frame.Seconds < .34f) Assert.False(frame.Decisions.Single(d => d.Actor == job.Actor).ActiveRusher);
        }
        Assert.True(runtime.Current!.Observation.Receivers.Single(r => r.Id == job.ReleaseKey).Blocking);
        Assert.True(runtime.Current.Decisions.Single(d => d.Actor == job.Actor).ActiveRusher);
        Assert.Null(runtime.Current.Decisions.Single(d => d.Actor == job.Actor).Primary);
        Assert.Equal(5, runtime.Current.Pressure!.ActualCount);
        green.IsBeingBlocked = true; var position = green.Position;
        back.Position += new Vector2(0, 4); back.Velocity = new(0, 6);
        for (int i = 0; i < hz / 3; i++)
        {
            back.Position += back.Velocity / hz;
            runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / hz);
        }
        var recovery = runtime.Current!.Decisions.Single(d => d.Actor == job.Actor);
        Assert.False(recovery.ActiveRusher); Assert.Equal(job.ReleaseKey, recovery.Primary);
        Assert.Contains("LateRelease", recovery.Reason); Assert.Equal(position, green.Position);
        Assert.Contains(runtime.Current.Observation.Visible.Defenders, a => a.Id == job.Actor && a.Contact);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void DelayedPackageKeepsFiveOwnersAndLaunchesAfterDeclaredTime(int hz)
    {
        var (plan, offense, actors) = Setup(PressureKind.Delayed); var runtime = new DefensiveRuntime(plan);
        var job = plan.Pressure.Conditional!; bool launched = false;
        for (int i = 0; i < hz; i++)
        {
            var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / hz)!;
            Assert.Equal(5, frame.Decisions.Count(d => d.Primary != null));
            var delayed = frame.Decisions.Single(d => d.Actor == job.Actor);
            Assert.Null(delayed.Helping);
            if (frame.Seconds < job.Delay - .00001f) Assert.False(delayed.ActiveRusher);
            else { Assert.True(delayed.ActiveRusher); launched = true; }
        }
        Assert.True(launched);
        Assert.DoesNotContain(plan.Pressure.Entries, e => e.AlternateGap == job.Gap);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void EarlyThrowOrConfirmedRunCancelsDelayedLaunch(bool run)
    {
        var (plan, offense, actors) = Setup(PressureKind.Delayed); var runtime = new DefensiveRuntime(plan);
        runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), .2f);
        if (run) offense.Qb.Position = new(26, 62);
        else offense.Ball.SetInAir(new(26, 65), Vector2.UnitY, 20, 40, 0);
        runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), .1f);
        var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f)!;
        Assert.Equal(0, frame.Pressure!.ActualCount);
        Assert.DoesNotContain(frame.Decisions, d => d.ActiveRusher);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void BlockedStuntWaitIsBoundedAndFallbackPreservesContain(int hz)
    {
        var (plan, offense, actors) = Setup(PressureKind.Stunt); var runtime = new DefensiveRuntime(plan);
        var stunt = plan.Pressure.Stunt!; actors.Single(a => a.Slot == stunt.Penetrator).IsBeingBlocked = true;
        for (int i = 0; i < hz; i++)
        {
            var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / hz)!;
            var looper = frame.Pressure!.Jobs.Single(j => j.Actor == stunt.Looper);
            if (frame.Seconds < stunt.MaxWait - .00001f)
            { Assert.Equal(PressureStage.Staging, looper.Stage); Assert.False(looper.Participating); }
            else
            { Assert.Equal(stunt.FallbackGap, looper.Gap); Assert.True(looper.Participating); Assert.Contains("Fallback", looper.Reason); }
            Assert.Equal(2, frame.Pressure.Jobs.Count(j => j.Contain != ContainRole.None));
            Assert.Equal(4, frame.Pressure.Jobs.Select(j => j.Gap).Distinct().Count());
        }
    }

    [Fact]
    public void ClearStuntPathReleasesLooperBeforeTimeoutWithoutMovingActors()
    {
        var (plan, offense, actors) = Setup(PressureKind.Stunt); var runtime = new DefensiveRuntime(plan); var stunt = plan.Pressure.Stunt!;
        foreach (var actor in actors.Where(a => a.Slot != stunt.Looper)) actor.Position = new(2, 90);
        actors.Single(a => a.Slot == stunt.Penetrator).Position = new(26, 55);
        var positions = actors.Select(a => a.Position).ToArray();
        var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), .1f)!;
        var looper = frame.Pressure!.Jobs.Single(j => j.Actor == stunt.Looper);
        Assert.True(looper.Participating); Assert.Contains("PathClear", looper.Reason);
        Assert.Equal(positions, actors.Select(a => a.Position));
    }

    [Theory] [InlineData(true, true)] [InlineData(true, false)] [InlineData(false, true)]
    public void ScreenRequiresBothReleasedBlockerAndMovingOutlet(bool blockerMoves, bool outletMoves)
    {
        var (plan, offense, actors) = Setup(PressureKind.Base); var coordinator = new PressureCoordinator(plan);
        actors.Single(a => a.Slot == DefenderSlot.DT1).Position = new(24, 61);
        var visible = DefensiveRuntimeTests.Sight(offense, actors);
        var outlet = plan.Offense.Skills[0].Id;
        var receivers = visible.Receivers.Select(r => new ObservedReceiver(r.Id, r.Id == outlet ? new(24, 59) : r.Position,
            r.Id == outlet && outletMoves ? new(-2, 0) : Vector2.Zero, ReleaseDirection.Outward, false, false, false, false, 1)).ToArray();
        var blockers = plan.Offense.Linemen.Select(b => new VisibleBlocker(b.Id, b.Id == 0 ? new(24, 63) : b.Position,
            b.Id == 0 && blockerMoves ? new(0, 3) : Vector2.Zero, .6f));
        var sight = new DefensiveSighting(visible.Quarterback, visible.Ball, visible.Receivers, visible.Defenders, blockers);
        Dictionary<DefenderSlot, DefensiveIntent> intents = null!;
        for (int i = 1; i <= 3; i++)
        {
            var frame = new DefensiveObservationFrame(i * .1f, sight, receivers, false, 0, 0);
            intents = plan.Assignments.ToDictionary(a => a.ActorId, a => new DefensiveIntent(a.ActorId, DefensiveIntentKind.Leverage,
                a.Alignment, (a.Pass as ManResponsibility)?.Target, null, "test", "test", false));
            foreach (var intent in coordinator.Decide(frame, null)) intents[intent.Actor] = intent;
            coordinator.ApplyConditionalAndScreens(frame, intents, null, new());
        }
        Assert.Equal(blockerMoves && outletMoves, intents.Values.Any(i => i.Rule == "PRESS-12"));
        Assert.Equal(5, intents.Values.Count(i => i.Primary != null));
        Assert.Equal(2, coordinator.Current!.Jobs.Count(j => j.Contain != ContainRole.None && j.Participating));
    }

    [Fact]
    public void SuspendedTimersAndIdleManualConditionalNeverCreateExtraRush()
    {
        var (plan, offense, actors) = Setup(PressureKind.Delayed); var runtime = new DefensiveRuntime(plan); var job = plan.Pressure.Conditional!;
        var sight = DefensiveRuntimeTests.Sight(offense, actors);
        var initial = runtime.Decide(sight, .1f, human: job.Actor)!;
        Assert.Same(initial, runtime.Decide(sight, 10, suspended: true, human: job.Actor));
        var frame = runtime.Decide(sight, .5f, human: job.Actor)!;
        Assert.Equal(4, frame.Pressure!.ActualCount);
        Assert.False(frame.Decisions.Single(i => i.Actor == job.Actor).ActiveRusher);
        Assert.Contains("ManualActorNotParticipating", frame.Decisions.Single(i => i.Actor == job.Actor).Reason);
    }

    [Theory] [InlineData(PressureKind.GreenDog)] [InlineData(PressureKind.Delayed)] [InlineData(PressureKind.Stunt)]
    public void ConditionalPackagesFinishThroughProductionControllers(PressureKind kind)
    {
        foreach (int hz in new[] { 30, 60, 120 })
        {
            var drive = new DefensiveDrive(new ScriptedInput(), 202, fixedCall: "pass.gun-doubles.pa-cross", engine: DefensiveEngineMode.ZoneDevelopment);
            Assert.True(drive.SelectPressure(new(kind)), drive.SelectionRejection);
            drive.Execution.Control = new(true) { AutomateDefense = true }; drive.Snap();
            for (int i = 0; drive.Live && i < hz * 30; i++)
            {
                drive.Update(1f / hz); var frame = drive.Execution.DefenseRuntime!.Current!;
                Assert.Equal(11, frame.Decisions.Count);
                Assert.Equal(frame.Decisions.Count(d => d.ActiveRusher), frame.Pressure!.ActualCount);
            }
            Assert.False(drive.Live); Assert.Single(drive.Match.History);
        }
    }

    [Fact]
    public void CatalogFiltersUnownedBackAndConsumedHelperWithoutWeakeningCoverage()
    {
        foreach (var play in new PlayManager().Catalog.Plays)
        foreach (bool flip in new[] { false, true })
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
        {
            var formation = new FormationFactory().CreateFormation(PlayResolver.Resolve(play, flip), 60);
            var basis = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(formation, 60), DefensiveCallDefinition.Get("def.cover1"), package);
            foreach (var kind in new[] { PressureKind.GreenDog, PressureKind.Delayed, PressureKind.Stunt })
            {
                if (kind == PressureKind.GreenDog && !basis.Surface.Threats.Any(t => t.Backfield))
                {
                    Assert.DoesNotContain(PressureVariants.Available(basis), v => v.Kind == kind);
                    Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(basis.Offense, basis.Call, package, pressure: new(kind, side)));
                    continue;
                }
                var plan = DefensivePlanCompiler.Compile(basis.Offense, basis.Call, package, pressure: new(kind, side));
                Assert.Empty(DefensivePlanValidator.Validate(plan));
                Assert.Equal(plan.Offense.Skills.Count, plan.Assignments.Count(a => a.Pass is ManResponsibility));
                Assert.Equal(4, plan.Pressure.Entries.Count);
            }
        }
        var (original, _, _) = Setup(PressureKind.Base);
        var spy = DefensivePlanCompiler.Compile(original.Offense, original.Call, adjustments: new(spy: DefenderSlot.SS));
        Assert.DoesNotContain(PressureVariants.Available(spy), v => v.Kind == PressureKind.Delayed);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void EdgePressureMaintainsNearShoulderAndBacksideReversalBeforeThrow(int hz)
    {
        var (basis, offense, actors) = DefensiveRuntimeTests.Setup("def.edge");
        var runtime = new DefensiveRuntime(basis);
        for (int tick = 0; tick < hz; tick++)
        {
            offense.Qb.Position = new(tick < hz / 2 ? 13 : 40, 55);
            var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / hz)!;
            var left = frame.Pressure!.Jobs.Single(j => j.Contain == ContainRole.LeftEdge);
            var right = frame.Pressure.Jobs.Single(j => j.Contain == ContainRole.RightEdge);
            Assert.True(left.Target.X < offense.Qb.Position.X); Assert.True(right.Target.X > offense.Qb.Position.X);
            Assert.True(left.Participating && right.Participating);
            Assert.Equal(5, frame.Decisions.Count(d => d.Primary != null));
            Assert.Equal(5, frame.Pressure.Jobs.Select(j => j.Gap).Distinct().Count());
        }
        offense.Ball.SetInAir(offense.Qb.Position, Vector2.UnitY, 20, 40, 0);
        var thrown = runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / hz)!;
        Assert.Equal(0, thrown.Pressure!.ActualCount);
        Assert.Equal(5, thrown.Decisions.Count(d => d.Primary != null));
    }

    [Fact]
    public void ThrowCancelsWaitingStuntAndVisibleLinemanBlocksLoopPath()
    {
        var (plan, offense, actors) = Setup(PressureKind.Stunt); var runtime = new DefensiveRuntime(plan); var stunt = plan.Pressure.Stunt!;
        foreach (var actor in actors.Where(a => a.Slot != stunt.Looper)) actor.Position = new(2, 90);
        actors.Single(a => a.Slot == stunt.Penetrator).Position = new(26, 55);
        var loop = actors.Single(a => a.Slot == stunt.Looper);
        var entry = plan.Pressure.Entries.Single(e => e.Actor == stunt.Looper).Entry;
        var sight = DefensiveRuntimeTests.Sight(offense, actors);
        var blocked = new DefensiveSighting(sight.Quarterback, sight.Ball, sight.Receivers, sight.Defenders,
            [new(0, (loop.Position + entry) / 2, Vector2.Zero, 1)]);
        var frame = runtime.Decide(blocked, .1f)!;
        Assert.Equal(PressureStage.Staging, frame.Pressure!.Jobs.Single(j => j.Actor == loop.Slot).Stage);
        offense.Ball.SetInAir(offense.Qb.Position, Vector2.UnitY, 20, 40, 0);
        frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), .5f)!;
        Assert.Equal(0, frame.Pressure!.ActualCount);
        Assert.All(frame.Pressure.Jobs, j => Assert.Equal(PressureStage.Released, j.Stage));
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void LateReleaseFromProductionBlockContactUsesMotorRecoveryWithoutTeleport(int hz)
    {
        var (plan, offense, actors) = Setup(PressureKind.GreenDog); var runtime = new DefensiveRuntime(plan);
        var job = plan.Pressure.Conditional!; var green = actors.Single(a => a.Slot == job.Actor);
        var back = offense.Receivers.Single(r => r.Index == job.ReleaseKey!.Value.Value);
        var contact = actors.Single(a => a.Slot == DefenderSlot.DT2);
        contact.Position = back.Position + Vector2.UnitX; contact.IsBeingBlocked = true;
        for (int i = 0; i < hz / 2; i++) runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / hz);
        Assert.True(runtime.Current!.Decisions.Single(d => d.Actor == green.Slot).ActiveRusher);
        // Arrange a lineman engaging the launched actor, then run the production contact controller.
        var blocker = offense.Blockers[0]; green.Position = blocker.Position + Vector2.UnitY;
        green.IsRusher = true; green.Velocity = -Vector2.UnitY * green.Speed;
        var resolved = PlayResolver.Resolve(new PlayManager().Catalog["pass.gun-doubles.pa-cross"]);
        OffensiveLinemanAI.UpdateBlockers([blocker], [green], resolved, 60, 1f / hz, false, _ => { }, offense.Qb.Position, offense.Qb.Position, true);
        Assert.True(green.IsBeingBlocked);
        Assert.True(green.Velocity.Length() < green.Speed);
        back.Position += new Vector2(0, 5); back.Velocity = new(0, 6);
        bool recovered = false;
        for (int i = 0; i < hz / 3; i++)
        {
            back.Position += back.Velocity / hz; var before = green.Position;
            var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / hz)!;
            Assert.Equal(before, green.Position);
            var intent = frame.Decisions.Single(d => d.Actor == green.Slot);
            green.IsRusher = intent.ActiveRusher;
            green.Velocity = DefenderMotor.Velocity(frame.Observation.Visible.Defenders.Single(d => d.Id == green.Slot),
                intent, frame.Observation.Visible.Defenders, 1, 1f / hz);
            green.Update(1f / hz);
            Assert.True(Vector2.Distance(before, green.Position) <= green.Speed / hz + .001f);
            recovered |= !intent.ActiveRusher && intent.Primary == job.ReleaseKey && intent.Reason.Contains("LateRelease");
        }
        Assert.True(recovered);
    }
}
