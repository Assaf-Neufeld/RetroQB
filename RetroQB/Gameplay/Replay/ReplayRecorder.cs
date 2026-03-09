using RetroQB.Entities;
using System.Linq;
using System.Numerics;
using RetroQB.Core;

namespace RetroQB.Gameplay.Replay;

public sealed class ReplayRecorder
{
    private const float MaxCaptureSeconds = 14f;
    private const float LongPlayThresholdSeconds = 7.25f;
    private const float OpeningWindowSeconds = 1.35f;
    private const float ClosingWindowSeconds = 1.4f;
    private const float PassLeadSeconds = 0.85f;
    private const float PassTrailSeconds = 1.55f;
    private const float PressureLeadSeconds = 0.45f;
    private const float PressureTrailSeconds = 0.9f;
    private const float CloseThreatDistance = 4.5f;
    private const float FrontThreatDistance = 8.0f;
    private const float OpenFieldSkipDistance = 10.0f;
    private const float GoalLineReplayDistance = 5.0f;

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
        if (duration > LongPlayThresholdSeconds)
        {
            frames = BuildHighlightFrames(frames, captureFps);
            duration = frames.Count > 0 ? frames[^1].ElapsedSeconds : 0f;
        }

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

    private static List<ReplayFrame> BuildHighlightFrames(IReadOnlyList<ReplayFrame> frames, float captureFps)
    {
        if (frames.Count == 0)
        {
            return new List<ReplayFrame>();
        }

        float totalDuration = frames[^1].ElapsedSeconds;
        var windows = new List<(float Start, float End)>
        {
            (0f, Math.Min(OpeningWindowSeconds, totalDuration)),
            (Math.Max(0f, totalDuration - ClosingWindowSeconds), totalDuration)
        };

        AddPassWindow(frames, windows, totalDuration);
        AddGoalLineWindow(frames, windows, totalDuration);

        foreach (var frame in frames)
        {
            if (!IsThreatFrame(frame))
            {
                continue;
            }

            windows.Add((
                Math.Max(0f, frame.ElapsedSeconds - PressureLeadSeconds),
                Math.Min(totalDuration, frame.ElapsedSeconds + PressureTrailSeconds)));
        }

        List<(float Start, float End)> mergedWindows = MergeWindows(windows);
        List<ReplayFrame> keptFrames = frames
            .Where(frame => mergedWindows.Any(window => frame.ElapsedSeconds >= window.Start && frame.ElapsedSeconds <= window.End))
            .ToList();

        if (keptFrames.Count == 0)
        {
            keptFrames = frames.ToList();
        }

        return NormalizeFrameTimes(keptFrames, captureFps);
    }

    private static void AddGoalLineWindow(IReadOnlyList<ReplayFrame> frames, List<(float Start, float End)> windows, float totalDuration)
    {
        float triggerY = FieldGeometry.OpponentGoalLine - GoalLineReplayDistance;

        foreach (var frame in frames)
        {
            float focusY = GetReplayFocusY(frame);
            if (focusY < triggerY)
            {
                continue;
            }

            windows.Add((frame.ElapsedSeconds, totalDuration));
            return;
        }
    }

