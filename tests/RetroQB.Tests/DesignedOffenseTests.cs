using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Gameplay.Defense;
using RetroQB.Routes;
using RetroQB.Stats;

namespace RetroQB.Tests;

public sealed class DesignedOffenseTests
{
    [Fact]
    public void ProductionFixturesSelectActualEngineCallsForEitherHumanControlRole()
    {
        foreach (bool defense in new[] { false, true })
        foreach (string id in new[] { "run.power-right", "pass.gun-trips.wr-screen", "pass.ace.bootleg" })
        {
            var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
            var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: id,
                match: new(user, cpu, defense ? cpu.Id : user.Id), engine: DefensiveEngineMode.Production);
            foreach (var call in DefensivePlaybook.ForEngine(DefensiveEngineMode.Production))
                Assert.True(defense ? drive.SelectDefense(call.Id) : drive.SelectCompiledDefenseForDevelopment(call.Id), call.Id);
            Assert.Equal(defense, drive.Execution.Control.HumanOnDefense);
            if (defense) Assert.NotNull(drive.Execution.Control.ControlledDefender(drive.Actors.Defenders));
            else Assert.Null(drive.Execution.Control.ControlledDefender(drive.Actors.Defenders));
            Assert.True(drive.Snap()); drive.Update(1f / 60);
        }
    }
    [Fact]
    public void RunTracksAndBlockingJobsAreDistinctAndPersonnelAware()
    {
        var catalog = PlaybookBuilder.BuildCatalog();
        foreach (string id in new[] { "gun-doubles", "pistol-twins", "i-pro" })
        {
            var power = PlayResolver.Resolve(catalog[$"run.{id}.power"]);
            var counter = PlayResolver.Resolve(catalog[$"run.{id}.counter"]);
            Assert.Contains(power.LineBlocking, j => j.Job == BlockingJob.Pull);
            Assert.Contains(power.Assignments.Values, a => a.Blocking?.Job == BlockingJob.KickOut);
            Assert.True(counter.LineBlocking.Any(j => j.Job == BlockingJob.KickOut)
                || counter.Assignments.Values.Any(a => a.Blocking?.Job == BlockingJob.KickOut));
            Assert.True(counter.RunPlan!.Path[0].X * counter.RunningBackSide < 0);
            Assert.True(counter.RunPlan.Path[2].X * counter.RunningBackSide > 0);
            Assert.NotEqual(power.RunPlan!.Path, counter.RunPlan.Path);
        }
        var stretch = PlayResolver.Resolve(catalog["run.gun-doubles.stretch"]);
        Assert.All(stretch.LineBlocking, j => Assert.NotNull(j.ApproachOffset));
        Assert.Contains(stretch.LineBlocking, j => j.IsBackside);
        Assert.All(catalog.Plays.Where(p => p.Family == PlayType.Run), p => Assert.NotNull(p.RunPlan));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void CombinationUsesIdentityAndNeverReleasesBothPartnersOrAbandonsPenetration(bool shuffled)
    {
        var (manager, field) = Field("run.gun-doubles.power");
        var play = manager.SelectedPlay; var pair = play.RunPlan!.Combinations.Single();
        var anchor = field.Blockers.Single(b => b.LineSlot == pair.AnchorSlot);
        var release = field.Blockers.Single(b => b.LineSlot == pair.ReleaseSlot);
        var first = new Defender(anchor.Position + new Vector2(0, 2), DefensivePosition.DL, DefenderSlot.DT1);
        var penetration = new Defender(release.Position + new Vector2(0, -1), DefensivePosition.LB, DefenderSlot.MLB);
        var blockers = shuffled ? field.Blockers.AsEnumerable().Reverse().ToArray() : field.Blockers.ToArray();
        var held = CombinationBlocking.Decide(blockers, [first, penetration], play, 60, .6f);
        Assert.False(release.CombinationReleased);
        Assert.Same(first, held[anchor].Target); Assert.Same(first, held[release].Target);
        var clear = CombinationBlocking.Decide(blockers, [first], play, 60, .01f);
        Assert.True(release.CombinationReleased); Assert.False(anchor.CombinationReleased);
        Assert.Same(first, clear[anchor].Target);
        Assert.Equal(BlockingJob.Lead, clear[release].Assignment.Job);
        Assert.Null(clear[release].Target);
        first.Position = anchor.Position + new Vector2(0, -2);
        clear = CombinationBlocking.Decide(blockers, [first], play, 60, .01f);
        Assert.Same(first, clear[anchor].Target);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void CounterRunnerPressesMisdirectionThenCutsForVisiblePenetration(int hz)
    {
        var play = PlayResolver.Resolve(PlaybookBuilder.BuildCatalog()["run.gun-doubles.counter"]);
        var ai = new BallCarrierAI(); var path = play.RunPlan!.At(60);
        var position = new Vector2(Constants.FieldWidth / 2, 57);
        var first = ai.DecideRun(position, [], [], path, 1f / hz);
        Assert.True(first.X * play.RunningBackSide < 0);
        var cut = ai.DecideRun(position, [position + first * 2], [], path, 1f / hz);
        Assert.NotEqual(first, cut);
        ai.Reset(); Assert.Equal(first, ai.DecideRun(position, [], [], path, 1f / hz));
    }

    [Theory] [InlineData(false, 30)] [InlineData(true, 30)]
    [InlineData(false, 60)] [InlineData(true, 60)] [InlineData(false, 120)] [InlineData(true, 120)]
    public void ScreensReleaseRealBlockersCatchBehindLineAndEscortWithoutStealingCarrierControl(bool flipped, int hz)
    {
        foreach (string id in new[] { "pass.gun-doubles.rb-screen", "pass.split-backs.rb-screen", "pass.gun-trips.wr-screen", "pass.gun-doubles.wr-screen" })
        {
            var (manager, field) = Field(id, flipped);
            var input = new ScriptedInput(); var execution = new PlayExecutionController(input, new BlockingController());
            var priorities = new ReceiverPriorityManager(); priorities.AssignPriorities(field.Receivers);
            var balls = new BallController(new CatchRandom(), new ThrowingMechanics(), new StatisticsTracker(), priorities);
            balls.Reset(60);
            var screen = manager.SelectedPlay.Screen!;
            var target = field.Receivers.Single(r => r.Slot == screen.Target);
            var before = field.Blockers.ToDictionary(b => b.LineSlot, b => b.Position);
            for (int frame = 0; frame < hz * 3; frame++) Tick();
            Assert.Equal(ScreenPhase.Releasing, execution.Design.ScreenPhase);
            Assert.True(target.Position.Y < 60);
            Assert.Equal(RoutePhase.Settled, target.RouteState.Phase);
            foreach (int slot in screen.LineEscorts.Keys)
                Assert.True(Vector2.Distance(before[slot], field.Blockers.Single(b => b.LineSlot == slot).Position) > 3);
            Assert.True(balls.TryThrow(target.Index, field.Ball, field.Qb, field.Receivers, [], manager,
                field.Qb.TeamAttributes, true));
            for (int frame = 0; frame < hz * 3 && field.Ball.State == BallState.InAir; frame++)
            {
                Tick(); balls.Update(field.Ball, field.Qb, field.Receivers, [], field.Qb.TeamAttributes,
                    DefensiveTeamAttributes.Default, target.Index, 1f / hz);
            }
            Assert.Same(target, field.Ball.Holder);
            input.Frame = new(Movement: Vector2.UnitX);
            var carrierBefore = target.Position; Tick();
            Assert.Equal(ScreenPhase.Escorting, execution.Design.ScreenPhase);
            Assert.True(target.Position.X > carrierBefore.X); Assert.Equal(carrierBefore.Y, target.Position.Y);
            foreach (int slot in screen.LineEscorts.Keys)
                Assert.Same(screen.LineEscorts[slot], field.Blockers.Single(b => b.LineSlot == slot).BlockingState.Assignment);
            execution.Design.Stop(); Assert.Equal(ScreenPhase.Exited, execution.Design.ScreenPhase);
            void Tick() => execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], field.Blockers, manager,
                false, false, false, Clamp, 1f / hz);
        }
    }

    [Fact]
    public void CpuCanReadBehindLineScreenAndHotOutletButWaitsForItsGate()
    {
        var read = new ReceiverRead(4, new(36, 57), Vector2.Zero, true, false, -10, false, true);
        var view = new QuarterbackObservation(new(26, 55), 60, true, [read], [new(22, 55)]);
        var ai = new QuarterbackAI();
        Assert.Null(ai.Decide(view, .3f).ReceiverIndex);
        Assert.Equal(4, ai.Decide(view with { Reads = [read with { Developed = true }] }, .3f).ReceiverIndex);
    }

    [Theory] [InlineData(false, 30)] [InlineData(true, 30)]
    [InlineData(false, 60)] [InlineData(true, 60)] [InlineData(false, 120)] [InlineData(true, 120)]
    public void MovingPocketsWaitForFakeMirrorAndYieldToMovementPressureThrowAndTimeout(bool flipped, int hz)
    {
        foreach (string id in new[] { "pass.pistol-twins.bootleg", "pass.ace.bootleg", "pass.i-pro.bootleg", "pass.gun-doubles.rollout" })
        {
            var (manager, field) = Field(id, flipped);
            var input = new ScriptedInput(); var execution = new PlayExecutionController(input, new BlockingController());
            var start = field.Qb.Position;
            for (int frame = 0; frame < hz * 4; frame++) Tick();
            Assert.True((field.Qb.Position.X - start.X) * (flipped ? -1 : 1) > 7, id);
            Assert.True(field.Qb.Position.Y < 60);
            Assert.Same(field.Qb, field.Ball.Holder);
            if (manager.SelectedPlay.Backfield.Action == BackfieldAction.PlayAction)
                Assert.Equal(BackfieldPhase.Released, execution.Backfield.Phase);
            input.Frame = new(Movement: new(flipped ? 1 : -1, 0)); Tick();
            Assert.Equal(PocketPhase.Exited, execution.Design.PocketPhase);
            input.Frame = new(); Tick(); Assert.Equal(PocketPhase.Exited, execution.Design.PocketPhase);
            var state = new DesignedOffenseController();
            state.Update(manager.SelectedPlay, field.Ball, field.Qb, execution.Backfield, false, true, .01f, 60);
            Assert.Equal(PocketPhase.Exited, state.PocketPhase);
            state.Reset(); field.Ball.SetInAir(field.Qb.Position, Vector2.UnitY * 10, 10, 20, 0);
            state.Update(manager.SelectedPlay, field.Ball, field.Qb, execution.Backfield, false, false, .01f, 60);
            Assert.Equal(PocketPhase.Exited, state.PocketPhase);
            void Tick() => execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], field.Blockers, manager,
                false, false, false, Clamp, 1f / hz);
        }
        var (blockedManager, blocked) = Field("pass.ace.bootleg");
        var backfield = new BackfieldController(); var design = new DesignedOffenseController();
        blocked.Receivers.Single(r => r.Slot == ReceiverSlot.RB1).Position = new(1, 20);
        backfield.Update(blockedManager.SelectedPlay, blocked.Ball, blocked.Qb, blocked.Receivers, 3);
        design.Update(blockedManager.SelectedPlay, blocked.Ball, blocked.Qb, backfield, false, false, .01f, 60);
        Assert.Equal(BackfieldPhase.Aborted, backfield.Phase); Assert.Equal(PocketPhase.Exited, design.PocketPhase);
        Assert.True(backfield.AllowsThrow);
    }

    [Fact]
    public void ScreenExitsOnOtherCatchTurnoverScrambleAndDeadBallAndResetsForANewSnap()
    {
        var (manager, field) = Field("pass.gun-doubles.rb-screen");
        var backfield = new BackfieldController(); var design = new DesignedOffenseController();
        void Update() => design.Update(manager.SelectedPlay, field.Ball, field.Qb, backfield, false, false, 1, 60);
        Update(); Assert.Equal(ScreenPhase.Releasing, design.ScreenPhase);
        field.Ball.SetHeld(field.Receivers.Single(r => r.Slot == ReceiverSlot.WR1), BallState.HeldByReceiver);
        Update(); Assert.Equal(ScreenPhase.Exited, design.ScreenPhase);
        design.Reset(); field.Ball.SetDead(field.Qb.Position); Update(); Assert.Equal(ScreenPhase.Exited, design.ScreenPhase);
        design.Reset(); field.Ball.SetHeld(field.Qb, BallState.HeldByQB); field.Qb.Position = new(26, 61);
        Update(); Assert.Equal(ScreenPhase.Exited, design.ScreenPhase);
        design.Reset(); field.Qb.Position = new(26, 55);
        design.Update(manager.SelectedPlay, new Ball(field.Qb.Position), field.Qb, backfield, false, false, .01f, 60);
        Assert.Equal(ScreenPhase.Protecting, design.ScreenPhase);
    }

    [Fact]
    public void DesignedGeometryMatchesPreviewAndDoubleFlipRestoresEveryLandmark()
    {
        foreach (var source in PlaybookBuilder.BuildCatalog().Plays)
        {
            var p = PlayResolver.Resolve(source); var twice = p.Flip().Flip();
            Assert.Equal(p.RunPlan?.Path, twice.RunPlan?.Path);
            Assert.Equal(p.Screen?.CatchOffset, twice.Screen?.CatchOffset);
            Assert.Equal(p.QuarterbackPath?.Points, twice.QuarterbackPath?.Points);
            if (p.RunPlan == null) continue;
            var field = new FormationFactory().CreateFormation(p, 60); RouteAssigner.AssignRoutes(field.Receivers, p);
            var carrier = field.Receivers.Single(r => r.Slot == p.BallCarrierSlot);
            Assert.Equal(p.RunPlan.At(60), RouteVisualizer.GetRouteWaypoints(carrier, p, 60).Skip(2));
        }
        Assert.Throws<ArgumentException>(() => new RunPlan([new(float.NaN, 0)]));
        Assert.Throws<ArgumentException>(() => new RunPlan([Vector2.Zero], [new(0, 1, Vector2.Zero), new(1, 2, Vector2.Zero)]));
        Assert.Throws<ArgumentException>(() => new QuarterbackPath([new(1, 1)]));
    }

    private static (PlayManager, FormationResult) Field(string id, bool flipped = false)
    {
        var manager = new PlayManager(); manager.StartNewDrive(new(50)); manager.SelectCatalogPlay(id, new Random(1));
        if (flipped) manager.FlipSelectedPlay();
        var field = new FormationFactory().CreateFormation(manager.SelectedPlay, 60);
        RouteAssigner.AssignRoutes(field.Receivers, manager.SelectedPlay); return (manager, field);
    }
    private static void Clamp(Entity e) => e.Position = new(Math.Clamp(e.Position.X, e.Radius, Constants.FieldWidth - e.Radius),
        Math.Clamp(e.Position.Y, e.Radius, Constants.FieldLength - e.Radius));
    private sealed class CatchRandom : Random { public override double NextDouble() => .99; }
}
