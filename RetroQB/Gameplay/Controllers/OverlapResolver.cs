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
    }

    /// <summary>
    /// Marks a defender as having their tackle broken.
    /// </summary>
    public void AddBrokenTackleDefender(Defender defender)
    {
        _brokenTackleDefenders.Add(defender);
    }

    /// <summary>
    /// Checks if a defender has already had their tackle broken.
    /// </summary>
    public bool HasBrokenTackle(Defender defender)
    {
        return _brokenTackleDefenders.Contains(defender);
    }
    
    /// <summary>
    /// Checks if a defender who broke a tackle has separated enough from the carrier to re-engage.
    /// </summary>
    public bool CanReengageAfterBrokenTackle(Defender defender, Vector2 carrierPosition)
    {
        if (!_brokenTackleDefenders.Contains(defender))
        {
            return true;
        }
        
        float separation = Vector2.Distance(defender.Position, carrierPosition);
        if (separation >= ReengageSeparationDistance)
        {
            _brokenTackleDefenders.Remove(defender);
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Maximum distance past the line of scrimmage where defenders can physically
    /// impede (press/jam) receivers. Beyond this, only the defender is pushed.
    /// </summary>
    private const float PressZoneYards = 5f;

    /// <summary>
    /// How much of the overlap push goes to the defender vs the receiver
    /// inside the press zone (0 = all receiver, 1 = all defender).
    /// 0.8 means 80% of the push goes to the defender, 20% to the receiver.
    /// </summary>
    private const float PressReceiverAdvantage = 0.8f;

    public void ResolveOverlaps(
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Defender> defenders,
        float lineOfScrimmage,
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
                            // Check if carrier has moved far enough for defender to re-engage
                            if (CanReengageAfterBrokenTackle(defender, ballCarrier.Position))
                            {
                                // Defender can re-engage - don't push, allow tackle check
                                continue;
                            }
                            
                            PushBrokenTackleDefenderAway(defender, ballCarrier, clampToField);
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

                    // Enforce 5-yard press zone rule: beyond the zone,
                    // only the defender yields — the receiver runs free.
                    bool aIsReceiver = a is Receiver;
                    bool bIsReceiver = b is Receiver;
                    float pressLimit = lineOfScrimmage + PressZoneYards;

                    if (a is Receiver receiverA && b is Receiver receiverB
                        && !receiverA.IsBlocking && !receiverB.IsBlocking
                        && !receiverA.HasBall && !receiverB.HasBall)
                    {
                        ResolveRouteRunnerOverlap(receiverA, receiverB, minDist - dist, clampToField);
                        continue;
                    }

                    if (aIsReceiver && bIsDefender)
                    {
                        if (TryResolveReceiverDefenderOverlap((Receiver)a, (Defender)b, pushDir, push, pressLimit, receiverIsFirst: true, clampToField))
                        {
                            continue;
                        }
                    }
                    else if (bIsReceiver && aIsDefender)
                    {
                        if (TryResolveReceiverDefenderOverlap((Receiver)b, (Defender)a, pushDir, push, pressLimit, receiverIsFirst: false, clampToField))
                        {
                            continue;
                        }
                    }

                    a.Position -= pushDir * push;
                    b.Position += pushDir * push;
                    clampToField(a);
                    clampToField(b);
                }
            }
        }
    }

    private static void PushBrokenTackleDefenderAway(Defender defender, Entity ballCarrier, Action<Entity> clampToField)
    {
        Vector2 pushDir = defender.Position - ballCarrier.Position;
        if (pushDir.LengthSquared() <= 0.0001f)
        {
            Vector2 fallback = ballCarrier.Velocity;
            if (fallback.LengthSquared() <= 0.0001f)
            {
                fallback = new Vector2(0f, -1f);
            }

            pushDir = -Vector2.Normalize(fallback);
        }
        else
        {
            pushDir = Vector2.Normalize(pushDir);
        }

        float minSeparation = defender.Radius + ballCarrier.Radius + 0.6f;
        defender.Position = ballCarrier.Position + pushDir * minSeparation;
        defender.Velocity = pushDir * (defender.Speed * 0.6f);
        clampToField(defender);
    }

    private static bool TryResolveReceiverDefenderOverlap(
        Receiver receiver,
        Defender defender,
        Vector2 pairPushDir,
        float halfOverlap,
        float pressLimit,
        bool receiverIsFirst,
        Action<Entity> clampToField)
    {
        if (receiver.IsBlocking)
        {
            return false;
        }

        Vector2 receiverPushDir = receiverIsFirst ? -pairPushDir : pairPushDir;
        Vector2 defenderPushDir = -receiverPushDir;
        float totalOverlap = halfOverlap * 2f;

        if (receiver.Position.Y > pressLimit)
        {
            defender.Position += defenderPushDir * totalOverlap;
            clampToField(defender);
            return true;
        }

        float receiverPush = totalOverlap * (1f - PressReceiverAdvantage);
        float defenderPush = totalOverlap * PressReceiverAdvantage;
        receiver.Position += receiverPushDir * receiverPush;
        defender.Position += defenderPushDir * defenderPush;
        clampToField(receiver);
        clampToField(defender);
        return true;
    }

    private static void ResolveRouteRunnerOverlap(Receiver a, Receiver b, float overlap, Action<Entity> clampToField)
    {
        Vector2 avgVelocity = a.Velocity + b.Velocity;
        Vector2 forwardDir = avgVelocity.LengthSquared() > 0.001f
            ? Vector2.Normalize(avgVelocity)
            : new Vector2(0f, 1f);

        Vector2 lateralDir = new(-forwardDir.Y, forwardDir.X);
        if (lateralDir.LengthSquared() <= 0.0001f)
        {
            lateralDir = new Vector2(1f, 0f);
        }
        else
        {
            lateralDir = Vector2.Normalize(lateralDir);
        }

        Vector2 positionDelta = b.Position - a.Position;
        Vector2 normalDir = positionDelta.LengthSquared() > 0.0001f
            ? Vector2.Normalize(positionDelta)
            : lateralDir;
        float lateralSign = MathF.Sign(Vector2.Dot(positionDelta, lateralDir));
        if (lateralSign == 0f)
        {
            float routeStartSign = MathF.Sign(Vector2.Dot(b.RouteStart - a.RouteStart, lateralDir));
            lateralSign = routeStartSign != 0f ? routeStartSign : (a.Index < b.Index ? 1f : -1f);
        }

        float normalPush = overlap * 0.45f;
        float lateralPush = overlap * 0.65f;
        float forwardGap = MathF.Abs(Vector2.Dot(positionDelta, forwardDir));
        float forwardNudge = forwardGap < 0.35f ? overlap * 0.12f : 0f;
        float orderingSign = MathF.Sign(Vector2.Dot(positionDelta, forwardDir));
        if (orderingSign == 0f)
        {
            orderingSign = a.Index < b.Index ? 1f : -1f;
        }

        Vector2 separation = normalDir * normalPush + lateralDir * lateralSign * lateralPush;
        if (separation.LengthSquared() > 0.0001f)
        {
            separation = Vector2.Normalize(separation) * overlap;
        }
        else
        {
            separation = lateralDir * lateralSign * overlap;
        }

        Vector2 leadLag = forwardDir * orderingSign * forwardNudge;

        Vector2 halfSeparation = separation * 0.5f;
        Vector2 halfLeadLag = leadLag * 0.5f;

        a.Position -= halfSeparation;
        b.Position += halfSeparation;
        a.Position -= halfLeadLag;
        b.Position += halfLeadLag;

        clampToField(a);
        clampToField(b);
    }
}
