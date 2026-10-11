using Raylib_cs;

namespace RetroQB.Core;

/// <summary>Shared screen-space bounds. Field and HUD reserve the same columns.</summary>
public readonly record struct HudLayout(int ScreenWidth, int ScreenHeight)
{
    public static int PanelWidthFor(int width) => Math.Clamp(240 + (width - 1000) * 80 / 440, 240, 320);
    public int PanelWidth => PanelWidthFor(ScreenWidth);
    public bool Compact => ScreenHeight < 820 || PanelWidth < 280;
    public int Body => Compact ? 14 : 16;
    public int Hint => Compact ? 12 : 14;
    public int RowHeight => Compact ? 18 : 22;
    public Rectangle Left => new(10, 10, PanelWidth, ScreenHeight - 20);
    public Rectangle Right => new(ScreenWidth - PanelWidth - 10, 10, PanelWidth, ScreenHeight - 20);
    public static HudLayout Current => new(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
}
