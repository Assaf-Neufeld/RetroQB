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
        : base(position, Constants.QbRadius, "QB", ResolveQbColor(teamAttributes))
    {
        TeamAttributes = teamAttributes ?? OffensiveTeamAttributes.Default;
        Color = ResolveQbColor(TeamAttributes);
    }

    private static Color ResolveQbColor(OffensiveTeamAttributes? teamAttributes)
    {
        var color = teamAttributes?.PrimaryColor ?? Palette.QB;
        return IsDefenseRed(color) ? Palette.QB : color;
    }

    private static bool IsDefenseRed(Color color)
    {
        // Check if color is "reddish" enough to be confused with defense red
        // Defense red is around (220, 70, 60) - we detect similar reds
        return color.R > 180 && color.G < 120 && color.B < 120;
    }

    public void ApplyInput(Vector2 inputDir, bool sprinting, bool aimMode, float dt)
    {
        float maxSpeed = sprinting ? TeamAttributes.GetQbSprintSpeed() : TeamAttributes.GetQbMaxSpeed();
        if (aimMode)
        {
            maxSpeed *= 0.55f;
        }

        if (inputDir.LengthSquared() > 0.001f)
        {
            inputDir = Vector2.Normalize(inputDir);
            Velocity += inputDir * TeamAttributes.GetQbAcceleration() * dt;
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
                Vector2 decel = Vector2.Normalize(Velocity) * TeamAttributes.GetQbFriction() * dt;
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
