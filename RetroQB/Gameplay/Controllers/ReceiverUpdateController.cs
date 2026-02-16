using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles per-frame updates for all receivers during active play:
/// route running, controlled receiver movement, man-coverage shakes,
/// ball-tracking adjustments, and automatic blocking transitions.
/// </summary>
public sealed class ReceiverUpdateController
{
    private readonly BlockingController _blockingController;

    public ReceiverUpdateController(BlockingController blockingController)
    {
        _blockingController = blockingController;
    }

    /// <summary>
    /// Updates all receivers for the current frame.
    /// </summary>
    public void UpdateAll(
        IReadOnlyList<Receiver> receivers,
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Defender> defenders,
        Receiver? controlledReceiver,
        Vector2 inputDir,
        bool sprint,
        bool qbPastLos,
        bool isZoneCoverage,
        PlayManager playManager,
        float dt,
        Action<Entity> clampToField)
    {
        bool isRunPlayPreHandoff = IsRunPlayActivePreHandoff(playManager.SelectedPlayType, ball);
        bool isRunPlayWithRb = BlockingUtils.IsRunPlayActiveWithRunningBack(playManager.SelectedPlayType, ball);
        bool isBallHeldByReceiver = ball.State == BallState.HeldByReceiver;
        bool isPassCompletion = isBallHeldByReceiver && !isRunPlayWithRb;

        foreach (var receiver in receivers)
        {
            bool isBallCarrier = isBallHeldByReceiver && ball.Holder == receiver;

            // After a pass completion, non-catching receivers track toward the ball carrier
            if (isPassCompletion && !isBallCarrier && receiver != controlledReceiver)
            {
                UpdateReceiverTrackingBallCarrier(receiver, ball.Holder!, dt, clampToField);
                continue;
            }

            bool autoBlockWr = !receiver.IsBlocking
                && !receiver.IsRunningBack
                && !receiver.IsTightEnd
                && !isBallCarrier
                && receiver != controlledReceiver
                && (isRunPlayWithRb || isBallHeldByReceiver);

            if (receiver.IsBlocking || autoBlockWr)
            {
                _blockingController.UpdateBlockingReceiver(
                    receiver, qb, ball, defenders,
                    playManager.SelectedPlay, playManager.SelectedPlayType,
                    playManager.LineOfScrimmage, dt,
                    (pos, radius, preferRushers) => GetClosestDefender(defenders, pos, radius, preferRushers),
                    clampToField);
                receiver.Update(dt);
                clampToField(receiver);
                continue;
            }

            if (isRunPlayPreHandoff && receiver.IsRunningBack)
            {
                UpdateRunningBackPreHandoff(receiver, qb, dt, clampToField);
                continue;
            }

            if (receiver == controlledReceiver)
            {
                UpdateControlledReceiver(receiver, inputDir, sprint, isRunPlayWithRb, dt, clampToField);
                continue;
            }

            UpdateRouteReceiver(receiver, qb, ball, defenders, qbPastLos, isZoneCoverage, dt, clampToField);
        }
    }

    private void UpdateRunningBackPreHandoff(Receiver receiver, Quarterback qb, float dt, Action<Entity> clampToField)
    {
        Vector2 toQb = qb.Position - receiver.Position;
        float dist = toQb.Length();
        if (dist > 0.01f)
        {
            toQb /= dist;
        }

        float settleRange = 4.5f;
        float approachSpeed = receiver.Speed * 0.55f;
        if (dist <= settleRange)
        {
            receiver.Velocity = toQb * approachSpeed;
        }
        else
        {
            RouteRunner.UpdateRoute(receiver, dt);
        }

        receiver.Update(dt);
        clampToField(receiver);
    }

    private static void UpdateControlledReceiver(Receiver receiver, Vector2 inputDir, bool sprint, bool isRunPlayWithRb, float dt, Action<Entity> clampToField)
    {
        float carrierSpeed = sprint ? receiver.Speed * 1.15f : receiver.Speed;
        if (isRunPlayWithRb && receiver.IsRunningBack)
        {
            carrierSpeed *= 1.08f;
        }
        receiver.Velocity = inputDir * carrierSpeed;

        if (isRunPlayWithRb && receiver.IsRunningBack && inputDir.LengthSquared() > 0.001f)
        {
            Vector2 currentDir = receiver.Velocity.LengthSquared() > 0.001f
                ? Vector2.Normalize(receiver.Velocity)
                : inputDir;
            float turnDot = Vector2.Dot(currentDir, inputDir);
            if (turnDot < 0.45f)
            {
                receiver.Velocity += inputDir * (carrierSpeed * 0.35f);
            }
        }

        receiver.Update(dt);
        clampToField(receiver);
    }

