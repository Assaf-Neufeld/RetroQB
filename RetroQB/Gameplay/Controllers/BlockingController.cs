using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles all blocking logic for receivers, tight ends, and running backs.
/// </summary>
public sealed class BlockingController
{
    /// <summary>
    /// Updates a blocking receiver's behavior.
    /// </summary>
    public void UpdateBlockingReceiver(
        Receiver receiver,
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Defender> defenders,
        PlayDefinition selectedPlay,
        PlayType selectedPlayType,
        float lineOfScrimmage,
        float dt,
        Func<Vector2, float, bool, Defender?> getClosestDefender,
        Action<Entity> clampToField)
    {
        bool isRunPlay = BlockingUtils.IsRunPlayActiveWithRunningBack(selectedPlayType, ball);
        Vector2? ballCarrierPosition = BlockingUtils.GetBallCarrierPosition(ball, qb);

        if (receiver.IsRunningBack && receiver.IsBlocking)
        {
            UpdateRbBlocking(receiver, qb, ball, defenders, selectedPlay, dt, getClosestDefender, clampToField, ballCarrierPosition);
            return;
        }

        if (receiver.IsTightEnd && isRunPlay)
        {
            int runSide = Math.Sign(selectedPlay.RunningBackSide);
            if (runSide != 0)
            {
                UpdateTightEndRunBlocking(receiver, qb, selectedPlay, defenders, runSide, lineOfScrimmage, dt, getClosestDefender, clampToField, ballCarrierPosition);
                return;
            }
        }

        // Generic receiver blocking
        UpdateGenericBlocking(receiver, qb, ball, defenders, selectedPlay, selectedPlayType, dt, getClosestDefender, clampToField, ballCarrierPosition);
    }

    private static Vector2 GetDriveDirection(int runSide, float xFactor)
    {
        return BlockingUtils.SafeNormalize(new Vector2(runSide * xFactor, 1f));
    }

    private void UpdateRbBlocking(
        Receiver receiver,
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Defender> defenders,
        PlayDefinition selectedPlay,
        float dt,
        Func<Vector2, float, bool, Defender?> getClosestDefender,
        Action<Entity> clampToField,
        Vector2? ballCarrierPosition)
    {
        int side = receiver.RouteSide == 0 ? (receiver.Position.X <= qb.Position.X ? -1 : 1) : receiver.RouteSide;
        Vector2 pocketSpot = qb.Position + new Vector2(1.7f * side, -0.4f);

        Defender? rbTarget = getClosestDefender(qb.Position, Constants.BlockEngageRadius + 6.0f, true);
        if (rbTarget != null)
        {
            float blockMultiplier = GetReceiverBlockStrength(receiver) * GetDefenderBlockDifficulty(rbTarget);
            Vector2 toTarget = BlockingUtils.SafeNormalize(rbTarget.Position - receiver.Position);
            receiver.Velocity = toTarget * (receiver.Speed * 0.9f);

            float contactRange = receiver.Radius + rbTarget.Radius + 0.8f;
            float distance = Vector2.Distance(receiver.Position, rbTarget.Position);
            if (distance <= contactRange)
            {
                ApplyBlockContact(receiver, rbTarget, contactRange, distance, blockMultiplier, dt, clampToField, ballCarrierPosition, baseSlow: 0.12f, holdStrengthMult: 1.1f, overlapBoost: 6f);
            }
        }
        else
        {
            MoveTowardSpot(receiver, pocketSpot, 0.6f, 0.8f);
        }
    }

    private void UpdateTightEndRunBlocking(
        Receiver receiver,
        Quarterback qb,
        PlayDefinition selectedPlay,
        IReadOnlyList<Defender> defenders,
        int runSide,
        float lineOfScrimmage,
        float dt,
        Func<Vector2, float, bool, Defender?> getClosestDefender,
        Action<Entity> clampToField,
        Vector2? ballCarrierPosition)
    {
        bool isSweep = BlockingUtils.IsSweepFormation(selectedPlay.Formation);
        float edgeX = Math.Clamp(qb.Position.X + (runSide * (isSweep ? 5.0f : 4.1f)), 1.1f, Constants.FieldWidth - 1.1f);
        float edgeY = lineOfScrimmage + (isSweep ? 2.8f : 2.2f);
        Vector2 edgeSpot = new Vector2(edgeX, edgeY);

        Defender? edgeTarget = getClosestDefender(edgeSpot, Constants.BlockEngageRadius + 2.2f, true);
        float closeToEdgeRadius = 1.2f;
        bool closeToEdge = Vector2.DistanceSquared(receiver.Position, edgeSpot) <= closeToEdgeRadius * closeToEdgeRadius;
        bool targetNearEdge = edgeTarget != null && Vector2.DistanceSquared(edgeTarget.Position, edgeSpot) <= (Constants.BlockEngageRadius * 0.9f) * (Constants.BlockEngageRadius * 0.9f);

        if (edgeTarget != null && (closeToEdge || targetNearEdge))
        {
            float blockMultiplier = GetReceiverBlockStrength(receiver) * GetDefenderBlockDifficulty(edgeTarget);
            Vector2 toTarget = BlockingUtils.SafeNormalize(edgeTarget.Position - receiver.Position);
            receiver.Velocity = toTarget * receiver.Speed;

            float contactRange = receiver.Radius + edgeTarget.Radius + 0.9f;
            float distance = Vector2.Distance(receiver.Position, edgeTarget.Position);
            if (distance <= contactRange)
            {
                Vector2 driveDir = GetDriveDirection(runSide, 0.85f);
                ApplyBlockContact(receiver, edgeTarget, contactRange, distance, blockMultiplier, dt, clampToField, ballCarrierPosition, baseSlow: 0.08f, holdStrengthMult: 1.5f, overlapBoost: 8f, driveDir: driveDir, driveStrength: 0.9f);
            }
        }
        else
        {
            MoveTowardSpot(receiver, edgeSpot, 0.85f, 0.9f);
        }
    }

