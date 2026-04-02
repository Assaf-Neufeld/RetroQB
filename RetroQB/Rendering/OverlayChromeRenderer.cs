using Raylib_cs;

namespace RetroQB.Rendering;

public enum OverlayVariant
{
    Hero,
    Modal,
    Toast
}

public readonly record struct OverlayFrame(
    int X,
    int Y,
    int Width,
    int Height);

public static class OverlayChromeRenderer
{
    private readonly record struct VariantSpec(
        int ScrimAlpha,
        int ShadowPad,
        int GlowPad,
        int ShellInset,
        int InnerInset,
        int AccentStripHeight);

    private static VariantSpec GetSpec(OverlayVariant variant) => variant switch
    {
        OverlayVariant.Hero  => new(ScrimAlpha: 156, ShadowPad: 12, GlowPad: 5, ShellInset: 6, InnerInset: 9, AccentStripHeight: 8),
        OverlayVariant.Modal => new(ScrimAlpha: 136, ShadowPad: 9,  GlowPad: 4, ShellInset: 5, InnerInset: 8, AccentStripHeight: 6),
        _                    => new(ScrimAlpha: 112, ShadowPad: 7,  GlowPad: 3, ShellInset: 4, InnerInset: 6, AccentStripHeight: 5),
    };

    public static OverlayFrame DrawWindowCentered(
        int preferredWidth,
        int preferredHeight,
        Color accent,
        OverlayVariant variant,
        int horizontalMargin,
        int verticalMargin,
        bool drawScrim = true)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int width = Math.Min(preferredWidth, Math.Max(260, screenW - horizontalMargin));
        int height = Math.Min(preferredHeight, Math.Max(180, screenH - verticalMargin));
        int x = (screenW - width) / 2;
        int y = (screenH - height) / 2;

        var spec = GetSpec(variant);

        if (drawScrim)
        {
            Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(4, 8, 14, spec.ScrimAlpha));
        }

        Raylib.DrawRectangle(x + spec.ShadowPad, y + spec.ShadowPad, width, height, new Color(0, 0, 0, 92));
        Raylib.DrawRectangle(x - spec.GlowPad, y - spec.GlowPad, width + (spec.GlowPad * 2), height + (spec.GlowPad * 2), new Color((int)accent.R, (int)accent.G, (int)accent.B, 74));
        Raylib.DrawRectangle(x, y, width, height, new Color(10, 14, 22, 246));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, width, height), 2f, accent);
        Raylib.DrawRectangle(x + spec.ShellInset, y + spec.ShellInset, width - (spec.ShellInset * 2), spec.AccentStripHeight, new Color((int)accent.R, (int)accent.G, (int)accent.B, 132));

        int innerX = x + spec.InnerInset;
        int innerY = y + spec.InnerInset;
        int innerWidth = width - (spec.InnerInset * 2);
        int innerHeight = height - (spec.InnerInset * 2);

        Raylib.DrawRectangle(innerX, innerY, innerWidth, innerHeight, new Color(18, 24, 36, 246));
        Raylib.DrawRectangleLines(innerX, innerY, innerWidth, innerHeight, new Color(72, 92, 116, 168));

        return new OverlayFrame(x, y, width, height);
    }

    public static void DrawPanelSurface(int x, int y, int width, int height, Color accent, Color fill)
    {
        Raylib.DrawRectangle(x, y, width, height, fill);
        Raylib.DrawRectangle(x, y, width, 3, new Color((int)accent.R, (int)accent.G, (int)accent.B, 120));
        Raylib.DrawRectangleLines(x, y, width, height, new Color((int)accent.R, (int)accent.G, (int)accent.B, 165));
    }
}
