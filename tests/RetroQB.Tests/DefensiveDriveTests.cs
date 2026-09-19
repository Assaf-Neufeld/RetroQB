using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;

namespace RetroQB.Tests;

public sealed class DefensiveDriveTests
{
    [Fact]
    public void CONTROL03_BlockContactSlowsManualDefenderBeforeIntegration()
    {
        var input = new ScriptedInput { Frame = new(Movement: -Vector2.UnitY) };
        var drive = new DefensiveDrive(input, 101, fixedCall: "run.hb-dive");
        var manual = drive.Linebacker;
        manual.Position = drive.Actors.Blockers[2].Position + new Vector2(0, 1.5f);
        var actors = drive.Actors;
        drive.Execution.CpuIntent = new(Vector2.Zero);
        drive.Execution.UpdatePlay(actors.Qb, actors.Ball, actors.Receivers, actors.Defenders, actors.Blockers,
            drive.Plays, false, actors.UsesZoneResponsibilities, actors.IsUnderneathManCoverage, _ => { }, 1f / 60);
        Assert.True(manual.IsBeingBlocked);
        Assert.True(manual.Velocity.Length() < manual.Speed * drive.Plays.DefenderSpeedMultiplier);
    }

    [Fact]
    public void CONTROL02_PossessionNeverChangesControlledDefenderAndContactUsesSharedTackling()
    {
        var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: "pass.mesh");
        var linebacker = drive.Linebacker;
        var receiver = drive.Actors.Receivers[0];
        drive.Actors.Ball.SetInAir(new(25, 40), Vector2.UnitY * 10, 10, 15, 0);
        Assert.Same(linebacker, drive.Linebacker);
        drive.Actors.Ball.SetHeld(receiver, BallState.HeldByReceiver);
        Assert.Same(linebacker, drive.Linebacker);
        linebacker.Position = receiver.Position = new(25, 50);
        var tackle = new TackleController(new Random(101), new OverlapResolver());
        Assert.Equal(TackleCheckResult.Tackle, tackle.CheckTackleOrScore(drive.Actors.Ball, drive.Actors.Qb,
            [linebacker], drive.Match.Opponent.OffensiveAttributes, _ => { }));
        Assert.Equal(linebacker.Slot, tackle.LastTerminal!.Defender);
        Assert.Same(linebacker, drive.Linebacker);
    }

    [Fact]
    public void CPU03_SharedThrowCommandRejectsFakeBlockingTargetAndCrossedLine()
    {
        var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: "pass.mesh");
        var priority = new ReceiverPriorityManager(); priority.AssignPriorities(drive.Actors.Receivers);
        var ball = new BallController(new Random(101), new ThrowingMechanics(), new RetroQB.Stats.StatisticsTracker(), priority);
        var a = drive.Actors; int target = priority.GetFirstReceiverIndex();
        bool Throw(bool ready) => ball.TryThrow(target, a.Ball, a.Qb, a.Receivers, a.Defenders,
            drive.Plays, drive.Match.Opponent.OffensiveAttributes, ready);
        Assert.False(Throw(false));
        a.Receivers[target].IsBlocking = true; Assert.False(Throw(true));
        a.Receivers[target].IsBlocking = false;
        a.Qb.Position.Y = drive.Plays.LineOfScrimmage + 1; Assert.False(Throw(true));
        Assert.False(ball.PassAttemptedThisPlay);
        a.Qb.Position = new(3, drive.Plays.LineOfScrimmage - 3);
        Assert.True(ball.TryThrowAway(a.Ball, a.Qb, drive.Plays, drive.Match.Opponent.OffensiveAttributes, true));
        Assert.Equal(BallState.InAir, a.Ball.State);
        Assert.Null(ball.ThrowTargetSlot);
        for (int i = 0; i < 240 && ball.LastTerminal == null; i++)
            ball.Update(a.Ball, a.Qb, [], [], drive.Match.Opponent.OffensiveAttributes, drive.Match.User.DefensiveAttributes, 0, 1f / 60);
        Assert.Equal(PlayEndReason.Incomplete, ball.LastTerminal!.Reason);
    }
    [Theory]
    [InlineData("run.hb-dive", DefenderSlot.MLB)]
    [InlineData("pass.mesh", DefenderSlot.OLB1)]
    public void CONTROL01_OnlyFixedLinebackerTakesHumanMovement(string call, DefenderSlot slot)
    {
        var input = new ScriptedInput { Frame = new(Movement: Vector2.UnitX) };
        var drive = new DefensiveDrive(input, 101, fixedCall: call);
        Assert.Equal(slot, drive.Linebacker.Slot);
        // Isolate integration from contact; all other defenders still run their assignments.
        drive.Linebacker.Position = new(8, 60);
        Vector2 start = drive.Linebacker.Position;
        var others = drive.Actors.Defenders.Where(d => d != drive.Linebacker).Select(d => d.Position).ToArray();
        drive.Snap(); drive.Update(1f / 60);
        Assert.Equal(start.X + drive.Linebacker.Speed * drive.Plays.DefenderSpeedMultiplier / 60, drive.Linebacker.Position.X, 4);
        Assert.Equal(start.Y, drive.Linebacker.Position.Y);
        Assert.Contains(drive.Actors.Defenders.Where(d => d != drive.Linebacker).Select((d, i) => d.Position != others[i]), moved => moved);
        input.Frame = new(); start = drive.Linebacker.Position;
        drive.Update(1f / 60);
        Assert.Equal(start, drive.Linebacker.Position);
    }

    [Theory]
    [InlineData("run.hb-dive")]
    [InlineData("pass.mesh")]
    [InlineData("pass.four-verts")]
    [InlineData("pass.slant-flat")]
    [InlineData("pass.gun-doubles.pa-cross")]
    public void CPU05_OpenFieldUsesRealExchangeOrCatchAndScoresWithoutHumanOffensiveInput(string call)
    {
        var input = new ScriptedInput();
        var drive = new DefensiveDrive(input, 101, fixedCall: call);
        drive.Actors.Defenders.RemoveAll(d => d != drive.Linebacker);
        drive.Linebacker.Position = new(1, 12);
        drive.Snap(); bool carrier = false, flight = false;
        for (int i = 0; i < 1800 && drive.Live; i++)
        {
            drive.Update(1f / 60);
            carrier |= drive.Actors.Ball.State == BallState.HeldByReceiver;
            flight |= drive.Actors.Ball.State == BallState.InAir;
        }
        Assert.True(!drive.Live, $"{call}: ball={drive.Actors.Ball.State}, qb={drive.Actors.Qb.Position}, holder={drive.Actors.Ball.Holder?.Position}, velocity={drive.Actors.Ball.Holder?.Velocity}, intent={drive.Execution.CpuIntent}, exchange={drive.Execution.Backfield.Phase}, noProgress={drive.SecondsWithoutProgress}, blockers={string.Join(';', drive.Actors.Blockers.Select(b => b.Position))}");
        Assert.Equal(PlayEndReason.Touchdown, drive.LastResult!.Event.Reason);
        Assert.True(carrier);
        Assert.Equal(call.StartsWith("pass."), flight);
        Assert.Equal(0, drive.Match.User.Stats.Qb.Attempts);
        Assert.Equal(7, drive.Match.Opponent.Score);
        Assert.Equal(Vector2.Zero, drive.Linebacker.Velocity);
    }

    [Theory]
    [InlineData(101, 30)] [InlineData(101, 60)] [InlineData(101, 120)]
    [InlineData(42, 60)] [InlineData(77, 60)] [InlineData(2026, 60)]
    public void DRIVE01_SeededDriveKeepsSeriesAndTerminatesWithoutForcedWhistles(int seed, int hz)
    {
        var input = new ScriptedInput(); var drive = new DefensiveDrive(input, seed);
        for (int play = 0; play < 40 && !drive.Complete; play++)
        {
            var expected = drive.Match.Series;
            if (drive.LastResult != null) Assert.True(drive.Continue());
            Assert.Equal(expected.OwnYardLine + 10, drive.Plays.LineOfScrimmage);
            Assert.True(drive.Snap());
            for (int tick = 0; tick < 30 * hz && drive.Live; tick++)
            {
                var delta = (drive.Actors.Ball.Holder?.Position ?? drive.Actors.Ball.Position) - drive.Linebacker.Position;
                input.Frame = new(Movement: delta.LengthSquared() > .01f ? Vector2.Normalize(delta) : Vector2.Zero);
                drive.Update(1f / hz);
                Assert.All(drive.Players, a => Assert.True(float.IsFinite(a.Position.X) && float.IsFinite(a.Position.Y)));
            }
            Assert.True(!drive.Live, $"seed={seed}, hz={hz}, call={drive.Plays.SelectedPlay.Id}, series={expected}, noProgress={drive.SecondsWithoutProgress}, steering={drive.SteeringChanges}");
        }
        Assert.True(drive.Complete);
        Assert.NotEmpty(drive.Match.History);
        Assert.True(drive.Throws > 0);
    }

    [Fact]
    public void CPU01_OpenPrimaryWaitsForReadIntervalAndCPU02ProgressesPastCoveredOrIneligibleReads()
    {
        var ai = new QuarterbackAI(.25f);
        var primary = new ReceiverRead(0, new(10, 40), Vector2.Zero, true, false);
        var outlet = new ReceiverRead(1, new(40, 32), Vector2.Zero, true, false);
        var view = new QuarterbackObservation(new(25, 25), 30, true, [primary, outlet], []);
        Assert.Null(ai.Decide(view, .24f).ReceiverIndex);
        Assert.Equal(0, ai.Decide(view, .02f).ReceiverIndex);
        ai.Reset();
        Assert.Null(ai.Decide(view with { Defenders = [primary.Position] }, .25f).ReceiverIndex);
        Assert.Equal(1, ai.Decide(view with { Defenders = [primary.Position] }, .25f).ReceiverIndex);
        ai.Reset();
        Assert.Null(ai.Decide(view with { Reads = [primary with { Blocking = true }] }, .5f).ReceiverIndex);
        ai.Reset();
        Assert.Null(ai.Decide(view with { Reads = [primary with { Eligible = false }] }, .5f).ReceiverIndex);
    }

    [Fact]
    public void CPU03_FakeLocksThrowsAndCPU04CoveredReadsScrambleWithoutSyntheticIncompletion()
    {
        var ai = new QuarterbackAI();
        var view = new QuarterbackObservation(new(25, 25), 30, false, [], []);
        Assert.Equal(Vector2.Zero, ai.Decide(view, 1).Movement);
        Assert.Null(ai.Decide(view, 1).ReceiverIndex);
        Assert.True(ai.Decide(view with { AllowsThrow = true }, 1).Movement.Y > 0);
        var input = new ScriptedInput();
        var drive = new DefensiveDrive(input, 101, fixedCall: "pass.gun-doubles.pa-cross");
        drive.Snap(); drive.Update(.05f);
        Assert.Equal(BallState.HeldByQB, drive.Actors.Ball.State);
        Assert.Equal(0, drive.Throws);
    }

    [Fact]
    public void CPU06_CarrierAvoidsCongestedLaneAndHonorsSideline()
    {
        var ai = new BallCarrierAI();
        var left = ai.Decide(new(25, 40), [new(28, 44)], [new(28, 43)], new(28, 45), .2f);
        Assert.True(left.X < 0); Assert.True(left.Y > 0);
        ai.Reset();
        var edge = ai.Decide(new(1, 40), [], [], new(-10, 44), .2f);
        Assert.True(edge.X >= 0); Assert.True(edge.Y > 0);
    }

    [Fact]
    public void HumanOffensiveKeysDoNotChangeCPUDecisions()
    {
        var a = new DefensiveDrive(new ScriptedInput(), 42, fixedCall: "pass.mesh");
        var b = new DefensiveDrive(new ScriptedInput { Frame = new(ThrowTarget: 2, PassSelection: 3, RunSelection: 0) }, 42, fixedCall: "pass.mesh");
        a.Snap(); b.Snap();
        for (int i = 0; i < 1800 && a.Live; i++) { a.Update(1f / 60); b.Update(1f / 60); }
        Assert.Equal(a.LastResult, b.LastResult);
        Assert.Equal(a.Throws, b.Throws);
        Assert.Equal(a.Actors.Ball.Position, b.Actors.Ball.Position);
    }

    [Fact]
    public void DRIVE01_FirstDownContinuesFromEarnedSpot()
    {
        var input = new ScriptedInput();
        var drive = new DefensiveDrive(input, 101, new(20, 1, 1), "run.hb-dive");
        drive.Snap();
        for (int i = 0; i < 1800 && drive.Live; i++)
        {
            var delta = (drive.Actors.Ball.Holder?.Position ?? drive.Actors.Ball.Position) - drive.Linebacker.Position;
            input.Frame = new(Movement: delta.LengthSquared() > .01f ? Vector2.Normalize(delta) : Vector2.Zero);
            drive.Update(1f / 60);
        }
        Assert.NotNull(drive.LastResult);
        Assert.False(drive.Complete);
        Assert.Equal(1, drive.Match.Series.Down);
        Assert.True(drive.Match.Series.OwnYardLine > 21);
        var spot = drive.Match.Series.OwnYardLine;
        Assert.True(drive.Continue());
        Assert.Equal(spot + 10, drive.Plays.LineOfScrimmage);
    }
}
