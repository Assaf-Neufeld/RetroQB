using System.Numerics;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;

namespace RetroQB.Tests;

public sealed class PlayContactTests
{
    [Theory]
    [InlineData(-1, 40, false, PlayEndReason.OutOfBounds)]
    [InlineData(25, 26, true, PlayEndReason.Sack)]
    [InlineData(25, 40, true, PlayEndReason.Tackle)]
    [InlineData(25, 9, true, PlayEndReason.Safety)]
    [InlineData(25, 111, false, PlayEndReason.Touchdown)]
    public void TerminalReasonsKeepSpotAndActor(float x, float y, bool contact, PlayEndReason reason)
    {
        var position = new Vector2(x, y);
        var qb = new Quarterback(position);
        var ball = new Ball(position);
        ball.SetHeld(qb, BallState.HeldByQB);
        var controller = new TackleController(new Random(101), new OverlapResolver());
        Defender[] defenders = contact ? [new(position, DefensivePosition.LB, DefenderSlot.MLB)] : [];
        controller.CheckTackleOrScore(ball, qb, defenders, OffensiveTeamAttributes.Default, _ => { }, 30);
        var terminal = Assert.IsType<PlayContact>(controller.LastTerminal);
        Assert.Equal(reason, terminal.Reason);
        Assert.Equal(position, terminal.Position);
        Assert.Equal(contact ? DefenderSlot.MLB : (DefenderSlot?)null, terminal.Defender);
        var ended = terminal.ToEvent(new(1, "ballers", "run", new(20)));
        Assert.Equal(y - FieldGeometry.EndZoneDepth, ended.Spot);
        controller.Reset();
        Assert.Null(controller.LastTerminal);
    }
}
