using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Resolves physical overlaps between entities to prevent stacking.
/// </summary>
public sealed class OverlapResolver
{
    private readonly HashSet<Defender> _brokenTackleDefenders = new();
    private readonly Dictionary<Defender, Vector2> _brokenTacklePositions = new();
    
    /// <summary>
    /// Minimum separation distance required before a defender can attempt another tackle.
    /// </summary>
    private const float ReengageSeparationDistance = 3.5f;

    /// <summary>
    /// Clears the set of defenders who have had their tackles broken this play.
    /// </summary>
    public void Reset()
    {
        _brokenTackleDefenders.Clear();
        _brokenTacklePositions.Clear();
    }

    /// <summary>
    /// Marks a defender as having their tackle broken at a specific position.
    /// </summary>
    public void AddBrokenTackleDefender(Defender defender, Vector2 breakPosition)
    {
        _brokenTackleDefenders.Add(defender);
        _brokenTacklePositions[defender] = breakPosition;
    }

    /// <summary>
    /// Checks if a defender has already had their tackle broken.
    /// </summary>
    public bool HasBrokenTackle(Defender defender)
    {
        return _brokenTackleDefenders.Contains(defender);
    }
    
    /// <summary>
    /// Checks if a defender who broke a tackle has separated enough to re-engage.
    /// If the defender has moved far enough from where they broke the tackle, they can try again.
    /// </summary>
    public bool CanReengageAfterBrokenTackle(Defender defender, Vector2 currentCarrierPosition)
    {
        if (!_brokenTacklePositions.TryGetValue(defender, out var breakPosition))
        {
            return true; // No record, can engage
        }
        
        float distanceFromBreak = Vector2.Distance(currentCarrierPosition, breakPosition);
        if (distanceFromBreak >= ReengageSeparationDistance)
        {
            // Enough separation - remove from broken tackle set to allow fresh attempt
            _brokenTackleDefenders.Remove(defender);
            _brokenTacklePositions.Remove(defender);
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Resolves overlaps between all entities.
    /// </summary>
    public void ResolveOverlaps(
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Defender> defenders,
        Action<Entity> clampToField)
    {
        // Determine who is carrying the ball
        Entity? ballCarrier = ball.State switch
        {
            BallState.HeldByQB => qb,
            BallState.HeldByReceiver => ball.Holder,
            _ => null
        };

        var entities = new List<Entity>(receivers.Count + defenders.Count + blockers.Count + 1)
        {
            qb
        };
        entities.AddRange(receivers);
        entities.AddRange(blockers);
        entities.AddRange(defenders);

        for (int i = 0; i < entities.Count; i++)
        {
            for (int j = i + 1; j < entities.Count; j++)
            {
                Entity a = entities[i];
                Entity b = entities[j];

                // Skip overlap resolution between ball carrier and defenders (allows tackles)
                bool aIsDefender = a is Defender;
                bool bIsDefender = b is Defender;
                if (ballCarrier != null)
                {
                    if ((a == ballCarrier && bIsDefender) || (b == ballCarrier && aIsDefender))
                    {
                        var defender = aIsDefender ? (Defender)a : (Defender)b;
                        if (_brokenTackleDefenders.Contains(defender))
                        {
                            Entity carrier = a == ballCarrier ? a : b;
                            Vector2 deltaToDefender = defender.Position - carrier.Position;
                            if (deltaToDefender.LengthSquared() > 0.0001f)
                            {
                                Vector2 pushDir = Vector2.Normalize(deltaToDefender);
                                float minSeparation = defender.Radius + carrier.Radius + 0.6f;
                                defender.Position = carrier.Position + pushDir * minSeparation;
                                defender.Velocity = pushDir * (defender.Speed * 0.6f);
                                clampToField(defender);
                            }
                            continue;
                        }
                        continue;
                    }
                }

                // Don't let a blocking RB shove the QB around
                if ((a == qb && b is Receiver rbB && rbB.IsRunningBack && rbB.IsBlocking) ||
                    (b == qb && a is Receiver rbA && rbA.IsRunningBack && rbA.IsBlocking))
                {
                    continue;
                }

                Vector2 delta = b.Position - a.Position;
                float minDist = a.Radius + b.Radius + 0.05f;
                float distSq = delta.LengthSquared();
                if (distSq <= 0.0001f) continue;

                float dist = MathF.Sqrt(distSq);
                if (dist < minDist)
                {
                    Vector2 pushDir = delta / dist;
                    float push = (minDist - dist) * 0.5f;
                    a.Position -= pushDir * push;
                    b.Position += pushDir * push;
                    clampToField(a);
                    clampToField(b);
                }
            }
        }
    }
}
