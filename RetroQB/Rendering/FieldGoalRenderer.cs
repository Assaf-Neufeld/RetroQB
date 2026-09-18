using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

/// <summary>Special-teams formation and flight in the normal field camera, with a compact HUD meter.</summary>
public static class FieldGoalRenderer
{
    public static void DrawOnField(FieldGoalAttempt kick, float lineOfScrimmage, Color offense, Color defense)
    {
        float center = Constants.FieldWidth / 2;
        const float halfWidth = 3.0833f;
        Vector2 post = Constants.WorldToScreen(new(center, Constants.FieldLength));
        float width = halfWidth / Constants.FieldWidth * Constants.FieldRect.Width;
        // Goalposts sit at the back of the actual opponent end zone.
        float crossbarY = post.Y - 10;
        Raylib.DrawLineEx(post, new(post.X, crossbarY), 3, Palette.Gold);
        Raylib.DrawLineEx(new(post.X - width, crossbarY), new(post.X + width, crossbarY), 3, Palette.Gold);
        Raylib.DrawLineEx(new(post.X - width, crossbarY), new(post.X - width, crossbarY - 22), 3, Palette.Gold);
        Raylib.DrawLineEx(new(post.X + width, crossbarY), new(post.X + width, crossbarY - 22), 3, Palette.Gold);

        bool launched = kick.Phase is KickPhase.Flight or KickPhase.Result;
        float runUp = launched ? Math.Min(1, kick.FlightProgress / 0.12f) : 0;
        float flight = launched ? Math.Clamp((kick.FlightProgress - 0.12f) / 0.88f, 0, 1) : 0;
        // Scale the sprites with the field so the compact line stays readable at small sizes.
        float pixelsPerYard = Constants.FieldRect.Width / Constants.FieldWidth;
        float playerScale = Math.Clamp(pixelsPerYard / 9f, 0.75f, 1);
        float lineSpacing = Math.Max(2.4f, 17f / pixelsPerYard);
        float lineDepth = Math.Max(1.1f, 9f / pixelsPerYard);
        float edge = lineSpacing * 3 + 2.4f;
        void Player(float x, float y, string role, Color color, Vector2 velocity = default)
        {
            Vector2 screen = Constants.WorldToScreen(new(x, y));
            Rlgl.PushMatrix();
            Rlgl.Translatef(screen.X, screen.Y, 0);
            Rlgl.Scalef(playerScale, playerScale, 1);
            PixelPlayerRenderer.Draw(Vector2.Zero, velocity, role, color);
            Rlgl.PopMatrix();
        }

        // Nine protectors, holder and kicker; use the same sprites as regular plays.
        for (int i = -3; i <= 3; i++)
        {
            Player(center + i * lineSpacing, lineOfScrimmage - lineDepth, "OL", offense);
            Player(center + i * lineSpacing, lineOfScrimmage + lineDepth - kick.SnapProgress * 0.2f, "DL", defense);
        }
        foreach (int side in new[] { -1, 1 })
        {
            Player(center + side * edge, lineOfScrimmage - lineDepth - 1.4f, "TE", offense);
            Player(center + side * edge, lineOfScrimmage + lineDepth + 0.6f, "LB", defense);
            Player(center + side * lineSpacing * 2, lineOfScrimmage + lineDepth + 3.4f, "DB", defense);
        }
        Vector2 spot = kick.HolderSpot;
        Player(center + Math.Max(1.6f, 12f / pixelsPerYard), spot.Y, "H", offense);
        Vector2 kicker = Vector2.Lerp(kick.KickerStart, spot + new Vector2(-1.5f, -0.5f), runUp);
        Player(kicker.X, kicker.Y, "K", offense, launched && runUp < 1 ? new Vector2(1, 1) : Vector2.Zero);
        if (launched && runUp >= 1)
        {
            Vector2 foot = Constants.WorldToScreen(kicker);
            Raylib.DrawLineEx(foot + new Vector2(2, 5) * playerScale, foot + new Vector2(7, -1) * playerScale, 3 * playerScale, Palette.White);
        }

        if (kick.Phase is KickPhase.Setup or KickPhase.Snap)
        {
            FootballRenderer.Draw(kick.SnapBallPosition, new Vector2(0, -1), BallState.InAir,
                0, kick.SnapProgress * FieldGoalAttempt.SnapDuration);
            return;
        }

        float reach = Math.Min(kick.Distance, kick.Power * FieldGoalAttempt.MaxDistance);
        Vector2 target = new(center + Math.Clamp(kick.LateralError, -5, 5) * halfWidth, spot.Y + reach);
        Vector2 ground = Vector2.Lerp(spot, target, flight);
        // Cross the bar above it when in range; short kicks land on the field.
        float endHeight = kick.Power * FieldGoalAttempt.MaxDistance + 0.001f >= kick.Distance ? 4 : 0;
        float height = MathF.Sin(flight * MathF.PI) * 7 + endHeight * flight;
        FootballRenderer.DrawGroundShadow(ground, BallState.InAir, height);
        FootballRenderer.Draw(ground, target - spot, BallState.InAir, height, flight * FieldGoalAttempt.FlightDuration);
    }

