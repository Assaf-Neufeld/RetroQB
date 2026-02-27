using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

internal sealed class SidelineRenderer
{
    public void Draw()
    {
        Rectangle rect = Constants.FieldRect;
        int left = (int)rect.X;
        int right = (int)(rect.X + rect.Width);
        int top = (int)rect.Y;
        int bottom = (int)(rect.Y + rect.Height);

        // Sideline area width
        int sidelineWidth = Math.Max(8, (int)(rect.Width * 0.04f));

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

        // Draw team bench areas (simple rectangles on sidelines)
        int benchWidth = Math.Max(6, sidelineWidth - 4);
        int benchLength = Math.Max(40, (bottom - top) / 4);
        int benchY = top + (bottom - top) / 2 - benchLength / 2;
        Color benchColor = new Color(80, 80, 90, 200);

        Raylib.DrawRectangle(left - sidelineWidth + 2, benchY, benchWidth, benchLength, benchColor);
        Raylib.DrawRectangle(right + 2, benchY, benchWidth, benchLength, benchColor);
    }
}
