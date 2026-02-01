using System;
using System.Numerics;
using System.Linq;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.AI;

/// <summary>
/// Offensive line AI logic: blocking behavior and route visualization.
/// </summary>
public static class OffensiveLinemanAI
{
    public static void UpdateBlockers(
        IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Defender> defenders,
        PlayType playFamily,
        FormationType formation,
        float lineOfScrimmage,
        int runningBackSide,
        float dt,
        bool runBlockingBoost,
        Action<Blocker> clampToField,
        Vector2? ballCarrierPosition)
    {
        if (blockers.Count == 0) return;

        RunContext context = BuildRunContext(
            blockers,
            playFamily,
            formation,
            lineOfScrimmage,
            runningBackSide,
            sweepPush: 3.1f,
            basePush: 2.4f,
            runTargetOffset: 2.4f,
            passTargetOffset: -1.4f);

        foreach (var blocker in blockers)
        {
            RunProfile profile = GetRunProfile(blocker, context);
            Vector2 targetAnchor = ComputeAnchorForBlocker(blocker, context, profile);

            // Scale engagement radius by team's blocking strength
            float engageRadius = Constants.BlockEngageRadius * blocker.TeamAttributes.BlockingStrength;
            Defender? target = GetClosestDefender(defenders, blocker.Position, engageRadius, preferRushers: true);
            bool closeToAnchor = IsWithinEngageRange(blocker.Position, targetAnchor, 0.75f);
            
            // For pass plays, prioritize getting to anchor position first before engaging
            // Only engage early if defender is very close (about to get past)
            bool shouldEngage;
            if (context.IsRunPlay)
            {
                // Run plays: prioritize pulling to anchor before engaging.
                bool targetNearAnchor = target != null && IsWithinEngageRange(target.Position, targetAnchor, engageRadius * 1.1f);
                bool defenderVeryClose = target != null && IsWithinEngageRange(blocker.Position, target.Position, engageRadius * 0.6f);
                shouldEngage = target != null && (closeToAnchor || targetNearAnchor || defenderVeryClose);
            }
            else
            {
                // Pass plays: must be close to anchor first, OR defender is extremely close (emergency)
                float emergencyEngageRadius = 1.8f * blocker.TeamAttributes.BlockingStrength;
                bool defenderVeryClose = target != null && IsWithinEngageRange(blocker.Position, target.Position, emergencyEngageRadius);
                shouldEngage = target != null && (closeToAnchor || defenderVeryClose);
            }

            if (shouldEngage)
            {
                blocker.Velocity = ComputeApproachVelocity(blocker, target!, runBlockingBoost, context, profile);
                ApplyBlockContact(blocker, target!, runBlockingBoost, context, profile, dt, ballCarrierPosition);
            }
            else
            {
                MoveTowardAnchor(blocker, targetAnchor, context, profile, closeToAnchor);
            }

            blocker.Update(dt);
            clampToField(blocker);
        }
    }

    public static void DrawRoutes(
        IReadOnlyList<Blocker> blockers,
        PlayType playFamily,
        FormationType formation,
        float lineOfScrimmage,
        int runningBackSide)
    {
        if (blockers.Count == 0) return;

        RunContext context = BuildRunContext(
            blockers,
            playFamily,
            formation,
            lineOfScrimmage,
            runningBackSide,
            sweepPush: 3.1f,
            basePush: 2.4f,
            runTargetOffset: 2.4f,
            passTargetOffset: -1.4f);

        foreach (var blocker in blockers)
        {
            Vector2 start = blocker.Position;
            RunProfile profile = GetRunProfile(blocker, context);
            Vector2 end = ComputeAnchorForBlocker(blocker, context, profile);

            DrawRoute(start, end);
        }
    }

    private static RunContext BuildRunContext(
        IReadOnlyList<Blocker> blockers,
        PlayType playFamily,
        FormationType formation,
        float lineOfScrimmage,
        int runningBackSide,
        float sweepPush,
        float basePush,
        float runTargetOffset,
        float passTargetOffset)
    {
        bool isRunPlay = playFamily == PlayType.Run;
        int runSide = Math.Sign(runningBackSide);
        bool isSweep = isRunPlay && IsSweepFormation(formation);
        float lateralPush = isRunPlay && runSide != 0 ? runSide * (isSweep ? sweepPush : basePush) : 0f;
        float targetY = isRunPlay ? lineOfScrimmage + runTargetOffset : lineOfScrimmage + passTargetOffset;

        return new RunContext(isRunPlay, runSide, isSweep, lateralPush, targetY);
    }