    private void UpdateGenericBlocking(
        Receiver receiver,
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Defender> defenders,
        PlayDefinition selectedPlay,
        PlayType selectedPlayType,
        float dt,
        Func<Vector2, float, bool, Defender?> getClosestDefender,
        Action<Entity> clampToField,
        Vector2? ballCarrierPosition)
    {
        bool runBlockingBoost = BlockingUtils.IsRunPlayActiveWithRunningBack(selectedPlayType, ball);
        int runSide = Math.Sign(selectedPlay.RunningBackSide);

        Defender? target = getClosestDefender(receiver.Position, Constants.BlockEngageRadius, true);
        if (target != null)
        {
            float blockMultiplier = GetReceiverBlockStrength(receiver) * GetDefenderBlockDifficulty(target);
            Vector2 toTarget = BlockingUtils.SafeNormalize(target.Position - receiver.Position);
            receiver.Velocity = toTarget * receiver.Speed;
            if (runBlockingBoost && runSide != 0)
            {
                Vector2 driveDir = GetDriveDirection(runSide, 0.7f);
                receiver.Velocity += driveDir * (receiver.Speed * 0.3f);
            }

            float contactRange = receiver.Radius + target.Radius + (runBlockingBoost ? 0.9f : 0.6f);
            float distance = Vector2.Distance(receiver.Position, target.Position);
            if (distance <= contactRange)
            {
                float holdStrengthMult = runBlockingBoost ? 1.4f : 1f;
                float overlapBoost = runBlockingBoost ? 8f : 6f;
                float baseSlow = runBlockingBoost ? 0.08f : 0.15f;
                Vector2? driveDir = runBlockingBoost && runSide != 0 ? GetDriveDirection(runSide, 0.7f) : null;
                float driveStrength = runBlockingBoost && runSide != 0 ? 0.8f : 0f;

                ApplyBlockContact(receiver, target, contactRange, distance, blockMultiplier, dt, clampToField, ballCarrierPosition, baseSlow, holdStrengthMult, overlapBoost, driveDir, driveStrength);
            }
        }
        else
        {
            if (runBlockingBoost)
            {
                receiver.Velocity = new Vector2(runSide * receiver.Speed * 0.18f, receiver.Speed * 0.4f);
            }
            else
            {
                receiver.Velocity = new Vector2(0f, receiver.Speed * 0.4f);
            }
        }
    }

    private void ApplyBlockContact(
        Receiver receiver,
        Defender target,
        float contactRange,
        float distance,
        float blockMultiplier,
        float dt,
        Action<Entity> clampToField,
        Vector2? ballCarrierPosition,
        float baseSlow,
        float holdStrengthMult,
        float overlapBoost,
        Vector2? driveDir = null,
        float driveStrength = 0f)
    {
        Vector2 pushDir = BlockingUtils.SafeNormalize(target.Position - receiver.Position);
        float overlap = contactRange - distance;
        float holdStrength = (Constants.BlockHoldStrength * holdStrengthMult) * blockMultiplier;
        float overlapBoostFinal = overlapBoost * blockMultiplier;
        float shedBoost = BlockingUtils.GetTackleShedBoost(target.Position, ballCarrierPosition);
        if (shedBoost > 0f)
        {
            float shedScale = 1f - (0.65f * shedBoost);
            holdStrength *= shedScale;
            overlapBoostFinal *= shedScale;
        }
        target.Position += pushDir * (holdStrength + overlap * overlapBoostFinal) * dt;
        if (driveDir.HasValue && driveStrength > 0f)
        {
            target.Position += driveDir.Value * driveStrength * dt;
        }
        if (shedBoost > 0f)
        {
            baseSlow *= 1f - (0.6f * shedBoost);
        }
        target.Velocity *= BlockingUtils.GetDefenderSlowdown(blockMultiplier, baseSlow);
        receiver.Velocity *= 0.25f;
        clampToField(target);
    }

    private void MoveTowardSpot(Receiver receiver, Vector2 spot, float speedMult, float arrivalRadius)
    {
        Vector2 toSpot = BlockingUtils.SafeNormalize(spot - receiver.Position);
        receiver.Velocity = toSpot * (receiver.Speed * speedMult);

        if (Vector2.DistanceSquared(receiver.Position, spot) <= arrivalRadius * arrivalRadius)
        {
            receiver.Velocity = Vector2.Zero;
        }
    }

    public static float GetReceiverBlockStrength(Receiver receiver)
    {
        float baseStrength;
        if (receiver.IsTightEnd) baseStrength = 1.1f;
        else if (receiver.IsRunningBack) baseStrength = 1.0f;
        else baseStrength = 0.75f;
        
        return baseStrength * receiver.TeamAttributes.BlockingStrength;
    }

    public static float GetDefenderBlockDifficulty(Defender defender)
    {
        // Base difficulty by position (lower = harder to block)
        float baseDifficulty = defender.PositionRole switch
        {
            DefensivePosition.DL => 0.75f,
            DefensivePosition.DE => 0.80f,
            DefensivePosition.LB => 0.70f,  // LBs are harder to block (better at shedding)
            _ => 1.20f  // DBs are easier to block
        };
        
        // Apply team's position-specific block shed multiplier
        float shedMultiplier = defender.TeamAttributes.GetPositionBlockShedMultiplier(defender.PositionRole);
        return baseDifficulty / shedMultiplier;  // Higher shed = lower difficulty value = harder to block
    }
}
