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
        ResolvedPlay selectedPlay,
        PlayType selectedPlayType,
        float lineOfScrimmage,
        float dt,
        Func<Vector2, float, bool, Defender?> getClosestDefender,
        Action<Entity> clampToField)
    {
        var job = selectedPlay.Assignments[receiver.Slot].Blocking;
        Vector2? carrier = BlockingUtils.GetBallCarrierPosition(ball, qb);
        if (job != null)
        {
            var decision = BlockingSteering.Decide(job, receiver.BlockingState, receiver.Position,
                receiver.RouteStart.X, lineOfScrimmage, qb.Position, defenders,
                Constants.BlockEngageRadius + (job.IsRunBlock ? 2.2f : 8f));
            if (decision.Target is Defender target)
            {
                var profile = job.IsRunBlock
                    ? new BlockContactProfile(0.9f, 0.08f, 1.5f, 8f, job.DriveDirection, 0.9f)
                    : new BlockContactProfile(0.8f, 0.12f, 1.1f, 6f);
                ApproachAndBlock(receiver, target, 1f, profile, dt, clampToField, carrier);
            }
            else
                receiver.Velocity = BlockingSteering.MoveTo(receiver.Position, decision.Landmark, receiver.Speed * 0.85f, dt);
            return;
        }
        // Post-catch and scramble support has no pre-snap blocking assignment.
        UpdateGenericBlocking(receiver, selectedPlay, selectedPlayType == PlayType.Run, dt,
            getClosestDefender, clampToField, carrier);
    }
    private void UpdateGenericBlocking(
        Receiver receiver,
        ResolvedPlay selectedPlay,
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
