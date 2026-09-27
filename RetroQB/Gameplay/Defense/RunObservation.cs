using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public enum RunRead { Pass, Mesh, Handoff, QuarterbackRun, AfterCatch }
public sealed record ObservedRun(RunRead Read, ReceiverId? Carrier, Vector2 Position, Vector2 Velocity,
    ReceiverId? MeshParticipant, bool OutsidePocket)
{
    public bool Confirmed => Read is RunRead.Handoff or RunRead.QuarterbackRun or RunRead.AfterCatch;
}
public static class RunTuning
{
    public const float MeshRadius = 3.5f, MeshWindow = .35f, ReadStep = .8f, ReadRecoverySeconds = .45f;
    public const float PredictionSeconds = .65f, PursuitHorizon = 1.5f, HumanHorizon = .75f;
    public const float CutbackRecognitionSeconds = .12f, ReplacementMarginSeconds = .10f;
}

/// <summary>Possession and geometry only. It never receives a BackfieldController, route or play-action flag.</summary>
internal sealed class RunObservationBuilder
{
    private readonly ResolvedDefensivePlan _plan;
    private readonly Queue<(float Time, Vector2 Position)> _qb = new();
    private float _time, _meshTime;
    private ReceiverId? _mesh, _carrier;
    private RunRead _possessionRead;
    private BallState _previousBall = BallState.HeldByQB;
    private bool _qbCrossed;
    public RunObservationBuilder(ResolvedDefensivePlan plan)
    { _plan = plan; _qb.Enqueue((0, plan.Offense.Quarterback)); }

    public ObservedRun Observe(DefensiveSighting sight, IReadOnlyList<ObservedReceiver> receivers, float dt)
    {
        _time += dt; _qb.Enqueue((_time, sight.Quarterback));
        while (_qb.Count > 2 && _qb.ElementAt(1).Time < _time - DefensiveTuning.HistorySeconds) _qb.Dequeue();
        var old = _qb.Peek(); Vector2 velocity = (sight.Quarterback - old.Position) / MathF.Max(dt, _time - old.Time);
        bool escaped = sight.Quarterback.X < _plan.Offense.Linemen.Min(l => l.Position.X)
            || sight.Quarterback.X > _plan.Offense.Linemen.Max(l => l.Position.X);
        RunRead read = RunRead.Pass; Vector2 position = sight.Quarterback;
        if (sight.Ball.State == BallState.HeldByReceiver && sight.Ball.Carrier is { } id)
        {
            var r = receivers.Single(r => r.Id == id); position = r.Position; velocity = r.Velocity;
            if (_carrier != id || _previousBall != BallState.HeldByReceiver)
                _possessionRead = _previousBall == BallState.InAir ? RunRead.AfterCatch : RunRead.Handoff;
            _carrier = id; read = _possessionRead;
        }
        else if (sight.Ball.State == BallState.HeldByQB)
        {
            _qbCrossed |= sight.Quarterback.Y > _plan.Offense.LineOfScrimmage + .1f;
            if (_qbCrossed) read = RunRead.QuarterbackRun;
            else
            {
                var near = receivers.Where(r => _plan.Surface.Threats.Any(t => t.ActorId == r.Id && t.Backfield)
                    && r.Position.Y < _plan.Offense.LineOfScrimmage && Vector2.Distance(r.Position, sight.Quarterback) <= RunTuning.MeshRadius)
                    .OrderBy(r => Vector2.Distance(r.Position, sight.Quarterback)).ThenBy(r => r.Id.Value).FirstOrDefault();
                bool approaching = near != null && Vector2.Dot(sight.Quarterback - near.Position, near.Velocity - velocity) > .5f;
                if (near != null && (approaching || _mesh == near.Id && near.Velocity.LengthSquared() < 1))
                {
                    if (_mesh != near.Id) { _mesh = near.Id; _meshTime = 0; }
                    _meshTime += dt;
                    if (_meshTime <= RunTuning.MeshWindow) read = RunRead.Mesh;
                }
                else { _mesh = null; _meshTime = 0; }
            }
        }
        if (read != RunRead.Mesh && sight.Ball.State != BallState.HeldByQB) { _mesh = null; _meshTime = 0; }
        _previousBall = sight.Ball.State;
        return new(read, sight.Ball.State == BallState.HeldByReceiver ? sight.Ball.Carrier : null,
            position, velocity, read == RunRead.Mesh ? _mesh : null, escaped);
    }
}