    private void UpdateRouteReceiver(Receiver receiver, Quarterback qb, Ball ball, IReadOnlyList<Defender> defenders, bool qbPastLos, bool isZoneCoverage, float dt, Action<Entity> clampToField)
    {
        RouteRunner.UpdateRoute(receiver, dt);

        if (receiver.IsRunningBack && ball.State == BallState.HeldByQB && !qbPastLos)
        {
            ApplyRunningBackQbAvoidance(receiver, qb);
        }

        if (qbPastLos)
        {
            // Block for scrambling QB
            Defender? target = GetClosestDefender(defenders, receiver.Position, Constants.BlockEngageRadius, preferRushers: true);
            if (target != null)
            {
                Vector2 toTarget = target.Position - receiver.Position;
                if (toTarget.LengthSquared() > 0.001f)
                {
                    toTarget = Vector2.Normalize(toTarget);
                }
                receiver.Velocity = toTarget * (receiver.Speed * 0.9f);
            }
        }
        else if (ball.State == BallState.InAir && receiver.Eligible)
        {
            AdjustReceiverToBall(receiver, ball);
        }
        else if (!isZoneCoverage && receiver.Eligible)
        {
            Defender? manDefender = defenders.FirstOrDefault(d => d.CoverageReceiverIndex == receiver.Index);
            if (manDefender != null && manDefender.PositionRole == DefensivePosition.DB)
            {
                if (receiver.PositionRole == OffensivePosition.WR)
                {
                    float shakeSkill = receiver.TeamAttributes.GetReceiverCatchingAbility(receiver.Slot);
                    ApplyManCoverageShake(receiver, manDefender, shakeSkill);
                }
            }
        }

        receiver.Update(dt);
        clampToField(receiver);
    }

    private static void ApplyRunningBackQbAvoidance(Receiver receiver, Quarterback qb)
    {
        Vector2 currentVelocity = receiver.Velocity;
        if (currentVelocity.LengthSquared() < 0.001f)
        {
            return;
        }

        Vector2 toQb = qb.Position - receiver.Position;
        float distToQb = toQb.Length();
        const float avoidRadius = 3.1f;
        if (distToQb <= 0.001f || distToQb > avoidRadius)
        {
            return;
        }

        // Only steer when the RB is actually moving toward the QB.
        Vector2 toQbDir = toQb / distToQb;
        Vector2 moveDir = Vector2.Normalize(currentVelocity);
        float headingTowardQb = Vector2.Dot(moveDir, toQbDir);
        if (headingTowardQb < 0.15f)
        {
            return;
        }

        float awaySign = MathF.Sign(receiver.Position.X - qb.Position.X);
        if (awaySign == 0f)
        {
            awaySign = receiver.RouteSide != 0 ? receiver.RouteSide : 1f;
        }

        Vector2 lateralAway = new Vector2(awaySign, 0f);
        float proximity = 1f - Math.Clamp(distToQb / avoidRadius, 0f, 1f);
        float avoidWeight = Math.Clamp(proximity * 0.9f + headingTowardQb * 0.3f, 0f, 0.95f);

        Vector2 blended = moveDir * (1f - avoidWeight) + lateralAway * avoidWeight;
        if (blended.LengthSquared() > 0.001f)
        {
            blended = Vector2.Normalize(blended);
            receiver.Velocity = blended * receiver.Speed;
        }
    }

    private static void ApplyManCoverageShake(Receiver receiver, Defender defender, float shakeSkill)
    {
        float skillThreshold = 0.62f;
        if (shakeSkill < skillThreshold)
        {
            return;
        }

        Vector2 toDefender = defender.Position - receiver.Position;
        float distSq = toDefender.LengthSquared();
        if (distSq < 0.001f)
        {
            return;
        }

        float triggerDistance = 2.4f;
        if (distSq > triggerDistance * triggerDistance)
        {
            return;
        }

        Vector2 baseDir = receiver.Velocity.LengthSquared() > 0.001f
            ? Vector2.Normalize(receiver.Velocity)
            : new Vector2(0, 1);
        Vector2 toDefNorm = Vector2.Normalize(toDefender);
        float frontDot = Vector2.Dot(baseDir, toDefNorm);
        if (frontDot < 0.65f)
        {
            return;
        }

        float skillScale = Math.Clamp((shakeSkill - skillThreshold) / (0.92f - skillThreshold), 0.25f, 1.0f);
        Vector2 lateral = new Vector2(-baseDir.Y, baseDir.X);
        float awaySign = MathF.Sign(Vector2.Dot(lateral, receiver.Position - defender.Position));
        if (awaySign == 0)
        {
            awaySign = receiver.RouteSide != 0 ? receiver.RouteSide : 1;
        }

        Vector2 shakeDir = baseDir * (0.65f + 0.1f * skillScale) + lateral * (0.75f * awaySign * skillScale);
        if (shakeDir.LengthSquared() > 0.001f)
        {
            shakeDir = Vector2.Normalize(shakeDir);
        }

        receiver.Velocity = shakeDir * receiver.Speed;
    }

