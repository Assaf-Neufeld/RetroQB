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

    // UI column layout
    public const float SidePanelWidth = 320f;
    public const float ScoreboardPanelWidth = 320f;
    public const float ColumnGap = 10f;
    public const float OuterMargin = 10f;

    // Dynamic field rect - call UpdateFieldRect each frame
    private static Rectangle _fieldRect = new(300, 40, 284, 640);
    public static Rectangle FieldRect => _fieldRect;
    // Set only inside the timed renderer's scoped draw; simulation stays offense-relative.
    public static bool OffenseDownScreen { get; set; }
    public static Vector2 OrientDirection(Vector2 direction, bool downScreen)
        => downScreen ? new(direction.X, -direction.Y) : direction;
    public static int SidelineApronWidth => Math.Max(12, (int)(FieldRect.Width * 0.06f));

    public static void UpdateFieldRect()
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();
        
        // Field centered between left HUD and right scoreboard columns
        float availableWidth = screenW - SidePanelWidth - ScoreboardPanelWidth - (OuterMargin * 2) - (ColumnGap * 2);
        // Leave room for end-zone decks and the widest stadium stage. Use the
        // same envelope in every stage so advancing never changes the camera scale.
        float aspectRatio = FieldWidth / FieldLength;
        // Each end needs a 16px deck, 4px gap, 6px outer margin and the
        // apron (6% of field width, at least 12px). Solve before sizing the field.
        float availableHeight = Math.Max(120f,
            Math.Min(screenH - 76f, (screenH - 52f) / (1f + 0.12f * aspectRatio)));
        float maxFieldWidth = Math.Max(80f, (availableWidth - 64f) / 1.58f);
        
        // Calculate field size maintaining proper aspect ratio (53.3:120)
        float fieldHeight = availableHeight;
        float fieldWidth = fieldHeight * aspectRatio;
        
        // If too wide, scale down
        if (fieldWidth > maxFieldWidth)
        {
            fieldWidth = maxFieldWidth;
            fieldHeight = fieldWidth / aspectRatio;
        }
        
        // Center the field in the available area (between side panel and scoreboard)
        float fieldX = OuterMargin + SidePanelWidth + ColumnGap + (availableWidth - fieldWidth) / 2;
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
    public const float PassDefendPathRadius = 2.35f;
    public const float PassDefendBallRadius = 2.75f;
    public const float PassDefendShortLandingRadius = 5.2f;
    public const float PassDefendLongLandingRadius = 3.4f;

    public const float QbMaxSpeed = 8f;
    public const float QbSprintSpeed = 11f;
    public const float QbAcceleration = 28f;
    public const float QbFriction = 16f;
    public const float QbArmStrengthMin = 0.75f;
    public const float QbArmStrengthMax = 1.5f;
    public const float QbBaseMaxThrowDistance = 48f;

    public const float WrSpeed = 7.6f;
    public const float TeSpeed = 6.9f;
    public const float RbSpeed = 7.1f;
    public const float OlSpeed = 6.5f;
    public const float DlSpeed = 6.3f;
    public const float DeSpeed = 5.4f;
    public const float LbSpeed = 6.6f;
    public const float DbSpeed = 7.8f;

    public const float BallMinSpeed = 18f;
    public const float BallMaxSpeed = 32f;
    public const float BallMaxAirTime = 3.0f;

    // Pass flight tuning
    public const float PassOverthrowFactor = 0.2f;
    public const float PassOverthrowMin = 1.5f;
    public const float PassOverthrowMax = 6.0f;

    public const float PassArcShortDistance = 6f;
    public const float PassArcLongDistance = 28f;
    public const float PassArcMinHeight = 0.35f;
    public const float PassArcMaxHeight = 3.0f;
    public const float PassCatchMaxHeight = 0.9f;

    public const float ThrowPressureMinDistance = 2.8f;
    public const float ThrowPressureMaxDistance = 10.5f;
    public const float ThrowBaseInaccuracyDeg = 0.6f;
    public const float ThrowMaxInaccuracyDeg = 9.5f;
    public const float ShortPassMaxDistance = 8f;
    public const float MediumPassMaxDistance = 16f;

    public const float AimChargeSeconds = 1.0f;

    // Play timing
    public const float DefaultPlayOverDuration = 1.25f;
    public const float TouchdownPlayOverDuration = 2.6f;

    public const float BlockerSpeed = 6.0f;
    public const float BlockEngageRadius = 4.5f;
    public const float BlockHoldStrength = 5.8f;

    // Defensive coverage tuning
    public const float ZoneCoverageDepth = 12.5f;
    public const float ZoneCoverageDepthDb = 19.5f;
    public const float ZoneCoverageDepthFlat = 5.8f;
    public const float ZoneMatchWidthFlat = FieldWidth * 0.28f;
    public const float ZoneMatchWidthHook = FieldWidth * 0.30f;
    public const float ZoneMatchWidthDeep = FieldWidth * 0.56f;
    public const float ZoneMatchDepthBuffer = 2.8f;
    public const float ZoneMatchAttachRadius = 3.1f;
    public const float ZoneDeepCushion = 1.8f;
    public const float ZoneCarryDepth = 25.5f;
    public const float ZoneLookAheadDeep = 4.8f;
    public const float ZoneLookAheadUnderneath = 3.1f;
    public const float ZoneHookSideStretch = 4.2f;
    public const float ZoneHookMiddleStretch = 3.6f;
    public const float ZoneFlatStretch = 7.4f;

    public static Vector2 WorldToScreen(Vector2 worldPos)
    {
        float x = FieldRect.X + (worldPos.X / FieldWidth) * FieldRect.Width;
        float y = WorldToScreenY(worldPos.Y);
        return new Vector2(x, y);
    }

    public static float WorldToScreenY(float worldY)
    {
        return FieldRect.Y + (OffenseDownScreen ? worldY / FieldLength : 1 - worldY / FieldLength) * FieldRect.Height;
    }

    public static float WorldToScreenX(float worldX)
    {
        return FieldRect.X + (worldX / FieldWidth) * FieldRect.Width;
    }
}
