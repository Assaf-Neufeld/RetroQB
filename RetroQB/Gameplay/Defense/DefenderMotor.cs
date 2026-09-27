using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public enum DefensiveIntentKind { Leverage, Help, Rush, Contain, Contest, Recovery, Pursuit, Hold, ReadStep, Spy, GapFit, Force, Cutback, LastSupport, Tackle }
public sealed record DefensiveIntent(DefenderSlot Actor, DefensiveIntentKind Kind, Vector2 Target,
    ReceiverId? Primary, ReceiverId? Helping, string Rule, string Reason, bool ActiveRusher);

/// <summary>Velocity planning only. Production contact controllers run before a single entity integration.</summary>
public static class DefenderMotor
{
    public static Vector2 Clamp(Vector2 p) => new(Math.Clamp(p.X, Constants.DefenderRadius, Constants.FieldWidth - Constants.DefenderRadius),
        Math.Clamp(p.Y, Constants.DefenderRadius, Constants.FieldLength - Constants.DefenderRadius));

    public static Vector2 Velocity(DefenderMotion actor, DefensiveIntent intent, IReadOnlyList<DefenderMotion> teammates, float speedMultiplier, float dt)
    {
        Vector2 delta = Clamp(intent.Target) - actor.Position;
        float distance = delta.Length();
        Vector2 direction = distance > .001f ? delta / distance : Vector2.Zero;
        foreach (var teammate in teammates)
        {
            if (teammate.Id == actor.Id) continue;
            Vector2 offset = actor.Position - teammate.Position;
            float separation = offset.Length();
            if (separation < actor.Radius + teammate.Radius + .75f)
            {
                Vector2 away = separation > .01f ? offset / separation : actor.Id < teammate.Id ? -Vector2.UnitX : Vector2.UnitX;
                direction += away * Math.Clamp((actor.Radius + teammate.Radius + .75f - separation) / 2, 0, .6f);
            }
        }
        if (direction.LengthSquared() > 1) direction = Vector2.Normalize(direction);
        float maxSpeed = actor.Speed * speedMultiplier;
        float desiredSpeed = maxSpeed * Math.Clamp(distance / DefensiveTuning.ArrivalRadius, 0, 1);
        if (direction.LengthSquared() > .001f && actor.Velocity.LengthSquared() > .01f)
        {
            float oldAngle = MathF.Atan2(actor.Velocity.Y, actor.Velocity.X), newAngle = MathF.Atan2(direction.Y, direction.X);
            float turn = MathF.IEEERemainder(newAngle - oldAngle, MathF.Tau);
            float angle = oldAngle + Math.Clamp(turn, -DefensiveTuning.TurnRadiansPerSecond * dt, DefensiveTuning.TurnRadiansPerSecond * dt);
            direction = new(MathF.Cos(angle), MathF.Sin(angle));
        }
        float speed = Math.Clamp(desiredSpeed, MathF.Max(0, actor.Velocity.Length() - DefensiveTuning.Acceleration * dt),
            actor.Velocity.Length() + DefensiveTuning.Acceleration * dt);
        return direction * MathF.Min(speed, maxSpeed);
    }
}
