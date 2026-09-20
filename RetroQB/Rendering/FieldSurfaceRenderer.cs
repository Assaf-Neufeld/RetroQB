using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

internal sealed class FieldSurfaceRenderer
{
    private static readonly Color StripeDark = new(10, 70, 30, 255);
    private static readonly Color StripeLight = Palette.Field;

    public void Draw(string homeTeamName, Color homeTeamColor, string awayTeamName, Color awayTeamColor, float lineOfScrimmage)
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
            if (topScreen > botScreen) (topScreen, botScreen) = (botScreen, topScreen);

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

        DrawTurfGrain(rect);
        DrawTurfWear(rect, lineOfScrimmage);
        DrawMidfieldTurfEmblem(rect);

        DrawEndZones(rect, homeTeamName, homeTeamColor, awayTeamName, awayTeamColor);
    }

    private static void DrawTurfWear(Rectangle field, float lineOfScrimmage)
    {
        // Fixed world-space cleat marks: changing the LOS never moves the texture.
        // A small contrast boost near the current pocket suggests concentrated wear.
        for (int i = 0; i < 180; i++)
        {
            float worldX = 8f + (i * 37 % 370) / 10f;
            float worldY = Constants.EndZoneDepth + 3f + (i * 53 % 940) / 10f;
            float distance = MathF.Abs(worldY - lineOfScrimmage);
            int alpha = distance < 4f ? 38 : 14;
            int x = (int)Constants.WorldToScreenX(worldX);
            int y = (int)Constants.WorldToScreenY(worldY);
            int length = Math.Clamp((int)(field.Width / 100f), 1, 4);
            Raylib.DrawRectangle(x, y, length, 1, new Color(153, 144, 83, alpha));
            if (i % 3 == 0)
                Raylib.DrawPixel(x + 1, y + 2, new Color(6, 44, 22, alpha + 8));
        }
    }

    private static void DrawMidfieldTurfEmblem(Rectangle field)
    {
        int centerX = (int)MathF.Round(field.X + field.Width / 2f);
        int centerY = (int)MathF.Round(Constants.WorldToScreenY(Constants.EndZoneDepth + 50f));
        int halfWidth = Math.Clamp((int)(field.Width * 0.14f), 38, 84);
        int halfHeight = Math.Clamp((int)(halfWidth * 0.48f), 18, 40);

        Color outer = new(6, 61, 28, 210);
        Color middle = new(19, 96, 43, 220);
        Color inner = new(12, 76, 34, 225);
        Color detail = new(31, 111, 51, 185);

        DrawSteppedDiamond(centerX, centerY, halfWidth, halfHeight, outer);
        DrawSteppedDiamond(centerX, centerY, halfWidth - 7, halfHeight - 5, middle);
        DrawSteppedDiamond(centerX, centerY, halfWidth - 15, halfHeight - 10, inner);

        // Tonal chevrons suggest motion without creating a ball, badge, or target marker.
        int chevronWidth = Math.Max(12, halfWidth / 4);
        int chevronHeight = Math.Max(8, halfHeight / 2);
        DrawTurfChevron(centerX - chevronWidth - 3, centerY, chevronWidth, chevronHeight, pointsRight: true, detail);
        DrawTurfChevron(centerX + chevronWidth + 3, centerY, chevronWidth, chevronHeight, pointsRight: false, detail);
        Raylib.DrawRectangle(centerX - 3, centerY - halfHeight + 11, 6, (halfHeight * 2) - 22, detail);
    }

    private static void DrawSteppedDiamond(int centerX, int centerY, int halfWidth, int halfHeight, Color color)
    {
        int steps = Math.Clamp(halfHeight / 4, 4, 8);
        for (int step = 0; step < steps; step++)
        {
            float progress = step / (float)steps;
            int insetX = (int)MathF.Round(halfWidth * (1f - progress));
            int y = centerY - halfHeight + (step * halfHeight / steps);
            int width = Math.Max(2, (halfWidth - insetX) * 2);
            int bandHeight = Math.Max(2, halfHeight / steps);
            Raylib.DrawRectangle(centerX - width / 2, y, width, bandHeight, color);
            Raylib.DrawRectangle(centerX - width / 2, centerY + halfHeight - (step + 1) * bandHeight, width, bandHeight, color);
        }
    }

    private static void DrawTurfChevron(int centerX, int centerY, int width, int height, bool pointsRight, Color color)
    {
        int direction = pointsRight ? 1 : -1;
        for (int row = -height; row <= height; row += 3)
        {
            int taper = Math.Abs(row) / 2;
            int startX = pointsRight ? centerX - width / 2 : centerX + width / 2;
            int endX = centerX + direction * (width / 2 - taper);
            Raylib.DrawLine(startX, centerY + row, endX, centerY + row, color);
        }
    }

    private static void DrawTurfGrain(Rectangle rect)
    {
        int left = (int)rect.X + 4;
        int top = (int)rect.Y + 4;
        int width = Math.Max(1, (int)rect.Width - 8);
        int height = Math.Max(1, (int)rect.Height - 8);

        // Fixed pattern: no allocations, no per-frame randomness or shimmer.
        for (int i = 0; i < 150; i++)
        {
            int px = left + Math.Abs((i * 73 + 19) % width);
            int py = top + Math.Abs((i * 137 + 31) % height);
            Color speck = (i & 1) == 0
                ? new Color(88, 142, 76, 36)
                : new Color(2, 38, 17, 42);
            Raylib.DrawRectangle(px, py, (i % 5 == 0) ? 2 : 1, 1, speck);
        }
    }

    private static void DrawEndZones(Rectangle rect, string homeTeamName, Color homeTeamColor, string awayTeamName, Color awayTeamColor)
    {
        float bottom = rect.Y + rect.Height;
        float ownEndY = rect.Y + rect.Height * (1 - Constants.EndZoneDepth / Constants.FieldLength);
        float oppEndY = rect.Y + rect.Height * Constants.EndZoneDepth / Constants.FieldLength;

        int endzoneHeight = (int)(bottom - ownEndY);
        int topEndzoneHeight = (int)(oppEndY - rect.Y);

        Color homeEndZoneFill = Tint(homeTeamColor, 0.7f);
        Color awayEndZoneFill = Tint(awayTeamColor, 0.7f);

        Raylib.DrawRectangle((int)rect.X, (int)ownEndY, (int)rect.Width, endzoneHeight, homeEndZoneFill);
        Raylib.DrawRectangle((int)rect.X, (int)rect.Y, (int)rect.Width, topEndzoneHeight, awayEndZoneFill);

        Raylib.DrawRectangleLinesEx(new Rectangle(rect.X + 3, ownEndY + 3, rect.Width - 6, endzoneHeight - 6), 2f, new Color(255, 255, 255, 75));
        Raylib.DrawRectangleLinesEx(new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, topEndzoneHeight - 6), 2f, new Color(255, 255, 255, 75));

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

        // Paint sits on the turf: a thin dark keyline replaces the raised drop shadow.
        Color keyline = new(9, 21, 18, 160);
        Raylib.DrawText(label, drawX - 1, drawY, fontSize, keyline);
        Raylib.DrawText(label, drawX + 1, drawY, fontSize, keyline);
        Raylib.DrawText(label, drawX, drawY, fontSize, textColor);
        // Sparse translucent grain runs through both lettering and end-zone paint.
        for (int i = 0; i < 48; i++)
        {
            int px = drawX + (i * 47 % Math.Max(1, textWidth));
            int py = drawY + 2 + (i * 13 % Math.Max(1, fontSize - 4));
            Raylib.DrawPixel(px, py, new Color(20, 39, 28, 55));
        }
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
