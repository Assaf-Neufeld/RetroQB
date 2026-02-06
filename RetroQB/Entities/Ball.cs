using System;
using System.Numerics;
using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Entities;

public enum BallState
{
    HeldByQB,
    HeldByReceiver,
    InAir,
    Dead
}

public sealed class Ball : Entity
{
    public BallState State { get; private set; } = BallState.HeldByQB;
    public Entity? Holder { get; private set; }
    public float AirTime { get; private set; }
    public Vector2 ThrowStart { get; private set; }
    public float IntendedDistance { get; private set; }
    public float MaxTravelDistance { get; private set; }
    public float ArcApexHeight { get; private set; }

    public Ball(Vector2 position) : base(position, Constants.BallRadius, "o", Palette.Gold)
    {
    }

    public void SetHeld(Entity holder, BallState state)
    {
        Holder = holder;
        State = state;
        Velocity = Vector2.Zero;
        AirTime = 0f;
        ThrowStart = holder.Position;
        IntendedDistance = 0f;
        MaxTravelDistance = 0f;
        ArcApexHeight = 0f;
    }

    public void SetInAir(Vector2 position, Vector2 velocity, float intendedDistance, float maxTravelDistance, float arcApexHeight)
    {
        Holder = null;
        State = BallState.InAir;
        Position = position;
        Velocity = velocity;
        AirTime = 0f;
        ThrowStart = position;
        IntendedDistance = intendedDistance;
        MaxTravelDistance = maxTravelDistance;
        ArcApexHeight = arcApexHeight;
    }

    public void SetDead(Vector2 position)
    {
        Holder = null;
        State = BallState.Dead;
        Position = position;
        Velocity = Vector2.Zero;
        ThrowStart = position;
        IntendedDistance = 0f;
        MaxTravelDistance = 0f;
        ArcApexHeight = 0f;
    }

    public float GetTravelDistance()
    {
        return Vector2.Distance(ThrowStart, Position);
    }

    public float GetFlightProgress()
    {
        if (IntendedDistance <= 0.01f)
        {
            return 1f;
        }

        return Math.Clamp(GetTravelDistance() / IntendedDistance, 0f, 1f);
    }

    public float GetArcHeight()
    {
        if (State != BallState.InAir || ArcApexHeight <= 0f)
        {
            return 0f;
        }

        float t = GetFlightProgress();
        return ArcApexHeight * 4f * t * (1f - t);
    }

    public override void Update(float dt)
    {
        if (State == BallState.HeldByQB || State == BallState.HeldByReceiver)
        {
            if (Holder != null)
            {
                Position = Holder.Position;
            }
            return;
        }

        if (State == BallState.InAir)
        {
            AirTime += dt;
            base.Update(dt);
        }
    }

    public override void Draw()
    {
        Vector2 drawPos = Position;
        
        // When held, offset the ball slightly so it's visible next to the holder
        if ((State == BallState.HeldByQB || State == BallState.HeldByReceiver) && Holder != null)
        {
            drawPos = Holder.Position + new Vector2(0.8f, 0f); // Offset to the right of holder
        }
        
        Vector2 screen = Constants.WorldToScreen(drawPos);

        float height = GetArcHeight();
        if (height > 0f)
        {
            float heightPixels = (height / Constants.FieldLength) * Constants.FieldRect.Height;
            screen.Y -= heightPixels;
        }

        // Football dimensions
        float majorRadius = 7f;
        float minorRadius = 4.5f;

        // Scale up slightly when airborne to show height
        if (height > 0f)
        {
            float scale = 1f + height * 0.06f;
            majorRadius *= scale;
            minorRadius *= scale;
        }

        // Determine rotation angle: point along velocity when in air
        float angle = 0f; // default: horizontal
        if (State == BallState.InAir && Velocity.LengthSquared() > 0.1f)
        {
            // WorldToScreen flips Y, so negate Y for screen-space angle
            Vector2 screenVel = new Vector2(Velocity.X, -Velocity.Y);
            angle = MathF.Atan2(screenVel.Y, screenVel.X);
        }

        // Direction vectors along major (long) and minor (short) axes
        Vector2 major = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        Vector2 minor = new Vector2(-major.Y, major.X);

        Color brown = new Color(139, 90, 43, 255);
        Color darkBrown = new Color(100, 60, 25, 255);
        Color laceWhite = new Color(230, 230, 230, 255);

        // Drop shadow
        Vector2 shadowOff = new Vector2(1f, 2f);
        DrawFootballBody(screen + shadowOff, major, minor, majorRadius, minorRadius, new Color(10, 10, 12, 100));

        // Main football body
        DrawFootballBody(screen, major, minor, majorRadius, minorRadius, brown);

        // Pointed tips at each end
        Vector2 tipFront = screen + major * majorRadius;
        Vector2 tipBack = screen - major * majorRadius;
        Vector2 tipPerp = minor * (minorRadius * 0.4f);
        Raylib.DrawTriangle(tipFront, screen + major * (majorRadius * 0.6f) - tipPerp, screen + major * (majorRadius * 0.6f) + tipPerp, darkBrown);
        Raylib.DrawTriangle(tipBack, screen - major * (majorRadius * 0.6f) + tipPerp, screen - major * (majorRadius * 0.6f) - tipPerp, darkBrown);

        // Laces along the minor axis (centered, perpendicular to the long axis)
        float laceLen = majorRadius * 0.5f;
        Vector2 laceStart = screen - major * laceLen;
        Vector2 laceEnd = screen + major * laceLen;
        Raylib.DrawLineEx(laceStart, laceEnd, 1f, laceWhite);

        // Cross-stitches
        int stitchCount = 3;
        float stitchH = minorRadius * 0.35f;
        for (int i = 0; i < stitchCount; i++)
        {
            float t = (i + 0.5f) / stitchCount;
            Vector2 stitchCenter = Vector2.Lerp(laceStart, laceEnd, t);
            Raylib.DrawLineV(stitchCenter - minor * stitchH, stitchCenter + minor * stitchH, laceWhite);
        }
    }

    private static void DrawFootballBody(Vector2 center, Vector2 major, Vector2 minor, float majorR, float minorR, Color color)
    {
        // Approximate a rotated ellipse with a triangle fan
        const int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float a1 = (i / (float)segments) * MathF.PI * 2f;
            float a2 = ((i + 1) / (float)segments) * MathF.PI * 2f;

            Vector2 p1 = center + major * (MathF.Cos(a1) * majorR) + minor * (MathF.Sin(a1) * minorR);
            Vector2 p2 = center + major * (MathF.Cos(a2) * majorR) + minor * (MathF.Sin(a2) * minorR);

            Raylib.DrawTriangle(center, p2, p1, color);
        }
    }
}
