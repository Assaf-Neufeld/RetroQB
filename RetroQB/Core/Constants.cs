using Raylib_cs;
using System.Numerics;

namespace RetroQB.Core;

public static class Constants
{
    public const int ScreenWidth = 1280;
    public const int ScreenHeight = 720;
    public const int TargetFps = 60;

    public const float FieldWidth = 53.3f;
    public const float FieldLength = 120f;
    public const float EndZoneDepth = 10f;

    public static readonly Rectangle FieldRect = new(80, 40, 1120, 640);

    public const float QbRadius = 1.0f;
    public const float ReceiverRadius = 1.0f;
    public const float DefenderRadius = 1.1f;
    public const float BallRadius = 0.4f;

    public const float CatchRadius = 1.5f;
    public const float InterceptRadius = 1.6f;

    public const float QbMaxSpeed = 8f;
    public const float QbSprintSpeed = 11f;
    public const float QbAcceleration = 28f;
    public const float QbFriction = 16f;

    public const float ReceiverSpeed = 7.2f;
    public const float DefenderSpeed = 7.0f;

    public const float BallMinSpeed = 14f;
    public const float BallMaxSpeed = 22f;
    public const float BallMaxAirTime = 3.0f;

    public const float AimChargeSeconds = 1.0f;

    public const float BlockerSpeed = 6.0f;
    public const float BlockEngageRadius = 3.8f;
    public const float BlockHoldStrength = 4.2f;

    public static Vector2 WorldToScreen(Vector2 worldPos)
    {
        float x = FieldRect.X + (worldPos.X / FieldWidth) * FieldRect.Width;
        float y = FieldRect.Y + FieldRect.Height - (worldPos.Y / FieldLength) * FieldRect.Height;
        return new Vector2(x, y);
    }

    public static float WorldToScreenY(float worldY)
    {
        return FieldRect.Y + FieldRect.Height - (worldY / FieldLength) * FieldRect.Height;
    }

    public static float WorldToScreenX(float worldX)
    {
        return FieldRect.X + (worldX / FieldWidth) * FieldRect.Width;
    }
}
