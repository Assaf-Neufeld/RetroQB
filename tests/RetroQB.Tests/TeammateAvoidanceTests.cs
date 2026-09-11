using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class TeammateAvoidanceTests
{
    [Theory]
    [InlineData(1f / 60, false)]
    [InlineData(1f / 30, false)]
    [InlineData(0.1f, false)]
    [InlineData(1f / 60, true)]
    [InlineData(1f / 30, true)]
    [InlineData(0.1f, true)]
    public void CrossingReceiversAvoidContactAndFinishTheirRoutes(float dt, bool headOn)
    {
        var a = Runner(0, ReceiverSlot.WR1, new(17, 42), new(33, 42));
        var b = Runner(1, ReceiverSlot.WR2, headOn ? new(33, 42) : new(25, 34), headOn ? new(17, 42) : new(25, 50));
        var qb = new Quarterback(new(5, 20));
        var manager = Manager();
        var controller = new ReceiverUpdateController(new BlockingController());
        var ball = new Ball(qb.Position);
        ball.SetHeld(qb, BallState.HeldByQB);
        float minDistance = float.MaxValue;
        for (int i = 0; i < 6 / dt; i++)
        {
            controller.UpdateAll([a, b], qb, ball, [], null, Vector2.Zero, false, false, false, manager, dt, Clamp);
            minDistance = MathF.Min(minDistance, Vector2.Distance(a.Position, b.Position));
        }
        Assert.True(minDistance >= a.Radius + b.Radius, $"Minimum separation: {minDistance}");
        Assert.Equal(RoutePhase.Settled, a.RouteState.Phase);
        Assert.Equal(RoutePhase.Settled, b.RouteState.Phase);
        Assert.InRange(Vector2.Distance(a.Position, new(33, 42)), 0, 0.002f);
        Assert.InRange(Vector2.Distance(b.Position, headOn ? new(17, 42) : new(25, 50)), 0, 0.002f);
    }

    [Theory]
    [InlineData(1f / 60, 25f)]
    [InlineData(1f / 30, 25f)]
    [InlineData(0.1f, 25f)]
    [InlineData(1f / 60, 51.8f)]
    public void RunningBackGoesAroundQuarterbackAndReturnsToRoute(float dt, float x)
    {
        var rb = Runner(0, ReceiverSlot.RB1, new(x, 32), new(x, 48));
        var qb = new Quarterback(new(x, 38));
        var ball = new Ball(qb.Position);
        ball.SetHeld(qb, BallState.HeldByQB);
        var controller = new ReceiverUpdateController(new BlockingController());
        var manager = Manager();
        float minDistance = float.MaxValue;
        for (int i = 0; i < 6 / dt; i++)
        {
            controller.UpdateAll([rb], qb, ball, [], null, Vector2.Zero, false, false, false, manager, dt, Clamp);
            minDistance = MathF.Min(minDistance, Vector2.Distance(rb.Position, qb.Position));
        }
        Assert.True(minDistance >= rb.Radius + qb.Radius, $"Minimum separation: {minDistance}");
        Assert.Equal(new Vector2(x, 38), qb.Position);
        Assert.Equal(RoutePhase.Settled, rb.RouteState.Phase);
        Assert.InRange(Vector2.Distance(rb.Position, new(x, 48)), 0, 0.002f);
    }

    [Fact]
    public void ParallelAndSeparatingPlayersKeepTheirIntendedVelocity()
    {
        var a = Runner(0, ReceiverSlot.WR1, new(20, 40), new(20, 60));
        var b = Runner(1, ReceiverSlot.WR2, new(22.1f, 40), new(22.1f, 60));
        a.Velocity = b.Velocity = new(0, 7);
        var qb = new Quarterback(new(20, 37));
        TeammateAvoidance.Apply([a, b], qb, [], null, new(), 1f / 60);
        Assert.Equal(new Vector2(0, 7), a.Velocity);
        Assert.Equal(a.Velocity, b.Velocity);
    }

    [Fact]
    public void SteeringUsesAllIntendedVelocitiesRegardlessOfReceiverOrder()
    {
        var a = Runner(0, ReceiverSlot.WR1, new(20, 40), new(30, 40));
        var b = Runner(1, ReceiverSlot.WR2, new(25, 35), new(25, 45));
        var qb = new Quarterback(new(5, 20));
        a.Velocity = new(7, 0);
        b.Velocity = new(0, 7);
        TeammateAvoidance.Apply([a, b], qb, [], null, new(), 1f / 60);
        var expected = (a.Velocity, b.Velocity);
        a.Velocity = new(7, 0);
        b.Velocity = new(0, 7);
        TeammateAvoidance.Apply([b, a], qb, [], null, new(), 1f / 60);
        Assert.Equal(expected, (a.Velocity, b.Velocity));
    }

    [Fact]
    public void UserControlledCarrierKeepsInputWhileTeammateYields()
    {
        var carrier = Runner(0, ReceiverSlot.RB1, new(25, 35), new(25, 50));
        var teammate = Runner(1, ReceiverSlot.WR1, new(25, 39), new(25, 25));
        var qb = new Quarterback(new(5, 20));
        carrier.HasBall = true;
        carrier.Velocity = new(0, 8);
        teammate.Velocity = new(0, -7);
        TeammateAvoidance.Apply([carrier, teammate], qb, [], carrier, new(), 1f / 60);
        Assert.Equal(new Vector2(0, 8), carrier.Velocity);
        Assert.NotEqual(new Vector2(0, -7), teammate.Velocity);
        Assert.InRange(teammate.Velocity.Length(), 0, 7.001f);
    }

    [Fact]
    public void ReceiversAnticipateLinemenInTheirPath()
    {
        var receiver = Runner(0, ReceiverSlot.WR1, new(25, 35), new(25, 50));
        receiver.Velocity = new(0, 7);
        var qb = new Quarterback(new(5, 20));
        TeammateAvoidance.Apply([receiver], qb, [new Blocker(new(25, 39))], null, new(), 1f / 60);
        Assert.NotEqual(new Vector2(0, 7), receiver.Velocity);
        Assert.True(receiver.Velocity.Y > 0);
    }

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 1)]
    [InlineData(true, 9)]
    public void IntentionalExchangesStillComplete(bool run, int playIndex)
    {
        var manager = new PlayManager();
        if (run) manager.SelectRunPlay(playIndex, new Random(1));
        else manager.SelectPassPlay(playIndex, new Random(1));
        var play = manager.SelectedPlay;
        var field = new FormationFactory().CreateFormation(play, 40);
        RouteAssigner.AssignRoutes(field.Receivers, play);
        var exchange = new BackfieldController();
        var controller = new ReceiverUpdateController(new BlockingController());
        for (int i = 0; i < 180; i++)
        {
            const float dt = 1f / 60;
            exchange.Update(play, field.Ball, field.Qb, field.Receivers, dt);
            controller.UpdateAll(field.Receivers, field.Qb, field.Ball, [], field.Ball.Holder as Receiver,
                Vector2.UnitY, false, false, false, manager, dt, Clamp, exchange, field.Blockers);
            exchange.TryHandoff(play, field.Ball, field.Qb, field.Receivers);
        }
        Assert.Equal(run ? BackfieldPhase.Completed : BackfieldPhase.Released, exchange.Phase);
        Assert.Equal(run ? BallState.HeldByReceiver : BallState.HeldByQB, field.Ball.State);
    }

    [Fact]
    public void CatalogRoutesHaveFewerTeammateContactsWithAnticipation()
    {
        int baseline = CountContacts(false);
        int avoided = CountContacts(true);
        Assert.True(baseline > 0);
        Assert.True(avoided < baseline * 0.6f, $"Contact frames: baseline={baseline}, avoidance={avoided}");

        static int CountContacts(bool avoid)
        {
            int contacts = 0;
            foreach (var definition in PlaybookBuilder.BuildPassPlays())
            foreach (bool flipped in new[] { false, true })
            {
                var play = PlayResolver.Resolve(definition);
                if (flipped) play = play.Flip();
                var field = new FormationFactory().CreateFormation(play, 40);
                RouteAssigner.AssignRoutes(field.Receivers, play);
                var overlap = new OverlapResolver();
                var exchange = new BackfieldController();
                for (int frame = 0; frame < 300; frame++)
                {
                    const float dt = 1f / 60;
                    foreach (var receiver in field.Receivers) RouteRunner.UpdateRoute(receiver, dt);
                    if (avoid) TeammateAvoidance.Apply(field.Receivers, field.Qb, field.Blockers, null, exchange, dt);
                    foreach (var receiver in field.Receivers) { receiver.Update(dt); Clamp(receiver); }
                    for (int i = 0; i < field.Receivers.Count; i++)
                    {
                        var a = field.Receivers[i];
                        if (!a.IsBlocking && Vector2.Distance(a.Position, field.Qb.Position) < a.Radius + field.Qb.Radius)
                            contacts++;
                        for (int j = i + 1; j < field.Receivers.Count; j++)
                        {
                            var b = field.Receivers[j];
                            if (!a.IsBlocking && !b.IsBlocking && Vector2.Distance(a.Position, b.Position) < a.Radius + b.Radius)
                                contacts++;
                        }
                    }
                    overlap.ResolveOverlaps(field.Qb, field.Ball, field.Receivers, field.Blockers, [], 40, Clamp);
                }
            }
            return contacts;
        }
    }

    private static Receiver Runner(int index, ReceiverSlot slot, Vector2 start, Vector2 end) => new(index, slot, start)
    {
        RouteSide = 1,
        RouteDefinition = new([new(end - start)], RouteFinish.Settle)
    };

    private static PlayManager Manager()
    {
        var manager = new PlayManager();
        manager.SelectPassPlay(7, new Random(1)); // WR1, WR2 and RB1 all release; no exchange.
        return manager;
    }

    private static void Clamp(Entity entity) => entity.Position = new(
        Math.Clamp(entity.Position.X, entity.Radius, Constants.FieldWidth - entity.Radius),
        Math.Clamp(entity.Position.Y, entity.Radius, Constants.FieldLength - entity.Radius));
}
