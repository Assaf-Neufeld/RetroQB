using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

public sealed class BlockingState
{
    public bool ReachedApproach { get; internal set; }
    public Defender? Target { get; internal set; }
    internal BlockingAssignment? Assignment;
    public void Reset() { ReachedApproach = false; Target = null; Assignment = null; }
}

public readonly record struct BlockingDecision(Vector2 Landmark, Defender? Target, bool FollowingApproach);

/// <summary>Assignment-based target selection shared by skill blockers and linemen. Contact physics stays separate.</summary>
public static class BlockingSteering
{
    public static IReadOnlyList<Vector2> GetLandmarks(BlockingAssignment assignment, float homeX, float los, Vector2 qb)
    {
        Vector2 origin = assignment.Anchor switch
        {
            BlockingAnchor.Quarterback => qb,
            BlockingAnchor.FieldCenter => new(Constants.FieldWidth * 0.5f, los),
            _ => new(homeX, los)
        };
        Vector2 At(Vector2 offset) => new(Math.Clamp(origin.X + offset.X, 1.5f, Constants.FieldWidth - 1.5f),
            Math.Clamp(origin.Y + offset.Y, 0.6f, Constants.FieldLength - 0.6f));
        return assignment.ApproachOffset is Vector2 via ? new[] { At(via), At(assignment.TargetOffset) } : new[] { At(assignment.TargetOffset) };
    }

    public static BlockingDecision Decide(BlockingAssignment assignment, BlockingState state, Vector2 position,
        float homeX, float los, Vector2 qb, IReadOnlyList<Defender> defenders, float radius,
        Defender? protectionTarget = null, bool coordinatedProtection = false)
    {
        if (!ReferenceEquals(state.Assignment, assignment)) { state.Reset(); state.Assignment = assignment; }
        var landmarks = GetLandmarks(assignment, homeX, los, qb);
        if (landmarks.Count > 1 && !state.ReachedApproach)
        {
            if (Vector2.Distance(position, landmarks[0]) <= 0.35f) state.ReachedApproach = true;
            else return new(landmarks[0], null, true);
        }
        Vector2 anchor = landmarks[^1];
        bool protect = !assignment.IsRunBlock;
        if (protect && coordinatedProtection)
        {
            // The line planner owns target coverage and pursuit range. Independent
            // fallback here would double an inside rusher and abandon another lane.
            state.Target = protectionTarget;
            return new(anchor, state.Target, false);
        }
        // Recover into the pocket after losing a block, while keeping pursuit local.
        if (state.Target != null && (!defenders.Contains(state.Target)
            || (Vector2.Distance(state.Target.Position, anchor) > radius * 1.5f
                && !(protect && state.Target.IsRusher
                    && Vector2.Distance(state.Target.Position, qb) <= radius * 1.5f))
            || Vector2.Distance(state.Target.Position, position) > radius * 1.5f)) state.Target = null;
        if (state.Target == null)
        {
            float best = float.MaxValue;
            foreach (var defender in defenders)
            {
                float toAnchor = Vector2.Distance(defender.Position, protect ? qb : anchor);
                float toPlayer = Vector2.Distance(defender.Position, position);
                if (toPlayer > radius && toAnchor > radius) continue;
                // Lead/kick-out blockers select at the called gap, not the nearest QB threat.
                if (!protect && toAnchor > radius) continue;
                float score = protect ? MathF.Min(toAnchor, toPlayer) - (defender.IsRusher ? 6f : 0f) : toAnchor;
                if (assignment.Job == BlockingJob.KickOut && defender.PositionRole == DefensivePosition.DE) score -= 2f;
                if (score < best) { best = score; state.Target = defender; }
            }
        }
        var target = state.Target;
        bool canEngage = target != null && (Vector2.Distance(position, anchor) <= 1.2f
            || Vector2.Distance(position, target.Position) <= 2.6f
            || (protect && target.IsRusher)
            || (!protect && Vector2.Distance(target.Position, anchor) <= radius * 0.6f));
        return new(anchor, canEngage ? target : null, false);
    }

    public static Vector2 MoveTo(Vector2 position, Vector2 target, float speed, float dt)
    {
        Vector2 delta = target - position;
        float distance = delta.Length();
        return dt > 0 && distance > 0.01f ? delta / distance * MathF.Min(speed, distance / dt) : Vector2.Zero;
    }
}
