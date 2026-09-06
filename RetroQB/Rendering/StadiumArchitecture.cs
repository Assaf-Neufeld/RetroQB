using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

internal sealed partial class StadiumBackdropRenderer
{
    private readonly record struct StadiumLighting(
        Color Concrete, Color Rail, Color Trim, Color Lamp, Color Wash, string Label)
    {
        public static StadiumLighting For(SeasonStage stage) => stage switch
        {
            SeasonStage.RegularSeason => new(new(51, 47, 43, 255), new(110, 99, 80, 255),
                new(195, 155, 92, 255), new(255, 225, 166, 255), new(228, 169, 87, 18), "REGULAR SEASON"),
            SeasonStage.Playoff => new(new(35, 46, 61, 255), new(92, 118, 143, 255),
                new(117, 190, 224, 255), new(211, 238, 255, 255), new(93, 165, 235, 24), "PLAYOFFS"),
            SeasonStage.SuperBowl => new(new(42, 45, 59, 255), new(131, 135, 153, 255),
                new(235, 190, 83, 255), new(249, 248, 234, 255), new(224, 215, 171, 28), "SUPER BOWL"),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };
    }

    private static void DrawBowlShell(Rectangle field, int left, int right, int width,
        Color home, Color away, StadiumLighting lighting, float attendance)
    {
        int endClearance = Constants.SidelineApronWidth + 20;
        int top = (int)field.Y - endClearance;
        int bottom = (int)(field.Y + field.Height) + endClearance;
        GetSeatingBounds(field, out int seatingTop, out _, out _, out int seatingBottom);
        DrawCornerWing(left, width, top, bottom, seatingTop, seatingBottom, true, home, away, lighting, attendance);
        DrawCornerWing(right, width, top, bottom, seatingTop, seatingBottom, false, away, home, lighting, attendance);

        // Continuous end decks join both side wings outside the sideline apron.
        DrawEndDeck(left, top, right + width - left, home, away, lighting, true);
        DrawEndDeck(left, bottom - 16, right + width - left, away, home, lighting, false);
    }

    private static void DrawCornerWing(int x, int width, int top, int bottom,
        int seatingTop, int seatingBottom, bool leftSide, Color primary, Color secondary,
        StadiumLighting lighting, float attendance)
    {
        int extension = Math.Clamp(width / 4, 5, 12);
        int outerX = leftSide ? x - extension : x;
        int totalWidth = width + extension;
        Raylib.DrawRectangle(outerX + 3, top + 5, totalWidth, bottom - top, new Color(0, 0, 0, 110));

        // Stepped corners retain the pixel vocabulary while joining the long tiers.
        for (int y = top; y < bottom; y += 4)
        {
            int endDistance = Math.Min(y - top, bottom - y - 1);
            int inset = Math.Max(0, 12 - endDistance / 4);
            int stripX = outerX + (leftSide ? inset : 0);
            int stripWidth = totalWidth - inset;
            Raylib.DrawRectangle(stripX, y, stripWidth, Math.Min(4, bottom - y), lighting.Concrete);
            int outerEdge = leftSide ? stripX : stripX + stripWidth - 2;
            Raylib.DrawRectangle(outerEdge, y, 2, Math.Min(4, bottom - y), lighting.Rail);
        }

        // Smaller corner sections read as receding rows, with space between tiers.
        int spacing = attendance >= 0.95f ? 7 : 9;
        for (int y = top + 24; y < bottom - 18; y += spacing)
        {
            if (y >= seatingTop - 4 && y <= seatingBottom + 5) continue;
            int endDistance = Math.Min(y - top, bottom - y);
            int inset = Math.Max(0, 12 - endDistance / 4);
            int startX = x + 5 + (leftSide ? inset : 0);
            int endX = x + width - 5 - (leftSide ? 0 : inset);
            Raylib.DrawLine(startX, y + 4, endX, y + 4, AdjustColor(lighting.Rail, -40));
            for (int fanX = startX; fanX < endX - 3; fanX += 6)
            {
                int seed = Hash(fanX, y, 43);
                Color shirt = (seed & 3) == 0 ? secondary : primary;
                DrawSpectator(fanX, y, seed, Tint(shirt, 0.74f), 0.3f, 0f, 0f);
            }
        }

        int innerRailX = leftSide ? x + width - 2 : x;
        Raylib.DrawRectangle(innerRailX, top + 18, 2, bottom - top - 36, lighting.Rail);
        for (int y = top + 22; y < bottom - 20; y += 18)
            Raylib.DrawRectangle(innerRailX - 1, y, 4, 2, lighting.Trim);
    }

    private static void DrawEndDeck(int x, int y, int width, Color primary, Color secondary,
        StadiumLighting lighting, bool showStage)
    {
        Raylib.DrawRectangle(x, y, width, 16, lighting.Concrete);
        Raylib.DrawRectangle(x, y, width, 2, lighting.Rail);
        Raylib.DrawRectangle(x, y + 14, width, 2, lighting.Trim);
        for (int seatX = x + 6; seatX < x + width - 6; seatX += 7)
        {
            int seed = Hash(seatX, y, 17);
            if ((seatX - x) % 56 < 7) continue; // Cross aisles.
            DrawSpectator(seatX, y + 7, seed, Tint((seed & 2) == 0 ? primary : secondary, 0.8f), 0.2f, 0f, 0f);
        }

        string label = showStage ? lighting.Label : "RETRO QB  /  GAMEDAY";
        int labelWidth = Raylib.MeasureText(label, 10);
        int center = x + width / 2;
        Raylib.DrawRectangle(center - labelWidth / 2 - 9, y + 1, labelWidth + 18, 13, new Color(13, 18, 25, 255));
        Raylib.DrawText(label, center - labelWidth / 2, y + 3, 10, lighting.Trim);
        // Fixed LED points, with no flashing near the ball.
        for (int ledX = x + 4; ledX < x + width - 3; ledX += 12)
            Raylib.DrawPixel(ledX, y + 14, lighting.Lamp);
    }

