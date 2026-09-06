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
            Update(Controller(), LowBall(), receivers, [DefenderAt(Contact)]));
    }

    [Fact]
    public void IneligibleReceiverDoesNotPreventInterception()
    {
        var receiver = ReceiverAt(Contact);
        receiver.Eligible = false;
        Assert.Equal(BallUpdateResult.Intercepted,
            Update(Controller(), LowBall(), [receiver], [DefenderAt(Contact + new Vector2(0.5f, 0))]));
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
            Update(Controller(), LowBall(), [ReceiverAt(Contact + new Vector2(0.5f, 0))], [DefenderAt(Contact)]));
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
        Assert.Equal(BallUpdateResult.Intercepted, Update(Controller(), LowBall(), [], [nearer, farther]));
    }

    private static Ball LowBall()
    {
        var ball = new Ball(Contact);
        ball.SetInAir(Contact - new Vector2(0, 10), new Vector2(0, 10), 10, 20, 0);
        ball.Update(1);
        return ball;
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
