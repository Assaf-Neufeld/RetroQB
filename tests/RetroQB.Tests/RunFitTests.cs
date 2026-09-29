using System.Numerics;
using System.Text.Json;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Tests;

public sealed class RunFitTests
{
    private sealed class Fixture
    {
        public ResolvedDefensivePlan Plan { get; } = DefensiveRuntimeTests.Setup().Plan;
        public RunFitCoordinator Coordinator { get; }
        public Dictionary<DefenderSlot, DefenderMotion> Actors { get; }
        public Dictionary<DefenderSlot, DefensiveIntent> Intents { get; private set; } = new();
        public List<string> Events { get; } = new();
        public float Time;
        public Fixture()
        {
            Coordinator = new(Plan);
            Actors = Plan.Assignments.ToDictionary(a => a.ActorId, a => new DefenderMotion(a.ActorId, a.Alignment, Vector2.Zero, 9, 1.1f, 1, false));
        }
        public void Actor(DefenderSlot id, float x, float y, bool blocked = false, float vx = 0, float vy = 0)
            => Actors[id] = Actors[id] with { Position = new(x, y), Contact = blocked, Velocity = new(vx, vy) };
        public RunBoard Step(Vector2 position, Vector2 velocity, RunRead read = RunRead.Handoff, float dt = .02f, DefenderSlot? human = null)
        {
            Time += dt; Events.Clear();
            var receivers = Plan.Offense.Skills.Select(r => new ObservedReceiver(r.Id, r.Position, Vector2.Zero, ReleaseDirection.Unknown, false, false, false, false, 0)).ToArray();
            var sight = new DefensiveSighting(position, new(read is RunRead.Handoff or RunRead.AfterCatch ? BallState.HeldByReceiver : BallState.HeldByQB,
                position, velocity, 0, new(0)), receivers.Select(r => new VisibleMotion(r.Id, r.Position, r.Velocity, 1)), Actors.Values);
            var run = new ObservedRun(read, new(0), position, velocity, Plan.Surface.Threats.FirstOrDefault(t => t.Backfield)?.ActorId, false);
            var frame = new DefensiveObservationFrame(Time, sight, receivers, run.Confirmed, 0, 0) { Run = run };
            Intents = Plan.Assignments.ToDictionary(a => a.ActorId, a => new DefensiveIntent(a.ActorId, DefensiveIntentKind.Leverage,
                Actors[a.ActorId].Position, (a.Pass as ManResponsibility)?.Target, null, "TestCoverage", "Hold", a.Pass is RushResponsibility));
            Coordinator.Apply(frame, Intents, human, 1, Events);
            var board = Coordinator.Current!;
            if (run.Confirmed)
            {
                Assert.Equal(11, board.Jobs.Count); Assert.Equal(11, board.Jobs.Select(j => j.Actor).Distinct().Count());
                var named = new[] { board.Tackler, board.Force, board.CutbackPlayer, board.LastSupport }.Where(a => a != null).ToArray();
                Assert.Equal(named.Length, named.Distinct().Count());
                Assert.All(Intents.Values, i => { Assert.Null(i.Primary); Assert.Null(i.Helping); Assert.False(i.ActiveRusher); });
            }
            return board;
        }
    }

