using System.Numerics;
using RetroQB.Gameplay;

namespace RetroQB.AI;

public sealed record ReceiverRead(int Index, Vector2 Position, Vector2 Velocity, bool Eligible, bool Blocking, float MinimumDepth = 0);
public sealed record QuarterbackObservation(Vector2 Position, float LineOfScrimmage, bool AllowsThrow,
    IReadOnlyList<ReceiverRead> Reads, IReadOnlyList<Vector2> Defenders);

/// <summary>Observes public positions and its own read order only. Never receives defensive calls or user input.</summary>
public sealed class QuarterbackAI(float readInterval = .25f)
{
    private float _elapsed, _nextRead = MathF.Max(.1f, readInterval);
    private int _read;
    public void Reset() { _elapsed = 0; _read = 0; _nextRead = MathF.Max(.1f, readInterval); }

    public OffensiveIntent Decide(QuarterbackObservation view, float dt)
    {
        _elapsed += dt;
        if (!view.AllowsThrow) return new(Vector2.Zero);
        float pressure = view.Defenders.Count == 0 ? 100 : view.Defenders.Min(p => Vector2.Distance(p, view.Position));
        if (_elapsed > 4 && pressure < 6 && MathF.Abs(view.Position.X - Constants.FieldWidth / 2) > 9
            && view.Position.Y <= view.LineOfScrimmage)
            return new(Vector2.Zero, ThrowAway: true);
        if (view.Position.Y <= view.LineOfScrimmage + .1f && _elapsed >= _nextRead && view.Reads.Count > 0)
        {
            _nextRead = _elapsed + MathF.Max(.1f, readInterval);
            var read = view.Reads[_read++ % view.Reads.Count];
            if (read.Eligible && !read.Blocking && read.Position.Y >= view.LineOfScrimmage + read.MinimumDepth
                && IsOpen(view.Position, read, view.Defenders))
                return new(Vector2.Zero, ReceiverIndex: read.Index);
            // Under pressure, search the short outlet without waiting for another progression cycle.
            if (pressure < 5)
                foreach (var outlet in view.Reads.OrderBy(r => Vector2.DistanceSquared(r.Position, view.Position)))
                    if (outlet.Eligible && !outlet.Blocking && IsOpen(view.Position, outlet, view.Defenders))
                        return new(Vector2.Zero, ReceiverIndex: outlet.Index);
        }
        if (pressure < 4 || _elapsed > 2.5f || view.Position.Y > view.LineOfScrimmage)
        {
            float x = view.Defenders.Count == 0 ? 0 : MathF.Sign(view.Position.X - view.Defenders.MinBy(p => Vector2.DistanceSquared(p, view.Position)).X);
            if (view.Position.X < 4) x = 1;
            if (view.Position.X > Constants.FieldWidth - 4) x = -1;
            return new(Vector2.Normalize(new Vector2(x * .7f, 1)), true);
        }
        return new(Vector2.Zero);
    }

    public static bool IsOpen(Vector2 qb, ReceiverRead receiver, IReadOnlyList<Vector2> defenders)
    {
        Vector2 target = receiver.Position + receiver.Velocity * .25f;
        Vector2 lane = target - qb;
        float length = lane.LengthSquared();
        return defenders.All(p =>
        {
            float t = length < .01f ? 1 : Math.Clamp(Vector2.Dot(p - qb, lane) / length, 0, 1);
            return Vector2.Distance(p, target) > 2.8f && (t < .12f || Vector2.Distance(p, qb + lane * t) > 2.2f);
        });
    }
}
