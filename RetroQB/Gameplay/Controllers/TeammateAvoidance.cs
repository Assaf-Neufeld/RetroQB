using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>Short-range footwork around teammates; contact is still handled by OverlapResolver.</summary>
public static class TeammateAvoidance
{
    private const float LookAheadSeconds = 0.65f;
    private const float Clearance = 0.25f;
    private readonly record struct Obstacle(Vector2 Position, Vector2 Velocity, float Radius);

    public static void Apply(IReadOnlyList<Receiver> receivers, Quarterback qb,
        IReadOnlyList<Blocker> blockers, Receiver? controlledReceiver, BackfieldController exchange, float dt)
    {
        if (!float.IsFinite(dt) || dt <= 0) return;
        Span<Vector2> velocities = stackalloc Vector2[receivers.Count];
        Span<int> order = stackalloc int[receivers.Count];
        Span<Obstacle> neighbors = stackalloc Obstacle[receivers.Count + blockers.Count + 1];
        for (int i = 0; i < receivers.Count; i++)
        {
            velocities[i] = receivers[i].Velocity;
            int at = i;
            while (at > 0 && receivers[order[at - 1]].Index > receivers[i].Index)
            {
                order[at] = order[at - 1];
                at--;
            }
            order[at] = i;
        }
        // Stable priority lets the next runner account for a teammate's chosen cut,
        // instead of both independently dodging into the same space.
        foreach (int i in order)
        {
            var receiver = receivers[i];
            // The exchange deliberately closes on the QB. Never redirect user input.
            if (receiver == controlledReceiver || receiver.HasBall || exchange.ControlsParticipant(receiver.Slot)
                || receiver.Velocity.LengthSquared() < 0.001f) continue;

            int count = 0;
            Add(qb, qb.Velocity, receiver, neighbors, ref count);
            for (int j = 0; j < receivers.Count; j++)
                if (i != j) Add(receivers[j], velocities[j], receiver, neighbors, ref count);
            foreach (var blocker in blockers) Add(blocker, blocker.Velocity, receiver, neighbors, ref count);
            velocities[i] = Steer(receiver, neighbors[..count], dt);
        }
        for (int i = 0; i < receivers.Count; i++) receivers[i].Velocity = velocities[i];
    }

    private static void Add(Entity other, Vector2 velocity, Receiver receiver, Span<Obstacle> neighbors, ref int count)
    {
        float reach = receiver.Radius + other.Radius + Clearance
            + (receiver.Velocity.Length() + velocity.Length()) * LookAheadSeconds;
        if (Vector2.DistanceSquared(receiver.Position, other.Position) <= reach * reach)
            neighbors[count++] = new(other.Position, velocity, other.Radius);
    }

    private static Vector2 Steer(Receiver receiver, ReadOnlySpan<Obstacle> neighbors, float dt)
    {
        Vector2 desired = receiver.Velocity;
        float risk = CollisionCost(receiver, desired, neighbors);
        if (risk <= 0) return desired;

        float speed = desired.Length();
        Vector2 forward = desired / speed;
        Vector2 right = new(forward.Y, -forward.X);
        Vector2 best = desired;
        float bestScore = risk;
        // Modest cuts and a brief pace change preserve the route's destination.
        // A consistent right-foot preference breaks head-on ties without random jitter.
        ReadOnlySpan<float> angles = [0, 0.35f, -0.35f, 0.7f, -0.7f, 1.05f, -1.05f];
        ReadOnlySpan<float> paces = [1, 0.75f, 0.45f];
        foreach (float angle in angles)
        foreach (float pace in paces)
        {
            Vector2 candidate = (forward * MathF.Cos(angle) + right * MathF.Sin(angle)) * speed * pace;
            Vector2 next = receiver.Position + candidate * MathF.Max(dt, 0.2f);
            if (next.X < receiver.Radius || next.X > Constants.FieldWidth - receiver.Radius
                || next.Y < receiver.Radius || next.Y > Constants.FieldLength - receiver.Radius) continue;
            float deviation = Vector2.DistanceSquared(candidate, desired) / (speed * speed);
            float score = CollisionCost(receiver, candidate, neighbors) + deviation * 0.8f
                + (angle < 0 ? 0.015f : 0);
            if (score < bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }

    private static float CollisionCost(Receiver receiver, Vector2 velocity, ReadOnlySpan<Obstacle> neighbors)
    {
        float cost = 0;
        foreach (var other in neighbors)
        {
            Vector2 delta = other.Position - receiver.Position;
            Vector2 closing = velocity - other.Velocity;
            float closingSpeedSq = closing.LengthSquared();
            float approach = Vector2.Dot(delta, closing);
            // Nearby parallel routes and players already separating need no correction.
            if (closingSpeedSq < 0.001f || approach <= 0) continue;
            float time = Math.Clamp(approach / closingSpeedSq, 0, LookAheadSeconds);
            float separation = (delta - closing * time).Length();
            float clearance = receiver.Radius + other.Radius + Clearance;
            float intrusion = MathF.Max(0, 1 - separation / clearance);
            cost += intrusion * intrusion * 40f * (1 - 0.35f * time / LookAheadSeconds);
        }
        return cost;
    }
}
