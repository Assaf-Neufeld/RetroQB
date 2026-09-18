using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Input;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class PossessionControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InheritedRetreatProtectionExpires(bool run)
    {
        var (assist, _, _) = Transfer(run, Vector2.UnitY * 8, -Vector2.UnitY);
        Assert.True(assist.Resolve(-Vector2.UnitY).Y > 0);
        Assert.True(assist.SuppressTurnBoost);
        assist.Tick(.31f);
        Assert.Equal(-Vector2.UnitY, assist.Resolve(-Vector2.UnitY));
        Assert.False(assist.SuppressTurnBoost);
    }

    [Theory]
    [InlineData(1, 0, 0, -1)]
    [InlineData(0, 1, 1, 1)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, -1, 0, 1)]
    public void CatchBrieflyPreservesApproachThenHonorsHeldInput(float x, float y, float inputX, float inputY)
    {
        var direction = new Vector2(x, y);
        var inheritedInput = new Vector2(inputX, inputY);
        if (inheritedInput != Vector2.Zero) inheritedInput = Vector2.Normalize(inheritedInput);
        var (assist, _, _) = Transfer(false, direction * 8, inheritedInput);
        Assert.Equal(direction, assist.Resolve(inheritedInput));
        assist.Tick(.04f);
        Assert.Equal(direction, assist.Resolve(inheritedInput));
        assist.Tick(.05f);
        Assert.Equal(inheritedInput, assist.Resolve(inheritedInput));
        Assert.False(assist.SuppressTurnBoost);
        Assert.True(assist.CueRemaining > 0);

        var freshInput = -Vector2.UnitX;
        Assert.Equal(freshInput, assist.Resolve(freshInput));
        Assert.False(assist.SuppressTurnBoost);
        Assert.Equal(inheritedInput, assist.Resolve(inheritedInput));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshCutOrReleaseOverridesAssistImmediately(bool run)
    {
        var (assist, _, _) = Transfer(run, Vector2.UnitX * 8, -Vector2.UnitY);
        Assert.Equal(-Vector2.UnitX, assist.Resolve(-Vector2.UnitX));
        var (second, _, _) = Transfer(run, Vector2.UnitY, -Vector2.UnitY);
        Assert.Equal(Vector2.Zero, second.Resolve(Vector2.Zero));
        Assert.Equal(-Vector2.UnitY, second.Resolve(-Vector2.UnitY));
    }

    [Fact]
    public void CatchPreservesCrossingMotionAndStationaryCatchStartsGentlyUpfield()
    {
        var (moving, _, _) = Transfer(false, Vector2.UnitX * 8, -Vector2.UnitX);
        Assert.Equal(Vector2.UnitX, moving.Resolve(-Vector2.UnitX));
        var (stationary, _, _) = Transfer(false, Vector2.Zero, Vector2.Zero);
        Assert.Equal(Vector2.UnitY * .5f, stationary.Resolve(Vector2.Zero));
        var (compatible, _, _) = Transfer(false, Vector2.UnitY, Vector2.UnitY);
        Assert.Equal(Vector2.UnitY, compatible.Resolve(Vector2.UnitY));
    }

    [Fact]
    public void CueDoesNotRestartEveryFrameAndResetClearsIt()
    {
        var (assist, ball, play) = Transfer(false, Vector2.UnitY, Vector2.Zero);
        assist.Tick(.4f);
        assist.Observe(ball, play, Vector2.Zero);
        Assert.InRange(assist.CueRemaining, .19f, .21f);
        assist.Reset();
        Assert.Null(assist.Carrier);
        Assert.Equal(0, assist.CueRemaining);
        Assert.Equal(-Vector2.UnitY, assist.Resolve(-Vector2.UnitY));
    }

    [Theory]
    [InlineData(1f / 30)]
    [InlineData(1f / 60)]
    [InlineData(1f / 120)]
    public void AutomaticHandoffProtectsHeldQbRetreatInFullPlayUpdate(float dt)
    {
        var manager = new PlayManager();
        manager.SelectRunPlay(1, new Random(1));
        var field = new FormationFactory().CreateFormation(manager.SelectedPlay, manager.LineOfScrimmage);
        RouteAssigner.AssignRoutes(field.Receivers, manager.SelectedPlay);
        var rb = field.Receivers.Single(r => r.Slot == manager.SelectedPlay.BallCarrierSlot);
        rb.Position = manager.SelectedPlay.Backfield.GetMeshPoint(field.Qb.Position);
        var input = new MovementInput { Direction = -Vector2.UnitY };
        var execution = new PlayExecutionController(input, new BlockingController());
        void Step() => execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], [], manager,
            false, false, false, _ => { }, dt);
        Step();
        Assert.Same(rb, field.Ball.Holder);
        Step();
        Assert.True(rb.Velocity.Y > 0);
        input.Direction = -Vector2.UnitX;
        Step();
        Assert.True(rb.Velocity.X < 0);
        Assert.Equal(0, rb.Velocity.Y);
    }

    [Theory]
    [InlineData(false, 1f / 30)]
    [InlineData(false, 1f / 60)]
    [InlineData(false, 1f / 120)]
    [InlineData(true, 1f / 60)]
    public void CatchBetweenUpdatesBrieflyProtectsRetreatButAcceptsImmediateSidelineCut(bool freshCut, float dt)
    {
        var manager = new PlayManager();
        var field = new FormationFactory().CreateFormation(manager.SelectedPlay, manager.LineOfScrimmage);
        RouteAssigner.AssignRoutes(field.Receivers, manager.SelectedPlay);
        var input = new MovementInput { Direction = -Vector2.UnitY };
        var execution = new PlayExecutionController(input, new BlockingController());
        void Step() => execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], [], manager,
            false, false, false, _ => { }, dt);
        Step();
        var receiver = field.Receivers[0];
        receiver.Position = new Vector2(Constants.FieldWidth - 2, 40);
        receiver.Velocity = Vector2.UnitY * receiver.Speed;
        receiver.HasBall = true;
        field.Ball.SetHeld(receiver, BallState.HeldByReceiver);
        execution.ObservePossession(field.Ball, manager);
        if (freshCut) input.Direction = -Vector2.UnitX;
        Step();
        if (freshCut) Assert.True(receiver.Velocity.X < 0);
        else Assert.True(receiver.Velocity.Y > 0);
        Assert.InRange(execution.Possession.CueRemaining, .5f, .6f);
        for (int frame = 1; frame < (int)MathF.Ceiling(.1f / dt); frame++) Step();
        if (freshCut) Assert.True(receiver.Velocity.X < 0);
        else Assert.True(receiver.Velocity.Y < 0);
    }

    private static (PossessionControl, Ball, ResolvedPlay) Transfer(bool run, Vector2 velocity, Vector2 input)
    {
        var manager = new PlayManager();
        if (run) manager.SelectRunPlay(0, new Random(1));
        var field = new FormationFactory().CreateFormation(manager.SelectedPlay, manager.LineOfScrimmage);
        RouteAssigner.AssignRoutes(field.Receivers, manager.SelectedPlay);
        var carrier = run ? field.Receivers.Single(r => r.Slot == manager.SelectedPlay.BallCarrierSlot) : field.Receivers[0];
        carrier.Velocity = velocity;
        field.Ball.SetHeld(carrier, BallState.HeldByReceiver);
        var assist = new PossessionControl();
        assist.Observe(field.Ball, manager.SelectedPlay, input);
        return (assist, field.Ball, manager.SelectedPlay);
    }

    private sealed class MovementInput : IPlayerMovementInput
    {
        public Vector2 Direction { get; set; }
        public Vector2 GetMovementDirection() => Direction;
        public bool IsSprintHeld() => false;
    }
}
