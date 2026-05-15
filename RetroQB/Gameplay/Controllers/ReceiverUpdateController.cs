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
        bool isUnderneathManCoverage,
        PlayManager playManager,
        float dt,
        Action<Entity> clampToField)
    {
        bool isRunPlayPreHandoff = IsRunPlayActivePreHandoff(playManager.SelectedPlayType, ball);
        bool isRunPlayWithRb = BlockingUtils.IsRunPlayActiveWithRunningBack(playManager.SelectedPlayType, ball);
        bool isBallHeldByReceiver = ball.State == BallState.HeldByReceiver;
        bool isPassCompletion = isBallHeldByReceiver && !isRunPlayWithRb;
        int selectedReceiver = playManager.SelectedReceiver;

        foreach (var receiver in receivers)
        {
            bool isBallCarrier = isBallHeldByReceiver && ball.Holder == receiver;

            if (ShouldBlockReceiver(receiver, isRunPlayWithRb, isBallHeldByReceiver, isBallCarrier, controlledReceiver))
            {
                _blockingController.UpdateBlockingReceiver(
                    receiver, qb, ball, defenders,
                    playManager.SelectedPlay, playManager.SelectedPlayType,
                    playManager.LineOfScrimmage, dt,
                    (pos, radius, preferRushers) => BlockingUtils.GetClosestDefender(defenders, pos, radius, preferRushers),
                    clampToField);
                receiver.Update(dt);
                clampToField(receiver);
                continue;
            }

            // After a pass completion, non-blocking receivers flow toward the ball carrier.
            if (isPassCompletion && !isBallCarrier && receiver != controlledReceiver)
            {
                UpdateReceiverTrackingBallCarrier(receiver, ball.Holder!, dt, clampToField);
                continue;
            }

            if (isRunPlayPreHandoff && receiver.IsRunningBack)
            {
                UpdateRunningBackPreHandoff(receiver, qb, playManager.SelectedPlay, dt, clampToField);
                continue;
            }

            if (receiver == controlledReceiver)
            {
                UpdateControlledReceiver(receiver, inputDir, sprint, isRunPlayWithRb, dt, clampToField);
                continue;
            }

            UpdateRouteReceiver(receiver, qb, ball, defenders, qbPastLos, isUnderneathManCoverage, selectedReceiver, dt, clampToField);
        }
    }

    private static bool ShouldBlockReceiver(Receiver receiver, bool isRunPlayWithRb, bool isBallHeldByReceiver, bool isBallCarrier, Receiver? controlledReceiver)
    {
        if (receiver.IsBlocking)
        {
            return true;
        }

        if (isBallCarrier || receiver == controlledReceiver || receiver.IsRunningBack || receiver.IsTightEnd)
        {
            return false;
        }

        return isRunPlayWithRb || isBallHeldByReceiver;
    }

    private void UpdateRunningBackPreHandoff(Receiver receiver, Quarterback qb, PlayDefinition selectedPlay, float dt, Action<Entity> clampToField)
    {
        Vector2 meshTarget = GetRunningBackMeshTarget(qb, selectedPlay);
        Vector2 toMesh = meshTarget - receiver.Position;
        float dist = toMesh.Length();
        Vector2 meshDir = dist > 0.01f ? toMesh / dist : Vector2.Zero;
        float approachSpeed = receiver.Speed * GetRunningBackMeshSpeedMultiplier(selectedPlay.RunConcept);
        receiver.Velocity = meshDir * approachSpeed;

        if (selectedPlay.RunConcept == RunConcept.Counter && selectedPlay.RunningBackSide != 0)
        {
            receiver.Velocity += new Vector2(-selectedPlay.RunningBackSide * receiver.Speed * 0.08f, receiver.Speed * 0.05f);
        }

        if (selectedPlay.RunConcept == RunConcept.Draw && dist < 1.2f)
        {
            receiver.Velocity *= 0.7f;
        }

        receiver.Update(dt);
        clampToField(receiver);
    }

    private static Vector2 GetRunningBackMeshTarget(Quarterback qb, PlayDefinition selectedPlay)
    {
        int runSide = Math.Sign(selectedPlay.RunningBackSide);
        Vector2 offset = selectedPlay.RunConcept switch
        {
            RunConcept.Dive => new Vector2(0f, -0.65f),
            RunConcept.Power => new Vector2(runSide * 0.85f, -0.25f),
            RunConcept.Counter => new Vector2(-runSide * 0.95f, -0.2f),
            RunConcept.Sweep => new Vector2(runSide * 1.45f, -0.1f),
            RunConcept.Stretch => new Vector2(runSide * 1.1f, -0.2f),
            RunConcept.Draw => new Vector2(0f, -1.05f),
            _ => new Vector2(runSide * 0.6f, -0.3f)
        };

        Vector2 target = qb.Position + offset;
        if (selectedPlay.RunConcept == RunConcept.Counter && runSide != 0)
        {
            target.X += runSide * 0.35f;
        }

        return target;
    }

    private static float GetRunningBackMeshSpeedMultiplier(RunConcept runConcept)
    {
        return runConcept switch
        {
            RunConcept.Dive => 0.62f,
            RunConcept.Power => 0.57f,
            RunConcept.Counter => 0.52f,
            RunConcept.Sweep => 0.66f,
            RunConcept.Stretch => 0.6f,
            RunConcept.Draw => 0.46f,
            _ => 0.55f
        };
    }

    private static void UpdateControlledReceiver(Receiver receiver, Vector2 inputDir, bool sprint, bool isRunPlayWithRb, float dt, Action<Entity> clampToField)
    {
        float carrierSpeed = sprint ? receiver.Speed * 1.15f : receiver.Speed;
        if (isRunPlayWithRb && receiver.IsRunningBack)
        {
            carrierSpeed *= 1.08f;
        }

        Vector2 previousVelocity = receiver.Velocity;
        receiver.Velocity = inputDir * carrierSpeed;

        if (isRunPlayWithRb && receiver.IsRunningBack && inputDir.LengthSquared() > 0.001f)
        {
            Vector2 previousDir = previousVelocity.LengthSquared() > 0.001f
                ? Vector2.Normalize(previousVelocity)
                : inputDir;
            float turnDot = Vector2.Dot(previousDir, inputDir);
            if (turnDot < 0.45f)
            {
                receiver.Velocity += inputDir * (carrierSpeed * 0.35f);
            }
        }

        receiver.Update(dt);
        clampToField(receiver);
    }

    private void UpdateRouteReceiver(Receiver receiver, Quarterback qb, Ball ball, IReadOnlyList<Defender> defenders, bool qbPastLos, bool isUnderneathManCoverage, int selectedReceiver, float dt, Action<Entity> clampToField)
    {
        RouteRunner.UpdateRoute(receiver, dt);

        if (receiver.IsRunningBack && ball.State == BallState.HeldByQB && !qbPastLos)
        {
            ApplyRunningBackQbAvoidance(receiver, qb);
        }

        if (qbPastLos)
        {
            // Block for scrambling QB
            Defender? target = BlockingUtils.GetClosestDefender(defenders, receiver.Position, Constants.BlockEngageRadius, preferRushers: true);
            if (target != null)
            {
                Vector2 toTarget = BlockingUtils.SafeNormalize(target.Position - receiver.Position);
                receiver.Velocity = toTarget * (receiver.Speed * 0.9f);
            }
        }
        else if (ball.State == BallState.InAir && receiver.Eligible && receiver.Index == selectedReceiver)
        {
            AdjustReceiverToBall(receiver, ball);
        }
        else if (isUnderneathManCoverage && receiver.Eligible)
        {
            Defender? manDefender = defenders.FirstOrDefault(d => d.CoverageReceiverIndex == receiver.Index);
            if (manDefender != null && manDefender.PositionRole == DefensivePosition.DB)
            {
                if (receiver.PositionRole == OffensivePosition.WR)
                {
                    float shakeSkill = receiver.TeamAttributes.GetReceiverSkill(receiver.Slot);
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
        Vector2 predictedLanding = ball.GetPredictedLanding();
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
    /// After a pass completion, non-blocking receivers jog toward the ball carrier.
    /// </summary>
    private static void UpdateReceiverTrackingBallCarrier(Receiver receiver, Entity ballCarrier, float dt, Action<Entity> clampToField)
    {
        Vector2 trackDir = GetTrackDirectionToward(receiver.Position, ballCarrier.Position);
        float trackSpeed = receiver.Speed * 0.75f;
        receiver.Velocity = trackDir * trackSpeed;
        receiver.Update(dt);
        clampToField(receiver);
    }

    private static Vector2 GetTrackDirectionToward(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        if (delta.LengthSquared() < 0.001f)
        {
            return Vector2.Zero;
        }

        return Vector2.Normalize(delta);
    }

    private static bool IsRunPlayActivePreHandoff(PlayType selectedPlayType, Ball ball)
    {
        if (selectedPlayType != PlayType.Run) return false;
        return ball.State == BallState.HeldByQB;
    }
}
