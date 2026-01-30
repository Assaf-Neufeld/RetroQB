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

        Color sideline = new Color(235, 235, 235, 255);
        Color sidelineShadow = new Color(40, 40, 50, 140);

        Raylib.DrawLine(left - 2, top, left - 2, bottom, sidelineShadow);
        Raylib.DrawLine(right + 2, top, right + 2, bottom, sidelineShadow);
        Raylib.DrawLine(left, top, left, bottom, sideline);
        Raylib.DrawLine(right, top, right, bottom, sideline);

        int dashLen = 12;
        int gap = 6;
        for (int y = top + 6; y < bottom - dashLen; y += dashLen + gap)
        {
            Raylib.DrawLine(left - 5, y, left - 5, y + dashLen, sideline);
            Raylib.DrawLine(right + 5, y, right + 5, y + dashLen, sideline);
        }
    }
}
