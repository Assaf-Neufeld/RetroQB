using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

internal sealed class FieldMarkingsRenderer
{
    public void Draw(float lineOfScrimmage, float firstDownLine)
    {
        DrawYardLines();
        DrawHashMarks();
        DrawMarkers(lineOfScrimmage, firstDownLine);
    }

    private static void DrawYardLines()
    {
        Rectangle rect = Constants.FieldRect;
        float right = rect.X + rect.Width;
        for (int yard = 0; yard <= 100; yard += 10)
        {
            float worldY = Constants.EndZoneDepth + yard;
            float y = Constants.WorldToScreenY(worldY);
            Raylib.DrawRectangle((int)rect.X, (int)y - 1, (int)rect.Width, 3, new Color(3, 40, 19, 85));
            Raylib.DrawLine((int)rect.X, (int)y, (int)right, (int)y, Palette.YardLine);

            int displayYard = yard <= 50 ? yard : 100 - yard;
            if (displayYard > 0)
            {
                string yardText = displayYard.ToString();
                int fontSize = 16;
                Raylib.DrawText(yardText, (int)rect.X + 9, (int)y - fontSize - 1, fontSize, new Color(2, 25, 12, 150));
                Raylib.DrawText(yardText, (int)rect.X + 8, (int)y - fontSize - 2, fontSize, Palette.White);
                int textWidth = Raylib.MeasureText(yardText, fontSize);
                Raylib.DrawText(yardText, (int)right - textWidth - 7, (int)y - fontSize - 1, fontSize, new Color(2, 25, 12, 150));
                Raylib.DrawText(yardText, (int)right - textWidth - 8, (int)y - fontSize - 2, fontSize, Palette.White);
            }
        }
    }

    private static void DrawHashMarks()
    {
        Rectangle rect = Constants.FieldRect;
        float left = rect.X;
        float right = rect.X + rect.Width;
        float hashInset = rect.Width * 0.18f;
        float hashLength = rect.Width * 0.03f;

        for (int yard = 5; yard < 100; yard += 5)
        {
            if (yard % 10 == 0)
            {
                continue;
            }

            float worldY = Constants.EndZoneDepth + yard;
            float y = Constants.WorldToScreenY(worldY);

            Raylib.DrawLine((int)(left + hashInset), (int)y, (int)(left + hashInset + hashLength), (int)y, Palette.DarkGreen);
            Raylib.DrawLine((int)(right - hashInset - hashLength), (int)y, (int)(right - hashInset), (int)y, Palette.DarkGreen);
        }
    }

    private static void DrawMarkers(float lineOfScrimmage, float firstDownLine)
    {
        Rectangle rect = Constants.FieldRect;
        float right = rect.X + rect.Width;
        float losY = Constants.WorldToScreenY(lineOfScrimmage);
        float fdY = Constants.WorldToScreenY(firstDownLine);

        DrawMarkerLine((int)rect.X, (int)right, (int)losY, Palette.Cyan, "LOS");
        DrawMarkerLine((int)rect.X, (int)right, (int)fdY, Palette.Yellow, "1ST");
    }

    private static void DrawMarkerLine(int left, int right, int y, Color color, string label)
    {
        Raylib.DrawRectangle(left, y - 2, right - left, 5, new Color(0, 0, 0, 80));
        Raylib.DrawRectangle(left, y, right - left, 2, color);

        int fontSize = 10;
        int labelWidth = Raylib.MeasureText(label, fontSize) + 8;
        int labelX = left + ((right - left - labelWidth) / 2);
        Raylib.DrawRectangle(labelX, y - 6, labelWidth, 12, new Color(5, 10, 14, 215));
        Raylib.DrawRectangleLines(labelX, y - 6, labelWidth, 12, color);
        Raylib.DrawText(label, labelX + 4, y - 5, fontSize, color);
    }
}
