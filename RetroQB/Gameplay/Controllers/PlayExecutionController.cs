using System.Linq;
using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Input;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Coordinates the active play execution: QB, receivers, defenders, blockers.
/// Delegates receiver-specific logic to <see cref="ReceiverUpdateController"/>.
/// </summary>
public sealed class PlayExecutionController
{
    private readonly InputManager _input;
    private readonly ReceiverUpdateController _receiverController;

    public PlayExecutionController(InputManager input, BlockingController blockingController)
    {
        _input = input;
        _receiverController = new ReceiverUpdateController(blockingController);
    }

    /// <summary>
    /// Updates all entities during an active play.
    /// </summary>
    public void UpdatePlay(
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Defender> defenders,
        IReadOnlyList<Blocker> blockers,
        PlayManager playManager,
        bool qbPastLos,
        bool isZoneCoverage,
        Action<Entity> clampToField,
        float dt)
    {
        Vector2 inputDir = _input.GetMovementDirection();
        bool sprint = _input.IsSprintHeld();
        Receiver? controlledReceiver = ball.State == BallState.HeldByReceiver ? ball.Holder as Receiver : null;

        // Reset per-frame blocking contact state before any blocker logic runs
        BlockingUtils.ResetDefenderBlockingState(defenders);

        // Update QB
        UpdateQuarterback(qb, ball, defenders, inputDir, sprint, dt, clampToField);

        // Update receivers
        _receiverController.UpdateAll(receivers, qb, ball, defenders, controlledReceiver, inputDir, sprint, qbPastLos, isZoneCoverage, playManager, dt, clampToField);

        // Update defenders
        UpdateDefenders(defenders, qb, receivers, ball, playManager, qbPastLos, isZoneCoverage, clampToField, dt);

        // Update blockers
        UpdateBlockers(blockers, defenders, qb, ball, playManager, clampToField, dt);
    }

    private void UpdateQuarterback(
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Defender> defenders,
        Vector2 inputDir,
        bool sprint,
        float dt,
        Action<Entity> clampToField)
    {
        if (ball.State == BallState.HeldByQB)
        {
            qb.ApplyInput(inputDir, sprint, false, dt);
        }
        else if (ball.State == BallState.HeldByReceiver)
        {
            ApplyQuarterbackSupport(qb, ball, defenders, dt, clampToField);
        }
        else
        {
            qb.ApplyInput(Vector2.Zero, false, false, dt);
        }
        qb.Update(dt);
        clampToField(qb);
    }

    private static void ApplyQuarterbackSupport(
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Defender> defenders,
        float dt,
        Action<Entity> clampToField)
    {
        qb.ApplyInput(Vector2.Zero, false, false, dt);

        if (ball.Holder is not Receiver ballCarrier)
        {
            return;
        }

        const float supportRadius = 6.0f;
        const float defenderThreatRadius = 5.0f;
        const float qbAssistReach = 3.5f;
        const float engageBuffer = 0.9f;
        const float qbBlockStrength = 0.8f;
        const float qbBlockSlow = 0.16f;

        if (Vector2.DistanceSquared(qb.Position, ballCarrier.Position) > supportRadius * supportRadius)
        {
            return;
        }

        Defender? target = GetClosestThreatToBallCarrier(defenders, qb.Position, ballCarrier.Position, defenderThreatRadius, qbAssistReach);
        if (target == null)
        {
            return;
        }

        Vector2 toTarget = target.Position - qb.Position;
        if (toTarget.LengthSquared() > 0.001f)
        {
            qb.ApplyInput(Vector2.Normalize(toTarget), false, false, dt);
        }

        float contactRange = qb.Radius + target.Radius + engageBuffer;
        float distance = Vector2.Distance(qb.Position, target.Position);
        if (distance > contactRange)
        {
            return;
        }

        BlockingUtils.RegisterBlockContact(target);

        Vector2 pushDir = BlockingUtils.SafeNormalize(target.Position - qb.Position);
        float overlap = contactRange - distance;
        float shedBoost = BlockingUtils.GetTackleShedBoost(target.Position, ballCarrier.Position);
        float holdStrength = Constants.BlockHoldStrength * qbBlockStrength * (1f - (0.55f * shedBoost));
        float overlapBoost = 5.5f * (1f - (0.55f * shedBoost));

        target.Position += pushDir * (holdStrength + overlap * overlapBoost) * dt;
        target.Velocity *= BlockingUtils.GetDefenderSlowdown(qbBlockStrength, qbBlockSlow);
        qb.Velocity *= 0.22f;
        clampToField(target);
    }

    private void UpdateDefenders(
        IReadOnlyList<Defender> defenders,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        Ball ball,
        PlayManager playManager,
        bool qbPastLos,
        bool isZoneCoverage,
        Action<Entity> clampToField,
        float dt)
    {
        bool isRunPlayWithRb = BlockingUtils.IsRunPlayActiveWithRunningBack(playManager.SelectedPlayType, ball);
        float runDefenseAdjust = isRunPlayWithRb ? 0.9f : 1f;

        foreach (var defender in defenders)
        {
            float speedMultiplier = playManager.DefenderSpeedMultiplier * runDefenseAdjust;
            DefenderTargeting.UpdateDefender(defender, qb, receivers, ball, speedMultiplier, dt, qbPastLos, isZoneCoverage, playManager.LineOfScrimmage);
            clampToField(defender);
        }
    }

    private void UpdateBlockers(
        IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Defender> defenders,
        Quarterback qb,
        Ball ball,
        PlayManager playManager,
        Action<Entity> clampToField,
        float dt)
    {
        bool runBlockingBoost = BlockingUtils.IsRunPlayActiveWithRunningBack(playManager.SelectedPlayType, ball);
        Vector2? ballCarrierPosition = BlockingUtils.GetBallCarrierPosition(ball, qb);

        OffensiveLinemanAI.UpdateBlockers(
            blockers.ToList(),
            defenders.ToList(),
            playManager.SelectedPlay,
            playManager.LineOfScrimmage,
            dt,
            runBlockingBoost,
            clampToField,
            ballCarrierPosition);
    }

    /// <summary>
    /// Attempts to handoff to the running back on run plays.
    /// </summary>
    public void TryHandoffToRunningBack(
        PlayManager playManager,
        Ball ball,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers)
    {
        if (playManager.SelectedPlayType != PlayType.Run)
        {
            return;
        }

        if (ball.State != BallState.HeldByQB)
        {
            return;
        }

        Receiver? runningBack = receivers.FirstOrDefault(r => r.IsRunningBack);
        if (runningBack == null)
        {
            return;
        }

        float handoffRange = 3.2f;
        float distance = Vector2.Distance(qb.Position, runningBack.Position);
        if (distance > handoffRange)
        {
            return;
        }

        runningBack.HasBall = true;
        ball.SetHeld(runningBack, BallState.HeldByReceiver);
    }

    private static Defender? GetClosestThreatToBallCarrier(
        IReadOnlyList<Defender> defenders,
        Vector2 qbPosition,
        Vector2 ballCarrierPosition,
        float maxDistance,
        float maxQbDistance)
    {
        Defender? closest = null;
        float bestDistanceSq = maxDistance * maxDistance;
        float maxQbDistanceSq = maxQbDistance * maxQbDistance;

        foreach (var defender in defenders)
        {
            float distanceSq = Vector2.DistanceSquared(defender.Position, ballCarrierPosition);
            if (distanceSq >= bestDistanceSq)
            {
                continue;
            }

            if (Vector2.DistanceSquared(defender.Position, qbPosition) > maxQbDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            closest = defender;
        }

        return closest;
    }

}
