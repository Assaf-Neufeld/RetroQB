using System.Numerics;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Tests;

public sealed class ManDefensiveRuntimeTests
{
    private static (ResolvedDefensivePlan Plan, FormationResult Offense, List<Defender> Defenders) Pair(bool three = false)
    {
        var (_, f, _) = DefensiveRuntimeTests.Setup();
        f.Receivers[1].Position = new(39, 59); f.Receivers[2].Position = new(41, 59);
        if (three) f.Receivers[3].Position = new(43, 59);
        var plan = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(f, 60), DefensiveCallDefinition.Get("def.cover1"),
            adjustments: new(switchPairs: true));
        return (plan, f, plan.Assignments.Select(a => new Defender(a.Alignment, a.PositionRole, a.ActorId)).ToList());
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void DM03_PairSwitchCommitsBijectionOnceAndRecrossCannotSwapAgain(int hz)
    {
        var (plan, offense, defenders) = Pair(); var group = Assert.Single(plan.SwitchGroups); var runtime = new DefensiveRuntime(plan);
        var original = plan.Assignments.Single(a => a.ActorId == group.OutsideDefender).Pass as ManResponsibility;
        for (int tick = 0; tick < hz; tick++)
        {
            float direction = tick < hz / 3 ? 1 : -1;
            offense.Receivers[1].Velocity = new(6 * direction, 0); offense.Receivers[2].Velocity = new(-6 * direction, 0);
            offense.Receivers[1].Position += offense.Receivers[1].Velocity / hz; offense.Receivers[2].Position += offense.Receivers[2].Velocity / hz;
            runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / hz);
            Assert.Equal(5, runtime.Current!.Decisions.Where(i => i.Primary != null).Select(i => i.Primary).Distinct().Count());
        }
        Assert.Equal(1, runtime.SwitchCommitCount);
        Assert.Equal(new ReceiverId(1), runtime.Current!.Decisions.Single(i => i.Actor == group.OutsideDefender).Primary);
        Assert.NotEqual(original!.Target, runtime.Current.Decisions.Single(i => i.Actor == group.OutsideDefender).Primary);
        Assert.Equal(plan.Assignments.Count(a => a.Pass is HelpResponsibility), runtime.Current.Decisions.Count(i => i.Primary == null && !i.ActiveRusher));
    }

    [Fact]
    public void MAN07_AmbiguousPairTimesOutAndThreePlayerBunchKeepsExplicitLocks()
    {
        var (plan, offense, defenders) = Pair(); var runtime = new DefensiveRuntime(plan); var events = new List<string>();
        for (int tick = 0; tick < 40; tick++)
        {
            foreach (var r in offense.Receivers.Skip(1).Take(2)) { r.Velocity = new(0, 6); r.Position += r.Velocity * .02f; }
            events.AddRange(runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .02f)!.Events);
        }
        Assert.Equal(0, runtime.SwitchCommitCount); Assert.Contains(events, e => e.Contains("ProvisionalLocksRetained"));
        foreach (var a in plan.Assignments.Where(a => a.Pass is ManResponsibility))
            Assert.Equal(((ManResponsibility)a.Pass).Target, runtime.Current!.Decisions.Single(i => i.Actor == a.ActorId).Primary);
        var triple = Pair(three: true).Plan; Assert.Empty(triple.SwitchGroups); Assert.Equal("LockActorIds", triple.BunchCheck);
    }

    [Fact]
    public void MAN09_ShadowIsSolvedAsConstraintAndBracketConsumesExistingLowHelper()
    {
        var (_, offense, _) = DefensiveRuntimeTests.Setup(); var snapshot = OffensiveAlignmentSnapshot.From(offense, 60);
        var adjustments = new ManCoverageAdjustments(switchPairs: true,
            shadows: [new(DefenderSlot.CB1, new(3))], bracket: new(DefenderSlot.SS, new(3)));
        var plan = DefensivePlanCompiler.Compile(snapshot, DefensiveCallDefinition.Get("def.cover1"), adjustments: adjustments);
        Assert.Equal(new ManResponsibility(new(3)), plan.Assignments.Single(a => a.ActorId == DefenderSlot.CB1).Pass);
        Assert.Equal(new HelpResponsibility(HelperRole.LowMiddle, new(3)), plan.Assignments.Single(a => a.ActorId == DefenderSlot.SS).Pass);
        Assert.Equal(5, plan.Assignments.Count(a => a.Pass is ManResponsibility)); Assert.Empty(DefensivePlanValidator.Validate(plan));
        Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(snapshot, plan.Call, adjustments: new(shadows: [new(DefenderSlot.FS, new(3))])));
        Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(snapshot, DefensiveCallDefinition.Get("def.cover2-man"), adjustments: new(bracket: new(DefenderSlot.SS, new(3)))));
        Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(snapshot, plan.Call, adjustments: new(shadows: [new(DefenderSlot.CB1, new(0)), new(DefenderSlot.CB2, new(0))])));
        var cache = new DefensivePlanCache(); var plain = cache.Resolve(snapshot, plan.Call);
        Assert.NotSame(plain, cache.Resolve(snapshot, plan.Call, adjustments: adjustments));
    }

    [Fact]
    public void MAN04_TwoHalvesNeverPromiseTheSameHelpOrStealPrimaries()
    {
        var (plan, offense, defenders) = DefensiveRuntimeTests.Setup("def.cover2-man"); var runtime = new DefensiveRuntime(plan);
        for (int tick = 0; tick < 90; tick++)
        {
            foreach (var r in offense.Receivers) { r.Velocity = new(0, 6); r.Position += r.Velocity / 60; }
            runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / 60);
            var help = runtime.Current!.Decisions.Where(i => i.Helping != null).Select(i => i.Helping).ToArray();
            Assert.Equal(help.Length, help.Distinct().Count());
            Assert.All(runtime.Current.Decisions.Where(i => i.Actor is DefenderSlot.FS or DefenderSlot.SS), i => Assert.Null(i.Primary));
            Assert.Equal(5, runtime.Current.Decisions.Count(i => i.Primary != null));
        }
    }

    [Fact]
    public void MAN05_RobberJumpRemainsInLowMiddleAndReturnsWhenCrosserLeaves()
    {
        var (plan, offense, defenders) = DefensiveRuntimeTests.Setup("def.robber"); var runtime = new DefensiveRuntime(plan);
        var r = offense.Receivers[0]; r.Position = new(26, 64); r.Velocity = new(3, 0);
        for (int tick = 0; tick < 20; tick++) { r.Position += r.Velocity * .01f; runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .01f); }
        var robber = runtime.Current!.Decisions.Single(i => i.Actor == DefenderSlot.SS);
        Assert.Equal(new ReceiverId(0), robber.Helping); Assert.InRange(robber.Target.X, 18.65f, 34.65f); Assert.InRange(robber.Target.Y, 62, 69);
        r.Position = new(49, 64);
        runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .02f);
        Assert.Null(runtime.Current!.Decisions.Single(i => i.Actor == DefenderSlot.SS).Helping);
        Assert.Equal(plan.Assignments.Single(a => a.ActorId == DefenderSlot.SS).Alignment, runtime.Current.Decisions.Single(i => i.Actor == DefenderSlot.SS).Target);
    }

    [Theory]
    [InlineData("def.cover1")] [InlineData("def.cover2-man")] [InlineData("def.robber")] [InlineData("def.zero")] [InlineData("def.edge")]
    public void DM01_DM02_ManFamilyRetainsOwnershipThroughProductionMeshAndTwins(string call)
    {
        foreach (string play in new[] { "pass.mesh", "pass.pistol-twins.scissors" })
        {
            var result = DefenseRuntimeMatrix.Exercise(play, call, 101, 60, true, DefenseInputMode.FullAI, DefensiveEngineMode.ManDevelopment);
            Assert.Null(result.Failure); Assert.NotNull(result.Outcome);
            foreach (var frame in result.Decisions)
            {
                Assert.Equal(frame.Observation.ConfirmedCarrier ? 0 : 5, frame.Decisions.Where(i => i.Primary != null).Select(i => i.Primary).Distinct().Count());
                if (call == "def.zero") Assert.DoesNotContain(frame.Decisions, i => i.Helping != null);
            }
        }
    }

    [Fact]
    public void MAN09_DeclaredBracketCanHelpWhileMiddleSafetyCapsTheSamePrimary()
    {
        var (_, offense, _) = DefensiveRuntimeTests.Setup();
        var plan = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(offense, 60), DefensiveCallDefinition.Get("def.cover1"),
            adjustments: new(bracket: new(DefenderSlot.SS, new(0))));
        var defenders = plan.Assignments.Select(a => new Defender(a.Alignment, a.PositionRole, a.ActorId)).ToList();
        var runtime = new DefensiveRuntime(plan); var receiver = offense.Receivers[0]; receiver.Position = new(26, 63);
        for (int tick = 0; tick < 12; tick++) { receiver.Position += new Vector2(0, .06f); runtime.Decide(DefensiveRuntimeTests.Sight(offense, defenders), .02f); }
        var helper = runtime.Current!.Decisions.Single(i => i.Actor == DefenderSlot.SS);
        Assert.Equal(new ReceiverId(0), helper.Helping); Assert.Equal("MAN-09", helper.Rule);
        Assert.Equal(5, runtime.Current.Decisions.Count(i => i.Primary != null));
    }

    [Theory]
    [InlineData(30f)] [InlineData(109f)]
    public void AllCatalogFlipsAndPackagesValidateWithExplicitPairSwitches(float los)
    {
        foreach (var definition in new PlayManager().Catalog.Plays)
        foreach (bool flipped in new[] { false, true })
        foreach (var call in DefensiveCallDefinition.Supported)
        foreach (var package in Enum.GetValues<DefensePackage>())
        {
            var f = new FormationFactory().CreateFormation(PlayResolver.Resolve(definition, flipped), los);
            var plan = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(f, los), call, package, adjustments: new(switchPairs: true));
            Assert.Empty(DefensivePlanValidator.Validate(plan));
            Assert.Equal(plan.SwitchGroups.Count * 2, plan.SwitchGroups.SelectMany(g => new[] { g.OutsideDefender, g.InsideDefender }).Distinct().Count());
        }
    }

    [Theory]
    [InlineData("def.cover1")] [InlineData("def.cover2-man")] [InlineData("def.robber")] [InlineData("def.zero")]
    public void FlippedGoalLineManPlayRetainsLegalOwnership(string call)
    {
        var result = DefenseRuntimeMatrix.Exercise("pass.pistol-twins.scissors", call, 202, 60, true,
            DefenseInputMode.FullAI, DefensiveEngineMode.ManDevelopment, yard: 98, flipped: true);
        Assert.Null(result.Failure); Assert.NotNull(result.Outcome);
        Assert.All(result.Decisions.SelectMany(f => f.Decisions), d => Assert.InRange(d.Target.Y, 1.1f, 118.9f));
    }

    [Fact]
    public void NewMotorVelocityReachesContactBeforeIntegration()
    {
        var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: "pass.mesh", engine: DefensiveEngineMode.Cover1Development);
        drive.Execution.Control = new(true) { AutomateDefense = true }; drive.Snap(); bool slowed = false;
        for (int tick = 0; drive.Live && tick < 120; tick++)
        {
            drive.Update(1f / 60);
            var frame = drive.Execution.DefenseRuntime!.Current!;
            foreach (var d in drive.Actors.Defenders.Where(d => d.IsBeingBlocked))
            {
                var planned = DefenderMotor.Velocity(frame.Observation.Visible.Defenders.Single(s => s.Id == d.Slot),
                    frame.Decisions.Single(i => i.Actor == d.Slot), frame.Observation.Visible.Defenders, drive.Plays.DefenderSpeedMultiplier, 1f / 60);
                if (planned.Length() > .1f && d.Velocity.Length() < planned.Length() - .01f) slowed = true;
            }
        }
        Assert.True(slowed, "Production blocking must slow an already planned velocity before the single integration.");
    }
}
