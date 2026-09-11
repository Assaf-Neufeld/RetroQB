using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class BackfieldRouteTests
{
    public static IEnumerable<object[]> ExchangeCalls => PlaybookBuilder.BuildCatalog().Plays
        .Where(p => p.Backfield.Action != BackfieldAction.None)
        .SelectMany(p => new[] { new object[] { p.Id, false }, new object[] { p.Id, true } });

    [Theory]
    [MemberData(nameof(ExchangeCalls))]
    public void DiagramBeginsInTheActualExchangeDirection(string id, bool flipped)
    {
        var play = PlayResolver.Resolve(PlaybookBuilder.BuildCatalog()[id], flipped);
        var field = new FormationFactory().CreateFormation(play, 40);
        RouteAssigner.AssignRoutes(field.Receivers, play);
        var rb = field.Receivers.Single(r => r.Slot == play.Backfield.Participant);
        var points = RouteVisualizer.GetRouteWaypoints(rb, play, 40);
        var backfield = new BackfieldController();
        backfield.Update(play, field.Ball, field.Qb, field.Receivers, 1f / 60);
        backfield.MoveParticipant(rb, field.Qb, 1f / 60);

        Assert.Equal(rb.Position, points[0]);
        Assert.True(Vector2.Dot(points[1] - points[0], rb.Velocity) > 0, id);
        if (play.Family == PlayType.Run)
        {
            Assert.True(points[^1].Y > points[1].Y);
            Assert.Equal(play.RunningBackSide, Math.Sign(points[^1].X - points[1].X));
        }

        var mirrored = new FormationFactory().CreateFormation(play.Flip(), 40);
        RouteAssigner.AssignRoutes(mirrored.Receivers, play.Flip());
        var mirrorRb = mirrored.Receivers.Single(r => r.Slot == rb.Slot);
        var mirrorPoints = RouteVisualizer.GetRouteWaypoints(mirrorRb, play.Flip(), 40);
        Assert.Equal(points.Count, mirrorPoints.Count);
        for (int i = 0; i < points.Count; i++)
        {
            Assert.InRange(MathF.Abs(Constants.FieldWidth - points[i].X - mirrorPoints[i].X), 0, .001f);
            Assert.Equal(points[i].Y, mirrorPoints[i].Y);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayActionReleasesUpfieldInsteadOfReturningToBackfieldWaypoints(bool flipped)
    {
        foreach (var definition in PlaybookBuilder.BuildCatalog().Plays
            .Where(p => p.Backfield.Action == BackfieldAction.PlayAction))
        {
            var play = PlayResolver.Resolve(definition, flipped);
            var field = new FormationFactory().CreateFormation(play, 40);
            RouteAssigner.AssignRoutes(field.Receivers, play);
            var rb = field.Receivers.Single(r => r.Slot == play.Backfield.Participant);
            var backfield = new BackfieldController();
            // Cache the pre-snap path, as rendering and coverage setup do.
            var diagram = RouteVisualizer.GetRouteWaypoints(rb, play, 40);
            var originalPath = RouteGeometry.GetPath(rb);
            const float dt = 1f / 60;
            for (int frame = 0; frame < 240 && backfield.Phase != BackfieldPhase.Released; frame++)
            {
                backfield.Update(play, field.Ball, field.Qb, field.Receivers, dt);
                if (backfield.ControlsParticipant(rb.Slot))
                {
                    backfield.MoveParticipant(rb, field.Qb, dt);
                    rb.Update(dt);
                }
            }

            Assert.Equal(BackfieldPhase.Released, backfield.Phase);
            Assert.NotSame(originalPath, RouteGeometry.GetPath(rb));
            Assert.Equal(diagram.Skip(1), RouteGeometry.GetPath(rb).Points);
            RouteRunner.UpdateRoute(rb, dt);
            Assert.True(rb.Velocity.Y > 0, play.Id);
            Assert.Same(field.Qb, field.Ball.Holder);

            for (int frame = 0; frame < 600 && rb.RouteState.Phase != RoutePhase.Continuing; frame++)
            {
                rb.Update(dt);
                RouteRunner.UpdateRoute(rb, dt);
            }
            Assert.Equal(RoutePhase.Continuing, rb.RouteState.Phase);
        }
    }

    [Fact]
    public void AbortedFakeStartsTheRouteFromTheCurrentPositionOnlyOnce()
    {
        var play = PlayResolver.Resolve(PlaybookBuilder.BuildCatalog()["pass.gun-doubles.pa-cross"]);
        var field = new FormationFactory().CreateFormation(play, 40);
        RouteAssigner.AssignRoutes(field.Receivers, play);
        var rb = field.Receivers.Single(r => r.Slot == play.Backfield.Participant);
        _ = RouteGeometry.GetPath(rb);
        rb.Position += new Vector2(8, 8);
        var origin = rb.Position;
        var backfield = new BackfieldController();
        backfield.Update(play, field.Ball, field.Qb, field.Receivers, play.Backfield.ApproachTimeout);
        Assert.Equal(BackfieldPhase.Aborted, backfield.Phase);
        Assert.Equal(origin, rb.RouteStart);
        RouteRunner.UpdateRoute(rb, .1f);
        Assert.True(rb.Velocity.Y > 0);
        rb.Update(.1f);
        backfield.Update(play, field.Ball, field.Qb, field.Receivers, .1f);
        Assert.Equal(origin, rb.RouteStart);
    }
}
