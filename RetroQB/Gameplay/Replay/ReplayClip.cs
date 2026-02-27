namespace RetroQB.Gameplay.Replay;

public sealed class ReplayClip
{
    public int PlayNumber { get; }
    public PlayOutcome Outcome { get; }
    public float DurationSeconds { get; }
    public float CaptureFps { get; }
    public DateTime CreatedAtUtc { get; }
    public IReadOnlyList<ReplayFrame> Frames { get; }

    public ReplayClip(int playNumber, PlayOutcome outcome, float durationSeconds, float captureFps, IReadOnlyList<ReplayFrame> frames)
    {
        PlayNumber = playNumber;
        Outcome = outcome;
        DurationSeconds = durationSeconds;
        CaptureFps = captureFps;
        CreatedAtUtc = DateTime.UtcNow;
        Frames = frames;
    }
}
