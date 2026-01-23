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
        float lineOfScrimmage,
        int runningBackSide,
        float dt,
        bool runBlockingBoost,
        Action<Blocker> clampToField)
    {
        if (blockers.Count == 0) return;

        bool isRunPlay = playFamily == PlayType.QbRunFocus;
        int runSide = Math.Sign(runningBackSide);
        float lateralPush = isRunPlay && runSide != 0 ? runSide * 3.5f : 0f;
        float targetY = isRunPlay ? lineOfScrimmage + 2.4f : lineOfScrimmage - 1.4f;

        foreach (var blocker in blockers)
        {
            Vector2 targetAnchor = new Vector2(
                Math.Clamp(blocker.HomeX + lateralPush, 1.5f, Constants.FieldWidth - 1.5f),
                targetY);

            Defender? target = GetClosestDefender(defenders, blocker.Position, Constants.BlockEngageRadius, preferRushers: true);
            float anchorDistSq = Vector2.DistanceSquared(blocker.Position, targetAnchor);
            bool closeToAnchor = anchorDistSq <= 0.75f * 0.75f;

            if (target != null && (closeToAnchor || Vector2.DistanceSquared(blocker.Position, target.Position) <= Constants.BlockEngageRadius * Constants.BlockEngageRadius))
            {
                Vector2 toTarget = target.Position - blocker.Position;
                if (toTarget.LengthSquared() > 0.001f)
                {
                    toTarget = Vector2.Normalize(toTarget);
                }
                Vector2 baseVelocity = toTarget * blocker.Speed;
                if (runBlockingBoost)
                {
                    baseVelocity += new Vector2(0f, blocker.Speed * 0.45f);
                }
                if (isRunPlay)
                {
                    Vector2 driveDir = new Vector2(runSide * 0.7f, 1f);
                    if (driveDir.LengthSquared() > 0.001f)
                    {
                        driveDir = Vector2.Normalize(driveDir);
                    }
                    baseVelocity += driveDir * (blocker.Speed * 0.35f);
                }
                blocker.Velocity = baseVelocity;

                float contactRange = blocker.Radius + target.Radius + (runBlockingBoost ? 1.0f : 0.6f);
                float distance = Vector2.Distance(blocker.Position, target.Position);
                if (distance <= contactRange)
                {
                    Vector2 pushDir = target.Position - blocker.Position;
                    if (pushDir.LengthSquared() > 0.001f)
                    {
                        pushDir = Vector2.Normalize(pushDir);
                    }
                    float overlap = contactRange - distance;
                    float blockMultiplier = GetOlBlockStrength(target);
                    float holdStrength = runBlockingBoost ? Constants.BlockHoldStrength * 1.6f : Constants.BlockHoldStrength;
                    float overlapBoost = runBlockingBoost ? 9f : 6f;
                    holdStrength *= blockMultiplier;
                    overlapBoost *= blockMultiplier;
                    target.Position += pushDir * (holdStrength + overlap * overlapBoost) * dt;
                    if (isRunPlay)
                    {
                        Vector2 driveDir = new Vector2(runSide * 0.7f, 1f);
                        if (driveDir.LengthSquared() > 0.001f)
                        {
                            driveDir = Vector2.Normalize(driveDir);
                        }
                        target.Position += driveDir * (runBlockingBoost ? 1.2f : 0.8f) * dt;
                    }
                    float baseSlow = runBlockingBoost ? 0.05f : 0.15f;
                    target.Velocity *= GetDefenderSlowdown(blockMultiplier, baseSlow);
                    blocker.Velocity *= 0.25f;
                }
            }
            else
            {
                Vector2 toAnchor = targetAnchor - blocker.Position;
                if (toAnchor.LengthSquared() > 0.001f)
                {
                    toAnchor = Vector2.Normalize(toAnchor);
                }

                float anchorSpeed = isRunPlay ? blocker.Speed * 0.85f : blocker.Speed * 0.7f;
                blocker.Velocity = toAnchor * anchorSpeed;
                if (isRunPlay)
                {
                    blocker.Velocity += new Vector2(runSide * blocker.Speed * 0.15f, blocker.Speed * 0.2f);
                }

                if (closeToAnchor)
                {
                    float settleSpeed = isRunPlay ? blocker.Speed * 0.45f : blocker.Speed * 0.1f;
                    blocker.Velocity = new Vector2(runSide * settleSpeed * 0.3f, settleSpeed * (isRunPlay ? 1f : -1f));
                }
            }

            blocker.Update(dt);
            clampToField(blocker);
        }
    }

    public static void DrawRoutes(
        IReadOnlyList<Blocker> blockers,
        PlayType playFamily,
        float lineOfScrimmage,
        int runningBackSide)
    {
        if (blockers.Count == 0) return;

        bool isRunPlay = playFamily == PlayType.QbRunFocus;
        int runSide = Math.Sign(runningBackSide);
        float lateralPush = isRunPlay && runSide != 0 ? runSide * 2.8f : 0f;
        float targetY = isRunPlay ? lineOfScrimmage + 1.6f : lineOfScrimmage - 1.4f;

        foreach (var blocker in blockers)
        {
            Vector2 start = blocker.Position;
            Vector2 end = new Vector2(
                Math.Clamp(blocker.HomeX + lateralPush, 1.5f, Constants.FieldWidth - 1.5f),
                targetY);

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
}