    private static void AdjustReceiverToBall(Receiver receiver, Ball ball)
    {
        Vector2 routeVelocity = receiver.Velocity;
        Vector2 predictedLanding = GetPredictedBallLanding(ball);
        float flightProgress = ball.GetFlightProgress();

        Vector2 ballPath = predictedLanding - ball.Position;
        Vector2 targetPos;

        // Track the nearest reachable point on the remaining ball path.
        // This makes receivers react to where the ball is actually going,
        // not just where their route would have taken them.
        if (ballPath.LengthSquared() > 0.001f)
        {
            float pathLenSq = ballPath.LengthSquared();
            float along = Vector2.Dot(receiver.Position - ball.Position, ballPath) / pathLenSq;
            along = Math.Clamp(along, 0f, 1f);
            Vector2 closestOnPath = ball.Position + ballPath * along;

            float lateBias = Math.Clamp(flightProgress * 0.85f, 0f, 1f);
            targetPos = Vector2.Lerp(closestOnPath, predictedLanding, lateBias);
        }
        else
        {
            targetPos = ball.Position;
        }

        Vector2 toTarget = targetPos - receiver.Position;
        float distToTarget = toTarget.Length();
        if (distToTarget > 0.001f)
        {
            toTarget /= distToTarget;
        }

        const float progressThreshold = 0.20f;
        const float distanceStartAdjust = 14f;
        const float distanceHardAdjust = 5f;

        bool closeEnough = distToTarget <= distanceStartAdjust;
        bool flightReady = flightProgress >= progressThreshold;
        bool inCatchWindow = distToTarget <= Constants.CatchRadius + 1.6f;

        if (!inCatchWindow && !(closeEnough && flightReady))
        {
            return;
        }

        Vector2 baseDir = routeVelocity.LengthSquared() > 0.001f
            ? Vector2.Normalize(routeVelocity)
            : toTarget;

        float distDenominator = Math.Max(0.01f, distanceStartAdjust - distanceHardAdjust);
        float distFactor = 1f - Math.Clamp((distToTarget - distanceHardAdjust) / distDenominator, 0f, 1f);
        float progressFactor = Math.Clamp((flightProgress - progressThreshold) / (1f - progressThreshold), 0f, 1f);
        float adjustWeight = Math.Clamp(distFactor * 0.75f + progressFactor * 0.25f, 0f, 0.92f);

        if (inCatchWindow)
        {
            adjustWeight = Math.Max(adjustWeight, 0.88f);
        }

        Vector2 blendedDir = baseDir * (1f - adjustWeight) + toTarget * adjustWeight;
        if (blendedDir.LengthSquared() > 0.001f)
        {
            blendedDir = Vector2.Normalize(blendedDir);
        }

        float speedMult = 1f + flightProgress * 0.10f;
        receiver.Velocity = blendedDir * (receiver.Speed * speedMult);
    }

    /// <summary>
    /// Projects where the ball will land based on its throw start, velocity direction,
    /// and intended distance.
    /// </summary>
    private static Vector2 GetPredictedBallLanding(Ball ball)
    {
        Vector2 velocity = ball.Velocity;
        if (velocity.LengthSquared() < 0.001f)
        {
            return ball.Position;
        }

        Vector2 throwDir = Vector2.Normalize(velocity);
        return ball.ThrowStart + throwDir * ball.IntendedDistance;
    }

    internal static Defender? GetClosestDefender(IReadOnlyList<Defender> defenders, Vector2 position, float maxDistance, bool preferRushers)
    {
        Defender? closest = null;
        float bestDistSq = maxDistance * maxDistance;

        IEnumerable<Defender> candidates = defenders;
        if (preferRushers)
        {
            var rushers = defenders.Where(d => d.IsRusher).ToList();
            if (rushers.Count > 0)
            {
                candidates = rushers;
            }
        }

        foreach (var defender in candidates)
        {
            float distSq = Vector2.DistanceSquared(position, defender.Position);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                closest = defender;
            }
        }

        return closest;
    }

    /// <summary>
    /// After a pass completion, non-catching receivers jog toward the ball carrier.
    /// </summary>
    private static void UpdateReceiverTrackingBallCarrier(Receiver receiver, Entity ballCarrier, float dt, Action<Entity> clampToField)
    {
        Vector2 trackDir = PlayExecutionController.GetTrackDirectionToward(receiver.Position, ballCarrier.Position);
        float trackSpeed = receiver.Speed * 0.75f;
        receiver.Velocity = trackDir * trackSpeed;
        receiver.Update(dt);
        clampToField(receiver);
    }

    private static bool IsRunPlayActivePreHandoff(PlayType selectedPlayType, Ball ball)
    {
        if (selectedPlayType != PlayType.Run) return false;
        return ball.State == BallState.HeldByQB;
    }
}
