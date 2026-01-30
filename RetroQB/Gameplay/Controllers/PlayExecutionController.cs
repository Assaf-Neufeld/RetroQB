using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Input;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles the active play execution: movement, AI updates, and entity coordination.
/// </summary>
public sealed class PlayExecutionController
{
    private readonly InputManager _input;
    private readonly BlockingController _blockingController;

    public PlayExecutionController(InputManager input, BlockingController blockingController)
    {
        _input = input;
        _blockingController = blockingController;
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

        // Update QB
        UpdateQuarterback(qb, ball, inputDir, sprint, dt, clampToField);

        // Update receivers
        UpdateReceivers(receivers, qb, ball, defenders, controlledReceiver, inputDir, sprint, qbPastLos, playManager, dt, clampToField);

        // Update defenders
        UpdateDefenders(defenders, qb, receivers, ball, playManager, qbPastLos, isZoneCoverage, clampToField, dt);

        // Update blockers
        UpdateBlockers(blockers, defenders, qb, ball, playManager, clampToField, dt);
    }

    private void UpdateQuarterback(
        Quarterback qb,
        Ball ball,
        Vector2 inputDir,
        bool sprint,
        float dt,
        Action<Entity> clampToField)
    {
        if (ball.State == BallState.HeldByQB)
        {
            qb.ApplyInput(inputDir, sprint, false, dt);
        }
        else
        {
            qb.ApplyInput(Vector2.Zero, false, false, dt);
        }
        qb.Update(dt);
        clampToField(qb);
    }

    private void UpdateReceivers(
        IReadOnlyList<Receiver> receivers,
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Defender> defenders,
        Receiver? controlledReceiver,
        Vector2 inputDir,
        bool sprint,
        bool qbPastLos,
        PlayManager playManager,
        float dt,
        Action<Entity> clampToField)
    {
        bool isRunPlayPreHandoff = IsRunPlayActivePreHandoff(playManager.SelectedPlayType, ball);
        bool isRunPlayWithRb = IsRunPlayActiveWithRunningBack(playManager.SelectedPlayType, ball);
        bool isBallHeldByReceiver = ball.State == BallState.HeldByReceiver;

        foreach (var receiver in receivers)
        {
            bool isBallCarrier = isBallHeldByReceiver && ball.Holder == receiver;
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

            UpdateRouteReceiver(receiver, qb, ball, defenders, qbPastLos, dt, clampToField);
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
            ReceiverAI.UpdateRoute(receiver, dt);
        }

        receiver.Update(dt);
        clampToField(receiver);
    }

    private void UpdateControlledReceiver(Receiver receiver, Vector2 inputDir, bool sprint, bool isRunPlayWithRb, float dt, Action<Entity> clampToField)
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

    private void UpdateRouteReceiver(Receiver receiver, Quarterback qb, Ball ball, IReadOnlyList<Defender> defenders, bool qbPastLos, float dt, Action<Entity> clampToField)
    {
        ReceiverAI.UpdateRoute(receiver, dt);

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
            // Adjust toward ball
            AdjustReceiverToBall(receiver, ball);
        }

        receiver.Update(dt);
        clampToField(receiver);
    }

    private void AdjustReceiverToBall(Receiver receiver, Ball ball)
    {
        Vector2 routeVelocity = receiver.Velocity;
        Vector2 toBall = ball.Position - receiver.Position;
        float distToBall = toBall.Length();
        if (distToBall > 0.001f)
        {
            toBall /= distToBall;
        }

        bool ballAhead = ball.Position.Y >= receiver.Position.Y - 0.75f;
        bool allowComeback = distToBall <= Constants.CatchRadius + 0.75f;

        if (ballAhead || allowComeback)
        {
            Vector2 baseDir = routeVelocity.LengthSquared() > 0.001f
                ? Vector2.Normalize(routeVelocity)
                : toBall;

            float adjustWeight = Math.Clamp(1f - (distToBall / 12f), 0.15f, 0.6f);
            Vector2 blendedDir = baseDir * (1f - adjustWeight) + toBall * adjustWeight;
            if (blendedDir.LengthSquared() > 0.001f)
            {
                blendedDir = Vector2.Normalize(blendedDir);
            }
            receiver.Velocity = blendedDir * receiver.Speed;
        }
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
        bool isRunPlayWithRb = IsRunPlayActiveWithRunningBack(playManager.SelectedPlayType, ball);
        float runDefenseAdjust = isRunPlayWithRb ? 0.9f : 1f;

        foreach (var defender in defenders)
        {
            float speedMultiplier = playManager.DefenderSpeedMultiplier * runDefenseAdjust;
            DefenderAI.UpdateDefender(defender, qb, receivers, ball, speedMultiplier, dt, qbPastLos, isZoneCoverage, playManager.LineOfScrimmage);
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
        bool runBlockingBoost = IsRunPlayActiveWithRunningBack(playManager.SelectedPlayType, ball);
        Vector2? ballCarrierPosition = BlockingController.GetBallCarrierPosition(ball, qb);

        OffensiveLinemanAI.UpdateBlockers(
            blockers.ToList(),
            defenders.ToList(),
            playManager.SelectedPlayType,
            playManager.SelectedPlay.Formation,
            playManager.LineOfScrimmage,
            playManager.SelectedPlay.RunningBackSide,
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

    private static bool IsRunPlayActivePreHandoff(PlayType selectedPlayType, Ball ball)
    {
        if (selectedPlayType != PlayType.Run) return false;
        return ball.State == BallState.HeldByQB;
    }

    private static bool IsRunPlayActiveWithRunningBack(PlayType selectedPlayType, Ball ball)
    {
        if (selectedPlayType != PlayType.Run) return false;
        if (ball.State != BallState.HeldByReceiver) return false;
        return ball.Holder is Receiver receiver && receiver.IsRunningBack;
    }

    private static Defender? GetClosestDefender(IReadOnlyList<Defender> defenders, Vector2 position, float maxDistance, bool preferRushers)
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
}
