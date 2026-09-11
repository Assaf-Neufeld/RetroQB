using System.Numerics;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Input;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class PassProtectionMovementTests
{
    [Theory]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(6, false)]
    [InlineData(6, true)]
    public void BackHoldsPocketWhileQuarterbackDropsBackAndStrafes(int playIndex, bool flipped)
    {
        var manager = new PlayManager();
        manager.SelectPassPlay(playIndex, new Random(1));
        if (flipped) manager.FlipSelectedPlay();
        var field = new FormationFactory().CreateFormation(manager.SelectedPlay, manager.LineOfScrimmage);
        RouteAssigner.AssignRoutes(field.Receivers, manager.SelectedPlay);
        var rb = field.Receivers.Single(r => r.Slot == ReceiverSlot.RB1);
        var pocket = rb.Position;
        var qbStart = field.Qb.Position;
        var input = new MovementInput();
        var execution = new PlayExecutionController(input, new BlockingController());
        var overlap = new OverlapResolver();
        float maxRbMovement = 0;
        foreach (var direction in new[] { Vector2.Zero, -Vector2.UnitY, Vector2.UnitX, -Vector2.UnitX })
        {
            input.Direction = direction;
            for (int frame = 0; frame < 60; frame++)
            {
                execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], field.Blockers,
                    manager, false, false, false, Clamp, 1f / 60);
                overlap.ResolveOverlaps(field.Qb, field.Ball, field.Receivers, field.Blockers, [],
                    manager.LineOfScrimmage, Clamp, execution.Backfield);
                maxRbMovement = MathF.Max(maxRbMovement, Vector2.Distance(pocket, rb.Position));
            }
        }
        Assert.True(Vector2.Distance(qbStart, field.Qb.Position) > 3);
        Assert.InRange(maxRbMovement, 0, .1f);
        Assert.True(rb.IsBlocking);
        Assert.False(rb.Eligible);

        // A defender entering protection still gets picked up after the QB has moved.
        var threat = new Defender(rb.Position + new Vector2(flipped ? -2 : 2, 1),
            DefensivePosition.LB, DefenderSlot.MLB) { IsRusher = true };
        var receiverController = new ReceiverUpdateController(new BlockingController());
        var targetStart = threat.Position;
        receiverController.UpdateAll(field.Receivers, field.Qb, field.Ball, [threat], null,
            Vector2.Zero, false, false, false, manager, 1f / 60, Clamp, execution.Backfield, field.Blockers);
        Assert.Same(threat, rb.BlockingState.Target);
        Assert.True(Vector2.Dot(rb.Velocity, targetStart - pocket) > 0);
        Assert.True(Vector2.Distance(threat.Position, pocket) > Vector2.Distance(targetStart, pocket));
    }

    private sealed class MovementInput : IPlayerMovementInput
    {
        public Vector2 Direction { get; set; }
        public Vector2 GetMovementDirection() => Direction;
        public bool IsSprintHeld() => false;
    }

    private static void Clamp(Entity entity) => entity.Position = new(
        Math.Clamp(entity.Position.X, entity.Radius, Constants.FieldWidth - entity.Radius),
        Math.Clamp(entity.Position.Y, entity.Radius, Constants.FieldLength - entity.Radius));
}
