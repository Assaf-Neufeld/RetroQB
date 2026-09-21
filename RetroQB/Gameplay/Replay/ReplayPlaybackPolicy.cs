using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Replay;

public static class ReplayPlaybackPolicy
{
    public const float NormalSpeed = 1f;
    public const float HighlightSpeed = 0.38f;

    public static float GetSpeed(ReplayClip clip)
    {
        if (clip.Outcome == PlayOutcome.Touchdown) return HighlightSpeed;

        foreach (ReplayFrame frame in clip.Frames)
        {
            Vector2? carrier = frame.Ball.State switch
            {
                BallState.HeldByQB => frame.Quarterback.Position,
                BallState.HeldByReceiver => frame.Receivers
                    .Where(actor => actor.Id == frame.Ball.HolderId)
                    .Select(actor => (Vector2?)actor.Position).FirstOrDefault(),
                _ => null
            };
            if (carrier is not { } position) continue;

            bool nearGoalLine = position.Y >= FieldGeometry.OpponentGoalLine - 5f;
            bool nearContact = frame.Defenders.Any(defender =>
                Vector2.DistanceSquared(position, defender.Position) <= 2.25f);
            if (nearGoalLine || nearContact) return HighlightSpeed;
        }

        return NormalSpeed;
    }
}
