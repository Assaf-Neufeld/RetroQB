using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

public sealed class FieldRenderer
{
    private readonly StadiumBackdropRenderer _stadiumBackdrop;
    private readonly FieldSurfaceRenderer _fieldSurface;
    private readonly FieldMarkingsRenderer _fieldMarkings;
    private readonly SidelineRenderer _sidelineRenderer;

    public FieldRenderer()
        : this(new StadiumBackdropRenderer(), new FieldSurfaceRenderer(), new FieldMarkingsRenderer(), new SidelineRenderer())
    {
    }

    internal FieldRenderer(
        StadiumBackdropRenderer stadiumBackdrop,
        FieldSurfaceRenderer fieldSurface,
        FieldMarkingsRenderer fieldMarkings,
        SidelineRenderer sidelineRenderer)
    {
        _stadiumBackdrop = stadiumBackdrop;
        _fieldSurface = fieldSurface;
        _fieldMarkings = fieldMarkings;
        _sidelineRenderer = sidelineRenderer;
    }

    public void DrawField(float lineOfScrimmage, float firstDownLine)
    {
        _stadiumBackdrop.Draw();
        _fieldSurface.Draw();
        _fieldMarkings.Draw(lineOfScrimmage, firstDownLine);
        _sidelineRenderer.Draw();
        DrawBoundary();
    }

    private static void DrawBoundary()
    {
        Rectangle rect = Constants.FieldRect;
        Raylib.DrawRectangleLinesEx(rect, 2, Palette.DarkGreen);
    }
}
