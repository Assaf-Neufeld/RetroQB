using System.Numerics;

namespace RetroQB.AI;

/// <summary>Forward lane scoring uses nearby opponents, blockers, and an authored run entry.</summary>
public sealed class BallCarrierAI
{
    private Vector2 _direction = Vector2.UnitY;
    private float? _escapeX;
    public int SteeringChanges { get; private set; }
    public void Reset() { _direction = Vector2.UnitY; _escapeX = null; SteeringChanges = 0; }
    public Vector2 Decide(Vector2 position, IReadOnlyList<Vector2> defenders, IReadOnlyList<Vector2> blockers,
        Vector2? entry, float dt)
    {
        // A stationary wall of teammates may have no body-width forward gap.
        // Commit to one edge until clear instead of oscillating between blocked gaps.
        if (_escapeX.HasValue && MathF.Abs(position.X - _escapeX.Value) < .6f) _escapeX = null;
        var wall = blockers.Where(b => b.Y > position.Y && b.Y - position.Y < 5 && MathF.Abs(b.X - position.X) < 12).ToArray();
        if (!_escapeX.HasValue && wall.Length >= 2 && wall.Any(b => MathF.Abs(b.X - position.X) < 2.2f))
        {
            float left = Math.Clamp(wall.Min(b => b.X) - 2.5f, 1, Constants.FieldWidth - 1);
            float right = Math.Clamp(wall.Max(b => b.X) + 2.5f, 1, Constants.FieldWidth - 1);
            _escapeX = position.X - left < right - position.X ? left : right;
        }
        if (_escapeX.HasValue)
        {
            var escape = Vector2.Normalize(new Vector2(MathF.Sign(_escapeX.Value - position.X), .08f));
            _direction = Vector2.Normalize(Vector2.Lerp(_direction, escape, Math.Clamp(dt * 12, 0, 1)));
            return _direction;
        }
        Vector2 preferred = entry.HasValue && position.Y < entry.Value.Y
            ? Vector2.Normalize(new Vector2(entry.Value.X - position.X, MathF.Max(3, entry.Value.Y - position.Y)))
            : Vector2.UnitY;
        Vector2 best = Vector2.UnitY;
        float bestScore = float.NegativeInfinity;
        foreach (float x in new[] { -2f, -1f, -.5f, 0f, .5f, 1f, 2f })
        {
            var direction = Vector2.Normalize(new Vector2(x, 1));
            var next = position + direction * 4;
            float score = Vector2.Dot(direction, preferred) * 3 + Vector2.Dot(direction, _direction) * .6f;
            if (next.X < 1 || next.X > Constants.FieldWidth - 1) score -= 20;
            foreach (var defender in defenders) score -= 7 / MathF.Max(.5f, Vector2.Distance(next, defender));
            foreach (var blocker in blockers)
            {
                Vector2 offset = blocker - position;
                float along = Vector2.Dot(offset, direction);
                if (along > 0 && along < 5)
                {
                    float clearance = Vector2.Distance(blocker, position + direction * along);
                    score -= 12 / MathF.Max(.4f, clearance);
                }
            }
            if (score > bestScore) { bestScore = score; best = direction; }
        }
        if (MathF.Sign(best.X) != MathF.Sign(_direction.X) && MathF.Abs(best.X) > .2f) SteeringChanges++;
        _direction = Vector2.Normalize(Vector2.Lerp(_direction, best, Math.Clamp(dt * 8, 0, 1)));
        return _direction;
    }
}
