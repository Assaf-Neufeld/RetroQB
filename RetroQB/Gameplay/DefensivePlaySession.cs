using RetroQB.Gameplay.Replay;

namespace RetroQB.Gameplay;

public sealed record DefensiveInput(int? Call = null, bool Ready = false, bool Timeout = false,
    bool Pause = false, bool Replay = false, bool Focused = true);

/// <summary>Defensive pre-snap cadence and presentation; all simulation clocks remain owned by TimedMatch.</summary>
public sealed class DefensivePlaySession
{
    private readonly Random _cadence;
    private double _resultSeconds;
    public DefensiveDrive Drive { get; }
    public MatchClock Clock => Drive.Timed.Clock;
    public ReplayPlayer Replay { get; } = new();
    public double SnapRemaining { get; private set; }
    public DefensivePlaySession(DefensiveDrive drive, int seed)
    { Drive = drive; _cadence = new(unchecked(seed ^ 0x153781)); SetDeadline(); }

    private void SetDeadline() => SnapRemaining = 5 + _cadence.NextDouble() * 3;

    public void Update(float dt, DefensiveInput? input = null)
    {
        if (!float.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        input ??= new();
        if (!input.Focused) Clock.Suspend(ClockSuspension.FocusLoss); else Clock.Resume(ClockSuspension.FocusLoss);
        if (input.Pause)
        {
            if ((Clock.Suspension & ClockSuspension.Pause) != 0) Clock.Resume(ClockSuspension.Pause);
            else Clock.Suspend(ClockSuspension.Pause);
        }
        if (input.Replay)
        {
            if (Replay.IsPlaying) { Replay.Unload(); Clock.Resume(ClockSuspension.Replay); }
            else if (!Drive.Live && Drive.LastReplay is { } clip)
            { Replay.Load(clip); Clock.Suspend(ClockSuspension.Replay); }
        }
        if (Replay.IsPlaying)
        {
            if ((Clock.Suspension & ~ClockSuspension.Replay) == ClockSuspension.None) Replay.Update(dt);
            if (Replay.IsComplete || input.Ready) { Replay.Unload(); Clock.Resume(ClockSuspension.Replay); }
            return;
        }
        if (Clock.Suspension != ClockSuspension.None) return;
        if (input.Timeout) Clock.TryTimeout(Drive.Match.User.Definition.Id);
        if (Drive.Complete) return;
        if (Drive.Live) { Drive.Update(dt); return; }
        if (Drive.LastResult != null)
        {
            _resultSeconds += dt;
            if (_resultSeconds >= 1.25 && Drive.Continue()) { _resultSeconds = 0; SetDeadline(); }
            return;
        }
        if (input.Call is >= 0 and < 10) Drive.SelectDefense(DefensivePlaybook.All[input.Call.Value].Id);
        if (input.Ready) SnapRemaining = Math.Min(SnapRemaining, .35);
        int quarter = Clock.Quarter;
        Drive.AdvancePreSnap(dt); // Expiry always wins over snap input on the same tick.
        SnapRemaining = Math.Max(0, SnapRemaining - dt);
        if (Clock.Quarter != quarter || Drive.Complete) return;
        if (SnapRemaining <= 1e-8) Drive.Snap();
    }
}
