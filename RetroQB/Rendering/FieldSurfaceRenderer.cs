using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

internal sealed class FieldSurfaceRenderer
{
    private static readonly Color StripeDark = new(10, 70, 30, 255);
    private static readonly Color StripeLight = Palette.Field;

    public void Draw()
    {
        Rectangle rect = Constants.FieldRect;

        // Base fill
        Raylib.DrawRectangleRec(rect, Palette.Field);

        // Alternating mowed-grass stripes every 5 yards (playing field only)
        for (int yard = 0; yard < 100; yard += 5)
        {
            float topWorld = Constants.EndZoneDepth + yard;
            float botWorld = Constants.EndZoneDepth + yard + 5f;
            float topScreen = Constants.WorldToScreenY(botWorld);  // world Y is inverted vs screen Y
            float botScreen = Constants.WorldToScreenY(topWorld);

            // Clamp to field rect
            topScreen = MathF.Max(topScreen, rect.Y);
            botScreen = MathF.Min(botScreen, rect.Y + rect.Height);

            if (botScreen <= topScreen) continue;

            bool isDark = (yard / 5) % 2 == 0;
            if (isDark)
            {
                Raylib.DrawRectangle((int)rect.X, (int)topScreen, (int)rect.Width, (int)(botScreen - topScreen), StripeDark);
            }
            // Light stripes are already the base fill color — no draw needed
        }

        DrawEndZones(rect);
    }

    private static void DrawEndZones(Rectangle rect)
    {
        float bottom = rect.Y + rect.Height;
        float ownEndY = Constants.WorldToScreenY(Constants.EndZoneDepth);
        float oppEndY = Constants.WorldToScreenY(Constants.EndZoneDepth + 100f);

        int endzoneHeight = (int)(bottom - ownEndY);
        int topEndzoneHeight = (int)(oppEndY - rect.Y);

        Raylib.DrawRectangle((int)rect.X, (int)ownEndY, (int)rect.Width, endzoneHeight, Palette.EndZone);
        Raylib.DrawRectangle((int)rect.X, (int)rect.Y, (int)rect.Width, topEndzoneHeight, Palette.EndZone);

        int stripeCount = 6;
        int stripeHeight = Math.Max(2, endzoneHeight / (stripeCount * 2));
        for (int i = 0; i < stripeCount; i++)
        {
            int yOffset = i * stripeHeight * 2;
            Raylib.DrawRectangle((int)rect.X, (int)ownEndY + yOffset, (int)rect.Width, stripeHeight, Palette.EndZoneAccent);
            Raylib.DrawRectangle((int)rect.X, (int)oppEndY - stripeHeight - yOffset, (int)rect.Width, stripeHeight, Palette.EndZoneAccent);
        }

        Raylib.DrawText("END ZONE", (int)rect.X + 20, (int)bottom - 30, 18, Palette.EndZoneText);
        Raylib.DrawText("END ZONE", (int)rect.X + 20, (int)rect.Y + 8, 18, Palette.EndZoneText);
    }
}
