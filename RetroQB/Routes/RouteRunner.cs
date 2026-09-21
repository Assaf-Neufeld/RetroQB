using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Routes;

/// <summary>Follows explicit route stages using actual position, not an upfield-distance approximation.</summary>
public static class RouteRunner
{
    public static void UpdateRoute(Receiver receiver, float dt)
    {
        if (!float.IsFinite(dt) || dt <= 0) return;
        UpdateRouteVelocity(receiver, dt);
        receiver.RouteState.ExpectedPosition = receiver.Position + receiver.Velocity * dt;
    }

    private static void UpdateRouteVelocity(Receiver receiver, float dt)
    {
        var path = RouteGeometry.GetPath(receiver);
        var state = receiver.RouteState;
        bool displaced = state.ExpectedPosition is Vector2 expected
            && Vector2.DistanceSquared(expected, receiver.Position) > 0.0001f;
        if (state.LastPosition is Vector2 previous)
            receiver.RouteProgress += Vector2.Distance(previous, receiver.Position);
        state.LastPosition = receiver.Position;
        if (receiver.IsBlocking) { receiver.Velocity = Vector2.Zero; return; }
        if (receiver.HasBall)
        {
            receiver.Velocity = GetBallCarrierDirection(receiver) * receiver.Speed;
            return;
        }
        if (state.Phase == RoutePhase.Scrambling)
        {
            MoveWithinField(receiver, Vector2.Normalize(new Vector2(receiver.RouteSide * 0.35f, 0.65f)), dt);
            return;
        }
        receiver.Velocity = Vector2.Zero;
        float remaining = dt;
        if (state.DelayElapsed < path.Definition.DelaySeconds)
        {
            state.Phase = RoutePhase.Waiting;
            float wait = MathF.Min(remaining, path.Definition.DelaySeconds - state.DelayElapsed);
            state.DelayElapsed += wait;
            remaining -= wait;
            if (remaining <= 0) return;
        }
        while (state.StepIndex < path.Definition.Steps.Count)
        {
            Vector2 target = path.Points[state.StepIndex + 1];
            Vector2 delta = target - receiver.Position;
            float distance = delta.Length();
            // Avoidance/contact may keep a runner just off a turn's exact point.
            // Accept a half-yard turn radius, while holds and endpoints stay exact.
            bool intermediateTurn = state.StepIndex < path.Definition.Steps.Count - 1
                && path.Definition.Steps[state.StepIndex].HoldSeconds == 0;
            float arrivalRadius = displaced && intermediateTurn ? 0.5f : 0.001f;
            if (distance > arrivalRadius)
            {
                state.Phase = RoutePhase.Running;
                receiver.Velocity = delta / distance * MathF.Min(receiver.Speed * remaining, distance) / dt;
                return;
            }
            float hold = path.Definition.Steps[state.StepIndex].HoldSeconds;
            if (state.HoldElapsed < hold)
            {
                state.Phase = RoutePhase.Holding;
                float wait = MathF.Min(remaining, hold - state.HoldElapsed);
                state.HoldElapsed += wait;
                remaining -= wait;
                if (remaining <= 0) return;
            }
            state.StepIndex++;
            state.HoldElapsed = 0;
        }
        if (path.Definition.Finish == RouteFinish.Settle)
        {
            state.Phase = RoutePhase.Settled;
            return;
        }
        state.Phase = RoutePhase.Continuing;
        Vector2 direction = Vector2.Zero;
        for (int i = path.Points.Count - 1; i > 0 && direction.LengthSquared() < 0.001f; i--)
            direction = path.Points[i] - path.Points[i - 1];
        if (direction.LengthSquared() < 0.001f) { state.Phase = RoutePhase.Completed; return; }
        // Keep an outlet alive when a crossing or outward route reaches the sideline.
        bool atSideline = direction.X < 0 && receiver.Position.X <= Constants.ReceiverRadius + 0.001f
            || direction.X > 0 && receiver.Position.X >= Constants.FieldWidth - Constants.ReceiverRadius - 0.001f;
        if (atSideline && direction.Y >= 0) direction = Vector2.UnitY;
        MoveWithinField(receiver, Vector2.Normalize(direction), dt, remaining);
    }

    public static void RequestScramble(Receiver receiver)
    {
        _ = RouteGeometry.GetPath(receiver);
        receiver.RouteState.Phase = RoutePhase.Scrambling;
    }

    internal static Vector2 GetBallCarrierDirection(Receiver receiver) =>
        Vector2.Normalize(new Vector2(receiver.IsRunningBack ? receiver.RouteSide * 0.55f : 0, 1));

    internal static Vector2 GetBallCarrierDirection(Receiver receiver, RetroQB.Gameplay.ResolvedPlay play) =>
        play.RunConcept is RetroQB.Gameplay.RunConcept.Dive or RetroQB.Gameplay.RunConcept.Draw
            ? Vector2.UnitY : GetBallCarrierDirection(receiver);

    private static void MoveWithinField(Receiver receiver, Vector2 direction, float dt, float? remaining = null)
    {
        Vector2 target = RoutePath.Clamp(receiver.Position + direction * receiver.Speed * (remaining ?? dt));
        receiver.Velocity = (target - receiver.Position) / dt;
        if (receiver.Velocity.LengthSquared() < 0.001f) receiver.RouteState.Phase = RoutePhase.Completed;
    }
}
