using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

internal sealed class SidelineRenderer
{
    public void Draw(Color homeColor, Color awayColor, float lineOfScrimmage, float firstDownLine, int down)
    {
        Rectangle rect = Constants.FieldRect;
        int left = (int)rect.X;
        int right = (int)(rect.X + rect.Width);
        int top = (int)rect.Y;
        int bottom = (int)(rect.Y + rect.Height);

        // Sideline area width
        int sidelineWidth = Constants.SidelineApronWidth;

        Color sidelineArea = new Color(200, 200, 200, 255);
        Color sideline = new Color(255, 255, 255, 255);
        Color sidelineShadow = new Color(40, 40, 50, 140);

        // Draw sideline areas (the strip between field and bleachers)
        Raylib.DrawRectangle(left - sidelineWidth, top - sidelineWidth, (right - left) + sidelineWidth * 2, sidelineWidth, sidelineArea);
        Raylib.DrawRectangle(left - sidelineWidth, bottom, (right - left) + sidelineWidth * 2, sidelineWidth, sidelineArea);
        Raylib.DrawRectangle(left - sidelineWidth, top, sidelineWidth, bottom - top, sidelineArea);
        Raylib.DrawRectangle(right, top, sidelineWidth, bottom - top, sidelineArea);

        // Draw white boundary lines
        Raylib.DrawLine(left, top - 2, right, top - 2, sidelineShadow);
        Raylib.DrawLine(left, bottom + 2, right, bottom + 2, sidelineShadow);
        Raylib.DrawLine(left - 2, top, left - 2, bottom, sidelineShadow);
        Raylib.DrawLine(right + 2, top, right + 2, bottom, sidelineShadow);
        Raylib.DrawRectangle(left, top - 3, right - left, 3, sideline);
        Raylib.DrawRectangle(left, bottom, right - left, 3, sideline);
        Raylib.DrawRectangle(left - 3, top, 3, bottom - top, sideline);
        Raylib.DrawRectangle(right, top, 3, bottom - top, sideline);

        // Draw dashed outer boundary
        int dashLen = 12;
        int gap = 6;
        for (int x = left - sidelineWidth + 6; x < right + sidelineWidth - dashLen; x += dashLen + gap)
        {
            Raylib.DrawLine(x, top - sidelineWidth + 2, x + dashLen, top - sidelineWidth + 2, sideline);
            Raylib.DrawLine(x, bottom + sidelineWidth - 2, x + dashLen, bottom + sidelineWidth - 2, sideline);
        }

        for (int y = top + 6; y < bottom - dashLen; y += dashLen + gap)
        {
            Raylib.DrawLine(left - sidelineWidth + 2, y, left - sidelineWidth + 2, y + dashLen, sideline);
            Raylib.DrawLine(right + sidelineWidth - 2, y, right + sidelineWidth - 2, y + dashLen, sideline);
        }

        // Clip props to the apron so small windows never put staff on the field.
        DrawTeamArea(left - sidelineWidth, top, sidelineWidth, bottom - top, homeColor, true);
        DrawTeamArea(right, top, sidelineWidth, bottom - top, awayColor, false);
        DrawChainCrew(right, top, sidelineWidth, bottom - top, lineOfScrimmage, firstDownLine, down);
        DrawPylons(left, right);
    }

    private static void DrawTeamArea(int x, int y, int width, int height, Color team, bool leftSide)
    {
        Raylib.BeginScissorMode(x + 3, y, Math.Max(1, width - 6), height);
        int centerX = x + width / 2;
        int benchX = centerX + (width >= 18 ? (leftSide ? -3 : 3) : 0);
        int benchY = y + height * 3 / 8;
        int benchHeight = height / 4;
        Color ink = new(42, 47, 53, 255);
        // Segmented bench with feet and a lit seat edge.
        for (int offset = 0; offset < benchHeight - 18; offset += 26)
        {
            int by = benchY + offset;
            Raylib.DrawRectangle(benchX - 3, by + 2, 6, 19, ink);
            Raylib.DrawRectangle(benchX - 2, by, 4, 18, new Color(124, 136, 143, 255));
            Raylib.DrawRectangle(benchX - 2, by, 1, 18, new Color(192, 199, 198, 255));
        }

        for (int i = 0; i < 7; i++)
        {
            int py = benchY + 7 + i * Math.Max(12, benchHeight / 7);
            int playerX = width >= 18 ? centerX + (leftSide ? 3 : -3) : centerX;
            DrawStaff(playerX, py, team, true, i);
        }
        DrawStaff(centerX, benchY - 18, new Color(237, 232, 213, 255), false, 1);
        // Orange cooler with white lid, tap, and a dark stand.
        int coolerY = benchY + benchHeight + 10;
        Raylib.DrawRectangle(centerX - 3, coolerY + 6, 6, 3, ink);
        Raylib.DrawRectangle(centerX - 3, coolerY, 6, 6, new Color(225, 104, 34, 255));
        Raylib.DrawRectangle(centerX - 3, coolerY, 6, 2, new Color(247, 240, 219, 255));
        Raylib.DrawPixel(centerX + 2, coolerY + 4, Palette.White);

        // Camera operator away from the team bench.
        int cameraY = y + height / 5;
        DrawStaff(centerX, cameraY, new Color(64, 77, 84, 255), false, 2);
        Raylib.DrawRectangle(centerX - 3, cameraY - 4, 6, 3, ink);
        Raylib.DrawPixel(centerX + (leftSide ? 3 : -4), cameraY - 3, new Color(94, 162, 175, 255));
        Raylib.EndScissorMode();
    }

