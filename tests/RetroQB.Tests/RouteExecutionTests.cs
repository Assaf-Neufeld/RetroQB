using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class RouteExecutionTests
{
    [Theory]
    [InlineData(RouteType.Hitch, -1)]
    [InlineData(RouteType.Hitch, 1)]
    [InlineData(RouteType.Curl, -1)]
    [InlineData(RouteType.Curl, 1)]
    public void ShortRoutesKeepCrossingAfterTheirReadPoint(RouteType type, int side)
    {
        var receiver = Receiver(null);
        receiver.Route = type;
        receiver.RouteSide = side;
        var path = RouteGeometry.GetPath(receiver);
        for (int i = 0; i < 1000 && !RouteGeometry.HasCompletedBreak(receiver); i++) Tick(receiver, .02f);
        Assert.True(RouteGeometry.HasCompletedBreak(receiver));
        Assert.True(receiver.RouteState.StepIndex < path.Definition.Steps.Count);
        for (int i = 0; i < 1000 && receiver.RouteState.Phase != RoutePhase.Continuing; i++) Tick(receiver, .02f);
        Assert.Equal(RoutePhase.Continuing, receiver.RouteState.Phase);
        Vector2 position = receiver.Position;
        for (int i = 0; i < 25; i++) Tick(receiver, .02f);
        Assert.True((receiver.Position.X - position.X) * side < -1);
        Assert.InRange(MathF.Abs(receiver.Position.Y - position.Y), 0, .001f);
    }

    [Theory]
    [InlineData(-1, 0f)]
    [InlineData(1, 0f)]
    [InlineData(-1, 1f)]
    [InlineData(1, 1f)]
    public void ContinuingRoutesTurnUpfieldAtEitherSideline(int side, float rise)
    {
        float edge = side < 0 ? Constants.ReceiverRadius : Constants.FieldWidth - Constants.ReceiverRadius;
        var receiver = Receiver(new([new(new(4, rise))]));
        receiver.Position = receiver.RouteStart = new Vector2(edge - side * 2, 30);
        receiver.RouteSide = side;
        for (int i = 0; i < 100; i++) Tick(receiver, .02f);
        Assert.Equal(RoutePhase.Continuing, receiver.RouteState.Phase);
        Assert.InRange(MathF.Abs(receiver.Position.X - edge), 0, .001f);
        Assert.True(receiver.Position.Y > 32);
        Assert.InRange(MathF.Abs(receiver.Velocity.Y - receiver.Speed), 0, .001f);
    }

    [Fact]
    public void DisplacedRunnerCanRoundAnIntermediateTurnButMustReachAHoldExactly()
    {
        foreach (float hold in new[] { 0f, .5f })
        {
            var receiver = Receiver(new([new(new(0, 6), hold), new(new(6, 6))], RouteFinish.Settle));
            RouteRunner.UpdateRoute(receiver, 1f / 60);
            receiver.Position = receiver.RouteStart + new Vector2(.3f, 6);
            RouteRunner.UpdateRoute(receiver, 1f / 60);
            Assert.Equal(hold == 0 ? 1 : 0, receiver.RouteState.StepIndex);
            Assert.True(hold == 0 ? receiver.Velocity.X > 0 : receiver.Velocity.X < 0);
        }
    }

    [Theory]
    [InlineData(0.01666667f)]
    [InlineData(0.03333333f)]
    [InlineData(0.1f)]
    public void ComebackTravelsBackwardAndSettlesAtTheDisplayedEndpoint(float dt)
    {
        var receiver = Receiver(new([new(new(0, 6)), new(new(2, 3))], RouteFinish.Settle));
        var points = RouteVisualizer.GetRouteWaypoints(receiver);
        bool reachedStem = false;
        for (int i = 0; i < 500; i++)
        {
            Tick(receiver, dt);
            reachedStem |= receiver.Position.Y >= receiver.RouteStart.Y + 5.99f;
        }
        Assert.True(reachedStem);
        Assert.Equal(RoutePhase.Settled, receiver.RouteState.Phase);
        AssertClose(points[^1], receiver.Position);
        Assert.Equal(Vector2.Zero, receiver.Velocity);
        Assert.True(RouteGeometry.HasCompletedBreak(receiver));
    }

    [Fact]
    public void HoldingAndInitialDelayDoNotTriggerScrambleOrAdvanceWithoutMovement()
    {
        var receiver = Receiver(new([new(new(0, 2), 0.5f), new(new(2, 2))], RouteFinish.Settle, delaySeconds: 0.3f));
        var origin = receiver.Position;
        Tick(receiver, 0.2f);
        Assert.Equal(origin, receiver.Position);
        Assert.Equal(RoutePhase.Waiting, receiver.RouteState.Phase);
        for (int i = 0; i < 20; i++) RouteRunner.UpdateRoute(receiver, 0.1f); // Simulate a pinned receiver.
        Assert.Equal(0, receiver.RouteState.StepIndex);
        Assert.Equal(0, receiver.RouteProgress);
        Tick(receiver, 1);
        AssertClose(origin + new Vector2(0, 2), receiver.Position);
        Tick(receiver, 0.2f);
        Assert.Equal(RoutePhase.Holding, receiver.RouteState.Phase);
        Assert.Equal(Vector2.Zero, receiver.Velocity);
        Tick(receiver, 0.2f);
        AssertClose(origin + new Vector2(0, 2), receiver.Position);
        Tick(receiver, 0.2f);
        Assert.True(receiver.Position.X > origin.X);
    }

    [Fact]
    public void WheelVisitsEveryWaypointAndStagesNeverRegressWhenPushed()
    {
        var receiver = Receiver(new([new(new(0, 2)), new(new(5, 2)), new(new(5, 12))], RouteFinish.Settle));
        var points = RouteVisualizer.GetRouteWaypoints(receiver);
        for (int step = 1; step < points.Count; step++)
        {
            for (int i = 0; i < 200 && receiver.RouteState.StepIndex < step; i++) Tick(receiver, 0.02f);
            Assert.True(receiver.RouteState.StepIndex >= step);
            if (step == 2)
            {
                receiver.Position -= new Vector2(0, 4);
                RouteRunner.UpdateRoute(receiver, 0.02f);
                Assert.Equal(2, receiver.RouteState.StepIndex);
                Assert.True(receiver.Velocity.Y > 0);
            }
        }
        AssertClose(points[^1], receiver.Position);
    }

    [Fact]
    public void LargeFrameDoesNotSkipTheFirstTurn()
    {
        var receiver = Receiver(new([new(new(0, 1)), new(new(6, 1))], RouteFinish.Settle));
        Tick(receiver, 1);
        AssertClose(receiver.RouteStart + Vector2.UnitY, receiver.Position);
        Assert.Equal(0, receiver.RouteState.StepIndex);
        Tick(receiver, 1);
        Assert.True(receiver.Position.X > receiver.RouteStart.X);
        Assert.Equal(receiver.RouteStart.Y + 1, receiver.Position.Y);
    }

    [Fact]
    public void ScrambleMustBeRequestedExplicitlyAfterASettle()
    {
        var receiver = Receiver(new([new(new(0, 1))], RouteFinish.Settle));
        for (int i = 0; i < 30; i++) Tick(receiver, 0.1f);
        Vector2 settled = receiver.Position;
        for (int i = 0; i < 30; i++) Tick(receiver, 0.1f);
        Assert.Equal(settled, receiver.Position);
        RouteRunner.RequestScramble(receiver);
        Tick(receiver, 0.1f);
        Assert.Equal(RoutePhase.Scrambling, receiver.RouteState.Phase);
        Assert.True(receiver.Position.Y > settled.Y);
    }

    [Fact]
    public void DoubleMoveHasARealSecondTurnAndUsesTheSameDisplayedPath()
    {
        var receiver = Receiver(null);
        receiver.Route = RouteType.DoubleMove;
        var points = RouteVisualizer.GetRouteWaypoints(receiver);
        Assert.Equal(4, points.Count);
        Assert.True(points[2].X < points[1].X);
        Assert.True(points[3].Y > points[2].Y);
        for (int i = 0; i < 1000 && receiver.RouteState.Phase != RoutePhase.Continuing; i++) Tick(receiver, 0.02f);
        Assert.Equal(RoutePhase.Continuing, receiver.RouteState.Phase);
        Assert.InRange(MathF.Abs(receiver.Velocity.X), 0, 0.001f);
        Assert.True(receiver.Velocity.Y > 0);
    }

    [Fact]
    public void RoutePathsAreClampedToFieldAndDiagramsDoNotAdvanceSimulation()
    {
        var receiver = Receiver(new([new(new(100, 100))], RouteFinish.Settle));
        var points = RouteVisualizer.GetRouteWaypoints(receiver);
        for (int i = 0; i < 10; i++) Assert.Same(points, RouteVisualizer.GetRouteWaypoints(receiver));
        Assert.Equal(0, receiver.RouteState.StepIndex);
        Assert.Equal(RoutePhase.Waiting, receiver.RouteState.Phase);
        Assert.Equal(Constants.FieldWidth - Constants.ReceiverRadius, points[^1].X);
        Assert.Equal(Constants.FieldLength - Constants.ReceiverRadius, points[^1].Y);
    }

    [Fact]
    public void RouteDefinitionsOwnTheirWaypointsAndValidateTiming()
    {
        RouteStep[] steps = [new(new(0, 3))];
        var route = new RouteDefinition(steps);
        steps[0] = new(new(4, 4));
        Assert.Equal(new Vector2(0, 3), route.Steps[0].Offset);
        Assert.Throws<ArgumentException>(() => new RouteDefinition([]));
        Assert.Throws<ArgumentException>(() => new RouteDefinition([new(new(float.NaN, 3))]));
        Assert.Throws<ArgumentException>(() => new RouteDefinition([new(new(0, 3), -1)]));
        Assert.Throws<ArgumentException>(() => new RouteDefinition(steps, delaySeconds: -1));
        Assert.Throws<ArgumentException>(() => new RouteDefinition(steps, readAfterStep: 2));
    }

    private static Receiver Receiver(RouteDefinition? route) => new(0, ReceiverSlot.WR1, new Vector2(20, 30))
    {
        RouteDefinition = route, RouteSide = 1, SlantInside = true
    };

    private static void Tick(Receiver receiver, float dt)
    {
        RouteRunner.UpdateRoute(receiver, dt);
        receiver.Update(dt);
    }

    private static void AssertClose(Vector2 expected, Vector2 actual) => Assert.InRange(Vector2.Distance(expected, actual), 0, 0.002f);
}
