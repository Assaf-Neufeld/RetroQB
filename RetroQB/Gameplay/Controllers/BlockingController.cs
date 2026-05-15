using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles all blocking logic for receivers, tight ends, and running backs.
/// </summary>
public sealed class BlockingController
{
    private readonly record struct BlockContactProfile(
        float ContactBuffer,
        float BaseSlow,
        float HoldStrengthMultiplier,
        float OverlapBoost,
        Vector2? DriveDirection = null,
        float DriveStrength = 0f);

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
        bool isDesignedRun = selectedPlayType == PlayType.Run || selectedPlay.Family == PlayType.Run;
        Vector2? ballCarrierPosition = BlockingUtils.GetBallCarrierPosition(ball, qb);

        if (receiver.IsRunningBack && receiver.IsBlocking)
        {
            UpdateRbBlocking(receiver, qb, defenders, selectedPlayType, dt, getClosestDefender, clampToField, ballCarrierPosition);
            return;
        }

        if (receiver.IsTightEnd && isDesignedRun)
        {
            int runSide = Math.Sign(selectedPlay.RunningBackSide);
            if (runSide != 0)
            {
                UpdateTightEndRunBlocking(receiver, selectedPlay, runSide, lineOfScrimmage, dt, getClosestDefender, clampToField, ballCarrierPosition);
                return;
            }
        }