    /// <summary>
    /// Calculates how far back a blocker should drop for pass protection.
    /// Outside tackles drop back more to protect the edges from DEs.
    /// </summary>
    private static float GetPassBlockDropback(float homeX)
    {
        float centerX = Constants.FieldWidth * 0.5f;
        float distFromCenter = MathF.Abs(homeX - centerX);
        
        // Tackles (further from center) drop back more to form pocket
        // Guards and center hold closer to the line
        float maxDropback = 2.5f;  // Max dropback for outside tackles
        float minDropback = 0.8f;  // Min dropback for center
        float dropbackRange = 8f;  // Distance from center where max dropback kicks in
        
        float t = Math.Clamp(distFromCenter / dropbackRange, 0f, 1f);
        return minDropback + (maxDropback - minDropback) * t;
    }

    private static RunProfile GetRunProfile(Blocker blocker, RunContext context)
    {
        bool isBackside = context.IsRunPlay && IsBacksideBlocker(blocker.HomeX, context.RunSide);
        float laneShift = context.IsRunPlay ? GetRunLaneShift(blocker.HomeX, context.RunSide, context.IsSweep) : 0f;
        float backsideShift = context.IsRunPlay ? GetBacksideSealShift(isBackside, context.RunSide) : 0f;

        return new RunProfile(isBackside, laneShift, backsideShift);
    }

    private static Vector2 ComputeAnchorForBlocker(Blocker blocker, RunContext context, RunProfile profile)
    {
        // For pass plays, calculate position-specific dropback for pocket formation
        float passDropback = context.IsRunPlay ? 0f : GetPassBlockDropback(blocker.HomeX);
        
        return ComputeTargetAnchor(
            blocker.HomeX,
            context.LateralPush,
            profile.LaneShift,
            profile.BacksideShift,
            context.TargetY - passDropback);
    }

    private static Vector2 ComputeApproachVelocity(
        Blocker blocker,
        Defender target,
        bool runBlockingBoost,
        RunContext context,
        RunProfile profile)
    {
        Vector2 baseVelocity = NormalizeOrZero(target.Position - blocker.Position) * blocker.Speed;
        float blockStrength = blocker.TeamAttributes.BlockingStrength;
        
        // Only add forward push on run plays, not pass plays
        if (runBlockingBoost && context.IsRunPlay)
        {
            baseVelocity += new Vector2(0f, blocker.Speed * 0.45f * blockStrength);
        }

        if (context.IsRunPlay)
        {
            Vector2 driveDir = profile.IsBackside
                ? new Vector2(-context.RunSide * 0.6f, 1f)
                : new Vector2(context.RunSide * 0.7f, 1f);
            baseVelocity += NormalizeOrZero(driveDir) * (blocker.Speed * 0.35f * blockStrength);
        }
        else
        {
            // Pass blocking: stay between defender and QB, slight backward drift
            baseVelocity += new Vector2(0f, -blocker.Speed * 0.15f);
        }

        return baseVelocity;
    }