    private static void DrawStaff(int x, int y, Color shirt, bool helmet, int variant)
    {
        Color ink = new(39, 43, 48, 255);
        Raylib.DrawRectangle(x - 2, y + 3, 2, 3, ink);
        Raylib.DrawRectangle(x + 1, y + 3, 2, 3, ink);
        Raylib.DrawRectangle(x - 3, y - 1, 6, 5, shirt);
        Color head = helmet ? shirt : new Color(183, 128, 88, 255);
        Raylib.DrawRectangle(x - 2, y - 4, 4, 3, head);
        Raylib.DrawRectangle(x - 1, y - 4, 2, 1, helmet ? Palette.White : ink);
        if (helmet) Raylib.DrawPixel(x + (variant % 2), y + 1, Palette.White);
    }

    private static void DrawChainCrew(int x, int y, int width, int height, float lineOfScrimmage, float firstDownLine, int down)
    {
        Raylib.BeginScissorMode(x + 3, y, Math.Max(1, width - 6), height);
        int markerX = x + width / 2;
        int markerY = (int)Constants.WorldToScreenY(lineOfScrimmage);
        // Ten-yard chain stays at the series target through gains and losses.
        // At goal-to-go the goal line is the target; park the chain off the apron.
        if (firstDownLine < Constants.EndZoneDepth + 100f - 0.01f)
        {
            int chainX = x + width - 5;
            int targetY = (int)Constants.WorldToScreenY(firstDownLine);
            int startY = (int)Constants.WorldToScreenY(firstDownLine - 10f);
            for (int linkY = targetY; linkY <= startY; linkY += 3)
                Raylib.DrawPixel(chainX, linkY, new Color(88, 80, 65, 255));
            foreach (int poleY in new[] { targetY, startY })
            {
                Raylib.DrawRectangle(chainX - 1, poleY - 5, 2, 11, new Color(42, 44, 47, 255));
                Raylib.DrawRectangle(chainX - 2, poleY - 7, 4, 4, new Color(244, 119, 29, 255));
                DrawStaff(markerX - 1, poleY + 11, new Color(231, 197, 91, 255), false, 0);
            }
        }
        Raylib.DrawRectangle(markerX - 1, markerY - 5, 2, 12, new Color(42, 44, 47, 255));
        Raylib.DrawRectangle(markerX - 4, markerY - 9, 8, 10, new Color(244, 119, 29, 255));
        string label = Math.Clamp(down, 1, 4).ToString();
        Raylib.DrawText(label, markerX - Raylib.MeasureText(label, 8) / 2, markerY - 8, 8, new Color(24, 26, 28, 255));
        Raylib.EndScissorMode();
    }

    private static void DrawPylons(int left, int right)
    {
        // Four corners per end zone, slightly outside the white boundary.
        foreach (float worldY in new[] { 0f, Constants.EndZoneDepth, Constants.EndZoneDepth + 100f, Constants.FieldLength })
        foreach (int x in new[] { left - 4, right + 3 })
        {
            int y = (int)Constants.WorldToScreenY(worldY);
            Raylib.DrawRectangle(x - 1, y + 1, 5, 3, new Color(38, 40, 34, 100));
            Raylib.DrawRectangle(x - 2, y - 5, 4, 7, new Color(181, 70, 20, 255));
            Raylib.DrawRectangle(x - 2, y - 5, 3, 5, new Color(245, 125, 35, 255));
            Raylib.DrawRectangle(x - 2, y - 5, 3, 1, new Color(255, 193, 88, 255));
        }
    }
}
