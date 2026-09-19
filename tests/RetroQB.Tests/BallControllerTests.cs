using System.Numerics;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Stats;

namespace RetroQB.Tests;

public sealed class BallControllerTests
{
    private static readonly Vector2 Contact = new(25, 40);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsolatedDefenderInterceptsWithoutCatchableReceiver(bool includeDistantReceiver)
    {
        Receiver[] receivers = includeDistantReceiver ? [ReceiverAt(Contact + new Vector2(10, 0))] : [];
        Assert.Equal(BallUpdateResult.Intercepted,
            Update(Controller(new FixedRandom(0)), LowBall(), receivers, [DefenderAt(Contact)]));
    }

    [Fact]
    public void IneligibleReceiverDoesNotPreventInterception()
    {
        var receiver = ReceiverAt(Contact);
        receiver.Eligible = false;
        Assert.Equal(BallUpdateResult.Intercepted,
            Update(Controller(new FixedRandom(0)), LowBall(), [receiver], [DefenderAt(Contact + new Vector2(0.5f, 0))]));
    }

    [Fact]
    public void CloserReceiverCanWinContestedCatch()
    {
        var receiver = ReceiverAt(Contact);
        var ball = LowBall();
        Assert.Equal(BallUpdateResult.Continue,
            Update(Controller(), ball, [receiver], [DefenderAt(Contact + new Vector2(0.5f, 0))]));
        Assert.Equal(BallState.HeldByReceiver, ball.State);
        Assert.Same(receiver, ball.Holder);
    }

    [Fact]
    public void CloserDefenderWinsContestedPass()
    {
        Assert.Equal(BallUpdateResult.Intercepted,
            Update(Controller(new FixedRandom(0)), LowBall(), [ReceiverAt(Contact + new Vector2(0.5f, 0))], [DefenderAt(Contact)]));
    }

    [Fact]
    public void HighPassClearsDefender()
    {
        var ball = new Ball(Contact);
        ball.SetInAir(Contact - new Vector2(0, 5), new Vector2(0, 10), 10, 20, 4);
        ball.Update(0.5f);
        Assert.True(ball.GetArcHeight() > Constants.PassCatchMaxHeight);
        Assert.Equal(BallUpdateResult.Continue, Update(Controller(), ball, [], [DefenderAt(Contact)]));
    }

    [Fact]
    public void DefenderCanSwatWithoutReceiverNearby()
    {
        var random = new FixedRandom(0);
        Assert.Equal(BallUpdateResult.PassDefended,
            Update(Controller(random), LowBall(), [], [DefenderAt(Contact + new Vector2(2, 0))]));
        Assert.Equal(1, random.Calls);
    }

    [Fact]
    public void FailedSwatIsNotRerolledEveryFrame()
    {
        var random = new FixedRandom(0.99);
        var controller = Controller(random);
        var ball = LowBall();
        Defender[] defenders = [DefenderAt(Contact + new Vector2(2, 0))];
        for (int i = 0; i < 5; i++)
            Assert.Equal(BallUpdateResult.Continue, Update(controller, ball, [], defenders));
        Assert.Equal(1, random.Calls);
        controller.Reset(30);
        Update(controller, LowBall(), [], defenders);
        Assert.Equal(2, random.Calls);
    }

