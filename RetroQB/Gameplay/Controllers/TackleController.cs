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
    public PlayContact? LastTerminal { get; private set; }

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
        LastTerminal = null;
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
        Action<Entity> clampToField,
        float? lineOfScrimmage = null)
    {
        LastTerminal = null;
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
            LastTerminal = new(carrier.Position.Y <= FieldGeometry.EndZoneDepth
                ? PlayEndReason.Safety : PlayEndReason.OutOfBounds, carrier.Position);
            return TackleCheckResult.Tackle;
        }

        // Check touchdown
        if (Rules.IsTouchdown(carrier.Position))
        {
            LastTerminal = new(PlayEndReason.Touchdown, carrier.Position);
            return TackleCheckResult.Touchdown;
        }

        // Check defender contact
        foreach (var defender in defenders)
        {
            if (Vector2.Distance(defender.Position, carrier.Position) <= defender.Radius + carrier.Radius)
            {
                // Defenders actively being blocked have a reduced chance of making the tackle
                if (defender.IsBeingBlocked)
                {
                    float blockedTackleChance = 0.15f; // Only 15% chance to tackle while blocked
                    if (_rng.NextDouble() >= blockedTackleChance)
                    {
                        continue; // Block absorbed the tackle attempt
                    }
                }

                // Check for tackle break if carrier is a running back
                if (carrier is Receiver receiver && receiver.IsRunningBack)
                {
                    if (TryBreakTackle(defender, receiver, offensiveTeam, clampToField))
                    {
                        continue;
                    }
                }

                carrier.Animation.Trigger(PlayerPose.Tackled, defender.Position - carrier.Position);
                defender.Animation.Trigger(PlayerPose.Tackled, carrier.Position - defender.Position);
                var reason = carrier.Position.Y <= FieldGeometry.EndZoneDepth ? PlayEndReason.Safety
                    : carrier is Quarterback && lineOfScrimmage.HasValue && carrier.Position.Y < lineOfScrimmage
                    ? PlayEndReason.Sack : PlayEndReason.Tackle;
                LastTerminal = new(reason, carrier.Position, defender.Slot);
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
        // Check if defender who previously broke a tackle has separated enough to re-engage
        if (_overlapResolver.HasBrokenTackle(defender))
        {
            // Check if they've moved far enough to get another tackle attempt
            if (!_overlapResolver.CanReengageAfterBrokenTackle(defender, ballCarrier))
            {
                // Still too close to the break point - can't tackle yet
                return true;
            }
            // Otherwise, they've re-engaged and can attempt a new tackle below
        }

        float breakChance = offensiveTeam.GetRbTackleBreakChance(ballCarrier.Slot);
        
        // Adjust break chance based on defender's tackle ability (LBs are better tacklers)
        float defenderTackleAbility = defender.TeamAttributes.GetEffectiveTackleAbility(defender.PositionRole) * defender.TackleMultiplier;
        breakChance = breakChance / defenderTackleAbility;  // Higher tackle ability = lower break chance
        breakChance = Math.Clamp(breakChance, 0.05f, 0.65f);
        
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