        UpdateGenericBlocking(receiver, selectedPlay, isDesignedRun, dt, getClosestDefender, clampToField, ballCarrierPosition);
    }

    private void UpdateRbBlocking(
        Receiver receiver,
        Quarterback qb,
        IReadOnlyList<Defender> defenders,
        PlayType selectedPlayType,
        float dt,
        Func<Vector2, float, bool, Defender?> getClosestDefender,
        Action<Entity> clampToField,
        Vector2? ballCarrierPosition)
    {
        int side = receiver.RouteSide == 0 ? (receiver.Position.X <= qb.Position.X ? -1 : 1) : receiver.RouteSide;
        Vector2 pocketSpot = qb.Position + new Vector2(1.7f * side, -0.4f);

        Defender? rbTarget;
        if (selectedPlayType == PlayType.Pass)
        {
            rbTarget = GetPassProtectionTarget(defenders, receiver.Position, qb.Position, Constants.BlockEngageRadius + 8.0f);
        }
        else
        {
            rbTarget = getClosestDefender(qb.Position, Constants.BlockEngageRadius + 6.0f, true);
        }

        if (rbTarget != null)
        {
            var profile = new BlockContactProfile(0.8f, 0.12f, 1.1f, 6f);
            ApproachAndBlock(receiver, rbTarget, 0.9f, profile, dt, clampToField, ballCarrierPosition);
        }
        else
        {
            MoveTowardSpot(receiver, pocketSpot, 0.6f, 0.8f);
        }
    }

    private void UpdateTightEndRunBlocking(
        Receiver receiver,
        PlayDefinition selectedPlay,
        int runSide,
        float lineOfScrimmage,
        float dt,
        Func<Vector2, float, bool, Defender?> getClosestDefender,
        Action<Entity> clampToField,
        Vector2? ballCarrierPosition)
    {
        bool isPerimeterRun = BlockingUtils.IsPerimeterRun(selectedPlay.RunConcept);
        float edgeWidth = selectedPlay.RunConcept switch
        {
            RunConcept.Sweep => 2.0f,
            RunConcept.Stretch => 1.6f,
            RunConcept.Counter => 0.9f,
            _ => 1.2f
        };

        float edgeDepth = selectedPlay.RunConcept switch
        {
            RunConcept.Sweep => 3.2f,
            RunConcept.Stretch => 2.9f,
            RunConcept.Counter => 2.4f,
            _ => 2.6f
        };

        // Base edge spot on the TE's own position so they block in the correct direction
        float edgeX = Math.Clamp(receiver.RouteStart.X + (runSide * edgeWidth), 1.1f, Constants.FieldWidth - 1.1f);
        float edgeY = lineOfScrimmage + edgeDepth;
        Vector2 edgeSpot = new Vector2(edgeX, edgeY);

        // Search for any defender near the edge — don't limit to rushers so DBs get picked up
        Defender? edgeTarget = getClosestDefender(edgeSpot, Constants.BlockEngageRadius + 2.2f, false);
        float closeToEdgeRadius = 1.2f;
        bool closeToEdge = Vector2.DistanceSquared(receiver.Position, edgeSpot) <= closeToEdgeRadius * closeToEdgeRadius;
        bool targetNearEdge = edgeTarget != null && Vector2.DistanceSquared(edgeTarget.Position, edgeSpot) <= (Constants.BlockEngageRadius * 0.9f) * (Constants.BlockEngageRadius * 0.9f);

        if (edgeTarget != null && (closeToEdge || targetNearEdge))
        {
            Vector2 driveDir = BlockingUtils.GetDriveDirection(runSide, 0.85f);
            var profile = new BlockContactProfile(0.9f, 0.08f, 1.5f, 8f, driveDir, 0.9f);
            ApproachAndBlock(receiver, edgeTarget, 1f, profile, dt, clampToField, ballCarrierPosition);
        }
        else
        {
            MoveTowardSpot(receiver, edgeSpot, isPerimeterRun ? 0.85f : 0.7f, 0.9f);
        }
    }

    private void UpdateGenericBlocking(
        Receiver receiver,
        PlayDefinition selectedPlay,
        bool isDesignedRun,
        float dt,
        Func<Vector2, float, bool, Defender?> getClosestDefender,
        Action<Entity> clampToField,
        Vector2? ballCarrierPosition)
    {
        int runSide = Math.Sign(selectedPlay.RunningBackSide);
        bool hasRunDirection = isDesignedRun && runSide != 0;

        // On run plays, pick up any defender (including DBs coming downhill)
        Defender? target = getClosestDefender(receiver.Position, Constants.BlockEngageRadius, !isDesignedRun);
        if (target != null)
        {
            Vector2? driveDir = hasRunDirection ? BlockingUtils.GetDriveDirection(runSide, 0.7f) : null;
            Vector2? extraVelocity = driveDir * (receiver.Speed * 0.3f);
            var profile = new BlockContactProfile(
                isDesignedRun ? 0.9f : 0.6f,
                isDesignedRun ? 0.08f : 0.15f,
                isDesignedRun ? 1.4f : 1f,
                isDesignedRun ? 8f : 6f,
                driveDir,
                hasRunDirection ? 0.8f : 0f);

            ApproachAndBlock(receiver, target, 1f, profile, dt, clampToField, ballCarrierPosition, extraVelocity);
        }
        else
        {
            if (isDesignedRun)
            {
                receiver.Velocity = new Vector2(runSide * receiver.Speed * 0.18f, receiver.Speed * 0.4f);
            }
            else
            {
                receiver.Velocity = new Vector2(0f, receiver.Speed * 0.4f);
            }
        }
    }

    private void ApproachAndBlock(
        Receiver receiver,
        Defender target,
        float speedMultiplier,
        BlockContactProfile profile,
        float dt,
        Action<Entity> clampToField,
        Vector2? ballCarrierPosition,
        Vector2? extraVelocity = null)
    {
        Vector2 toTarget = BlockingUtils.SafeNormalize(target.Position - receiver.Position);
        receiver.Velocity = toTarget * (receiver.Speed * speedMultiplier);
        if (extraVelocity.HasValue)
        {
            receiver.Velocity += extraVelocity.Value;
        }

        float contactRange = receiver.Radius + target.Radius + profile.ContactBuffer;
        float distance = Vector2.Distance(receiver.Position, target.Position);
        if (distance > contactRange)
        {
            return;
        }

        float blockMultiplier = GetReceiverBlockStrength(receiver) * GetDefenderBlockDifficulty(target);
        ApplyBlockContact(receiver, target, contactRange, distance, blockMultiplier, dt, clampToField, ballCarrierPosition, profile);
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
        BlockContactProfile profile)
    {
        BlockingUtils.RegisterBlockContact(target);
        float doubleTeamEffectiveness = BlockingUtils.GetDoubleTeamEffectiveness(target);

        Vector2 pushDir = BlockingUtils.SafeNormalize(target.Position - receiver.Position);
        float overlap = contactRange - distance;
        float holdStrength = (Constants.BlockHoldStrength * profile.HoldStrengthMultiplier) * blockMultiplier * doubleTeamEffectiveness;
        float overlapBoostFinal = profile.OverlapBoost * blockMultiplier * (0.85f + 0.15f * doubleTeamEffectiveness);
        float shedBoost = BlockingUtils.GetTackleShedBoost(target.Position, ballCarrierPosition);
        float driveStrength = profile.DriveStrength;
        if (shedBoost > 0f)
        {
            float shedScale = 1f - (0.65f * shedBoost);
            holdStrength *= shedScale;
            overlapBoostFinal *= shedScale;
            driveStrength *= shedScale;
        }
        target.Position += pushDir * (holdStrength + overlap * overlapBoostFinal) * dt;
        if (profile.DriveDirection.HasValue && driveStrength > 0f)
        {
            float driveBlockScale = Math.Clamp(blockMultiplier, 0.6f, 1.4f);
            target.Position += profile.DriveDirection.Value * driveStrength * driveBlockScale * dt;
        }
        float baseSlow = profile.BaseSlow;
        if (shedBoost > 0f)
        {
            baseSlow *= 1f - (0.6f * shedBoost);
        }
        if (doubleTeamEffectiveness > 1f)
        {
            baseSlow *= 0.45f;
        }
        target.Velocity *= BlockingUtils.GetDefenderSlowdown(blockMultiplier, baseSlow);
        receiver.Velocity *= 0.25f;
        clampToField(target);
    }

    private static Defender? GetPassProtectionTarget(
        IReadOnlyList<Defender> defenders,
        Vector2 receiverPosition,
        Vector2 qbPosition,
        float maxDistance)
    {
        Defender? best = null;
        float bestDistSq = maxDistance * maxDistance;
        float bestScore = float.MaxValue;

        foreach (var defender in defenders)
        {
            float receiverDistSq = Vector2.DistanceSquared(receiverPosition, defender.Position);
            float qbDistSq = Vector2.DistanceSquared(qbPosition, defender.Position);
            if (receiverDistSq > bestDistSq && qbDistSq > bestDistSq)
            {
                continue;
            }

            float score = MathF.Sqrt(MathF.Min(receiverDistSq, qbDistSq));
            if (defender.IsRusher)
            {
                score -= 6f;
            }

            score += defender.PositionRole switch
            {
                DefensivePosition.DE => -1.5f,
                DefensivePosition.LB => -1.0f,
                DefensivePosition.DL => -0.6f,
                _ => 0f
            };

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = defender;
        }

        return best;
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

    private static float GetReceiverBlockStrength(Receiver receiver)
    {
        if (receiver.IsTightEnd)
        {
            return 1.1f * receiver.TeamAttributes.GetTeBlockingStrength(receiver.Slot);
        }

        float baseStrength = receiver.IsRunningBack ? 1.0f : 0.75f;
        return baseStrength * receiver.TeamAttributes.BlockingStrength;
    }

    private static float GetDefenderBlockDifficulty(Defender defender)
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
        float shedMultiplier = defender.TeamAttributes.GetPositionBlockShedMultiplier(defender.PositionRole) * defender.BlockShedMultiplier;
        return baseDifficulty / shedMultiplier;  // Higher shed = lower difficulty value = harder to block
    }
}
