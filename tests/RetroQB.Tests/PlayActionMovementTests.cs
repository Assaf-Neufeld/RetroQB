using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Input;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class PlayActionMovementTests
{
    [Theory]
    [InlineData(false, 1f / 60)]
    [InlineData(true, 1f / 60)]
    [InlineData(false, 1f / 30)]
    [InlineData(true, 1f / 30)]
    [InlineData(false, .1f)]
    [InlineData(true, .1f)]
    public void PaCrossCompletesFakeWithoutPushingQuarterbackAndRunsTheWheel(bool flipped, float dt)
    {
        var manager = Manager(flipped);
        var play = manager.SelectedPlay;
        var field = new FormationFactory().CreateFormation(play, manager.LineOfScrimmage);
        RouteAssigner.AssignRoutes(field.Receivers, play);
        var qbStart = field.Qb.Position;
        var rb = field.Receivers.Single(r => r.Slot == ReceiverSlot.RB1);
        var diagram = RouteVisualizer.GetRouteWaypoints(rb, play, manager.LineOfScrimmage);
        var execution = new PlayExecutionController(new MovementInput(), new BlockingController());
        var overlap = new OverlapResolver();
        float maxQbDrift = 0;
        for (int frame = 0; frame < 6 / dt; frame++)
        {
            execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], field.Blockers,
                manager, false, false, false, Clamp, dt);
            overlap.ResolveOverlaps(field.Qb, field.Ball, field.Receivers, field.Blockers, [],
                manager.LineOfScrimmage, Clamp, execution.Backfield);
            maxQbDrift = MathF.Max(maxQbDrift, Vector2.Distance(qbStart, field.Qb.Position));
        }

        Assert.InRange(maxQbDrift, 0, .01f);
        Assert.Equal(BackfieldPhase.Released, execution.Backfield.Phase);
        Assert.True(rb.RouteState.Phase == RoutePhase.Continuing,
            $"phase={rb.RouteState.Phase}, step={rb.RouteState.StepIndex}, pos={rb.Position}, velocity={rb.Velocity}, path={string.Join(';', RouteGeometry.GetPath(rb).Points)}");
        Assert.InRange(MathF.Abs(rb.Position.X - diagram[^1].X), 0, .1f);
        Assert.True(rb.Position.Y >= diagram[^1].Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovementImmediatelyCancelsTheFakeAndKeepsQbAndRbIndependent(bool duringFake)
    {
        var manager = Manager(false);
        var field = new FormationFactory().CreateFormation(manager.SelectedPlay, manager.LineOfScrimmage);
        RouteAssigner.AssignRoutes(field.Receivers, manager.SelectedPlay);
        var input = new MovementInput();
        var execution = new PlayExecutionController(input, new BlockingController());
        var rb = field.Receivers.Single(r => r.Slot == ReceiverSlot.RB1);
        if (duringFake)
        {
            rb.Position = manager.SelectedPlay.Backfield.GetMeshPoint(field.Qb.Position);
            execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], field.Blockers,
                manager, false, false, false, Clamp, 1f / 60);
            Assert.Equal(BackfieldPhase.Faking, execution.Backfield.Phase);
        }
        input.Direction = Vector2.UnitX;
        var qbStart = field.Qb.Position;
        var rbStart = rb.Position;
        execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], field.Blockers,
            manager, false, false, false, Clamp, 1f / 60);
        Assert.True(field.Qb.Position.X > qbStart.X);
        Assert.Equal(BackfieldPhase.Aborted, execution.Backfield.Phase);
        Assert.True(execution.Backfield.AllowsThrow);
        Assert.Same(field.Qb, field.Ball.Holder);
        Assert.Equal(rbStart, rb.RouteStart);
        for (int frame = 0; frame < 60; frame++)
            execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], field.Blockers,
                manager, false, false, false, Clamp, 1f / 60);
        Assert.True(rb.Position.X < rbStart.X);
        Assert.Equal(rbStart, rb.RouteStart);
    }

    private sealed class MovementInput : IPlayerMovementInput
    {
        public Vector2 Direction { get; set; }
        public Vector2 GetMovementDirection() => Direction;
        public bool IsSprintHeld() => false;
    }

    private static PlayManager Manager(bool flipped)
    {
        var manager = new PlayManager();
        const string id = "pass.gun-doubles.pa-cross";
        manager.SetCallSheet(new PlayCallSheet(manager.Catalog,
            manager.CallSheet.PassIds.Take(9).Append(id), manager.CallSheet.RunIds));
        manager.SelectPassPlay(9, new Random(1));
        if (flipped) manager.FlipSelectedPlay();
        return manager;
    }

    private static void Clamp(Entity entity) => entity.Position = new(
        Math.Clamp(entity.Position.X, entity.Radius, Constants.FieldWidth - entity.Radius),
        Math.Clamp(entity.Position.Y, entity.Radius, Constants.FieldLength - entity.Radius));
}
