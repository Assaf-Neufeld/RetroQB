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

        DrawBleachersColumn(rect, leftBleacherX, bleacherWidth, bleacherBase, bleacherEdge, crowd1, crowd2, crowd3);
        DrawBleachersColumn(rect, rightBleacherX, bleacherWidth, bleacherBase, bleacherEdge, crowd1, crowd2, crowd3);

        DrawStadiumLights(leftBleacherX + bleacherWidth / 2, rightBleacherX + bleacherWidth / 2, rect);
    }

    private static void DrawBleachersColumn(Rectangle rect, int x, int width, Color baseColor, Color edgeColor, Color c1, Color c2, Color c3)
    {
        int topLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 90f);
        int bottomLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 10f);
        int height = Math.Max(0, bottomLimit - topLimit);

        if (height <= 0)
        {
            return;
        }

        int upperHeight = (int)(height * 0.38f);
        int lowerHeight = height - upperHeight;
        int upperY = topLimit;
        int lowerY = topLimit + upperHeight;

        Color upperBase = AdjustColor(baseColor, 12);
        Color lowerBase = baseColor;
        Color rail = AdjustColor(edgeColor, 20);

        Raylib.DrawRectangle(x, upperY, width, upperHeight, upperBase);
        Raylib.DrawRectangle(x, lowerY, width, lowerHeight, lowerBase);
        Raylib.DrawRectangleLines(x, topLimit, width, height, edgeColor);

        Raylib.DrawRectangle(x, lowerY - 2, width, 2, rail);
        Raylib.DrawRectangle(x, topLimit - 4, width, 3, AdjustColor(edgeColor, -8));

        int aisleCount = 2;
        for (int i = 1; i <= aisleCount; i++)
        {
            int aisleX = x + (width * i) / (aisleCount + 1);
            Raylib.DrawLine(aisleX, topLimit + 4, aisleX, topLimit + height - 4, AdjustColor(edgeColor, 10));
        }

        int seatSize = Math.Max(2, width / 10);
        int seatSpacing = seatSize + 4;

        DrawCrowd(x, upperY + 6, width, upperHeight - 12, c1, c2, c3, 3, seatSize, seatSpacing);
        DrawCrowd(x, lowerY + 4, width, lowerHeight - 8, c1, c2, c3, 4, seatSize, seatSpacing);
    }

    private static void DrawCrowd(int x, int y, int width, int height, Color c1, Color c2, Color c3, int rows, int dotSize, int spacing)
    {
        if (height <= 0 || width <= 0)
        {
            return;
        }

        int columns = Math.Max(3, width / spacing);
        int totalWidth = columns * spacing - (spacing - dotSize);
        int startX = x + Math.Max(2, (width - totalWidth) / 2);

        for (int r = 0; r < rows; r++)
        {
            int rowOffset = r % 2 == 0 ? 0 : spacing / 2;
            int rowY = y + r * spacing;
            for (int c = 0; c < columns; c++)
            {
                int seatX = startX + c * spacing + rowOffset / 2;
                for (int seatY = rowY; seatY < y + height; seatY += rows * spacing)
                {
                    Color crowdColor = ((seatY / spacing + c + r) % 3) switch
                    {
                        0 => c1,
                        1 => c2,
                        _ => c3
                    };
                    Raylib.DrawRectangle(seatX, seatY, dotSize, dotSize, crowdColor);
                }
            }
        }
    }

    private static void DrawStadiumLights(int leftX, int rightX, Rectangle rect)
    {
        int topY = Math.Max(8, (int)rect.Y - 24);
        int midY = Math.Max(16, (int)rect.Y - 8);
        Color pole = new Color(60, 60, 75, 255);
        Color light = new Color(255, 244, 210, 230);
        Color glow = new Color(255, 244, 210, 120);

        DrawLightTower(leftX, topY, midY, pole, light, glow);
        DrawLightTower(rightX, topY, midY, pole, light, glow);
    }

    private static void DrawLightTower(int x, int topY, int midY, Color pole, Color light, Color glow)
    {
        int poleHeight = Math.Max(10, midY - topY + 18);
        Raylib.DrawRectangle(x - 2, topY, 4, poleHeight, pole);
        Raylib.DrawRectangle(x - 12, topY, 24, 8, pole);
        for (int i = -8; i <= 8; i += 4)
        {
            int lightY = Math.Max(2, topY - 6);
            Raylib.DrawRectangle(x + i, lightY, 3, 3, light);
            Raylib.DrawRectangle(x + i - 1, lightY - 1, 5, 5, glow);
        }
    }

    private static Color AdjustColor(Color color, int delta)
    {
        int r = Math.Clamp(color.R + delta, 0, 255);
        int g = Math.Clamp(color.G + delta, 0, 255);
        int b = Math.Clamp(color.B + delta, 0, 255);
        return new Color((byte)r, (byte)g, (byte)b, color.A);
    }
}
