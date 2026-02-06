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

        // Predict where the ball will land based on throw trajectory
        Vector2 predictedLanding = GetPredictedBallLanding(ball);
        float flightProgress = ball.GetFlightProgress();

        // Blend between predicted landing (early) and actual ball position (late)
        float landingWeight = Math.Clamp(1f - flightProgress * 1.3f, 0f, 1f);
        Vector2 targetPos = Vector2.Lerp(ball.Position, predictedLanding, landingWeight);

        Vector2 toTarget = targetPos - receiver.Position;
        float distToTarget = toTarget.Length();
        if (distToTarget > 0.001f)
        {
            toTarget /= distToTarget;
        }

        // --- Early in the flight: keep running the route ---
        // Only begin adjusting once the ball is past 55% of its flight AND within 10 yards,
        // or if the ball is already very close (catch window).
        const float progressThreshold = 0.55f;
        const float distanceStartAdjust = 10f;   // begin gentle steering
        const float distanceHardAdjust = 4.5f;   // commit to the ball

        bool closeEnough = distToTarget <= distanceStartAdjust;
        bool flightLate = flightProgress >= progressThreshold;
        bool inCatchWindow = distToTarget <= Constants.CatchRadius + 1.5f;

        if (!inCatchWindow && !(closeEnough && flightLate))
        {
            // Keep running the route unmodified
            return;
        }

        bool ballAhead = targetPos.Y >= receiver.Position.Y - 1.0f;
        bool allowComeback = distToTarget <= Constants.CatchRadius + 1.2f;

        if (ballAhead || allowComeback)
        {
            Vector2 baseDir = routeVelocity.LengthSquared() > 0.001f
                ? Vector2.Normalize(routeVelocity)
                : toTarget;

            // Gentle ramp: 0 at distanceStartAdjust → 0.8 at distanceHardAdjust → ~1 at catch radius
            float distFactor = 1f - Math.Clamp((distToTarget - distanceHardAdjust) / (distanceStartAdjust - distanceHardAdjust), 0f, 1f);
            float progressFactor = Math.Clamp((flightProgress - progressThreshold) / (1f - progressThreshold), 0f, 1f);
            float adjustWeight = Math.Clamp(distFactor * 0.8f + progressFactor * 0.2f, 0f, 0.85f);

            // Once very close, lock on fully
            if (inCatchWindow)
            {
                adjustWeight = Math.Max(adjustWeight, 0.75f);
            }

            Vector2 blendedDir = baseDir * (1f - adjustWeight) + toTarget * adjustWeight;
            if (blendedDir.LengthSquared() > 0.001f)
            {
                blendedDir = Vector2.Normalize(blendedDir);
            }

            // Slight speed boost when tracking deep into flight to close distance
            float speedMult = 1f + flightProgress * 0.08f;
            receiver.Velocity = blendedDir * (receiver.Speed * speedMult);
        }
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