    private static void ApplyBlockContact(
        Blocker blocker,
        Defender target,
        bool runBlockingBoost,
        RunContext context,
        RunProfile profile,
        float dt,
        Vector2? ballCarrierPosition)
    {
        float blockStrength = blocker.TeamAttributes.BlockingStrength;
        float contactRangeBoost = runBlockingBoost ? 1.0f * blockStrength : 0.6f * blockStrength;
        float contactRange = blocker.Radius + target.Radius + contactRangeBoost;
        float distance = Vector2.Distance(blocker.Position, target.Position);
        if (distance > contactRange)
        {
            return;
        }

        Vector2 pushDir = NormalizeOrZero(target.Position - blocker.Position);
        float overlap = contactRange - distance;
        float blockMultiplier = GetOlBlockStrength(blocker, target);
        float holdStrength = runBlockingBoost ? Constants.BlockHoldStrength * 1.6f : Constants.BlockHoldStrength;
        float overlapBoost = runBlockingBoost ? 9f : 6f;
        holdStrength *= blockMultiplier;
        overlapBoost *= blockMultiplier;
        float shedBoost = GetTackleShedBoost(target.Position, ballCarrierPosition);
        if (shedBoost > 0f)
        {
            float shedScale = 1f - (0.65f * shedBoost);
            holdStrength *= shedScale;
            overlapBoost *= shedScale;
        }
        target.Position += pushDir * (holdStrength + overlap * overlapBoost) * dt;
        
        // Only drive forward on run plays
        if (context.IsRunPlay)
        {
            Vector2 driveDir = profile.IsBackside
                ? new Vector2(-context.RunSide * 0.55f, 1f)
                : new Vector2(context.RunSide * 0.7f, 1f);
            float driveStrength = (runBlockingBoost ? 1.2f : 0.8f) * blockStrength;
            target.Position += NormalizeOrZero(driveDir) * driveStrength * dt;
        }
        // Pass blocking: push defender laterally away from QB, not forward
        else
        {
            float centerX = Constants.FieldWidth * 0.5f;
            float lateralDir = Math.Sign(target.Position.X - centerX);
            if (lateralDir == 0) lateralDir = Math.Sign(blocker.Position.X - centerX);
            target.Position += new Vector2(lateralDir * 0.8f, -0.3f) * dt;
        }
        float baseSlow = runBlockingBoost ? 0.05f : 0.15f;
        if (shedBoost > 0f)
        {
            baseSlow *= 1f - (0.6f * shedBoost);
        }
        target.Velocity *= GetDefenderSlowdown(blockMultiplier, baseSlow);
        blocker.Velocity *= 0.25f;
    }

    private static void MoveTowardAnchor(Blocker blocker, Vector2 targetAnchor, RunContext context, RunProfile profile, bool closeToAnchor)
    {
        Vector2 toAnchor = NormalizeOrZero(targetAnchor - blocker.Position);
        float anchorSpeed = context.IsRunPlay ? blocker.Speed * 0.85f : blocker.Speed * 0.6f;
        blocker.Velocity = toAnchor * anchorSpeed;
        
        if (context.IsRunPlay)
        {
            float lateralBias = profile.IsBackside ? -context.RunSide : context.RunSide;
            blocker.Velocity += new Vector2(lateralBias * blocker.Speed * 0.2f, blocker.Speed * 0.2f);
        }
        else
        {
            // Pass blocking: drift backward to maintain pocket depth
            blocker.Velocity += new Vector2(0f, -blocker.Speed * 0.25f);
        }

        if (closeToAnchor)
        {
            if (context.IsRunPlay)
            {
                float settleSpeed = blocker.Speed * 0.45f;
                float lateralBias = profile.IsBackside ? -context.RunSide : context.RunSide;
                blocker.Velocity = new Vector2(lateralBias * settleSpeed * 0.3f, settleSpeed);
            }
            else
            {
                // Pass blocking: hold position, slight backward drift
                blocker.Velocity = new Vector2(0f, -blocker.Speed * 0.1f);
            }
        }
    }

    private static void DrawRoute(Vector2 start, Vector2 end)
    {
        Vector2 a = Constants.WorldToScreen(start);
        Vector2 b = Constants.WorldToScreen(end);
        Raylib.DrawLineEx(a, b, 2.0f, Palette.RouteBlocking);

        Vector2 dir = end - start;
        if (dir.LengthSquared() > 0.001f)
        {
            dir = Vector2.Normalize(dir);
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            Vector2 orthoStart = end - perp * 0.8f;
            Vector2 orthoEnd = end + perp * 0.8f;
            Vector2 oA = Constants.WorldToScreen(orthoStart);
            Vector2 oB = Constants.WorldToScreen(orthoEnd);
            Raylib.DrawLineEx(oA, oB, 2.0f, Palette.RouteBlocking);
        }
    }

    private static bool IsWithinEngageRange(Vector2 a, Vector2 b, float range)
    {
        float rangeSq = range * range;
        return Vector2.DistanceSquared(a, b) <= rangeSq;
    }

    private static Defender? GetClosestDefender(
        IReadOnlyList<Defender> defenders,
        Vector2 position,
        float maxDistance,
        bool preferRushers)
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

