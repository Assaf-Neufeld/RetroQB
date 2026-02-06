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

        // Update QB
        UpdateQuarterback(qb, ball, playManager, inputDir, sprint, dt, clampToField);

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
        PlayManager playManager,
        Vector2 inputDir,
        bool sprint,
        float dt,
        Action<Entity> clampToField)
    {
        if (ball.State == BallState.HeldByQB)
        {
            qb.ApplyInput(inputDir, sprint, false, dt);
        }
        else if (BlockingUtils.IsRunPlayActiveWithRunningBack(playManager.SelectedPlayType, ball))
        {
            Vector2 clearOutDir = GetQbClearOutDirection(qb, ball, playManager);
            qb.ApplyInput(clearOutDir, sprinting: false, aimMode: false, dt);
        }
        else
        {
            qb.ApplyInput(Vector2.Zero, false, false, dt);
        }
        qb.Update(dt);
        clampToField(qb);
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

    private static Vector2 GetQbClearOutDirection(Quarterback qb, Ball ball, PlayManager playManager)
    {
        if (ball.Holder is not Receiver runningBack)
        {
            return Vector2.Zero;
        }

        Vector2 awayFromRb = qb.Position - runningBack.Position;
        if (awayFromRb.LengthSquared() < 0.001f)
        {
            awayFromRb = new Vector2(-playManager.SelectedPlay.RunningBackSide, -0.6f);
        }

        Vector2 backfieldBias = new Vector2(0f, -0.4f);
        Vector2 clearOut = awayFromRb + backfieldBias;
        if (clearOut.LengthSquared() > 0.001f)
        {
            clearOut = Vector2.Normalize(clearOut);
        }

        return clearOut;
    }

}