    public static void DrawHud(FieldGoalAttempt kick)
    {
        int x = (int)Constants.OuterMargin + 15;
        int y = (int)Constants.OuterMargin + 78;
        void Info(string text, Color color, int size = 14)
        {
            Raylib.DrawText(text, x, y, size, color);
            y += size + 10;
        }
        Info("FIELD GOAL", Palette.Gold, 22);
        Info($"{kick.Distance:F0} YARDS  |  3 POINTS", Palette.White);
        Info(kick.InRange ? kick.Difficulty : "OUT OF RANGE - MAX 60 YD", Palette.Cyan);
        y += 12;
        Info("SPACE: SNAP THE BALL", Palette.White);
        Info("Then time the kick:", Palette.Muted, 12);
        Info("SPACE: POWER / LOCK / KICK", Palette.White, 12);
        y += 12;
        Info("Aim for the CENTER GREEN", Palette.Gold, 13);
        Info("Longer kick = less green", Palette.Muted, 12);
        Info("Red power = short kick", Palette.Muted, 12);
        Info("Early: right  |  Late: left", Palette.Muted, 12);
        Info("Make: you +3, opponent +3", Palette.Muted, 12);
        Info("Miss: opponent +7", Palette.Muted, 12);
        if (kick.Phase == KickPhase.Setup)
        {
            y += 12;
            Info(kick.InRange ? "SPACE: SNAP BALL" : "MOVE CLOSER BEFORE KICKING", Palette.Gold, 13);
            Info("ESC: BACK TO PLAYBOOK", Palette.White, 13);
        }
        else if (kick.Phase == KickPhase.Snap)
            Info("SNAP TO HOLDER...", Palette.Gold);
        else if (kick.TimingActive)
            Info("PLAY FROZEN - TIME YOUR KICK", Palette.Gold, 12);
        else if (kick.Phase == KickPhase.Flight)
            Info("KICK IS AWAY...", Palette.Gold);

        // The meter appears only after the holder receives the snap.
        if (!kick.ShowMeter) return;

        // Anchor beside the kicker, keeping the ball's path and both side panels clear.
        int left = (int)(Constants.OuterMargin + Constants.SidePanelWidth + Constants.ColumnGap + 4);
        int available = Raylib.GetScreenWidth() - left * 2;
        int panelWidth = Math.Min(270, available);
        Vector2 anchor = Constants.WorldToScreen(kick.KickerStart);
        int panelX = Math.Clamp((int)anchor.X - panelWidth / 2, left, Raylib.GetScreenWidth() - left - panelWidth);
        int top = Math.Clamp((int)anchor.Y + 22, 10, Raylib.GetScreenHeight() - 106);
        Raylib.DrawRectangle(panelX, top, panelWidth, 96, new Color(12, 23, 29, 235));
        Raylib.DrawRectangleLines(panelX, top, panelWidth, 96, Palette.PanelLine);
        void Center(string text, int offset, Color color, int size = 13)
        {
            while (size > 9 && Raylib.MeasureText(text, size) > panelWidth - 16) size--;
            Raylib.DrawText(text, panelX + (panelWidth - Raylib.MeasureText(text, size)) / 2, top + offset, size, color);
        }
        bool accuracy = kick.Phase is KickPhase.Accuracy or KickPhase.Flight or KickPhase.Result;
        Center(accuracy ? "ACCURACY  |  CENTER GREEN" : "POWER  |  CENTER GREEN", 9, Palette.White);
        float meterX = panelX + 14, meterWidth = panelWidth - 28, meterY = top + 31;
        float target = accuracy ? FieldGoalAttempt.AccuracyTarget : FieldGoalAttempt.PowerTarget;
        float halfWidth = accuracy ? kick.AccuracyHalfWidth : kick.PowerHalfWidth;
        // The green band exactly matches the timing window used to resolve the kick.
        Raylib.DrawRectangleRec(new(meterX, meterY, meterWidth, 17), Palette.Red);
        Raylib.DrawRectangleRec(new(meterX + (target - halfWidth - 0.06f) * meterWidth, meterY, (halfWidth + 0.06f) * 2 * meterWidth, 17), Palette.Orange);
        Raylib.DrawRectangleRec(new(meterX + (target - halfWidth) * meterWidth, meterY, halfWidth * 2 * meterWidth, 17), Palette.Lime);
        Raylib.DrawLineEx(new(meterX + kick.Marker * meterWidth, meterY - 3), new(meterX + kick.Marker * meterWidth, meterY + 20), 3, Palette.White);
        string prompt = kick.Phase switch
        {
            KickPhase.Ready => "SPACE: START POWER",
            KickPhase.Power => "SPACE: LOCK IN THE GREEN",
            KickPhase.Accuracy => "SPACE: KICK IN THE GREEN",
            KickPhase.Flight => "KICK IS AWAY...",
            _ => kick.Result
        };
        Center(prompt, 58, Palette.Gold);
        Center(kick.Phase == KickPhase.Result ? $"AWAY +{kick.OpponentPoints}  |  ENTER: CONTINUE" : "PLAY FROZEN - TIME YOUR KICK", 78, Palette.Muted, 10);
    }
}
