using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Entities;

public sealed class Quarterback : Entity
{
    public bool HasBall { get; set; } = true;
    
    /// <summary>
    /// Reference to the team attributes for this quarterback.
    /// </summary>
    public OffensiveTeamAttributes TeamAttributes { get; }

    public Quarterback(Vector2 position, OffensiveTeamAttributes? teamAttributes = null) 
        : base(position, Constants.QbRadius, "QB", Palette.QB)
    {
        TeamAttributes = teamAttributes ?? OffensiveTeamAttributes.Default;
    }

    public void ApplyInput(Vector2 inputDir, bool sprinting, bool aimMode, float dt)
    {
        float maxSpeed = sprinting ? TeamAttributes.QbSprintSpeed : TeamAttributes.QbMaxSpeed;
        if (aimMode)
        {
            maxSpeed *= 0.55f;
        }

        if (inputDir.LengthSquared() > 0.001f)
        {
            inputDir = Vector2.Normalize(inputDir);
            Velocity += inputDir * TeamAttributes.QbAcceleration * dt;
            if (Velocity.Length() > maxSpeed)
            {
                Velocity = Vector2.Normalize(Velocity) * maxSpeed;
            }
        }
        else
        {
            float speed = Velocity.Length();
            if (speed > 0.1f)
            {
                Vector2 decel = Vector2.Normalize(Velocity) * TeamAttributes.QbFriction * dt;
                if (decel.Length() > speed)
                {
                    Velocity = Vector2.Zero;
                }
                else
                {
                    Velocity -= decel;
                }
            }
            else
            {
                Velocity = Vector2.Zero;
            }
        }
    }
}