    [Theory] [InlineData(30f)] [InlineData(109f)]
    public void ConditionalFitsCoverEveryActualGapAcrossAllCallsCatalogPackagesAndFlips(float los)
    {
        foreach (var play in new PlayManager().Catalog.Plays)
        foreach (bool flip in new[] { false, true })
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var call in DefensiveCallDefinition.All)
        {
            var formation = new FormationFactory().CreateFormation(PlayResolver.Resolve(play, flip), los);
            var plan = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(formation, los), call, package);
            Assert.Empty(DefensivePlanValidator.Validate(plan));
            Assert.Equal(plan.Surface.Gaps.Count, plan.Fits.Gaps.Count);
            Assert.Equal(plan.Fits.Gaps.Count, plan.Fits.Gaps.Select(g => g.Owner).Distinct().Count());
            Assert.Equal(5, new[] { plan.Fits.LeftForce, plan.Fits.RightForce, plan.Fits.LeftCutback, plan.Fits.RightCutback, plan.Fits.LastSupport }.Distinct().Count());
        }
    }

    [Fact]
    public void SpyConsumesOnlyDeclaredLowHelperAndCannotBorrowReservedBudgets()
    {
        var (original, offense, _) = DefensiveRuntimeTests.Setup();
        var spy = DefensivePlanCompiler.Compile(original.Offense, original.Call, adjustments: new(spy: DefenderSlot.SS));
        Assert.Equal(5, spy.Assignments.Count(a => a.Pass is ManResponsibility));
        Assert.Single(spy.Assignments, a => a.Pass is HelpResponsibility { Role: HelperRole.Spy });
        Assert.Contains(spy.Assignments, a => a.ActorId == DefenderSlot.FS && a.Pass is HelpResponsibility { Role: HelperRole.DeepMiddle });
        foreach (var invalid in new[] { DefenderSlot.CB1, DefenderSlot.FS, DefenderSlot.DE1 })
            Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(original.Offense, original.Call, adjustments: new(spy: invalid)));
        Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(original.Offense, original.Call, adjustments: new(spy: DefenderSlot.SS, bracket: new(DefenderSlot.SS, new(0)))));
        var cache = new DefensivePlanCache(); var normal = cache.Resolve(original.Offense, original.Call);
        Assert.NotSame(normal, cache.Resolve(original.Offense, original.Call, adjustments: new(spy: DefenderSlot.SS)));
        var defenders = spy.Assignments.Select(a => new Defender(a.Alignment, a.PositionRole, a.ActorId)).ToList();
        offense.Qb.Position = new(12, 56); var runtime = new DefensiveRuntime(spy);
        var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .02f)!;
        var cue = frame.Decisions.Single(i => i.Actor == DefenderSlot.SS);
        Assert.Equal(DefensiveIntentKind.Spy, cue.Kind); Assert.Null(cue.Primary); Assert.False(cue.ActiveRusher);
        Assert.True(cue.Target.X < offense.Qb.Position.X); Assert.Equal(5, frame.Decisions.Count(i => i.Primary != null));
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void DM14_VisibleMeshCreatesBoundedReadAndReleaseRestoresCoverageWithoutMovingDeepPlayers(int hz)
    {
        var f = new Fixture(); var starts = f.Actors.ToDictionary(p => p.Key, p => p.Value.Position);
        var first = f.Step(new(26, 56), Vector2.Zero, RunRead.Mesh, 1f / hz);
        Assert.NotEmpty(first.Jobs);
        for (int i = 0; i < hz / 4; i++)
        {
            var board = f.Step(new(26, 56), Vector2.Zero, RunRead.Mesh, 1f / hz);
            foreach (var read in board.Jobs)
            {
                Assert.True(Vector2.Distance(starts[read.Actor], read.Target) <= RunTuning.ReadStep + .001f);
                Assert.False(RunFitCompiler.DeepPass(f.Plan.Assignments.Single(a => a.ActorId == read.Actor).Pass));
                f.Actors[read.Actor] = f.Actors[read.Actor] with { Position = read.Target };
            }
            foreach (var deep in f.Plan.Assignments.Where(a => RunFitCompiler.DeepPass(a.Pass))) Assert.Equal(starts[deep.ActorId], f.Intents[deep.ActorId].Target);
        }
        f.Step(new(26, 56), Vector2.Zero, RunRead.Pass, 1f / hz);
        Assert.DoesNotContain(f.Intents.Values, i => i.Kind == DefensiveIntentKind.ReadStep);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void ObserverRecognizesPossessionCatchAndQuarterbackCrossingButNeverMovingQuarterbackAlone(int hz)
    {
        var (plan, offense, defenders) = DefensiveRuntimeTests.Setup(); var runtime = new DefensiveRuntime(plan);
        for (int i = 0; i < hz / 2; i++) { offense.Qb.Position += new Vector2(-4f / hz, 0); runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / hz); }
        Assert.False(runtime.Current!.Observation.ConfirmedCarrier); Assert.Equal(5, runtime.Current.Decisions.Count(i => i.Primary != null));
        offense.Qb.Position = new(12, 60.2f); var crossed = runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / hz)!;
        Assert.Equal(RunRead.QuarterbackRun, crossed.Run!.Read); Assert.Equal(11, crossed.Run.Jobs.Count);
        offense.Qb.Position = new(12, 59); Assert.True(runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / hz)!.Observation.ConfirmedCarrier);
        var receiver = offense.Receivers[0]; offense.Ball.SetInAir(receiver.Position, Vector2.Zero, 5, 10, 0);
        runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / hz); offense.Ball.SetHeld(receiver, BallState.HeldByReceiver);
        Assert.Equal(RunRead.AfterCatch, runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / hz)!.Run!.Read);
    }

    [Fact]
    public void MeshObservationUsesConvergenceAndExpiresWithoutHiddenFakeMetadata()
    {
        var (plan, offense, defenders) = DefensiveRuntimeTests.Setup(offense: "pass.gun-doubles.pa-cross"); var runtime = new DefensiveRuntime(plan);
        var back = offense.Receivers.Single(r => new ReceiverId(r.Index) == plan.Surface.Threats.First(t => t.Backfield).ActorId);
        back.Position = offense.Qb.Position + new Vector2(0, -3);
        bool seen = false;
        for (int i = 0; i < 10; i++) { back.Position += new Vector2(0, .12f); seen |= runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .02f)!.Run!.Read == RunRead.Mesh; }
        Assert.True(seen);
        for (int i = 0; i < 50; i++) runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .02f);
        Assert.Equal(RunRead.Pass, runtime.Current!.Run!.Read); Assert.False(runtime.Current.Observation.ConfirmedCarrier);
        offense.Ball.SetHeld(back, BallState.HeldByReceiver);
        Assert.Equal(RunRead.Handoff, runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .02f)!.Run!.Read);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void RUN02_InsideHandoffUsesDistinctGapsCutbackAndGoalSideSupport(int hz)
    {
        var f = new Fixture(); var board = f.Step(new(26, 58), new(0, 4), dt: 1f / hz);
        Assert.Contains(board.Jobs, j => j.Duty == RunDuty.Gap);
        Assert.Equal(board.Jobs.Count(j => j.GapIndex != null), board.Jobs.Where(j => j.GapIndex != null).Select(j => j.GapIndex).Distinct().Count());
        Assert.NotNull(board.CutbackPlayer); Assert.NotNull(board.LastSupport);
        Assert.True(board.Jobs.Single(j => j.Duty == RunDuty.LastSupport).Target.Y > 58);
        Assert.True(board.Jobs.Select(j => j.Target).Distinct().Count() >= 7);
    }

    [Fact]
    public void RUN03_BlockedForceTransfersOnlyToReachableFreeSupport()
    {
        var f = new Fixture(); var force = f.Plan.Fits.RightForce;
        f.Actor(force, 37, 60, blocked: true);
        var replacement = f.Plan.Assignments.First(a => a.ActorId != f.Plan.Fits.LastSupport && a.ActorId != f.Plan.Fits.LeftCutback
            && a.ActorId != f.Plan.Fits.LeftForce && a.ActorId != force).ActorId;
        f.Actor(replacement, 41, 63);
        var board = f.Step(new(36, 60), new(3, 2));
        Assert.NotEqual(force, board.Force);
        Assert.Contains(board.Transfers, t => t.Duty == RunDuty.Force && t.Source == force && t.Stage == "Commit");
        Assert.True(board.Jobs.Single(j => j.Duty == RunDuty.Force).Target.X > 36);
        foreach (var key in f.Actors.Keys.ToArray()) f.Actors[key] = f.Actors[key] with { Contact = true };
        board = f.Step(new(36, 60), new(3, 2));
        Assert.Contains(board.Transfers, t => t.Duty == RunDuty.Force && t.Stage == "Decline");
        Assert.Contains(board.Concessions, c => c.Contains("ForceExposed"));
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void DM15_CutbackUsesPreviousBacksideResponderAndKeepsLastSupport(int hz)
    {
        var f = new Fixture(); var position = new Vector2(35, 64);
        var before = f.Step(position, new(3, 2), dt: 1f / hz);
        Assert.NotNull(before.CutbackPlayer); var oldCut = before.CutbackPlayer!.Value;
        f.Actor(oldCut, 33, 66);
        bool observed = false;
        for (int i = 0; i < hz / 3; i++)
        {
            var board = f.Step(position, new(-3, 2), dt: 1f / hz);
            if (!board.Cutback) continue;
            observed = true; Assert.NotNull(board.LastSupport);
            Assert.Equal(oldCut, board.Tackler);
        }
        Assert.True(observed);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void RUN08_BlockedOrBeatenFirstTacklerHandsOffToNextReachableActor(bool blocked)
    {
        var f = new Fixture(); var protectedActors = new[] { f.Plan.Fits.LastSupport, f.Plan.Fits.LeftCutback, f.Plan.Fits.RightCutback, f.Plan.Fits.LeftForce, f.Plan.Fits.RightForce };
        var pair = f.Actors.Keys.Where(k => !protectedActors.Contains(k)).Take(2).ToArray();
        f.Actor(pair[0], 26, 64); f.Actor(pair[1], 27, 68);
        var first = f.Step(new(26, 62), new(0, 4)); Assert.Equal(pair[0], first.Tackler);
        f.Actor(pair[0], 26, 62, blocked); var next = f.Step(new(26, 65), new(0, 4));
        Assert.NotEqual(pair[0], next.Tackler); Assert.NotNull(next.Tackler);
        Assert.Contains(next.Concessions, c => c.Contains(blocked ? "BlockedFirstTackler" : "BeatenFirstTackler"));
        Assert.Contains(next.Transfers, t => t.Duty == RunDuty.Tackle && t.Stage == "Commit");
    }

    [Fact]
    public void RUN07_AfterCatchUsesPredictedInterceptAndComplementaryCutoffAngles()
    {
        var f = new Fixture();
        foreach (var id in f.Actors.Keys.ToArray()) f.Actor(id, 6, 55);
        f.Actor(f.Plan.Fits.LastSupport, 26, 82); f.Actor(f.Plan.Fits.RightForce, 32, 69); f.Actor(DefenderSlot.DT1, 26, 71);
        var board = f.Step(new(26, 67), new(2, 5), RunRead.AfterCatch);
        Assert.NotNull(board.Tackler); Assert.True(board.Jobs.Select(j => j.Target).Distinct().Count() > 6);
        Assert.All(f.Intents.Values, i => Assert.Equal("RUN-07", i.Rule));
        var tackle = board.Jobs.Single(j => j.Duty == RunDuty.Tackle); Assert.True(tackle.Target.Y > 67);
        Assert.Contains(board.Jobs, j => j.Duty == RunDuty.LastSupport && j.Target.Y > tackle.Target.Y);
    }

    [Fact]
    public void UnreachableOrHumanAbandonedRunSupportCannotGainSpeedOrInventAStop()
    {
        var f = new Fixture(); foreach (var id in f.Actors.Keys.ToArray()) f.Actor(id, 2, 30, vy: -8);
        var board = f.Step(new(45, 100), new(0, 12), human: f.Plan.ControlledActor);
        Assert.Null(board.Tackler); Assert.Contains(board.Concessions, c => c.Contains("NoReachableImmediateStop"));
        foreach (var intent in f.Intents.Values)
        {
            var actor = f.Actors[intent.Actor]; var velocity = DefenderMotor.Velocity(actor, intent, f.Actors.Values.ToArray(), 1, .02f);
            Assert.True(velocity.Length() <= actor.Speed + .001f);
        }
    }

    [Fact]
    public void StationaryRecoveryUsesOneAiActorAndCancelsWhenCarrierMoves()
    {
        var f = new Fixture(); RunBoard board = null!;
        for (int i = 0; i < 60; i++) board = f.Step(new(26, 56), Vector2.Zero, human: f.Plan.ControlledActor);
        var path = Assert.Single(board.Jobs, j => j.Reason.Contains("OutsideRecoveryPath"));
        Assert.NotEqual(f.Plan.ControlledActor, path.Actor);
        Assert.NotEqual(board.Force, path.Actor); Assert.NotEqual(board.CutbackPlayer, path.Actor); Assert.NotEqual(board.LastSupport, path.Actor);
        board = f.Step(new(26, 57), new(0, 4), human: f.Plan.ControlledActor);
        Assert.DoesNotContain(board.Jobs, j => j.Reason.Contains("OutsideRecoveryPath"));
    }

    [Fact]
    public void BehindLosEscapeAndReversalPreserveManCoverageAndBothContainEdgesUntilLateThrow()
    {
        var (plan, offense, defenders) = DefensiveRuntimeTests.Setup(); var runtime = new DefensiveRuntime(plan);
        foreach (float x in new[] { 12f, 40f })
        {
            offense.Qb.Position = new(x, 56); var frame = runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .1f)!;
            Assert.False(frame.Observation.ConfirmedCarrier); Assert.Equal(5, frame.Decisions.Count(i => i.Primary != null));
            Assert.Equal(2, frame.Decisions.Count(i => i.Rule == "PRESS-11"));
            Assert.Contains(frame.Decisions, i => i.Actor == DefenderSlot.FS && i.Kind == DefensiveIntentKind.Help);
        }
        offense.Ball.SetInAir(new(40, 58), new(0, 10), 20, 30, 3);
        var thrown = runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .02f)!;
        Assert.False(thrown.Observation.ConfirmedCarrier); Assert.Equal(5, thrown.Decisions.Count(i => i.Primary != null));
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void RealHandoffsAndPlayActionFinishThroughProductionPhysicsAtEveryRate(int hz)
    {
        foreach (string play in new[] { "run.hb-dive", "run.stretch-right", "run.counter-left", "pass.gun-doubles.pa-cross" })
        foreach (string call in new[] { "def.cover1", "def.cover3" })
        {
            var sample = DefenseRuntimeMatrix.Exercise(play, call, 101, hz, true, DefenseInputMode.FullAI, DefensiveEngineMode.ZoneDevelopment);
            Assert.Null(sample.Failure); Assert.NotNull(sample.Outcome);
            Assert.DoesNotContain(sample.Decisions.SelectMany(d => d.Decisions), i => i.Rule == "P2-CARRIER-FALLBACK");
            if (play.StartsWith("run.")) Assert.Contains(sample.Decisions, d => d.Run?.Read == RunRead.Handoff);
        }
    }

    [Fact]
    public void RunBoardsAreDeterministicFreezeAndSurviveReplayWhileHumanRemainsManual()
    {
        var a = DefenseRuntimeMatrix.Exercise("run.stretch-right", "def.cover3", 202, 60, true, DefenseInputMode.Stationary, DefensiveEngineMode.ZoneDevelopment);
        var b = DefenseRuntimeMatrix.Exercise("run.stretch-right", "def.cover3", 202, 60, true, DefenseInputMode.Stationary, DefensiveEngineMode.ZoneDevelopment);
        Assert.Null(a.Failure); Assert.Null(b.Failure); Assert.Equal(JsonSerializer.Serialize(a, DefenseRedesignMatrix.Json), JsonSerializer.Serialize(b, DefenseRedesignMatrix.Json));
        var input = new ScriptedInput(); var drive = new DefensiveDrive(input, 101, fixedCall: "run.stretch-right", engine: DefensiveEngineMode.ZoneDevelopment);
        drive.SelectCompiledDefenseForDevelopment("def.cover3"); drive.Snap(); var start = drive.Linebacker.Position;
        for (int i = 0; i < 12; i++) drive.Update(1f / 60);
        Assert.Equal(start, drive.Linebacker.Position);
        var snapshot = drive.Execution.DefenseRuntime!.Current!;
        Assert.Same(snapshot, drive.Execution.DefenseRuntime.Decide(snapshot.Observation.Visible, 1, suspended: true));
        string saved = JsonSerializer.Serialize(snapshot, DefenseRedesignMatrix.Json);
        for (int i = 0; drive.Live && i < 1800; i++) drive.Update(1f / 60);
        Assert.False(drive.Live); Assert.Contains(drive.LastReplay!.Frames, f => f.DefensiveDecision?.Run?.Read == RunRead.Handoff);
        Assert.Equal(saved, JsonSerializer.Serialize(snapshot, DefenseRedesignMatrix.Json));
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void StationaryHumanRunnerCannotStrandTheDefenseOutsideContactRange(int hz)
    {
        foreach (var call in DefensiveCallDefinition.All)
        foreach (string play in new[] { "run.hb-dive", "run.counter-left", "run.counter-right", "run.stretch-right" })
        {
            var sample = DefenseRuntimeMatrix.Exercise(play, call.Id, 101, hz, false, DefenseInputMode.Stationary, DefensiveEngineMode.MatchDevelopment);
            Assert.True(sample.Failure == null, $"{play}/{call.Id}/{hz}: {sample.Failure}\n{JsonSerializer.Serialize(sample.Decisions.Last(), DefenseRedesignMatrix.Json)}");
            Assert.NotNull(sample.Outcome);
        }
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void ActualAbortedFakeAllowsLateThrowWithoutPrematureCoverageCollapse(int hz)
    {
        var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.Get("scarlet-guard");
        var drive = new DefensiveDrive(input, 101, fixedCall: "pass.gun-doubles.pa-cross", match: new(user, cpu, user.Id), engine: DefensiveEngineMode.ZoneDevelopment);
        drive.SelectCompiledDefenseForDevelopment("def.cover3"); drive.Snap(); bool aborted = false, thrown = false;
        for (int tick = 0; drive.Live && tick < hz * 30; tick++)
        {
            input.Frame = new(Movement: tick < hz / 3 ? Vector2.UnitX : Vector2.Zero, ThrowTarget: tick >= hz ? 0 : null);
            drive.Update(1f / hz);
            aborted |= drive.Execution.Backfield.Phase == RetroQB.Gameplay.Controllers.BackfieldPhase.Aborted;
            var frame = drive.Execution.DefenseRuntime!.Current!;
            if (frame.Observation.Visible.Ball.State == BallState.HeldByQB)
            {
                Assert.False(frame.Observation.ConfirmedCarrier);
                Assert.DoesNotContain(frame.Decisions, i => i.Kind is DefensiveIntentKind.Pursuit or DefensiveIntentKind.Tackle);
            }
            thrown |= frame.Observation.Visible.Ball.State == BallState.InAir;
        }
        Assert.True(aborted); Assert.True(thrown); Assert.False(drive.Live); Assert.Single(drive.Match.History);
    }

    [Fact]
    public void StationaryProtectedPocketKeepsCoverageUntilObservedEscapeThenResolves()
    {
        var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.Get("scarlet-guard");
        var drive = new DefensiveDrive(input, 202, fixedCall: "pass.mesh", match: new(user, cpu, user.Id), engine: DefensiveEngineMode.ZoneDevelopment);
        drive.SelectCompiledDefenseForDevelopment("def.cover4"); drive.Snap();
        for (int i = 0; drive.Live && i < 1800; i++) drive.Update(1f / 60);
        // This seed leaves the four fixed rush entries held by protection. Elapsed time is not run evidence.
        Assert.True(drive.Live); Assert.Empty(drive.Match.History);
        Assert.False(drive.Execution.DefenseRuntime!.Current!.Observation.ConfirmedCarrier);
        Assert.Equal(4, drive.Execution.DefenseRuntime.Current.Decisions.Count(i => i.ActiveRusher));
        Assert.DoesNotContain(drive.Execution.DefenseRuntime.Current.Decisions, i => i.Kind == DefensiveIntentKind.Tackle);
        // Now supply a visible escape; only production contact may end the play.
        drive.Actors.Qb.Position = new(26, drive.Plays.LineOfScrimmage + 2);
        drive.Update(1f / 60);
        Assert.Equal(RunRead.QuarterbackRun, drive.Execution.DefenseRuntime.Current!.Run!.Read);
        for (int i = 0; drive.Live && i < 600; i++) drive.Update(1f / 60);
        Assert.False(drive.Live); Assert.Single(drive.Match.History);
    }
}
