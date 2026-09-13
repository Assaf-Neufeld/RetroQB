using System.Numerics;
using Raylib_cs;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;

namespace RetroQB.AI;

/// <summary>Executes resolved line jobs; formation/concept decisions belong to BlockingPlanner.</summary>
public static class OffensiveLinemanAI
{
    public static void UpdateBlockers(IReadOnlyList<Blocker> blockers, IReadOnlyList<Defender> defenders,
        ResolvedPlay play, float lineOfScrimmage, float dt, bool runBlockingBoost,
        Action<Blocker> clampToField, Vector2? ballCarrierPosition, Vector2? quarterbackPosition = null, bool? openingComplete = null)
    {
        if (dt <= 0) return;
        Vector2 qb = quarterbackPosition ?? play.Quarterback.AtLineOfScrimmage(lineOfScrimmage);
        var protection = PassProtectionPlanner.Assign(blockers, defenders, openingComplete ?? runBlockingBoost);
        foreach (var blocker in blockers)
        {
            var assignment = ((openingComplete ?? runBlockingBoost) ? blocker.BlockingAssignment : blocker.OpeningAssignment)
                ?? throw new InvalidOperationException("Lineman has no resolved blocking assignment.");
            float radius = Constants.BlockEngageRadius * blocker.TeamAttributes.BlockingStrength;
            if (IsTacklePosition(blocker.HomeX)) radius *= 1.15f;
            protection.TryGetValue(blocker, out var assignedRusher);
            var decision = BlockingSteering.Decide(assignment, blocker.BlockingState, blocker.Position,
                blocker.HomeX, lineOfScrimmage, qb, defenders, radius, assignedRusher,
                coordinatedProtection: !assignment.IsRunBlock);
            if (decision.Target is Defender target)
            {
                blocker.Velocity = ComputeApproachVelocity(blocker, target, assignment, qb, dt);
                ApplyBlockContact(blocker, target, runBlockingBoost, assignment, dt, ballCarrierPosition);
                clampToField(blocker);
            }
            else
            {
                blocker.Velocity = BlockingSteering.MoveTo(blocker.Position, decision.Landmark,
                    blocker.Speed * (assignment.IsRunBlock ? 1.1f : 0.6f), dt);
            }
            blocker.Update(dt);
            clampToField(blocker);
        }
    }

    public static void DrawRoutes(IReadOnlyList<Blocker> blockers, ResolvedPlay play, float lineOfScrimmage)
    {
        foreach (var blocker in blockers)
        {
            var job = blocker.BlockingAssignment;
            if (job == null) continue;
            Vector2 start = blocker.Position;
            foreach (var end in BlockingSteering.GetLandmarks(job, blocker.HomeX, lineOfScrimmage,
                play.Quarterback.AtLineOfScrimmage(lineOfScrimmage)))
            {
                DrawRoute(start, end);
                start = end;
            }
        }
    }

    private static Vector2 ComputeApproachVelocity(
        Blocker blocker,
        Defender target,
        BlockingAssignment assignment,
        Vector2 qb,
        float dt)
    {
        if (!assignment.IsRunBlock)
        {
            // Set on the QB side of the rush instead of charging upfield at it.
            // Once beaten, close directly so the offset cannot hold us off contact.
            bool beaten = target.Position.Y < blocker.Position.Y;
            Vector2 intercept = beaten ? target.Position : target.Position
                + BlockingUtils.SafeNormalize(qb - target.Position) * (blocker.Radius + target.Radius);
            if (!beaten) intercept.Y = MathF.Min(intercept.Y, blocker.HomeY - 1.5f);
            return BlockingSteering.MoveTo(blocker.Position, intercept, blocker.Speed, dt);
        }
        // Close on the defender first. Forward drive belongs to contact physics;
        // adding it here carries blockers past lateral or backfield penetration.
        return BlockingSteering.MoveTo(blocker.Position, target.Position, blocker.Speed, dt);
    }

    private static void ApplyBlockContact(
        Blocker blocker,
        Defender target,
        bool runBlockingBoost,
        BlockingAssignment assignment,
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

        BlockingUtils.RegisterBlockContact(target);
        float doubleTeamEffectiveness = BlockingUtils.GetDoubleTeamEffectiveness(target);

        Vector2 pushDir = BlockingUtils.SafeNormalize(target.Position - blocker.Position);
        float overlap = contactRange - distance;
        float blockMultiplier = GetOlBlockStrength(blocker, target);
        float holdStrength = runBlockingBoost ? Constants.BlockHoldStrength * 1.6f : Constants.BlockHoldStrength;
        float overlapBoost = runBlockingBoost ? 9f : 6f;
        holdStrength *= blockMultiplier * doubleTeamEffectiveness;
        overlapBoost *= blockMultiplier * (0.85f + 0.15f * doubleTeamEffectiveness);
        float shedBoost = BlockingUtils.GetTackleShedBoost(target.Position, ballCarrierPosition);
        if (shedBoost > 0f)
        {
            float shedScale = 1f - (0.65f * shedBoost);
            holdStrength *= shedScale;
            overlapBoost *= shedScale;
        }
        target.Position += pushDir * (holdStrength + overlap * overlapBoost) * dt;
        
        // Only drive forward on run plays
        if (assignment.IsRunBlock)
        {
            Vector2 driveDir = assignment.DriveDirection;
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
        if (doubleTeamEffectiveness > 1f)
        {
            baseSlow *= 0.45f;
        }
        target.Velocity *= BlockingUtils.GetDefenderSlowdown(blockMultiplier, baseSlow);
        blocker.Velocity *= 0.25f;
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

}
