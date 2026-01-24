using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

public sealed class FieldRenderer
{
    public void DrawField(float lineOfScrimmage, float firstDownLine)
    {
        Rectangle rect = Constants.FieldRect;
        float right = rect.X + rect.Width;
        float bottom = rect.Y + rect.Height;

        DrawStadiumBackdrop();
        Raylib.DrawRectangleRec(rect, Palette.Field);
        DrawEndZones();
        DrawYardLines();
        DrawHashMarks();
        DrawMarkers(lineOfScrimmage, firstDownLine);
        DrawSidelines();
        Raylib.DrawRectangleLinesEx(rect, 2, Palette.DarkGreen);
    }

    private void DrawStadiumBackdrop()
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
        Color stepLight = AdjustColor(baseColor, 18);
        Color stepDark = AdjustColor(baseColor, -10);
        Color rail = AdjustColor(edgeColor, 20);

        Raylib.DrawRectangle(x, upperY, width, upperHeight, upperBase);
        Raylib.DrawRectangle(x, lowerY, width, lowerHeight, lowerBase);
        Raylib.DrawRectangleLines(x, topLimit, width, height, edgeColor);

        // Steps removed per request to avoid dark overlays.

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

    private static void DrawBleacherSteps(int x, int y, int width, int height, Color light, Color dark)
    {
        int stepHeight = Math.Max(3, height / 10);
        bool toggle = false;
        for (int stepY = y; stepY < y + height; stepY += stepHeight)
        {
            Raylib.DrawRectangle(x, stepY, width, stepHeight, toggle ? light : dark);
            toggle = !toggle;
        }
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
        int topY = (int)rect.Y - 24;
        int midY = (int)rect.Y - 8;
        Color pole = new Color(60, 60, 75, 255);
        Color light = new Color(255, 244, 210, 230);
        Color glow = new Color(255, 244, 210, 120);

        DrawLightTower(leftX, topY, midY, pole, light, glow);
        DrawLightTower(rightX, topY, midY, pole, light, glow);
    }

    private static void DrawLightTower(int x, int topY, int midY, Color pole, Color light, Color glow)
    {
        Raylib.DrawRectangle(x - 2, topY, 4, midY - topY + 18, pole);
        Raylib.DrawRectangle(x - 12, topY, 24, 8, pole);
        for (int i = -8; i <= 8; i += 4)
        {
            Raylib.DrawRectangle(x + i, topY - 6, 3, 3, light);
            Raylib.DrawRectangle(x + i - 1, topY - 7, 5, 5, glow);
        }
    }

    private static Color AdjustColor(Color color, int delta)
    {
        int r = Math.Clamp(color.R + delta, 0, 255);
        int g = Math.Clamp(color.G + delta, 0, 255);
        int b = Math.Clamp(color.B + delta, 0, 255);
        return new Color((byte)r, (byte)g, (byte)b, color.A);
    }

    private void DrawEndZones()
    {
        Rectangle rect = Constants.FieldRect;
        float right = rect.X + rect.Width;
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

    private void DrawYardLines()
    {
        Rectangle rect = Constants.FieldRect;
        float right = rect.X + rect.Width;
        for (int yard = 0; yard <= 100; yard += 10)
        {
            float worldY = Constants.EndZoneDepth + yard;
            float y = Constants.WorldToScreenY(worldY);
            Raylib.DrawLine((int)rect.X, (int)y, (int)right, (int)y, Palette.YardLine);
            
            // Display yard line numbers on both sides of the field
            // Convert to standard football yard line display (0-50-0)
            int displayYard = yard <= 50 ? yard : 100 - yard;
            if (displayYard > 0) // Don't show 0 at goal lines
            {
                string yardText = displayYard.ToString();
                // Left side yard marker
                Raylib.DrawText(yardText, (int)rect.X + 8, (int)y - 8, 16, Palette.White);
                // Right side yard marker  
                int textWidth = Raylib.MeasureText(yardText, 16);
                Raylib.DrawText(yardText, (int)right - textWidth - 8, (int)y - 8, 16, Palette.White);
            }
        }
    }

    private void DrawHashMarks()
    {
        Rectangle rect = Constants.FieldRect;
        float left = rect.X;
        float right = rect.X + rect.Width;
        float hashInset = rect.Width * 0.18f;
        float hashLength = rect.Width * 0.03f;

        for (int yard = 5; yard < 100; yard += 5)
        {
            if (yard % 10 == 0) continue;
            float worldY = Constants.EndZoneDepth + yard;
            float y = Constants.WorldToScreenY(worldY);

            Raylib.DrawLine((int)(left + hashInset), (int)y, (int)(left + hashInset + hashLength), (int)y, Palette.DarkGreen);
            Raylib.DrawLine((int)(right - hashInset - hashLength), (int)y, (int)(right - hashInset), (int)y, Palette.DarkGreen);
        }
    }

    private void DrawSidelines()
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

    private void DrawMarkers(float lineOfScrimmage, float firstDownLine)
    {
        Rectangle rect = Constants.FieldRect;
        float right = rect.X + rect.Width;
        float losY = Constants.WorldToScreenY(lineOfScrimmage);
        float fdY = Constants.WorldToScreenY(firstDownLine);

        Raylib.DrawLine((int)rect.X, (int)losY, (int)right, (int)losY, Palette.Yellow);
        Raylib.DrawLine((int)rect.X, (int)fdY, (int)right, (int)fdY, Palette.Orange);
    }
}
