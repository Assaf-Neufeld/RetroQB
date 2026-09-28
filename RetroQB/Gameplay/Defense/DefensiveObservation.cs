using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public static class DefensiveTuning
{
    public const float HistorySeconds = .15f, ReleaseSeconds = .12f, BlockSeconds = .35f;
    public const float ThrowRecognitionSeconds = .12f, CommitmentSeconds = .25f, ChallengerRatio = 1.2f;
    public const float SwitchWindowSeconds = .40f, ProjectionSeconds = .35f, HelpHorizonSeconds = 1.5f;
    public const float TurnRadiansPerSecond = 7f, Acceleration = 35f, ArrivalRadius = 1.5f;
}

public enum ReleaseDirection { Unknown, Outward, Inward, Upfield }
public sealed record VisibleMotion(ReceiverId Id, Vector2 Position, Vector2 Velocity, float Radius);
public sealed record VisibleBlocker(int Id, Vector2 Position, Vector2 Velocity, float Radius);
public sealed record DefenderMotion(DefenderSlot Id, Vector2 Position, Vector2 Velocity, float Speed,
    float Radius, float RecognitionRating, bool Contact);
public sealed record VisibleBall(BallState State, Vector2 Position, Vector2 Velocity, float Height, ReceiverId? Carrier);

/// <summary>The only live input to defensive decisions. No route, eligibility, protection, throw target or landing metadata.</summary>
public sealed class DefensiveSighting
{
    public Vector2 Quarterback { get; }
    public VisibleBall Ball { get; }
    public IReadOnlyList<VisibleMotion> Receivers { get; }
    public IReadOnlyList<DefenderMotion> Defenders { get; }
    public IReadOnlyList<VisibleBlocker> Linemen { get; }
    public DefensiveSighting(Vector2 quarterback, VisibleBall ball, IEnumerable<VisibleMotion> receivers, IEnumerable<DefenderMotion> defenders,
        IEnumerable<VisibleBlocker>? linemen = null)
    {
        Quarterback = quarterback; Ball = ball;
        Receivers = Array.AsReadOnly(receivers.OrderBy(r => r.Id.Value).ToArray());
        Defenders = Array.AsReadOnly(defenders.OrderBy(d => d.Id).ToArray());
        Linemen = Array.AsReadOnly((linemen ?? []).OrderBy(b => b.Id).ToArray());
    }
    public static DefensiveSighting Capture(Quarterback qb, Ball ball, IReadOnlyList<Receiver> receivers, IReadOnlyList<Defender> defenders, IReadOnlyList<Blocker>? blockers = null)
        => new(qb.Position, new(ball.State, ball.Position, ball.Velocity, ball.GetArcHeight(),
                ball.Holder is Receiver r ? new ReceiverId(r.Index) : null),
            receivers.Select(r => new VisibleMotion(new(r.Index), r.Position, r.Velocity, r.Radius)),
            defenders.Select(d => new DefenderMotion(d.Slot, d.Position, d.Velocity, d.Speed, d.Radius,
                d.InterceptionMultiplier, d.IsBeingBlocked)),
            blockers?.OrderBy(b => b.HomeX).Select((b, i) => new VisibleBlocker(i, b.Position, b.Velocity, b.Radius)));
}

public sealed record ObservedReceiver(ReceiverId Id, Vector2 Position, Vector2 Velocity, ReleaseDirection Release,
    bool Vertical, bool Blocking, bool LateRelease, bool Contact, float ConfidenceSeconds);
public sealed record DefensiveObservationFrame(float Seconds, DefensiveSighting Visible,
    IReadOnlyList<ObservedReceiver> Receivers, bool ConfirmedCarrier, float AirSeconds, float HeightVelocity)
{
    public ObservedRun? Run { get; init; }
}

