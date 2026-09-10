using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

public enum BackfieldPhase { None, Approaching, Faking, Released, Completed, Aborted }

/// <summary>Owns exchange timing and possession transitions. Animation cannot trigger a handoff.</summary>
public sealed class BackfieldController
{
    private ResolvedPlay? _play;
    private Ball? _ball;
    private float _elapsed;
    private float _fakeElapsed;
    public BackfieldPhase Phase { get; private set; }
    public bool AllowsThrow => _play?.Backfield.Action != BackfieldAction.PlayAction
        || Phase is BackfieldPhase.Released or BackfieldPhase.Completed or BackfieldPhase.Aborted;
    public bool HoldsQuarterback => !AllowsThrow;
    public bool OpeningComplete => Phase is BackfieldPhase.None or BackfieldPhase.Released or BackfieldPhase.Completed or BackfieldPhase.Aborted;

    public void Reset()
    {
        _play = null;
        _ball = null;
        _elapsed = _fakeElapsed = 0;
        Phase = BackfieldPhase.None;
    }

    private void Ensure(ResolvedPlay play, Ball ball)
    {
        if (ReferenceEquals(_play, play) && ReferenceEquals(_ball, ball)) return;
        Reset();
        _play = play;
        _ball = ball;
        Phase = play.Backfield.Action == BackfieldAction.None ? BackfieldPhase.None : BackfieldPhase.Approaching;
    }

    public void Update(ResolvedPlay play, Ball ball, Quarterback qb, IReadOnlyList<Receiver> receivers, float dt)
    {
        Ensure(play, ball);
        if (!float.IsFinite(dt) || dt <= 0 || Phase is BackfieldPhase.None or BackfieldPhase.Completed or BackfieldPhase.Aborted) return;
        if (ball.State != BallState.HeldByQB || ball.Holder != qb) { Phase = BackfieldPhase.Completed; return; }
        _elapsed += dt;
        var plan = play.Backfield;
        if (plan.Action == BackfieldAction.Handoff) return;
        var participant = receivers.SingleOrDefault(r => r.Slot == plan.Participant);
        if (participant == null) { Phase = BackfieldPhase.Aborted; return; }
        if (Phase == BackfieldPhase.Released) return;
        if (Phase == BackfieldPhase.Faking)
        {
            _fakeElapsed += dt;
            if (_fakeElapsed >= plan.FakeDuration) Phase = BackfieldPhase.Released;
        }
        else if (_elapsed >= plan.MinimumDelay && AtMesh(plan, qb, participant))
        {
            Phase = BackfieldPhase.Faking;
            qb.Animation.Trigger(PlayerPose.Throwing, participant.Position - qb.Position);
            participant.Animation.Trigger(PlayerPose.Catching, qb.Position - participant.Position);
        }
        else if (_elapsed >= plan.ApproachTimeout)
            Phase = BackfieldPhase.Aborted; // A blocked fake must not lock the QB indefinitely.
    }

    public bool ControlsParticipant(ReceiverSlot slot) => _play?.Backfield.Participant == slot
        && Phase is BackfieldPhase.Approaching or BackfieldPhase.Faking;

    public void MoveParticipant(Receiver participant, Quarterback qb, float dt)
    {
        if (_play == null || !ControlsParticipant(participant.Slot)) return;
        participant.Velocity = Phase == BackfieldPhase.Faking ? Vector2.Zero : BlockingSteering.MoveTo(
            participant.Position, MeshPoint(_play.Backfield, qb), participant.Speed * _play.Backfield.SpeedMultiplier, dt);
    }

    public bool TryHandoff(ResolvedPlay play, Ball ball, Quarterback qb, IReadOnlyList<Receiver> receivers)
    {
        Ensure(play, ball);
        if (play.Backfield.Action != BackfieldAction.Handoff || Phase != BackfieldPhase.Approaching
            || ball.State != BallState.HeldByQB || ball.Holder != qb || _elapsed < play.Backfield.MinimumDelay) return false;
        var carrier = receivers.SingleOrDefault(r => r.Slot == play.Backfield.Participant);
        if (carrier == null || !AtMesh(play.Backfield, qb, carrier)) return false;
        carrier.HasBall = true;
        qb.HasBall = false;
        ball.SetHeld(carrier, BallState.HeldByReceiver);
        Phase = BackfieldPhase.Completed;
        return true;
    }

    private static Vector2 MeshPoint(BackfieldSequence plan, Quarterback qb) => new(
        Math.Clamp(qb.Position.X + plan.MeshOffset.X, Constants.ReceiverRadius, Constants.FieldWidth - Constants.ReceiverRadius),
        Math.Clamp(qb.Position.Y + plan.MeshOffset.Y, Constants.ReceiverRadius, Constants.FieldLength - Constants.ReceiverRadius));

    private static bool AtMesh(BackfieldSequence plan, Quarterback qb, Receiver participant) =>
        Vector2.Distance(qb.Position, participant.Position) <= 3.2f
        && Vector2.Distance(MeshPoint(plan, qb), participant.Position) <= plan.MeshRadius;
}
