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
        Color crowd1 = new Color(235, 235, 235, 255);
        Color crowd2 = new Color(250, 250, 250, 255);
        Color crowd3 = new Color(215, 215, 220, 255);

        DrawBleachersColumn(rect, leftBleacherX, bleacherWidth, isLeftSide: true, bleacherBase, bleacherEdge, crowd1, crowd2, crowd3);
        DrawBleachersColumn(rect, rightBleacherX, bleacherWidth, isLeftSide: false, bleacherBase, bleacherEdge, crowd1, crowd2, crowd3);

        DrawStadiumLights(leftBleacherX, rightBleacherX, bleacherWidth);
    }

    private static void DrawBleachersColumn(Rectangle rect, int x, int width, bool isLeftSide, Color baseColor, Color edgeColor, Color c1, Color c2, Color c3)
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

        int nosebleedBandWidth = Math.Min(Math.Clamp(width / 3, 7, 16), Math.Max(4, width - 8));
        int mainBandWidth = Math.Max(4, width - nosebleedBandWidth);
        int mainBandX = isLeftSide ? x + nosebleedBandWidth : x;
        int nosebleedX = isLeftSide ? x : x + mainBandWidth;

        Color upperBase = AdjustColor(baseColor, 12);
        Color lowerBase = AdjustColor(baseColor, -2);
        Color concourseBase = AdjustColor(baseColor, -16);
        Color rail = AdjustColor(edgeColor, 22);
        Color tierLine = AdjustColor(edgeColor, -4);
        Color frontFace = AdjustColor(edgeColor, 18);
        Color nosebleedUpperBase = AdjustColor(baseColor, 4);
        Color nosebleedLowerBase = AdjustColor(baseColor, -8);

        Raylib.DrawRectangle(x, upperY, width, upperHeight, upperBase);
        Raylib.DrawRectangle(x, lowerY, width, lowerHeight, lowerBase);
        Raylib.DrawRectangle(x, splitTop, width, splitBottom - splitTop, concourseBase);
        Raylib.DrawRectangleLines(x, topLimit, width, height, edgeColor);

        Raylib.DrawRectangle(nosebleedX, upperY, nosebleedBandWidth, upperHeight, nosebleedUpperBase);
        Raylib.DrawRectangle(nosebleedX, lowerY, nosebleedBandWidth, lowerHeight, nosebleedLowerBase);

        int depthDividerX = isLeftSide ? x + nosebleedBandWidth : x + mainBandWidth;
        Raylib.DrawRectangle(depthDividerX - 1, topLimit + 1, 2, height - 2, AdjustColor(edgeColor, 18));
        Raylib.DrawRectangle(x, splitTop - 1, width, 2, rail);
        Raylib.DrawRectangle(x, splitBottom - 1, width, 2, rail);
        Raylib.DrawRectangle(x, topLimit - 4, width, 3, AdjustColor(edgeColor, -8));
        Raylib.DrawRectangle(x, bottomLimit, width, 3, frontFace);

        int innerEdgeX = isLeftSide ? x + width - 3 : x + 1;
        Raylib.DrawRectangle(innerEdgeX, topLimit + 1, 2, height - 2, AdjustColor(edgeColor, 8));

        DrawTierLines(x, upperY, width, upperHeight, tierLine);
        DrawTierLines(x, lowerY, width, lowerHeight, tierLine);

        int aisleCount = 3;
        for (int i = 1; i <= aisleCount; i++)
        {
            int aisleX = x + (width * i) / (aisleCount + 1);
            Color aisle = AdjustColor(edgeColor, 10);
            Raylib.DrawLine(aisleX, topLimit + 4, aisleX, splitTop - 2, aisle);
            Raylib.DrawLine(aisleX, splitBottom + 2, aisleX, topLimit + height - 4, aisle);

            int stairW = Math.Max(1, width / 18);
            Raylib.DrawRectangle(aisleX - stairW / 2, topLimit + 6, stairW, upperHeight - 10, AdjustColor(baseColor, -10));
            Raylib.DrawRectangle(aisleX - stairW / 2, lowerY + 4, stairW, lowerHeight - 8, AdjustColor(baseColor, -12));
        }

        int upperSeatSize = Math.Max(2, width / 11);
        int lowerSeatSize = Math.Max(2, width / 10);
        int upperSeatSpacing = upperSeatSize + 4;
        int lowerSeatSpacing = lowerSeatSize + 3;
        int nosebleedSeatSize = Math.Max(1, upperSeatSize - 1);
        int nosebleedSeatSpacing = nosebleedSeatSize + 3;

        int crowdTopPad = 6;
        int crowdBottomPad = 6;
        int concourseClearance = Math.Max(6, upperSeatSize + 2);

        int upperCrowdY = upperY + crowdTopPad;
        int upperCrowdBottom = splitTop - concourseClearance;
        int upperCrowdHeight = Math.Max(0, upperCrowdBottom - upperCrowdY);
        int lowerCrowdY = lowerY + concourseClearance;
        int lowerCrowdBottom = lowerY + lowerHeight - crowdBottomPad;
        int lowerCrowdHeight = Math.Max(0, lowerCrowdBottom - lowerCrowdY);

        int innerPad = 2;
        int mainCrowdX = mainBandX + innerPad;
        int mainCrowdWidth = Math.Max(1, mainBandWidth - (innerPad * 2));
        int nosebleedCrowdX = nosebleedX + 1;
        int nosebleedCrowdWidth = Math.Max(1, nosebleedBandWidth - 2);

        DrawCrowd(mainCrowdX, upperCrowdY, mainCrowdWidth, upperCrowdHeight, c1, c2, c3, upperSeatSize, upperSeatSpacing);
        DrawCrowd(mainCrowdX, lowerCrowdY, mainCrowdWidth, lowerCrowdHeight, c1, c2, c3, lowerSeatSize, lowerSeatSpacing);
        DrawCrowd(nosebleedCrowdX, upperCrowdY, nosebleedCrowdWidth, upperCrowdHeight, c1, c2, c3, nosebleedSeatSize, nosebleedSeatSpacing);
        DrawCrowd(nosebleedCrowdX, lowerCrowdY, nosebleedCrowdWidth, lowerCrowdHeight, c1, c2, c3, nosebleedSeatSize, nosebleedSeatSpacing);

        int ribbonHeight = Math.Clamp(height / 28, 2, 4);
        Raylib.DrawRectangle(x + 2, splitTop - ribbonHeight - 1, width - 4, ribbonHeight, AdjustColor(c2, -55));
        Raylib.DrawRectangle(x + 2, splitBottom + 1, width - 4, ribbonHeight, AdjustColor(c3, -50));
    }

    private static void DrawTierLines(int x, int y, int width, int height, Color lineColor)
    {
        if (height < 10 || width < 8)
        {
            return;
        }

        int rows = Math.Clamp(height / 9, 3, 12);
        for (int i = 1; i < rows; i++)
        {
            int rowY = y + (height * i) / rows;
            Raylib.DrawLine(x + 2, rowY, x + width - 3, rowY, lineColor);
        }
    }

    private static void DrawCrowd(int x, int y, int width, int height, Color c1, Color c2, Color c3, int dotSize, int spacing)
    {
        if (height <= 0 || width <= 0)
        {
            return;
        }

        // Bleachers are on LEFT and RIGHT sides of field
        // Rows run VERTICALLY (parallel to field edge) so spectators face the field
        // Multiple rows stack HORIZONTALLY (away from the field)
        int numRows = Math.Max(1, width / spacing);  // rows stack horizontally (width)
        int seatsPerRow = Math.Max(3, height / spacing);  // seats run vertically (height)

        for (int row = 0; row < numRows; row++)
        {
            int rowX = x + row * spacing + spacing / 2;
            // Offset alternate rows vertically for staggered seating
            int rowOffset = row % 2 == 0 ? 0 : spacing / 2;

            for (int seat = 0; seat < seatsPerRow; seat++)
            {
                int seatY = y + seat * spacing + rowOffset;
                if (seatY + dotSize > y + height)
                {
                    continue; // Don't draw seats outside the bleacher area
                }

                int colorBand = (row * 17 + seat * 11) % 6;
                Color crowdColor = colorBand switch
                {
                    0 or 1 => c1,
                    2 or 3 => c2,
                    _ => c3
                };
                Raylib.DrawRectangle(rowX, seatY, dotSize, dotSize, crowdColor);
            }
        }
    }

    private static void DrawStadiumLights(int leftBleacherX, int rightBleacherX, int bleacherWidth)
    {
        int bleacherTop = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 90f);
        int leftA = leftBleacherX + (int)(bleacherWidth * 0.28f);
        int leftB = leftBleacherX + (int)(bleacherWidth * 0.72f);
        int rightA = rightBleacherX + (int)(bleacherWidth * 0.28f);
        int rightB = rightBleacherX + (int)(bleacherWidth * 0.72f);

        Color pole = new Color(60, 60, 75, 255);
        Color light = new Color(255, 244, 210, 230);
        Color glow = new Color(255, 244, 210, 120);

        DrawLightTower(leftA, bleacherTop, pole, light, glow);
        DrawLightTower(leftB, bleacherTop, pole, light, glow);
        DrawLightTower(rightA, bleacherTop, pole, light, glow);
        DrawLightTower(rightB, bleacherTop, pole, light, glow);
    }

    private static void DrawLightTower(int x, int bleacherTopY, Color pole, Color light, Color glow)
    {
        int poleWidth = 6;
        int poleHeight = 32;
        int poleTopY = bleacherTopY - poleHeight;
        int headY = poleTopY - 10;
        int lightBarWidth = 34;
        int lightBarHeight = 10;

        // Pole rises from the bleacher top edge up to the light assembly.
        Raylib.DrawRectangle(x - (poleWidth / 2), poleTopY, poleWidth, poleHeight, pole);

        // Bracket and lamp bar centered over the pole.
        Raylib.DrawRectangle(x - 2, poleTopY - 5, 4, 6, AdjustColor(pole, 8));
        Raylib.DrawRectangle(x - (lightBarWidth / 2), headY, lightBarWidth, lightBarHeight, pole);
        Raylib.DrawRectangleLines(x - (lightBarWidth / 2), headY, lightBarWidth, lightBarHeight, AdjustColor(pole, 15));

        for (int i = -12; i <= 12; i += 6)
        {
            int lightX = x + i;
            int lightY = headY + 3;
            Raylib.DrawRectangle(lightX - 2, lightY, 4, 4, light);
            Raylib.DrawRectangle(lightX - 3, lightY - 1, 6, 6, glow);
        }

        // Mounting base at bleacher top for cleaner placement.
        Raylib.DrawRectangle(x - 4, bleacherTopY - 1, 8, 3, AdjustColor(pole, 10));
    }

    private static Color AdjustColor(Color color, int delta)
    {
        int r = Math.Clamp(color.R + delta, 0, 255);
        int g = Math.Clamp(color.G + delta, 0, 255);
        int b = Math.Clamp(color.B + delta, 0, 255);
        return new Color((byte)r, (byte)g, (byte)b, color.A);
    }
}