public sealed class DefensiveObservationBuilder
{
    private sealed class Track
    {
        public readonly Queue<(float Time, Vector2 Position)> History = new();
        public ReleaseDirection Candidate, Release;
        public float Stable, BlockTime, ReleaseTime;
        public bool Blocking, WasBlocking;
    }
    private readonly ResolvedDefensivePlan _plan;
    private readonly Dictionary<ReceiverId, Track> _tracks = new();
    private float _seconds, _airSeconds, _lastHeight;
    private bool _wasAir;
    private readonly RunObservationBuilder _run;
    public DefensiveObservationBuilder(ResolvedDefensivePlan plan)
    {
        _plan = plan; _run = new(plan);
        foreach (var p in plan.Offense.Skills)
        { var track = new Track(); track.History.Enqueue((0, p.Position)); _tracks.Add(p.Id, track); }
    }
    public DefensiveObservationFrame Observe(DefensiveSighting sighting, float dt, IReadOnlyDictionary<ReceiverId, DefenderSlot>? owners = null)
    {
        if (!float.IsFinite(dt) || dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt));
        if (!sighting.Receivers.Select(r => r.Id).SequenceEqual(_plan.Offense.Skills.Select(r => r.Id)))
            throw new InvalidOperationException("Observed receiving identities differ from the compiled plan.");
        if (!sighting.Defenders.Select(d => d.Id).SequenceEqual(_plan.Assignments.Select(d => d.ActorId)))
            throw new InvalidOperationException("Observed defensive identities differ from the compiled plan.");
        _seconds += dt;
        bool air = sighting.Ball.State == BallState.InAir;
        _airSeconds = air ? _airSeconds + dt : 0;
        float heightVelocity = air && _wasAir ? (sighting.Ball.Height - _lastHeight) / dt : 0;
        _lastHeight = sighting.Ball.Height; _wasAir = air;
        float los = _plan.Offense.LineOfScrimmage;
        float verticalGate = MathF.Min(6, MathF.Max(1.5f, .35f * (FieldGeometry.PlayableBackLine - los)));
        var observations = new List<ObservedReceiver>();
        foreach (var p in sighting.Receivers)
        {
            var track = _tracks[p.Id];
            track.History.Enqueue((_seconds, p.Position));
            while (track.History.Count > 2 && track.History.ElementAt(1).Time < _seconds - .20f) track.History.Dequeue();
            var old = track.History.First();
            var recent = track.History.LastOrDefault(h => h.Time <= _seconds - DefensiveTuning.HistorySeconds);
            if (recent == default) recent = old;
            Vector2 velocity = (_seconds - recent.Time) > .001f ? (p.Position - recent.Position) / (_seconds - recent.Time) : Vector2.Zero;
            var surface = _plan.Surface.Threats.Single(t => t.ActorId == p.Id);
            float side = surface.Side == FieldSide.Balanced ? p.Position.X < _plan.Surface.CenterX ? -1 : 1 : (int)surface.Side;
            ReleaseDirection candidate = velocity.LengthSquared() < 1 ? ReleaseDirection.Unknown
                : MathF.Abs(velocity.X) > MathF.Abs(velocity.Y) ? velocity.X * side > 0 ? ReleaseDirection.Outward : ReleaseDirection.Inward
                : velocity.Y > 0 ? ReleaseDirection.Upfield : ReleaseDirection.Unknown;
            if (track.Candidate != candidate) { track.Candidate = candidate; track.Stable = 0; }
            track.Stable += dt;
            if (candidate == ReleaseDirection.Unknown) track.Release = ReleaseDirection.Unknown;
            else if (track.Stable + .00001f >= DefensiveTuning.ReleaseSeconds) track.Release = candidate;
            // Physical proximity PLUS sustained relative engagement, not a receiver's hidden blocking flag.
            bool contact = sighting.Defenders.Any(d => Vector2.Distance(d.Position, p.Position) <= d.Radius + p.Radius + .75f
                && (d.Contact || p.Velocity.LengthSquared() > .25f || d.Velocity.LengthSquared() > .25f)
                && Vector2.Distance(d.Velocity, p.Velocity) <= 3f);
            track.BlockTime = contact ? track.BlockTime + dt : 0;
            if (track.BlockTime >= DefensiveTuning.BlockSeconds) { track.Blocking = true; track.WasBlocking = true; track.ReleaseTime = 0; }
            bool separating = !contact && candidate != ReleaseDirection.Unknown;
            track.ReleaseTime = separating ? track.ReleaseTime + dt : 0;
            bool late = track.WasBlocking && track.ReleaseTime + .00001f >= DefensiveTuning.ReleaseSeconds;
            if (late) track.Blocking = false;
            DefenderSlot? primary = owners != null && owners.TryGetValue(p.Id, out var owner) ? owner
                : _plan.Assignments.FirstOrDefault(a => a.Pass == new ManResponsibility(p.Id))?.ActorId;
            var defender = primary == null ? null : sighting.Defenders.Single(d => d.Id == primary);
            bool imminent = defender != null && velocity.Y > .5f
                && p.Position.Y + velocity.Y * .60f >= defender.Position.Y && p.Position.Y >= los;
            bool vertical = p.Position.Y >= los + verticalGate && p.Position.Y - old.Position.Y >= .8f || imminent;
            observations.Add(new(p.Id, p.Position, velocity, track.Release, vertical, track.Blocking, late, contact, track.Stable));
        }
        var run = _run.Observe(sighting, observations, dt);
        return new(_seconds, sighting, observations.AsReadOnly(), run.Confirmed, _airSeconds, heightVelocity) { Run = run };
    }
}