    [Fact]
    public void StandingAtFutureLandingPointDoesNotDeflectDistantBall()
    {
        var ball = new Ball(Contact);
        ball.SetInAir(Contact - new Vector2(0, 20), new Vector2(0, 10), 40, 45, 0);
        ball.Update(2);
        var random = new FixedRandom(0);
        Assert.Equal(BallUpdateResult.Continue,
            Update(Controller(random), ball, [], [DefenderAt(ball.GetPredictedLanding())]));
        Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void OutOfBoundsPassIsIncompleteEvenWithDefenderOnBall()
    {
        var ball = LowBall();
        ball.Position = new Vector2(-1, 40);
        Assert.Equal(BallUpdateResult.Incomplete,
            Update(Controller(), ball, [], [DefenderAt(ball.Position)]));
    }

    [Fact]
    public void EveryDefenderUsesTheirOwnInterceptionRadius()
    {
        var nearer = DefenderAt(Contact + new Vector2(0.8f, 0));
        nearer.ApplyStarBoost(1, 1, 0.1f, 1);
        var farther = DefenderAt(Contact + new Vector2(1, 0));
        Assert.Equal(BallUpdateResult.Intercepted, Update(Controller(new FixedRandom(0)), LowBall(), [], [nearer, farther]));
    }

    [Fact]
    public void UnsecuredInterceptionBecomesPassDefendedWithOneRoll()
    {
        var random = new FixedRandom(0.99);
        var ball = LowBall();
        var controller = Controller(random);
        Assert.Equal(BallUpdateResult.PassDefended,
            Update(controller, ball, [ReceiverAt(Contact + new Vector2(0.5f, 0))],
                [DefenderAt(Contact), DefenderAt(Contact + new Vector2(0.1f, 0))]));
        Assert.Equal(1, random.Calls);
        Assert.False(controller.PassCompletedThisPlay);
        Assert.NotEqual(BallState.HeldByReceiver, ball.State);
    }

    [Fact]
    public void CenteredDefenderSecuresBallMoreOftenThanReachingDefender()
    {
        Assert.Equal(BallUpdateResult.Intercepted,
            Update(Controller(new FixedRandom(0.5)), LowBall(), [], [DefenderAt(Contact)]));
        float radius = DefensiveTeamAttributes.Default.GetEffectiveInterceptRadius(DefensivePosition.DB);
        Assert.Equal(BallUpdateResult.PassDefended,
            Update(Controller(new FixedRandom(0.5)), LowBall(), [],
                [DefenderAt(Contact + new Vector2(radius * 0.95f, 0))]));
    }

    [Fact]
    public void PassDefendedAdvancesDownAndDisplaysDistinctResult()
    {
        var drive = new DriveState();
        float line = drive.LineOfScrimmage;
        float distance = drive.Distance;
        var result = drive.ResolvePassDefended();
        Assert.Equal(PlayOutcome.PassDefended, result.Outcome);
        Assert.Equal(2, drive.Down);
        Assert.Equal(line, drive.LineOfScrimmage);
        Assert.Equal(distance, drive.Distance);
        Assert.Equal(0, drive.AwayScore);
        Assert.Equal("Pass defended", new PlayRecord { Outcome = result.Outcome }.GetResultText());
        drive.ResolvePassDefended();
        drive.ResolvePassDefended();
        Assert.Equal(PlayOutcome.Turnover, drive.ResolvePassDefended().Outcome);
    }

    private static Ball LowBall()
    {
        var ball = new Ball(Contact);
        ball.SetInAir(Contact - new Vector2(0, 10), new Vector2(0, 10), 10, 20, 0);
        ball.Update(1);
        return ball;
    }

    [Theory]
    [InlineData(0, PlayEndReason.Interception)]
    [InlineData(0.99, PlayEndReason.PassDefended)]
    public void TerminalContactRetainsBallSpotAndDefender(double roll, PlayEndReason reason)
    {
        var controller = Controller(new FixedRandom(roll));
        var ball = LowBall();
        Update(controller, ball, [], [DefenderAt(Contact)]);
        var terminal = Assert.IsType<PlayContact>(controller.LastTerminal);
        Assert.Equal(reason, terminal.Reason);
        Assert.Equal(ball.Position, terminal.Position);
        Assert.Equal(DefenderSlot.CB1, terminal.Defender);
        var ended = terminal.ToEvent(new(1, "ballers", "pass", new(20)));
        Assert.Equal(ball.Position.Y - FieldGeometry.EndZoneDepth, ended.Spot);
        Assert.NotEqual(20, ended.Spot);
        controller.Reset(30);
        Assert.Null(controller.LastTerminal);
    }

    private static Receiver ReceiverAt(Vector2 position) => new(0, ReceiverSlot.WR1, position);
    private static Defender DefenderAt(Vector2 position) => new(position, DefensivePosition.DB, DefenderSlot.CB1);
    private static BallController Controller(Random? random = null) => new(
        random ?? new FixedRandom(0.99), new ThrowingMechanics(), new StatisticsTracker(), new ReceiverPriorityManager());
    private static BallUpdateResult Update(BallController controller, Ball ball, Receiver[] receivers, Defender[] defenders) =>
        controller.Update(ball, new Quarterback(new Vector2(25, 30)), receivers, defenders,
            OffensiveTeamAttributes.Default, DefensiveTeamAttributes.Default, 0, 0);

    private sealed class FixedRandom(double value) : Random
    {
        public int Calls { get; private set; }
        public override double NextDouble() { Calls++; return value; }
    }
}
