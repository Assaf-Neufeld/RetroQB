using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles tackle resolution, tackle breaks, and scoring detection.
/// </summary>
public sealed class TackleController
{
    private readonly Random _rng;
    private readonly OverlapResolver _overlapResolver;

    public TackleController(Random rng, OverlapResolver overlapResolver)
    {
        _rng = rng;
        _overlapResolver = overlapResolver;
    }

    /// <summary>
    /// Resets tackle state for a new play.
    /// </summary>
    public void Reset()
    {
        _overlapResolver.Reset();
    }

    /// <summary>
    /// Checks for tackle, touchdown, or out-of-bounds.
    /// </summary>
    public TackleCheckResult CheckTackleOrScore(
        Ball ball,
        Quarterback qb,
        IReadOnlyList<Defender> defenders,
        OffensiveTeamAttributes offensiveTeam,
        Action<Entity> clampToField)
    {
        Entity? carrier = ball.State switch
        {
            BallState.HeldByQB => qb,
            BallState.HeldByReceiver => ball.Holder,
            _ => null
        };

        if (carrier == null)
        {
            return TackleCheckResult.None;
        }

        // Check sideline out of bounds
        if (IsSidelineOutOfBounds(carrier.Position))
        {
            return TackleCheckResult.Tackle;
        }

        // Check touchdown
        if (Rules.IsTouchdown(carrier.Position))
        {
            return TackleCheckResult.Touchdown;
        }

        // Check defender contact
        foreach (var defender in defenders)
        {
            if (Vector2.Distance(defender.Position, carrier.Position) <= defender.Radius + carrier.Radius)
            {
                // Check for tackle break if carrier is a running back
                if (carrier is Receiver receiver && receiver.IsRunningBack)
                {
                    if (TryBreakTackle(defender, receiver, offensiveTeam, clampToField))
                    {
                        continue;
                    }
                }

                return TackleCheckResult.Tackle;
            }
        }

        return TackleCheckResult.None;
    }

    private bool TryBreakTackle(
        Defender defender,
        Receiver ballCarrier,
        OffensiveTeamAttributes offensiveTeam,
        Action<Entity> clampToField)
    {
        // Each defender only gets one tackle attempt per play
        if (_overlapResolver.HasBrokenTackle(defender))
        {
            return true;
        }

        float breakChance = offensiveTeam.GetRbTackleBreakChance(ballCarrier.Slot);
        if (_rng.NextDouble() < breakChance)
        {
            _overlapResolver.AddBrokenTackleDefender(defender);

            // Push defender away from the ball carrier
            if (ballCarrier != null)
            {
                Vector2 pushDir = defender.Position - ballCarrier.Position;
                if (pushDir.LengthSquared() <= 0.001f)
                {
                    Vector2 fallback = ballCarrier.Velocity;
                    if (fallback.LengthSquared() <= 0.001f)
                    {
                        fallback = new Vector2(0, -1f);
                    }
                    pushDir = -Vector2.Normalize(fallback);
                }
                else
                {
                    pushDir = Vector2.Normalize(pushDir);
                }

                float minSeparation = defender.Radius + ballCarrier.Radius + 0.7f;
                if (pushDir.Y < 0f)
                {
                    minSeparation += 0.8f;
                }

                defender.Position = ballCarrier.Position + pushDir * minSeparation;
                defender.Velocity = pushDir * (defender.Speed * 0.6f);
                clampToField(defender);
            }

            return true;
        }

        return false;
    }

    private static bool IsSidelineOutOfBounds(Vector2 position)
    {
        return position.X < 0 || position.X > Constants.FieldWidth;
    }
}

public enum TackleCheckResult
{
    None,
    Tackle,
    Touchdown
}
