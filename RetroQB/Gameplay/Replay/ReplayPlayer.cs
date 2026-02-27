namespace RetroQB.Gameplay.Replay;

public sealed class ReplayPlayer
{
    public const float DefaultPlaybackSpeed = 0.35f;
    private const float MinimumVisibleSeconds = 0.35f;

    private ReplayClip? _clip;
    private float _playbackSeconds;
    private int _frameIndex;

    public ReplayClip? Clip => _clip;
    public ReplayFrame? CurrentFrame => _clip == null || _clip.Frames.Count == 0 ? null : _clip.Frames[_frameIndex];
    public bool IsPlaying => _clip != null;
    public bool IsComplete =>
        _clip == null ||
        (_clip.Frames.Count > 0 && _frameIndex >= _clip.Frames.Count - 1 && _playbackSeconds >= MinimumVisibleSeconds);
    public float PlaybackSpeed { get; private set; } = DefaultPlaybackSpeed;

    public void Load(ReplayClip clip, float playbackSpeed = DefaultPlaybackSpeed)
    {
        _clip = clip;
        _playbackSeconds = 0f;
        _frameIndex = 0;
        PlaybackSpeed = MathF.Max(0.05f, playbackSpeed);
    }

    public void Update(float dt)
    {
        if (_clip == null || _clip.Frames.Count == 0 || IsComplete)
        {
            return;
        }

        _playbackSeconds += dt * PlaybackSpeed;
        while (_frameIndex < _clip.Frames.Count - 1 && _clip.Frames[_frameIndex + 1].ElapsedSeconds <= _playbackSeconds)
        {
            _frameIndex++;
        }
    }

    public void SkipToEnd()
    {
        if (_clip == null || _clip.Frames.Count == 0)
        {
            return;
        }

        _frameIndex = _clip.Frames.Count - 1;
        _playbackSeconds = _clip.Frames[^1].ElapsedSeconds;
    }

    public void Unload()
    {
        _clip = null;
        _playbackSeconds = 0f;
        _frameIndex = 0;
        PlaybackSpeed = DefaultPlaybackSpeed;
    }
}
