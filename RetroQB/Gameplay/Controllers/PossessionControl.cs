using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>Protects a new carrier from stale input, without delaying a fresh cut.</summary>
public sealed class PossessionControl
{
    private Receiver? _carrier;
    private Vector2 _inheritedInput;
    private Vector2 _direction;
    private float _assistRemaining;
    private bool _preserveCatchDirection;
    public float CueRemaining { get; private set; }
    public Receiver? Carrier => _carrier;
    public bool SuppressTurnBoost { get; private set; }

    public void Reset()
    {
        _carrier = null;
        _assistRemaining = CueRemaining = 0;
        _preserveCatchDirection = false;
        SuppressTurnBoost = false;
    }

    public void Tick(float dt)
    {
        _assistRemaining = MathF.Max(0, _assistRemaining - dt);
        CueRemaining = MathF.Max(0, CueRemaining - dt);
    }

    public void Observe(Ball ball, ResolvedPlay play, Vector2 input)
    {
        var carrier = ball.State == BallState.HeldByReceiver ? ball.Holder as Receiver : null;
        if (carrier == _carrier) return;
        Reset();
        _carrier = carrier;
        if (carrier == null) return;
        _inheritedInput = input;
        bool run = play.Family == PlayType.Run;
        _preserveCatchDirection = !run;
        _direction = run ? RouteRunner.GetBallCarrierDirection(carrier, play)
            : carrier.Velocity.LengthSquared() > .01f ? Vector2.Normalize(carrier.Velocity) : Vector2.UnitY * .5f;
        _assistRemaining = .3f;
        CueRemaining = .6f;
    }

    public Vector2 Resolve(Vector2 input)
    {
        SuppressTurnBoost = _assistRemaining > 0 || _preserveCatchDirection;
        if (!SuppressTurnBoost) return input;
        // A release, new key, or meaningful stick turn immediately hands over control.
        if (Vector2.DistanceSquared(input, _inheritedInput) > .04f)
        {
            _assistRemaining = 0;
            _preserveCatchDirection = false;
            SuppressTurnBoost = false;
            return input;
        }
        // A catch keeps the receiver's approach direction until fresh input.
        // Expiring the cue must not restore a key still held from moving the QB.
        if (_preserveCatchDirection) return _direction;
        if (input.LengthSquared() > .001f && Vector2.Dot(Vector2.Normalize(input), Vector2.Normalize(_direction)) >= .45f)
            return input;
        return _direction;
    }
}
