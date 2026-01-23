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
        int leftBleacherX = (int)Math.Max(margin, rect.X - bleacherWidth - 8);
        int rightBleacherX = (int)Math.Min(screenW - margin - bleacherWidth, rect.X + rect.Width + 8);

        Color bleacherBase = new Color(45, 45, 55, 255);
        Color bleacherEdge = new Color(70, 70, 85, 255);
        Color crowd1 = new Color(210, 210, 220, 255);
        Color crowd2 = new Color(240, 200, 90, 255);
        Color crowd3 = new Color(120, 200, 170, 255);

        DrawBleachersColumn(rect, leftBleacherX, bleacherWidth, bleacherBase, bleacherEdge, crowd1, crowd2, crowd3);
        DrawBleachersColumn(rect, rightBleacherX, bleacherWidth, bleacherBase, bleacherEdge, crowd1, crowd2, crowd3);
    }

    private static void DrawBleachersColumn(Rectangle rect, int x, int width, Color baseColor, Color edgeColor, Color c1, Color c2, Color c3)
    {
        int topLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 90f);
        int bottomLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 10f);
        int height = Math.Max(0, bottomLimit - topLimit);

        Raylib.DrawRectangle(x, topLimit, width, height, baseColor);
        Raylib.DrawRectangleLines(x, topLimit, width, height, edgeColor);

        int crowdRows = 3;
        int rowWidth = Math.Max(4, width / (crowdRows + 1));
        int dotSize = Math.Max(2, rowWidth - 3);
        int spacing = dotSize + 4;

        for (int r = 0; r < crowdRows; r++)
        {
            int colX = x + 4 + r * rowWidth;
            int startY = topLimit + 4 + (r % 2 == 0 ? 0 : spacing / 2);
            for (int y = startY; y < topLimit + height - 4; y += spacing)
            {
                Color crowdColor = ((y / spacing + r) % 3) switch
                {
                    0 => c1,
                    1 => c2,
                    _ => c3
                };
                Raylib.DrawRectangle(colX, y, dotSize, dotSize, crowdColor);
            }
        }
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
