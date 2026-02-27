namespace RetroQB.Gameplay.Replay;

public sealed class ReplayFrame
{
    public float ElapsedSeconds { get; init; }
    public float LineOfScrimmage { get; init; }
    public float FirstDownLine { get; init; }
    public ReplayActorFrame Quarterback { get; init; }
    public IReadOnlyList<ReplayActorFrame> Receivers { get; init; } = Array.Empty<ReplayActorFrame>();
    public IReadOnlyList<ReplayActorFrame> Blockers { get; init; } = Array.Empty<ReplayActorFrame>();
    public IReadOnlyList<ReplayActorFrame> Defenders { get; init; } = Array.Empty<ReplayActorFrame>();
    public ReplayBallFrame Ball { get; init; }
}
