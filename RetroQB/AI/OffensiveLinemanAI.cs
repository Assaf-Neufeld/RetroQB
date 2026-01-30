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
            float pullShift = profile.IsPuller ? context.RunSide * (context.IsSweep ? 3.6f : 2.6f) : 0f;
            float pullForward = profile.IsPuller ? (context.IsSweep ? 1.6f : 1.1f) : 0f;
            Vector2 targetAnchor = ComputeAnchorForBlocker(blocker, context, profile, pullShift, pullForward);

            Defender? target = GetClosestDefender(defenders, blocker.Position, Constants.BlockEngageRadius, preferRushers: true);
            bool closeToAnchor = IsWithinEngageRange(blocker.Position, targetAnchor, 0.75f);
            bool shouldEngage = target != null
                && (closeToAnchor || IsWithinEngageRange(blocker.Position, target.Position, Constants.BlockEngageRadius));

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
            sweepPush: 2.8f,
            basePush: 2.1f,
            runTargetOffset: 1.6f,
            passTargetOffset: -1.4f);

        foreach (var blocker in blockers)
        {
            Vector2 start = blocker.Position;
            RunProfile profile = GetRunProfile(blocker, context);
            float pullShift = profile.IsPuller ? context.RunSide * (context.IsSweep ? 3.4f : 2.4f) : 0f;
            float pullForward = profile.IsPuller ? (context.IsSweep ? 1.2f : 0.8f) : 0f;
            Vector2 end = ComputeAnchorForBlocker(blocker, context, profile, pullShift, pullForward);

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
        var pullingGuards = isRunPlay && runSide != 0
            ? IdentifyPullingGuards(blockers, runSide, isSweep)
            : EmptyPullerSet;

        return new RunContext(isRunPlay, runSide, isSweep, lateralPush, targetY, pullingGuards);
    }

    private static RunProfile GetRunProfile(Blocker blocker, RunContext context)
    {
        bool isPuller = context.IsRunPlay && context.PullingGuards.Contains(blocker);
        bool isBackside = context.IsRunPlay && !isPuller && IsBacksideBlocker(blocker.HomeX, context.RunSide);
        float laneShift = context.IsRunPlay ? GetRunLaneShift(blocker.HomeX, context.RunSide, context.IsSweep) : 0f;
        float backsideShift = context.IsRunPlay ? GetBacksideSealShift(isBackside, context.RunSide) : 0f;

        return new RunProfile(isPuller, isBackside, laneShift, backsideShift);
    }

    private static Vector2 ComputeAnchorForBlocker(Blocker blocker, RunContext context, RunProfile profile, float pullShift, float pullForward)
    {
        return ComputeTargetAnchor(
            blocker.HomeX,
            context.LateralPush,
            profile.LaneShift,
            profile.BacksideShift,
            pullShift,
            context.TargetY,
            pullForward);
    }

    private static Vector2 ComputeApproachVelocity(
        Blocker blocker,
        Defender target,
        bool runBlockingBoost,
        RunContext context,
        RunProfile profile)
    {
        Vector2 baseVelocity = NormalizeOrZero(target.Position - blocker.Position) * blocker.Speed;
        if (runBlockingBoost)
        {
            baseVelocity += new Vector2(0f, blocker.Speed * 0.45f);
        }

        if (context.IsRunPlay)
        {
            Vector2 driveDir = profile.IsBackside
                ? new Vector2(-context.RunSide * 0.6f, 1f)
                : new Vector2(context.RunSide * (profile.IsPuller ? 0.9f : 0.7f), 1f);
            baseVelocity += NormalizeOrZero(driveDir) * (blocker.Speed * 0.35f);
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
        float contactRange = blocker.Radius + target.Radius + (runBlockingBoost ? 1.0f : 0.6f);
        float distance = Vector2.Distance(blocker.Position, target.Position);
        if (distance > contactRange)
        {
            return;
        }

        Vector2 pushDir = NormalizeOrZero(target.Position - blocker.Position);
        float overlap = contactRange - distance;
        float blockMultiplier = GetOlBlockStrength(target);
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
        if (context.IsRunPlay)
        {
            Vector2 driveDir = profile.IsBackside
                ? new Vector2(-context.RunSide * 0.55f, 1f)
                : new Vector2(context.RunSide * (profile.IsPuller ? 0.95f : 0.7f), 1f);
            target.Position += NormalizeOrZero(driveDir) * (runBlockingBoost ? 1.2f : 0.8f) * dt;
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
        float anchorSpeed = context.IsRunPlay ? blocker.Speed * 0.85f : blocker.Speed * 0.7f;
        blocker.Velocity = toAnchor * anchorSpeed;
        if (context.IsRunPlay)
        {
            float lateralBias = profile.IsBackside ? -context.RunSide : context.RunSide;
            blocker.Velocity += new Vector2(lateralBias * blocker.Speed * 0.2f, blocker.Speed * 0.2f);
            if (profile.IsPuller)
            {
                blocker.Velocity += new Vector2(context.RunSide * blocker.Speed * 0.35f, blocker.Speed * 0.1f);
            }
        }

        if (closeToAnchor)
        {
            float settleSpeed = context.IsRunPlay ? blocker.Speed * 0.45f : blocker.Speed * 0.1f;
            float lateralBias = profile.IsBackside ? -context.RunSide : context.RunSide;
            blocker.Velocity = new Vector2(lateralBias * settleSpeed * 0.3f, settleSpeed * (context.IsRunPlay ? 1f : -1f));
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

    private static float GetOlBlockStrength(Defender defender)
    {
        float defenderEase = defender.PositionRole switch
        {
            DefensivePosition.DL => 0.75f,
            DefensivePosition.LB => 0.95f,
            _ => 1.15f
        };

        return 1.35f * defenderEase;
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
        float pullShift,
        float targetY,
        float pullForward)
    {
        float clampedX = Math.Clamp(homeX + lateralPush + laneShift + backsideShift + pullShift, 1.5f, Constants.FieldWidth - 1.5f);
        return new Vector2(clampedX, targetY + pullForward);
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
            float targetY,
            HashSet<Blocker> pullingGuards)
        {
            IsRunPlay = isRunPlay;
            RunSide = runSide;
            IsSweep = isSweep;
            LateralPush = lateralPush;
            TargetY = targetY;
            PullingGuards = pullingGuards;
        }

        public bool IsRunPlay { get; }
        public int RunSide { get; }
        public bool IsSweep { get; }
        public float LateralPush { get; }
        public float TargetY { get; }
        public HashSet<Blocker> PullingGuards { get; }
    }

    private readonly struct RunProfile
    {
        public RunProfile(bool isPuller, bool isBackside, float laneShift, float backsideShift)
        {
            IsPuller = isPuller;
            IsBackside = isBackside;
            LaneShift = laneShift;
            BacksideShift = backsideShift;
        }

        public bool IsPuller { get; }
        public bool IsBackside { get; }
        public float LaneShift { get; }
        public float BacksideShift { get; }
    }

    private static readonly HashSet<Blocker> EmptyPullerSet = new();

    private static HashSet<Blocker> IdentifyPullingGuards(IReadOnlyList<Blocker> blockers, int runSide, bool isSweep)
    {
        var pullers = new HashSet<Blocker>();
        if (blockers.Count < 3 || runSide == 0)
        {
            return pullers;
        }

        float centerX = Constants.FieldWidth * 0.5f;
        var ordered = blockers.OrderBy(b => Math.Abs(b.HomeX - centerX)).ToList();
        Blocker center = ordered[0];
        Blocker? leftGuard = ordered.FirstOrDefault(b => b.HomeX < centerX && b != center);
        Blocker? rightGuard = ordered.FirstOrDefault(b => b.HomeX > centerX && b != center);

        if (isSweep)
        {
            if (leftGuard != null) pullers.Add(leftGuard);
            if (rightGuard != null) pullers.Add(rightGuard);
            return pullers;
        }

        if (runSide > 0)
        {
            if (leftGuard != null) pullers.Add(leftGuard);
        }
        else
        {
            if (rightGuard != null) pullers.Add(rightGuard);
        }

        return pullers;
    }
}