using System.Numerics;
using System.Text.Json;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Gameplay.Defense;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class DefensiveRuntimeTests
{
    internal static (ResolvedDefensivePlan Plan, FormationResult Offense, List<Defender> Defense) Setup(string call = "def.cover1", string offense = "pass.mesh", float los = 60)
    {
        var play = PlayResolver.Resolve(new PlayManager().Catalog[offense]);
        var formation = new FormationFactory().CreateFormation(play, los);
        var plan = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(formation, los), DefensiveCallDefinition.Get(call));
        return (plan, formation, plan.Assignments.Select(a => new Defender(a.Alignment, a.PositionRole, a.ActorId)).ToList());
    }
    internal static DefensiveSighting Sight(FormationResult f, List<Defender> d) => DefensiveSighting.Capture(f.Qb, f.Ball, f.Receivers, d);

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void DM18_HiddenOffensiveMetadataCannotChangeDecisionsBeforeVisibleDivergence(int hz)
    {
        var (plan, offense, defenders) = Setup(); var a = new DefensiveRuntime(plan); var b = new DefensiveRuntime(plan);
        for (int tick = 0; tick < hz; tick++)
        {
            foreach (var r in offense.Receivers) { r.Position += new Vector2(0, 4f / hz); r.Velocity = new(0, 4); }
            var first = a.Decide(Sight(offense, defenders), 1f / hz);
            foreach (var r in offense.Receivers) { r.Route = RouteType.Wheel; r.Eligible = !r.Eligible; r.IsBlocking = !r.IsBlocking; r.RouteStart = Vector2.Zero; }
            var second = b.Decide(Sight(offense, defenders), 1f / hz);
            Assert.Equal(JsonSerializer.Serialize(first, DefenseRedesignMatrix.Json), JsonSerializer.Serialize(second, DefenseRedesignMatrix.Json));
        }
        offense.Receivers[0].Position += new Vector2(0, 5);
        Assert.NotEqual(a.Current!.Decisions, a.Decide(Sight(offense, defenders), 1f / hz)!.Decisions);
    }

    [Fact]
    public void OBS01_StationaryIsUnknownAndDoesNotInventARouteOrBlock()
    {
        var (plan, offense, defenders) = Setup(); var runtime = new DefensiveRuntime(plan);
        for (int i = 0; i < 60; i++) runtime.Decide(Sight(offense, defenders), 1f / 60);
        Assert.All(runtime.Current!.Observation.Receivers, r => { Assert.Equal(ReleaseDirection.Unknown, r.Release); Assert.False(r.Blocking); });
        Assert.Equal(5, runtime.Current.Decisions.Count(i => i.Primary != null));
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void OBS04_05_PhysicalEngagementThenSeparationRecognizesLateRelease(int hz)
    {
        var (plan, offense, defenders) = Setup(); var runtime = new DefensiveRuntime(plan);
        var receiver = offense.Receivers[0]; defenders[0].Position = receiver.Position + new Vector2(1, 0); defenders[0].IsBeingBlocked = true;
        for (int i = 0; i < hz / 5; i++) runtime.Decide(Sight(offense, defenders), 1f / hz);
        Assert.False(runtime.Current!.Observation.Receivers[0].Blocking);
        for (int i = 0; i < hz / 3; i++) runtime.Decide(Sight(offense, defenders), 1f / hz);
        Assert.True(runtime.Current!.Observation.Receivers[0].Blocking);
        receiver.Position += new Vector2(0, 4); receiver.Velocity = new(0, 6);
        for (int i = 0; i < hz / 3; i++) { receiver.Position += receiver.Velocity / hz; runtime.Decide(Sight(offense, defenders), 1f / hz); }
        Assert.False(runtime.Current!.Observation.Receivers[0].Blocking); Assert.True(runtime.Current.Observation.Receivers[0].LateRelease);
        Assert.Contains(runtime.Current.Decisions, d => d.Primary == new ReceiverId(receiver.Index) && !d.ActiveRusher);
    }

    [Fact]
    public void OBS06_07_FakeMetadataDoesNotConfirmRunAndFlightHasRecognitionDelay()
    {
        var (plan, offense, defenders) = Setup(); var runtime = new DefensiveRuntime(plan);
        var d = defenders.Single(d => d.Slot == DefenderSlot.CB1);
        offense.Ball.SetInAir(d.Position, Vector2.Zero, 20, 40, 0);
        var early = runtime.Decide(Sight(offense, defenders), .01f)!;
        Assert.DoesNotContain(early.Decisions, i => i.Kind == DefensiveIntentKind.Contest);
        for (int i = 0; i < 20; i++) runtime.Decide(Sight(offense, defenders), .01f);
        Assert.Contains(runtime.Current!.Decisions, i => i.Actor == d.Slot && i.Kind == DefensiveIntentKind.Contest);
        Assert.Contains(runtime.Current.Decisions, i => i.Actor == DefenderSlot.CB2 && i.Kind != DefensiveIntentKind.Contest);
        offense.Ball.SetHeld(offense.Qb, BallState.HeldByQB); offense.Qb.Position += new Vector2(1, 0);
        Assert.False(runtime.Decide(Sight(offense, defenders), .02f)!.Observation.ConfirmedCarrier);
        offense.Qb.Position = new(26, 61);
        Assert.True(runtime.Decide(Sight(offense, defenders), .02f)!.Observation.ConfirmedCarrier);
    }

    [Fact]
    public void MOVE05_HighBallAndRemoteFlightDoNotPullEveryoneOffTheirPrimary()
    {
        var (plan, offense, defenders) = Setup(); var runtime = new DefensiveRuntime(plan);
        var sight = Sight(offense, defenders);
        var high = new DefensiveSighting(sight.Quarterback, new(BallState.InAir, new(2, 110), new(1, 0), 3, null), sight.Receivers, sight.Defenders);
        for (int i = 0; i < 30; i++) runtime.Decide(high, .01f);
        Assert.DoesNotContain(runtime.Current!.Decisions, i => i.Kind == DefensiveIntentKind.Contest);
        Assert.Equal(5, runtime.Current.Decisions.Count(i => i.Primary != null));
    }

    [Fact]
    public void MAN02_03_UnreachableSafetyNeverJustifiesTrailCoverage()
    {
        var (plan, offense, defenders) = Setup(); var runtime = new DefensiveRuntime(plan);
        defenders.Single(d => d.Slot == DefenderSlot.FS).Position = new(2, 100);
        foreach (var r in offense.Receivers) { r.Position += new Vector2(0, 3); r.Velocity = new(0, 6); }
        for (int i = 0; i < 15; i++) runtime.Decide(Sight(offense, defenders), .02f);
        var corner = runtime.Current!.Decisions.Single(d => d.Actor == DefenderSlot.CB2);
        Assert.Contains("NoReachableHelp", corner.Reason);
        Assert.Equal(5, runtime.Current.Decisions.Count(i => i.Primary != null));
    }

    [Fact]
    public void MOVE02_04_BeatenCornerRetainsIdentityAndLegalRecoveryTarget()
    {
        var (plan, offense, defenders) = Setup(los: 109); var runtime = new DefensiveRuntime(plan);
        var receiver = offense.Receivers[0]; receiver.Position = new(receiver.Position.X, 117); receiver.Velocity = new(0, 10);
        var first = runtime.Decide(Sight(offense, defenders), .02f)!;
        Assert.All(first.Decisions, i => { Assert.InRange(i.Target.Y, 1.1f, 118.9f); Assert.InRange(i.Target.X, 1.1f, 52.2f); });
        for (int i = 0; i < 10; i++) { receiver.Position += new Vector2(0, .01f); runtime.Decide(Sight(offense, defenders), .01f); }
        Assert.Equal(new ReceiverId(0), runtime.Current!.Decisions.Single(i => i.Actor == DefenderSlot.CB1).Primary);
        Assert.DoesNotContain(runtime.Current.Decisions, i => i.Primary != null && i.ActiveRusher);
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void MotorBoundsSpeedTurnAndArrivalWithoutChangingIntent(int hz)
    {
        var self = new DefenderMotion(DefenderSlot.CB1, new(20, 70), new(8, 0), 8, 1.1f, 1, false);
        var intent = new DefensiveIntent(self.Id, DefensiveIntentKind.Leverage, new(20, 60), new(77), null, "MOVE-01", "test", false);
        Vector2 v = DefenderMotor.Velocity(self, intent, new[] { self }, 1, 1f / hz);
        Assert.InRange(v.Length(), 0, 8.0001f);
        Assert.InRange(MathF.Abs(MathF.Atan2(v.Y, v.X)), 0, DefensiveTuning.TurnRadiansPerSecond / hz + .0001f);
        var stopped = self with { Velocity = Vector2.Zero };
        Assert.Equal(Vector2.Zero, DefenderMotor.Velocity(stopped, intent with { Target = self.Position }, new[] { stopped }, 1, 1f / hz));
        Assert.Equal(new ReceiverId(77), intent.Primary);
    }

    [Fact]
    public void MAN02_ReachableMiddleHelpIsExplicitAndPrimariesKeepTheirIds()
    {
        var (plan, offense, defenders) = Setup(); var runtime = new DefensiveRuntime(plan);
        var r = offense.Receivers[0]; r.Position = new(26, 61);
        defenders.Single(d => d.Slot == DefenderSlot.FS).Position = new(26, 65);
        for (int tick = 0; tick < 15; tick++) { r.Velocity = new(0, 4); r.Position += r.Velocity * .02f; runtime.Decide(Sight(offense, defenders), .02f); }
        Assert.Equal(new ReceiverId(0), runtime.Current!.Decisions.Single(i => i.Actor == DefenderSlot.FS).Helping);
        Assert.Contains("ReachableHelp;", runtime.Current.Decisions.Single(i => i.Actor == DefenderSlot.CB1).Reason);
        Assert.Equal(new ReceiverId(0), runtime.Current.Decisions.Single(i => i.Actor == DefenderSlot.CB1).Primary);
    }

    [Fact]
    public void OBS08_OrdinaryChallengerMustWaitForCommitmentWindow()
    {
        var (plan, offense, defenders) = Setup(); var runtime = new DefensiveRuntime(plan);
        foreach (var d in defenders) d.Position = new(d.Position.X, 100);
        defenders.Single(d => d.Slot == DefenderSlot.FS).Position = new(26, 68);
        var first = offense.Receivers[0]; var second = offense.Receivers[1];
        first.Position = new(24, 61); second.Position = new(28, 62);
        for (int tick = 0; tick < 10; tick++)
        {
            first.Position += new Vector2(0, .08f); second.Position += new Vector2(0, .08f);
            runtime.Decide(Sight(offense, defenders), .02f);
        }
        var committed = runtime.Current!.Decisions.Single(i => i.Actor == DefenderSlot.FS).Helping;
        Assert.NotNull(committed);
        first.Position += new Vector2(0, .1f); second.Position += new Vector2(0, .08f);
        runtime.Decide(Sight(offense, defenders), .02f);
        Assert.Equal(committed, runtime.Current!.Decisions.Single(i => i.Actor == DefenderSlot.FS).Helping);
        for (int tick = 0; tick < 25; tick++)
        { first.Position += new Vector2(0, .14f); second.Position += new Vector2(0, .08f); runtime.Decide(Sight(offense, defenders), .02f); }
        Assert.Equal(new ReceiverId(0), runtime.Current!.Decisions.Single(i => i.Actor == DefenderSlot.FS).Helping);
    }

    [Fact]
    public void PRESS01_RolloutKeepsBothNamedContainEdgesAndSevenCoverageJobs()
    {
        var (plan, offense, defenders) = Setup(); var runtime = new DefensiveRuntime(plan);
        offense.Qb.Position = new(18, 56);
        var frame = runtime.Decide(Sight(offense, defenders), .02f)!;
        var near = frame.Decisions.Single(i => i.Actor == DefenderSlot.DE1); var far = frame.Decisions.Single(i => i.Actor == DefenderSlot.DE2);
        Assert.Equal(DefensiveIntentKind.Contain, near.Kind); Assert.Equal(DefensiveIntentKind.Contain, far.Kind);
        Assert.True(near.Target.X < offense.Qb.Position.X); Assert.True(far.Target.X > offense.Qb.Position.X);
        Assert.Equal(7, frame.Decisions.Count(i => !i.ActiveRusher));
    }

    [Fact]
    public void DEC00_SuspensionZeroDeltaAndWhistleFreezeState()
    {
        var (plan, offense, defenders) = Setup(); var runtime = new DefensiveRuntime(plan);
        var first = runtime.Decide(Sight(offense, defenders), .02f);
        Assert.Same(first, runtime.Decide(Sight(offense, defenders), 1, suspended: true));
        Assert.Same(first, runtime.Decide(Sight(offense, defenders), 0));
        runtime.Stop(); Assert.Same(first, runtime.Decide(Sight(offense, defenders), 1));
    }

    [Theory]
    [InlineData(30, true)] [InlineData(60, true)] [InlineData(120, true)]
    [InlineData(30, false)] [InlineData(60, false)] [InlineData(120, false)]
    public void LiveCover1CompletesThroughProductionControllersAndRecordsReplay(int hz, bool humanDefense)
    {
        var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.Get("scarlet-guard");
        var drive = new DefensiveDrive(input, 101, fixedCall: "pass.mesh", scriptedOffense: true,
            match: new(user, cpu, humanDefense ? cpu.Id : user.Id, start: new(50)), engine: DefensiveEngineMode.Cover1Development);
        Assert.True(drive.Snap());
        for (int tick = 0; drive.Live && tick < hz * 30; tick++)
        {
            input.Frame = new(ThrowTarget: !humanDefense && tick >= hz ? 0 : null);
            drive.Update(1f / hz);
        }
        Assert.False(drive.Live); Assert.NotNull(drive.LastResult); Assert.Single(drive.Match.History);
        Assert.NotNull(drive.LastReplay?.MatchContext?.CompiledPlan);
        Assert.Contains(drive.LastReplay!.Frames, f => f.DefensiveDecision != null);
        Assert.All(drive.LastReplay.Frames.Where(f => f.DefensiveDecision != null), f => Assert.Equal(11, f.DefensiveDecision!.Decisions.Count));
        Assert.True(drive.Execution.DefenseRuntime!.Stopped);
    }

    [Fact]
    public void PauseRestartSelectionAndStationaryHumanPreserveOwnership()
    {
        var input = new ScriptedInput(); var drive = new DefensiveDrive(input, 101, fixedCall: "pass.mesh", engine: DefensiveEngineMode.Cover1Development);
        var session = new DefensivePlaySession(drive, 101); var plan = drive.CompiledPlan;
        Assert.False(drive.SelectDefense("def.cover3")); Assert.Same(plan, drive.CompiledPlan); Assert.NotNull(drive.SelectionRejection);
        var before = drive.Linebacker.Position; drive.Snap(); drive.Update(.01f);
        Assert.Equal(before, drive.Linebacker.Position); Assert.Equal(Vector2.Zero, drive.Linebacker.Velocity);
        var state = drive.Execution.DefenseRuntime!.Current;
        session.Update(1, new(Pause: true)); Assert.Same(state, drive.Execution.DefenseRuntime.Current);
        session.Update(0, new(Pause: true)); session.Update(1, new(Focused: false)); Assert.Same(state, drive.Execution.DefenseRuntime.Current);
        drive.Restart(); Assert.Null(drive.Execution.DefenseRuntime!.Current); Assert.False(drive.Execution.DefenseRuntime.Stopped);
        Assert.Equal(plan!.Assignments, drive.CompiledPlan!.Assignments);
    }
}
