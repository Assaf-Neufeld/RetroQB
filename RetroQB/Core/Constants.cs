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

    // Side panel width for HUD
    public const float SidePanelWidth = 280f;

    // Dynamic field rect - call UpdateFieldRect each frame
    private static Rectangle _fieldRect = new(300, 40, 284, 640);
    public static Rectangle FieldRect => _fieldRect;

    public static void UpdateFieldRect()
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();
        
        // Field on the right side, leaving space for side panel on left
        float availableWidth = screenW - SidePanelWidth - 40; // 40 for margins
        float availableHeight = screenH - 80; // 40 margin top and bottom
        
        // Calculate field size maintaining proper aspect ratio (53.3:120)
        float aspectRatio = FieldWidth / FieldLength;
        float fieldHeight = availableHeight;
        float fieldWidth = fieldHeight * aspectRatio;
        
        // If too wide, scale down
        if (fieldWidth > availableWidth)
        {
            fieldWidth = availableWidth;
            fieldHeight = fieldWidth / aspectRatio;
        }
        
        // Center the field in the available area (right of side panel)
        float fieldX = SidePanelWidth + (availableWidth - fieldWidth) / 2 + 20;
        float fieldY = (screenH - fieldHeight) / 2;
        
        _fieldRect = new Rectangle(fieldX, fieldY, fieldWidth, fieldHeight);
    }

    public const float QbRadius = 1.0f;
    public const float ReceiverRadius = 1.0f;
    public const float DefenderRadius = 1.1f;
    public const float BallRadius = 0.4f;

    public const float CatchRadius = 1.8f;
    public const float InterceptRadius = 1.2f;
    public const float ContestedCatchRadius = 2.5f;

    public const float QbMaxSpeed = 8f;
    public const float QbSprintSpeed = 11f;
    public const float QbAcceleration = 28f;
    public const float QbFriction = 16f;

    public const float WrSpeed = 7.6f;
    public const float TeSpeed = 6.9f;
    public const float RbSpeed = 7.1f;
    public const float OlSpeed = 5.6f;
    public const float DlSpeed = 6.3f;
    public const float LbSpeed = 6.8f;
    public const float DbSpeed = 7.4f;

    public const float BallMinSpeed = 14f;
    public const float BallMaxSpeed = 26f;
    public const float BallMaxAirTime = 3.0f;

    public const float ThrowPressureMinDistance = 2.8f;
    public const float ThrowPressureMaxDistance = 10.5f;
    public const float ThrowBaseInaccuracyDeg = 0.6f;
    public const float ThrowMaxInaccuracyDeg = 9.5f;

    public const float AimChargeSeconds = 1.0f;

    public const float BlockerSpeed = 6.0f;
    public const float BlockEngageRadius = 3.8f;
    public const float BlockHoldStrength = 4.2f;

    // Defensive coverage tuning
    public const float ManCoverageDistanceThreshold = 5f;
    public const float ZoneCoverageDepth = 14f;
    public const float ZoneCoverageDepthDb = 22f;
    public const float ZoneCoverageDepthFlat = 6.5f;

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
