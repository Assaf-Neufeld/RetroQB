using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

internal sealed class FieldSurfaceRenderer
{
    private static readonly Color StripeDark = new(10, 70, 30, 255);
    private static readonly Color StripeLight = Palette.Field;

    public void Draw(string homeTeamName, Color homeTeamColor, string awayTeamName, Color awayTeamColor)
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

        DrawEndZones(rect, homeTeamName, homeTeamColor, awayTeamName, awayTeamColor);
    }

    private static void DrawEndZones(Rectangle rect, string homeTeamName, Color homeTeamColor, string awayTeamName, Color awayTeamColor)
    {
        float bottom = rect.Y + rect.Height;
        float ownEndY = Constants.WorldToScreenY(Constants.EndZoneDepth);
        float oppEndY = Constants.WorldToScreenY(Constants.EndZoneDepth + 100f);

        int endzoneHeight = (int)(bottom - ownEndY);
        int topEndzoneHeight = (int)(oppEndY - rect.Y);

        Color homeEndZoneFill = Tint(homeTeamColor, 0.7f);
        Color awayEndZoneFill = Tint(awayTeamColor, 0.7f);

        Raylib.DrawRectangle((int)rect.X, (int)ownEndY, (int)rect.Width, endzoneHeight, homeEndZoneFill);
        Raylib.DrawRectangle((int)rect.X, (int)rect.Y, (int)rect.Width, topEndzoneHeight, awayEndZoneFill);

        int stripeCount = 6;
        int stripeHeight = Math.Max(2, endzoneHeight / (stripeCount * 2));
        Color homeStripe = Tint(homeTeamColor, 0.9f);
        Color awayStripe = Tint(awayTeamColor, 0.9f);

        for (int i = 0; i < stripeCount; i++)
        {
            int yOffset = i * stripeHeight * 2;
            Raylib.DrawRectangle((int)rect.X, (int)ownEndY + yOffset, (int)rect.Width, stripeHeight, homeStripe);
            Raylib.DrawRectangle((int)rect.X, (int)oppEndY - stripeHeight - yOffset, (int)rect.Width, stripeHeight, awayStripe);
        }

        DrawEndZoneTeamName(
            homeTeamName,
            (int)rect.X,
            (int)ownEndY,
            (int)rect.Width,
            endzoneHeight,
            ContrastTextColor(homeEndZoneFill));

        DrawEndZoneTeamName(
            awayTeamName,
            (int)rect.X,
            (int)rect.Y,
            (int)rect.Width,
            topEndzoneHeight,
            ContrastTextColor(awayEndZoneFill));
    }

    private static void DrawEndZoneTeamName(string teamName, int x, int y, int width, int height, Color textColor)
    {
        string label = string.IsNullOrWhiteSpace(teamName)
            ? "TEAM"
            : teamName.ToUpperInvariant();

        int fontSize = Math.Clamp(height - 10, 16, 34);
        int textWidth = Raylib.MeasureText(label, fontSize);

        while (textWidth > width - 18 && fontSize > 14)
        {
            fontSize -= 1;
            textWidth = Raylib.MeasureText(label, fontSize);
        }

        int drawX = x + (width - textWidth) / 2;
        int drawY = y + (height - fontSize) / 2;

        Color shadow = new(8, 8, 8, 150);
        Raylib.DrawText(label, drawX + 2, drawY + 2, fontSize, shadow);
        Raylib.DrawText(label, drawX, drawY, fontSize, textColor);
    }

    private static Color ContrastTextColor(Color background)
    {
        int luma = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
        return luma > 130 ? new Color(20, 20, 22, 255) : new Color(244, 244, 244, 255);
    }

    private static Color Tint(Color color, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        return new Color(
            (byte)Math.Clamp((int)(color.R * factor), 0, 255),
            (byte)Math.Clamp((int)(color.G * factor), 0, 255),
            (byte)Math.Clamp((int)(color.B * factor), 0, 255),
            color.A);
    }
}