    private static void DrawStandStructure(int x, int width, bool leftSide, Color team, StadiumLighting lighting)
    {
        GetSeatingBounds(Constants.FieldRect, out int top, out int splitTop, out int splitBottom, out int bottom);
        int height = bottom - top;
        int upperWidth = Math.Max(8, width / 3);
        int upperX = leftSide ? x : x + width - upperWidth;
        // The outer tiers recede into shade, while the field-facing seats catch light.
        Raylib.DrawRectangle(upperX + 1, top + 2, upperWidth - 2, height - 4, new Color(5, 10, 20, 92));
        int brightX = leftSide ? x + upperWidth : x;
        Raylib.DrawRectangle(brightX, top + 2, width - upperWidth, height - 4, lighting.Wash);
        int dividerX = leftSide ? x + upperWidth : upperX;
        Raylib.DrawRectangle(dividerX - 2, top, 4, height, AdjustColor(lighting.Concrete, -10));
        Raylib.DrawRectangle(dividerX, top, 1, height, lighting.Rail);

        // Radial access stairs divide seating into four identifiable sections.
        int upperAisleY = top + (splitTop - top) / 2;
        int lowerAisleY = splitBottom + (bottom - splitBottom) / 2;
        DrawAccessAisle(x, upperAisleY, width, leftSide, lighting);
        DrawAccessAisle(x, lowerAisleY, width, leftSide, lighting);

        // Midfield concourse connects the upper-tier walkway to a recessed tunnel.
        Raylib.DrawRectangle(x, splitTop - 3, width, splitBottom - splitTop + 6, lighting.Concrete);
        Raylib.DrawRectangle(x, splitTop - 3, width, 1, lighting.Rail);
        Raylib.DrawRectangle(x, splitBottom + 2, width, 1, lighting.Rail);
        int tunnelWidth = Math.Clamp(width / 3, 10, 22);
        int tunnelX = leftSide ? x + width - tunnelWidth - 3 : x + 3;
        int tunnelY = splitTop - 1;
        int tunnelHeight = splitBottom - splitTop + 2;
        Raylib.DrawRectangle(tunnelX - 2, tunnelY - 2, tunnelWidth + 4, tunnelHeight + 4, lighting.Rail);
        Raylib.DrawRectangle(tunnelX, tunnelY, tunnelWidth, tunnelHeight, new Color(7, 10, 15, 255));
        Raylib.DrawRectangle(tunnelX + 2, tunnelY + 2, tunnelWidth - 4, 2, new Color(2, 4, 8, 255));
        Raylib.DrawRectangle(tunnelX + 2, tunnelY + tunnelHeight - 2, tunnelWidth - 4, 2, AdjustColor(lighting.Concrete, 22));
        Raylib.DrawRectangle(tunnelX + tunnelWidth / 2 - 2, tunnelY - 2, 4, 1, new Color(140, 214, 178, 255));

        // Section plates and the field-facing safety rail give the seating scale.
        DrawSectionPlate(x, top + 5, width, leftSide ? "101" : "201", lighting);
        DrawSectionPlate(x, bottom - 15, width, leftSide ? "102" : "202", lighting);
        int railX = leftSide ? x + width - 2 : x;
        Raylib.DrawRectangle(railX, top, 2, height, lighting.Rail);
        for (int y = top + 4; y < bottom; y += 16)
            Raylib.DrawRectangle(railX - 1, y, 4, 2, lighting.Trim);
        Raylib.DrawRectangle(leftSide ? x - 3 : x + width, top, 3, height, Tint(team, 0.7f));
    }

    private static void DrawAccessAisle(int x, int y, int width, bool leftSide, StadiumLighting lighting)
    {
        Raylib.DrawRectangle(x + 2, y - 4, width - 4, 9, AdjustColor(lighting.Concrete, -8));
        for (int step = 3; step < width - 3; step += 4)
        {
            int stepX = leftSide ? x + step : x + width - step;
            Raylib.DrawRectangle(stepX, y - 3, 1, 7, lighting.Rail);
        }
        Raylib.DrawLine(x + 2, y - 5, x + width - 3, y - 5, lighting.Rail);
        Raylib.DrawLine(x + 2, y + 5, x + width - 3, y + 5, lighting.Rail);
    }

    private static void DrawSectionPlate(int x, int y, int width, string label, StadiumLighting lighting)
    {
        int textWidth = Raylib.MeasureText(label, 8);
        if (textWidth + 8 > width) return;
        int labelX = x + (width - textWidth) / 2;
        Raylib.DrawRectangle(labelX - 3, y - 1, textWidth + 6, 10, new Color(16, 21, 29, 255));
        Raylib.DrawText(label, labelX, y, 8, lighting.Trim);
    }
}
