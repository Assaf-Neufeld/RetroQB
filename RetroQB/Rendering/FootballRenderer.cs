using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Rendering;

/// <summary>Shared pixel football for live play and recorded playback.</summary>
public static class FootballRenderer
{
    public static void DrawGroundShadow(Vector2 position, BallState state, float height)
    {
        if (state is BallState.HeldByQB or BallState.HeldByReceiver) return;
        Vector2 ground = Constants.WorldToScreen(position);
        float lift = Math.Clamp(height / Constants.PassArcMaxHeight, 0f, 1f);
        // The shadow stays at the ground position, below actors, as the ball rises.
        Raylib.DrawEllipse((int)ground.X + 1, (int)ground.Y + 2,
            6f + lift * 2f, 2f + lift, new Color(6, 15, 10, (int)(115 - lift * 55)));
    }

    public static void Draw(Vector2 position, Vector2 velocity, BallState state,
        float height, float airTime, Vector2? holderPosition = null, Vector2? holderVelocity = null,
        PlayerVisualFrame holderVisual = default)
    {
        bool held = state is BallState.HeldByQB or BallState.HeldByReceiver;
        bool flying = state == BallState.InAir;
        Vector2 screen = Constants.WorldToScreen(holderPosition ?? position);
        if (held)
        {
            // Match the player's screen-space arm and facing at every field size.
            screen += PixelPlayerRenderer.GetBallOffset(holderVelocity.GetValueOrDefault(), holderVisual);
        }
        screen.Y -= height / Constants.FieldLength * Constants.FieldRect.Height;
        screen = new Vector2(MathF.Round(screen.X), MathF.Round(screen.Y));

        Vector2 direction = flying && velocity.LengthSquared() > 0.1f
            ? Vector2.Normalize(new Vector2(velocity.X, -velocity.Y)) : Vector2.UnitX;
        Vector2 cross = new(-direction.Y, direction.X);
        float scale = held ? 0.78f : 1f + Math.Clamp(height, 0f, 3f) * 0.06f;
        float length = 8f * scale;
        float width = 4f * scale;
        // Recorded air time also makes pausing and scrubbing freeze the spiral.
        float spin = flying ? airTime * 19f : 0f;
        float laceOffset = MathF.Sin(spin) * width * 0.57f;
        bool lacesVisible = MathF.Cos(spin) > -0.15f;

        if (flying)
        {
            for (int i = 3; i >= 1; i--)
            {
                Vector2 trail = screen - direction * (length + i * 5f);
                Raylib.DrawRectangle((int)trail.X, (int)trail.Y, 2, 1,
                    new Color(241, 191, 112, 65 - i * 14));
            }
        }

        // Rasterize in screen pixels for crisp edges at every throwing angle.
        int radius = (int)MathF.Ceiling(length + 1f);
        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
        {
            Vector2 offset = new(x, y);
            float along = Vector2.Dot(offset, direction);
            float across = Vector2.Dot(offset, cross);
            float u = MathF.Abs(along) / length;
            float edge = width * (1f - u * u);
            if (u > 1f || MathF.Abs(across) > edge) continue;

            Color color = new(157, 83, 36, 255);
            if (across < -0.6f) color = new Color(192, 115, 52, 255);
            if (across > 1f) color = new Color(111, 51, 26, 255);
            if (MathF.Abs(across) > edge - 1f || u > 0.88f)
                color = new Color(48, 28, 21, 255);
            else if (u > 0.62f && u < 0.76f)
                color = new Color(221, 191, 141, 255);
            else if (lacesVisible && MathF.Abs(along) < length * 0.44f)
            {
                float seamDistance = MathF.Abs(across - laceOffset);
                bool stitch = MathF.Abs(along) < 0.55f
                    || MathF.Abs(MathF.Abs(along) - length * 0.27f) < 0.55f;
                if (seamDistance < 0.55f || (stitch && seamDistance < width * 0.48f))
                    color = new Color(255, 239, 200, 255);
            }
            Raylib.DrawPixel((int)screen.X + x, (int)screen.Y + y, color);
        }
    }
}
