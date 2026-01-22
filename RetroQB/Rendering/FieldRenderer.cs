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

        Raylib.DrawRectangleRec(rect, Palette.Field);
        DrawEndZones();
        DrawYardLines();
        DrawMarkers(lineOfScrimmage, firstDownLine);
        Raylib.DrawRectangleLinesEx(rect, 2, Palette.DarkGreen);
    }

    private void DrawEndZones()
    {
        Rectangle rect = Constants.FieldRect;
        float right = rect.X + rect.Width;
        float bottom = rect.Y + rect.Height;
        float ownEndY = Constants.WorldToScreenY(Constants.EndZoneDepth);
        float oppEndY = Constants.WorldToScreenY(Constants.EndZoneDepth + 100f);

        Raylib.DrawRectangle((int)rect.X, (int)ownEndY, (int)rect.Width, (int)(bottom - ownEndY), Palette.DarkGreen);
        Raylib.DrawRectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)(oppEndY - rect.Y), Palette.DarkGreen);

        Raylib.DrawText("END ZONE", (int)rect.X + 20, (int)bottom - 28, 16, Palette.YardLine);
        Raylib.DrawText("END ZONE", (int)rect.X + 20, (int)rect.Y + 8, 16, Palette.YardLine);
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
            Raylib.DrawText(yard.ToString(), (int)rect.X + 6, (int)y - 10, 12, Palette.YardLine);
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
