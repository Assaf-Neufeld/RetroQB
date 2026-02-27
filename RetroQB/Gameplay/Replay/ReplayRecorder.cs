using RetroQB.Entities;
using System.Linq;

namespace RetroQB.Gameplay.Replay;

public sealed class ReplayRecorder
{
    private const float MaxCaptureSeconds = 8f;

    private readonly List<ReplayFrame> _frames = new();
    private bool _isRecording;
    private float _elapsedSeconds;
    private int _playNumber;

    public bool IsRecording => _isRecording;

    public void Begin(int playNumber)
    {
        _frames.Clear();
        _isRecording = true;
        _elapsedSeconds = 0f;
        _playNumber = playNumber;
    }

    public void Capture(
        Quarterback qb,
        Ball ball,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Blocker> blockers,
        IReadOnlyList<Defender> defenders,
        float lineOfScrimmage,
        float firstDownLine,
        float dt)
    {
        if (!_isRecording)
        {
            return;
        }

        if (_elapsedSeconds >= MaxCaptureSeconds)
        {
            return;
        }

        _elapsedSeconds += dt;

        int holderId = GetEntityReplayId(ball.Holder, receivers, blockers, defenders);
        var frame = new ReplayFrame
        {
            ElapsedSeconds = _elapsedSeconds,
            LineOfScrimmage = lineOfScrimmage,
            FirstDownLine = firstDownLine,
            Quarterback = new ReplayActorFrame(
                0,
                ReplayActorKind.Quarterback,
                qb.Position,
                qb.Velocity,
                qb.Radius,
                qb.Glyph,
                qb.Color,
                ball.Holder == qb,
                true,
                false),
            Receivers = receivers
                .Select(receiver => new ReplayActorFrame(
                    100 + receiver.Index,
                    ReplayActorKind.Receiver,
                    receiver.Position,
                    receiver.Velocity,
                    receiver.Radius,
                    receiver.Glyph,
                    receiver.Color,
                    ball.Holder == receiver,
                    receiver.Eligible,
                    receiver.IsBlocking))
                .ToList(),
            Blockers = blockers
                .Select((blocker, index) => new ReplayActorFrame(
                    200 + index,
                    ReplayActorKind.Blocker,
                    blocker.Position,
                    blocker.Velocity,
                    blocker.Radius,
                    blocker.Glyph,
                    blocker.Color,
                    ball.Holder == blocker,
                    false,
                    false))
                .ToList(),
            Defenders = defenders
                .Select((defender, index) => new ReplayActorFrame(
                    300 + index,
                    ReplayActorKind.Defender,
                    defender.Position,
                    defender.Velocity,
                    defender.Radius,
                    defender.Glyph,
                    defender.Color,
                    ball.Holder == defender,
                    false,
                    false))
                .ToList(),
            Ball = new ReplayBallFrame(
                ball.Position,
                ball.Velocity,
                ball.State,
                holderId,
                ball.AirTime,
                ball.ThrowStart,
                ball.IntendedDistance,
                ball.ArcApexHeight)
        };

        _frames.Add(frame);
    }

    public ReplayClip? FinalizeClip(PlayOutcome outcome)
    {
        if (!_isRecording)
        {
            return null;
        }

        _isRecording = false;
        if (_frames.Count == 0)
        {
            return null;
        }

        float duration = _frames[^1].ElapsedSeconds;
        float captureFps = duration > 0f ? _frames.Count / duration : Constants.TargetFps;
        var frames = _frames.ToList();
        return new ReplayClip(_playNumber, outcome, duration, captureFps, frames);
    }

    public void Reset()
    {
        _frames.Clear();
        _elapsedSeconds = 0f;
        _isRecording = false;
        _playNumber = 0;
    }

    private static int GetEntityReplayId(Entity? entity, IReadOnlyList<Receiver> receivers, IReadOnlyList<Blocker> blockers, IReadOnlyList<Defender> defenders)
    {
        if (entity is null)
        {
            return -1;
        }

        if (entity is Quarterback)
        {
            return 0;
        }

        if (entity is Receiver receiver)
        {
            return 100 + receiver.Index;
        }

        for (int i = 0; i < blockers.Count; i++)
        {
            if (ReferenceEquals(entity, blockers[i]))
            {
                return 200 + i;
            }
        }

        for (int i = 0; i < defenders.Count; i++)
        {
            if (ReferenceEquals(entity, defenders[i]))
            {
                return 300 + i;
            }
        }

        return -1;
    }
}