    private static float GetOlBlockStrength(Blocker blocker, Defender defender)
    {
        float defenderEase = defender.PositionRole switch
        {
            DefensivePosition.DL => 0.75f,
            DefensivePosition.LB => 0.95f,
            _ => 1.15f
        };

        float teamStrength = blocker.TeamAttributes.BlockingStrength;
        return 1.35f * defenderEase * teamStrength;
    }

    private static float GetDefenderSlowdown(float blockMultiplier, float baseSlow)
    {
        float bonus = Math.Clamp(blockMultiplier - 1f, -0.6f, 0.6f);
        float adjusted = baseSlow - bonus * 0.06f;
        return Math.Clamp(adjusted, 0.05f, 0.22f);
    }

    private static float GetTackleShedBoost(Vector2 defenderPosition, Vector2? ballCarrierPosition)
    {
        if (ballCarrierPosition == null)
        {
            return 0f;
        }

        float distance = Vector2.Distance(defenderPosition, ballCarrierPosition.Value);
        const float shedRange = 6.0f;
        if (distance >= shedRange)
        {
            return 0f;
        }

        float t = 1f - (distance / shedRange);
        return Math.Clamp(t, 0f, 1f);
    }

    private static Vector2 NormalizeOrZero(Vector2 vector)
    {
        return vector.LengthSquared() > 0.001f ? Vector2.Normalize(vector) : Vector2.Zero;
    }

    private static Vector2 ComputeTargetAnchor(
        float homeX,
        float lateralPush,
        float laneShift,
        float backsideShift,
        float targetY)
    {
        float clampedX = Math.Clamp(homeX + lateralPush + laneShift + backsideShift, 1.5f, Constants.FieldWidth - 1.5f);
        return new Vector2(clampedX, targetY);
    }

    private static bool IsBacksideBlocker(float homeX, int runSide)
    {
        if (runSide == 0)
        {
            return false;
        }

        float centerX = Constants.FieldWidth * 0.5f;
        int side = Math.Sign(homeX - centerX);
        if (side == 0)
        {
            side = -runSide;
        }

        return side == -runSide;
    }

    private static float GetBacksideSealShift(bool isBackside, int runSide)
    {
        if (!isBackside || runSide == 0)
        {
            return 0f;
        }

        float sealAmount = 1.4f;
        return -runSide * sealAmount;
    }

    private static float GetRunLaneShift(float homeX, int runSide, bool isSweep)
    {
        if (runSide == 0)
        {
            return 0f;
        }

        float centerX = Constants.FieldWidth * 0.5f;
        float laneCenterX = centerX + (runSide * (isSweep ? 4.4f : 3.2f));
        float deltaFromLane = homeX - laneCenterX;
        int laneSide = Math.Sign(deltaFromLane);
        if (laneSide == 0)
        {
            laneSide = -runSide;
        }

        float baseSeparation = 1.2f;
        float distanceBias = Math.Clamp(MathF.Abs(deltaFromLane) / 5.5f, 0f, 1f);
        float separation = baseSeparation + (distanceBias * 0.6f);
        return laneSide * separation;
    }

    private static bool IsSweepFormation(FormationType formation)
    {
        return formation is FormationType.RunSweepLeft
            or FormationType.RunSweepRight
            or FormationType.RunTossLeft
            or FormationType.RunTossRight;
    }

    private readonly struct RunContext
    {
        public RunContext(
            bool isRunPlay,
            int runSide,
            bool isSweep,
            float lateralPush,
            float targetY)
        {
            IsRunPlay = isRunPlay;
            RunSide = runSide;
            IsSweep = isSweep;
            LateralPush = lateralPush;
            TargetY = targetY;
        }

        public bool IsRunPlay { get; }
        public int RunSide { get; }
        public bool IsSweep { get; }
        public float LateralPush { get; }
        public float TargetY { get; }
    }

    private readonly struct RunProfile
    {
        public RunProfile(bool isBackside, float laneShift, float backsideShift)
        {
            IsBackside = isBackside;
            LaneShift = laneShift;
            BacksideShift = backsideShift;
        }

        public bool IsBackside { get; }
        public float LaneShift { get; }
        public float BacksideShift { get; }
    }
}