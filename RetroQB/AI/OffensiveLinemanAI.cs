using System;
using System.Numerics;
using System.Linq;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;

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

        // Reset blocked state each frame — will be set by contact checks below
        foreach (var defender in defenders)
        {
            defender.IsBeingBlocked = false;
        }

        RunContext context = BuildRunContext(
            blockers,
            playFamily,
            formation,
            lineOfScrimmage,
            runningBackSide,
            sweepPush: 4.2f,
            basePush: 3.5f,
            runTargetOffset: 3.2f,
            passTargetOffset: -1.4f);

        foreach (var blocker in blockers)
        {
            RunProfile profile = GetRunProfile(blocker, context);
            Vector2 targetAnchor = ComputeAnchorForBlocker(blocker, context, profile);

            // Scale engagement radius by team's blocking strength
            // Tackles get a wider engagement zone to pick up edge rushers earlier
            float engageRadius = Constants.BlockEngageRadius * blocker.TeamAttributes.BlockingStrength;
            if (IsTacklePosition(blocker.HomeX))
            {
                engageRadius *= 1.15f;
            }
            // On run plays, engage any defender (including DBs coming up); on pass plays prefer rushers
            Defender? target = GetClosestDefender(defenders, blocker.Position, engageRadius, preferRushers: !context.IsRunPlay);
            bool closeToAnchor = IsWithinEngageRange(blocker.Position, targetAnchor, 0.9f);
            
            // For pass plays, prioritize getting to anchor position first before engaging
            // Only engage early if defender is very close (about to get past)
            bool shouldEngage;
            if (context.IsRunPlay)
            {
                // Run plays: commit to pulling — only engage once near anchor or defender is right on top
                bool targetNearAnchor = target != null && IsWithinEngageRange(target.Position, targetAnchor, engageRadius * 0.75f);
                bool defenderVeryClose = target != null && IsWithinEngageRange(blocker.Position, target.Position, engageRadius * 0.4f);
                shouldEngage = target != null && (closeToAnchor || targetNearAnchor || defenderVeryClose);
            }
            else
            {
                // Pass plays: must be close to anchor first, OR defender is close (emergency)
                // Tackles get a wider emergency radius to pick up edge rushers
                float emergencyEngageRadius = 2.6f * blocker.TeamAttributes.BlockingStrength;
                if (IsTacklePosition(blocker.HomeX) && target != null && target.PositionRole == DefensivePosition.DE)
                {
                    emergencyEngageRadius *= 1.3f;
                }
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
            sweepPush: 4.2f,
            basePush: 3.5f,
            runTargetOffset: 3.2f,
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
        bool isSweep = isRunPlay && BlockingUtils.IsSweepFormation(formation);
        bool isStretch = isRunPlay && BlockingUtils.IsStretchFormation(formation);
        float stretchPush = (basePush + sweepPush) * 0.5f; // midpoint between power and sweep
        float lateralPush = isRunPlay && runSide != 0
            ? runSide * (isStretch ? stretchPush : isSweep ? sweepPush : basePush)
            : 0f;
        float targetY = isRunPlay ? lineOfScrimmage + runTargetOffset : lineOfScrimmage + passTargetOffset;

        return new RunContext(isRunPlay, runSide, isSweep, isStretch, lateralPush, targetY);
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
        float laneShift = context.IsRunPlay ? GetRunLaneShift(blocker.HomeX, context.RunSide, context.IsSweep, context.IsStretch) : 0f;
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
        Vector2 baseVelocity = BlockingUtils.SafeNormalize(target.Position - blocker.Position) * blocker.Speed;
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
            baseVelocity += BlockingUtils.SafeNormalize(driveDir) * (blocker.Speed * 0.35f * blockStrength);
        }
        else
        {
            // Pass blocking: stay between defender and QB
            // Tackles mirror laterally toward edge rushers to cut off the corner
            if (IsTacklePosition(blocker.HomeX) && target.PositionRole == DefensivePosition.DE)
            {
                float sideDir = MathF.Sign(target.Position.X - blocker.Position.X);
                baseVelocity += new Vector2(sideDir * blocker.Speed * 0.40f, -blocker.Speed * 0.05f);
            }
            else
            {
                baseVelocity += new Vector2(0f, -blocker.Speed * 0.15f);
            }
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

        // Mark the defender as actively being blocked
        target.IsBeingBlocked = true;

        Vector2 pushDir = BlockingUtils.SafeNormalize(target.Position - blocker.Position);
        float overlap = contactRange - distance;
        float blockMultiplier = GetOlBlockStrength(blocker, target);
        float holdStrength = runBlockingBoost ? Constants.BlockHoldStrength * 1.6f : Constants.BlockHoldStrength;
        float overlapBoost = runBlockingBoost ? 9f : 6f;
        holdStrength *= blockMultiplier;
        overlapBoost *= blockMultiplier;
        float shedBoost = BlockingUtils.GetTackleShedBoost(target.Position, ballCarrierPosition);
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
            target.Position += BlockingUtils.SafeNormalize(driveDir) * driveStrength * dt;
        }
        // Pass blocking: push defender laterally away from QB, not forward
        else
        {
            float centerX = Constants.FieldWidth * 0.5f;
            float lateralDir = Math.Sign(target.Position.X - centerX);
            if (lateralDir == 0) lateralDir = Math.Sign(blocker.Position.X - centerX);
            float lateralPush = target.PositionRole == DefensivePosition.DE ? 1.4f : 0.8f;
            target.Position += new Vector2(lateralDir * lateralPush, -0.4f) * dt;
        }
        float baseSlow = runBlockingBoost ? 0.05f : 0.12f;
        // DEs get extra slowdown when contacted — OL should neutralize their speed advantage
        if (target.PositionRole == DefensivePosition.DE)
        {
            baseSlow *= 0.7f;
        }
        if (shedBoost > 0f)
        {
            baseSlow *= 1f - (0.6f * shedBoost);
        }
        target.Velocity *= BlockingUtils.GetDefenderSlowdown(blockMultiplier, baseSlow);
        blocker.Velocity *= 0.25f;
    }

    private static void MoveTowardAnchor(Blocker blocker, Vector2 targetAnchor, RunContext context, RunProfile profile, bool closeToAnchor)
    {
        Vector2 toAnchor = BlockingUtils.SafeNormalize(targetAnchor - blocker.Position);
        float anchorSpeed = context.IsRunPlay ? blocker.Speed * 1.1f : blocker.Speed * 0.6f;
        blocker.Velocity = toAnchor * anchorSpeed;
        
        if (context.IsRunPlay)
        {
            // Strong lateral pull toward the run side — backside blockers pull across, playside push out
            float lateralBias = profile.IsBackside ? -context.RunSide : context.RunSide;
            float lateralMult = profile.IsBackside ? 0.45f : 0.3f;  // Backside blockers pull harder
            blocker.Velocity += new Vector2(lateralBias * blocker.Speed * lateralMult, blocker.Speed * 0.25f);
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
                float settleSpeed = blocker.Speed * 0.65f;
                float lateralBias = profile.IsBackside ? -context.RunSide : context.RunSide;
                blocker.Velocity = new Vector2(lateralBias * settleSpeed * 0.4f, settleSpeed);
            }
            else
            {
                // Pass blocking: hold position, slight backward drift
                blocker.Velocity = new Vector2(0f, -blocker.Speed * 0.1f);
            }
        }
    }

    private static readonly Color OlRouteColor = new(255, 214, 74, 100);

    private static void DrawRoute(Vector2 start, Vector2 end)
    {
        Vector2 a = Constants.WorldToScreen(start);
        Vector2 b = Constants.WorldToScreen(end);
        Raylib.DrawLineEx(a, b, 2.0f, OlRouteColor);

        Vector2 dir = end - start;
        if (dir.LengthSquared() > 0.001f)
        {
            dir = Vector2.Normalize(dir);
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            Vector2 orthoStart = end - perp * 0.8f;
            Vector2 orthoEnd = end + perp * 0.8f;
            Vector2 oA = Constants.WorldToScreen(orthoStart);
            Vector2 oB = Constants.WorldToScreen(orthoEnd);
            Raylib.DrawLineEx(oA, oB, 2.0f, OlRouteColor);
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
            DefensivePosition.DE => 0.90f,
            DefensivePosition.LB => 0.95f,
            _ => 1.15f
        };

        // Tackles get a large positional advantage vs DEs — that's their primary matchup
        if (defender.PositionRole == DefensivePosition.DE && IsTacklePosition(blocker.HomeX))
        {
            defenderEase += 0.40f;
        }

        float teamStrength = blocker.TeamAttributes.BlockingStrength;
        return 1.45f * defenderEase * teamStrength;
    }

    /// <summary>
    /// Returns true if the blocker is positioned as an offensive tackle (outer linemen).
    /// Tackles are the OL furthest from field center.
    /// </summary>
    private static bool IsTacklePosition(float homeX)
    {
        float centerX = Constants.FieldWidth * 0.5f;
        float distFromCenter = MathF.Abs(homeX - centerX);
        // Base OL at 0.42/0.46/0.50/0.54/0.58 — tackles are ~3.7+ units from center
        return distFromCenter >= 3.5f;
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

        // Backside blockers pull across toward the run side to seal pursuit
        float sealAmount = 2.8f;
        return -runSide * sealAmount;
    }

    private static float GetRunLaneShift(float homeX, int runSide, bool isSweep, bool isStretch)
    {
        if (runSide == 0)
        {
            return 0f;
        }

        float centerX = Constants.FieldWidth * 0.5f;
        // Stretch: tighter wall (4.4) between power (3.8) and sweep (5.2)
        float laneOffset = isStretch ? 4.4f : isSweep ? 5.2f : 3.8f;
        float laneCenterX = centerX + (runSide * laneOffset);
        float deltaFromLane = homeX - laneCenterX;
        int laneSide = Math.Sign(deltaFromLane);
        if (laneSide == 0)
        {
            laneSide = -runSide;
        }

        // Stretch: tighter separation so blockers form a cohesive wall
        float baseSeparation = isStretch ? 1.1f : 1.4f;
        float distanceBias = Math.Clamp(MathF.Abs(deltaFromLane) / 5.5f, 0f, 1f);
        float separation = baseSeparation + (distanceBias * (isStretch ? 0.5f : 0.7f));
        return laneSide * separation;
    }



    private readonly struct RunContext
    {
        public RunContext(
            bool isRunPlay,
            int runSide,
            bool isSweep,
            bool isStretch,
            float lateralPush,
            float targetY)
        {
            IsRunPlay = isRunPlay;
            RunSide = runSide;
            IsSweep = isSweep;
            IsStretch = isStretch;
            LateralPush = lateralPush;
            TargetY = targetY;
        }

        public bool IsRunPlay { get; }
        public int RunSide { get; }
        public bool IsSweep { get; }
        public bool IsStretch { get; }
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