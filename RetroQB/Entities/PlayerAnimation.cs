using System.Numerics;

namespace RetroQB.Entities;

public enum PlayerPose { Normal, Throwing, Catching, Tackled }

public readonly record struct PlayerVisualFrame(
    PlayerPose Pose, float ActionTime, float MotionTime, Vector2 Facing);

/// <summary>Presentation state only; never delays a throw or changes a collision.</summary>
public sealed class PlayerAnimation
{
    public PlayerVisualFrame Frame { get; private set; } = new(PlayerPose.Normal, 0f, 0f, Vector2.UnitY);

    public void Trigger(PlayerPose pose, Vector2 direction)
    {
        Frame = Frame with { Pose = pose, ActionTime = 0f,
            Facing = direction.LengthSquared() > 0.01f ? Vector2.Normalize(direction) : Frame.Facing };
    }

    public void Update(float dt, Vector2 velocity)
    {
        Frame = Advance(Frame, dt, velocity);
    }

    public static PlayerVisualFrame Advance(PlayerVisualFrame frame, float dt, Vector2 velocity)
    {
        dt = Math.Max(0f, dt);
        float actionTime = frame.ActionTime + dt;
        PlayerPose pose = frame.Pose;
        if ((pose == PlayerPose.Throwing && actionTime >= 0.36f)
            || (pose == PlayerPose.Catching && actionTime >= 0.30f))
            pose = PlayerPose.Normal;
        bool moving = velocity.LengthSquared() > 0.35f && pose != PlayerPose.Tackled;
        return new PlayerVisualFrame(pose, actionTime,
            frame.MotionTime + (moving ? dt : 0f),
            moving && pose == PlayerPose.Normal ? Vector2.Normalize(velocity) : frame.Facing);
    }
}
