namespace RetroQB.Gameplay.Replay;

public sealed class ReplayClipStore
{
    public ReplayClip? Current { get; private set; }
    public bool HasClip => Current != null;

    public void Store(ReplayClip? clip)
    {
        Current = clip;
    }

    public void Clear()
    {
        Current = null;
    }
}
