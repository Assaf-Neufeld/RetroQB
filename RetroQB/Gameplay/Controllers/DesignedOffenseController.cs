using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

public enum ScreenPhase { None, Protecting, Releasing, Escorting, Exited }
public enum PocketPhase { None, WaitingForFake, Moving, Holding, Exited }

/// <summary>Owns designed transitions only; the normal motors still integrate each actor once.</summary>
public sealed class DesignedOffenseController
{
    private ResolvedPlay? _play;
    private Ball? _ball;
    private float _elapsed;
    private int _pocketStep;
    public ScreenPhase ScreenPhase { get; private set; }
    public PocketPhase PocketPhase { get; private set; }
    public bool ScreenReleased => ScreenPhase is ScreenPhase.Releasing or ScreenPhase.Escorting;
    public void Reset()
    {
        _play = null; _ball = null; _elapsed = 0; _pocketStep = 0;
        ScreenPhase = ScreenPhase.None; PocketPhase = PocketPhase.None;
    }
    public void Update(ResolvedPlay play, Ball ball, Quarterback qb, BackfieldController backfield,
        bool manualMovement, bool pressureEscape, float dt, float los)
    {
        if (!ReferenceEquals(play, _play) || !ReferenceEquals(ball, _ball))
        {
            Reset(); _play = play; _ball = ball;
            ScreenPhase = play.Screen == null ? ScreenPhase.None : ScreenPhase.Protecting;
            PocketPhase = play.QuarterbackPath == null ? PocketPhase.None : PocketPhase.WaitingForFake;
        }
        _elapsed += MathF.Max(0, dt);
        if (play.Screen is { } screen && ScreenPhase != ScreenPhase.Exited)
        {
            if (ball.State == BallState.HeldByReceiver && ball.Holder is Receiver r && r.Slot == screen.Target)
                ScreenPhase = ScreenPhase.Escorting;
            else if (qb.Position.Y > los + .1f || ball.State is not (BallState.HeldByQB or BallState.InAir))
                ScreenPhase = ScreenPhase.Exited;
            else if (_elapsed >= screen.ReleaseSeconds || ball.State == BallState.InAir)
                ScreenPhase = ScreenPhase.Releasing;
        }
        if (PocketPhase is PocketPhase.None or PocketPhase.Exited) return;
        if (manualMovement || pressureEscape || backfield.Phase == BackfieldPhase.Aborted
            || ball.State != BallState.HeldByQB || ball.Holder != qb || qb.Position.Y > los + .1f)
            PocketPhase = PocketPhase.Exited;
        else if (backfield.AllowsThrow) PocketPhase = PocketPhase.Moving;
    }
    public Vector2? QuarterbackMovement(Vector2 position, float los, float speed, float dt)
    {
        if (PocketPhase == PocketPhase.WaitingForFake) return Vector2.Zero;
        if (PocketPhase is not (PocketPhase.Moving or PocketPhase.Holding) || _play?.QuarterbackPath == null) return null;
        var points = _play.QuarterbackPath.At(los);
        while (_pocketStep < points.Count && Vector2.Distance(position, points[_pocketStep]) < .4f) _pocketStep++;
        if (_pocketStep == points.Count) { PocketPhase = PocketPhase.Holding; return Vector2.Zero; }
        return BlockingSteering.MoveTo(position, points[_pocketStep], speed * .8f, dt) / speed;
    }
    public Vector2? EscortOrigin(Ball ball, float los) => _play?.Screen is { } screen && ScreenReleased
        ? ScreenPhase == ScreenPhase.Escorting ? ball.Holder?.Position : OffensiveGeometry.At(screen.CatchOffset, los) : null;
    public BlockingAssignment? LineJob(int slot) => ScreenReleased && _play?.Screen is { } s
        && s.LineEscorts.TryGetValue(slot, out var job) ? job : null;
    public BlockingAssignment? SkillJob(ReceiverSlot slot) => ScreenReleased && _play?.Screen is { } s
        && s.SkillEscorts.TryGetValue(slot, out var job) ? job : null;
    public void Stop()
    {
        if (ScreenPhase != ScreenPhase.None) ScreenPhase = ScreenPhase.Exited;
        if (PocketPhase != PocketPhase.None) PocketPhase = PocketPhase.Exited;
    }
}
