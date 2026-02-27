using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

internal sealed class StadiumBackdropRenderer
{
    public void Draw()
    {
        Rectangle rect = Constants.FieldRect;
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int margin = 6;
        int bleacherWidth = Math.Max(24, (int)(rect.Width * 0.14f));
        int sidelineBuffer = Math.Max(16, (int)(rect.Width * 0.08f));
        int leftBleacherX = (int)Math.Max(margin, rect.X - bleacherWidth - sidelineBuffer);
        int rightBleacherX = (int)Math.Min(screenW - margin - bleacherWidth, rect.X + rect.Width + sidelineBuffer);

        Color bleacherBase = new Color(45, 45, 55, 255);
        Color bleacherEdge = new Color(70, 70, 85, 255);

        DrawBleachersColumn(rect, leftBleacherX, bleacherWidth, isLeftSide: true, bleacherBase, bleacherEdge);
        DrawBleachersColumn(rect, rightBleacherX, bleacherWidth, isLeftSide: false, bleacherBase, bleacherEdge);

        DrawStadiumLights(leftBleacherX, rightBleacherX, bleacherWidth);
    }

    private static void DrawBleachersColumn(Rectangle rect, int x, int width, bool isLeftSide, Color baseColor, Color edgeColor)
    {
        int topLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 90f);
        int bottomLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 10f);
        int height = Math.Max(0, bottomLimit - topLimit);

        if (height <= 0)
        {
            return;
        }

        int midfieldY = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 50f);
        int splitY = Math.Clamp(midfieldY, topLimit + 1, bottomLimit - 1);

        int concourseHeight = Math.Clamp(height / 14, 8, 14);
        int splitTop = Math.Clamp(splitY - concourseHeight / 2, topLimit + 1, bottomLimit - 2);
        int splitBottom = Math.Clamp(splitTop + concourseHeight, splitTop + 1, bottomLimit - 1);

        int upperY = topLimit;
        int upperHeight = splitTop - upperY;
        int lowerY = splitBottom;
        int lowerHeight = bottomLimit - lowerY;

        Color upperBase = AdjustColor(baseColor, 8);
        Color lowerBase = AdjustColor(baseColor, 2);
        Color concourseBase = AdjustColor(baseColor, -8);
        Color rail = AdjustColor(edgeColor, 10);
        Color tierLine = AdjustColor(edgeColor, -10);
        Color frontFace = AdjustColor(edgeColor, 8);

        Raylib.DrawRectangle(x, upperY, width, upperHeight, upperBase);
        Raylib.DrawRectangle(x, lowerY, width, lowerHeight, lowerBase);
        Raylib.DrawRectangle(x, splitTop, width, splitBottom - splitTop, concourseBase);
        Raylib.DrawRectangleLines(x, topLimit, width, height, edgeColor);
        Raylib.DrawRectangle(x, splitTop - 1, width, 2, rail);
        Raylib.DrawRectangle(x, splitBottom - 1, width, 2, rail);
        Raylib.DrawRectangle(x, topLimit - 4, width, 3, AdjustColor(edgeColor, -12));
        Raylib.DrawRectangle(x, bottomLimit, width, 3, frontFace);

        int innerEdgeX = isLeftSide ? x + width - 3 : x + 1;
        Raylib.DrawRectangle(innerEdgeX, topLimit + 1, 2, height - 2, AdjustColor(edgeColor, 2));

        DrawTierLines(x, upperY, width, upperHeight, tierLine);
        DrawTierLines(x, lowerY, width, lowerHeight, tierLine);

        int accentHeight = Math.Clamp(height / 36, 1, 2);
        Raylib.DrawRectangle(x + 3, splitTop - accentHeight - 1, width - 6, accentHeight, AdjustColor(upperBase, -4));
        Raylib.DrawRectangle(x + 3, splitBottom + 1, width - 6, accentHeight, AdjustColor(lowerBase, -4));
    }

    private static void DrawTierLines(int x, int y, int width, int height, Color lineColor)
    {
        if (height < 10 || width < 8)
        {
            return;
        }

        int rows = Math.Clamp(height / 18, 2, 5);
        for (int i = 1; i < rows; i++)
        {
            int rowY = y + (height * i) / rows;
            Raylib.DrawLine(x + 2, rowY, x + width - 3, rowY, lineColor);
        }
    }

    private static void DrawStadiumLights(int leftBleacherX, int rightBleacherX, int bleacherWidth)
    {
        int bleacherTop = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 90f);
        int bleacherBottom = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 10f);
        int leftX = leftBleacherX + (bleacherWidth / 2);
        int rightX = rightBleacherX + (bleacherWidth / 2);

        Color pole = new Color(60, 60, 75, 255);
        Color light = new Color(255, 244, 210, 230);
        Color glow = new Color(255, 244, 210, 120);

        DrawLightTower(leftX, bleacherTop, facesLeft: true, isBottomMount: false, pole, light, glow);
        DrawLightTower(rightX, bleacherTop, facesLeft: false, isBottomMount: false, pole, light, glow);
        DrawLightTower(leftX, bleacherBottom, facesLeft: true, isBottomMount: true, pole, light, glow);
        DrawLightTower(rightX, bleacherBottom, facesLeft: false, isBottomMount: true, pole, light, glow);
    }

    private static void DrawLightTower(int x, int mountY, bool facesLeft, bool isBottomMount, Color pole, Color light, Color glow)
    {
        int poleWidth = 5;
        int poleHeight = isBottomMount ? 16 : 20;
        int poleTopY = mountY - poleHeight;
        int headY = poleTopY - 6;
        int armLength = 10;
        int armDir = facesLeft ? -1 : 1;
        int headX = x + (armLength * armDir);
        int armX = Math.Min(x, headX);
        int armWidth = Math.Abs(headX - x);
        int lightBarWidth = 30;
        int lightBarHeight = 8;

        Raylib.DrawRectangle(x - (poleWidth / 2), poleTopY, poleWidth, poleHeight, pole);
        Raylib.DrawRectangle(armX, poleTopY + 1, armWidth, 3, AdjustColor(pole, 6));

        Raylib.DrawRectangle(headX - 2, poleTopY - 4, 4, 5, AdjustColor(pole, 8));
        Raylib.DrawRectangle(headX - (lightBarWidth / 2), headY, lightBarWidth, lightBarHeight, pole);
        Raylib.DrawRectangleLines(headX - (lightBarWidth / 2), headY, lightBarWidth, lightBarHeight, AdjustColor(pole, 15));

        for (int i = -10; i <= 10; i += 5)
        {
            int lightX = headX + i;
            int lightY = headY + 2;
            Raylib.DrawRectangle(lightX - 2, lightY, 4, 4, light);
            Raylib.DrawRectangle(lightX - 3, lightY - 1, 6, 6, glow);
        }

        Raylib.DrawRectangle(x - 4, mountY - 1, 8, 3, AdjustColor(pole, 10));
    }

    private static Color AdjustColor(Color color, int delta)
    {
        int r = Math.Clamp(color.R + delta, 0, 255);
        int g = Math.Clamp(color.G + delta, 0, 255);
        int b = Math.Clamp(color.B + delta, 0, 255);
        return new Color((byte)r, (byte)g, (byte)b, color.A);
    }
}