    private static void AddPassWindow(IReadOnlyList<ReplayFrame> frames, List<(float Start, float End)> windows, float totalDuration)
    {
        int passStartIndex = -1;
        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].Ball.State != BallState.InAir)
            {
                continue;
            }

            bool isPassStart = i == 0 || frames[i - 1].Ball.State != BallState.InAir;
            if (!isPassStart)
            {
                continue;
            }

            passStartIndex = i;
            break;
        }

        if (passStartIndex < 0)
        {
            return;
        }

        int passEndIndex = passStartIndex;
        for (int i = passStartIndex + 1; i < frames.Count; i++)
        {
            passEndIndex = i;
            if (frames[i].Ball.State != BallState.InAir)
            {
                break;
            }
        }

        float passStart = Math.Max(0f, frames[passStartIndex].ElapsedSeconds - PassLeadSeconds);
        float passEnd = Math.Min(totalDuration, frames[passEndIndex].ElapsedSeconds + PassTrailSeconds);
        windows.Add((passStart, passEnd));
    }

    private static bool IsThreatFrame(ReplayFrame frame)
    {
        ReplayActorFrame? focus = GetFocusActor(frame);
        if (!focus.HasValue)
        {
            return false;
        }

        float nearestDefenderDistance = float.MaxValue;
        float nearestFrontDefenderDistance = float.MaxValue;

        foreach (var defender in frame.Defenders)
        {
            float distance = Vector2.Distance(defender.Position, focus.Value.Position);
            if (distance < nearestDefenderDistance)
            {
                nearestDefenderDistance = distance;
            }

            bool isInFront = defender.Position.Y >= focus.Value.Position.Y - 0.5f;
            if (isInFront && distance < nearestFrontDefenderDistance)
            {
                nearestFrontDefenderDistance = distance;
            }
        }

        bool closeThreat = nearestDefenderDistance <= CloseThreatDistance;
        bool frontThreat = nearestFrontDefenderDistance <= FrontThreatDistance;
        bool isQbFocus = focus.Value.Kind == ReplayActorKind.Quarterback;

        if (!isQbFocus && nearestDefenderDistance >= OpenFieldSkipDistance && !frontThreat)
        {
            return false;
        }

        return closeThreat || frontThreat;
    }

    private static ReplayActorFrame? GetFocusActor(ReplayFrame frame)
    {
        return frame.Ball.State switch
        {
            BallState.HeldByQB => frame.Quarterback,
            BallState.HeldByReceiver => GetActorById(frame, frame.Ball.HolderId),
            _ => null
        };
    }

    private static float GetReplayFocusY(ReplayFrame frame)
    {
        ReplayActorFrame? focus = GetFocusActor(frame);
        if (focus.HasValue)
        {
            return focus.Value.Position.Y;
        }

        return frame.Ball.Position.Y;
    }

    private static ReplayActorFrame? GetActorById(ReplayFrame frame, int actorId)
    {
        if (actorId == frame.Quarterback.Id)
        {
            return frame.Quarterback;
        }

        foreach (var receiver in frame.Receivers)
        {
            if (receiver.Id == actorId)
            {
                return receiver;
            }
        }

        foreach (var blocker in frame.Blockers)
        {
            if (blocker.Id == actorId)
            {
                return blocker;
            }
        }

        foreach (var defender in frame.Defenders)
        {
            if (defender.Id == actorId)
            {
                return defender;
            }
        }

        return null;
    }

    private static List<(float Start, float End)> MergeWindows(List<(float Start, float End)> windows)
    {
        if (windows.Count == 0)
        {
            return windows;
        }

        var ordered = windows.OrderBy(window => window.Start).ToList();
        var merged = new List<(float Start, float End)> { ordered[0] };

        for (int i = 1; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var last = merged[^1];
            if (current.Start <= last.End + 0.05f)
            {
                merged[^1] = (last.Start, Math.Max(last.End, current.End));
                continue;
            }

            merged.Add(current);
        }

        return merged;
    }

    private static List<ReplayFrame> NormalizeFrameTimes(IReadOnlyList<ReplayFrame> frames, float captureFps)
    {
        float frameStep = captureFps > 0f ? 1f / captureFps : 1f / Constants.TargetFps;
        float elapsed = 0f;
        var normalized = new List<ReplayFrame>(frames.Count);

        foreach (var frame in frames)
        {
            elapsed += frameStep;
            normalized.Add(new ReplayFrame
            {
                ElapsedSeconds = elapsed,
                LineOfScrimmage = frame.LineOfScrimmage,
                FirstDownLine = frame.FirstDownLine,
                Quarterback = frame.Quarterback,
                Receivers = frame.Receivers,
                Blockers = frame.Blockers,
                Defenders = frame.Defenders,
                Ball = frame.Ball
            });
        }

        return normalized;
    }
}
