using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

public sealed class ReplayOverlayRenderer
{
    private const float FlashFrequencyHz = 2.8f;

    public void DrawReplayBadge(bool isPaused)
    {
        Rectangle field = Constants.FieldRect;
        int x = (int)field.X + 16;
        int y = (int)field.Y + 12;

        float phase = MathF.Sin((float)(Raylib.GetTime() * Math.PI * 2.0 * FlashFrequencyHz));
        byte alpha = isPaused ? (byte)255 : (phase > 0f ? (byte)255 : (byte)85);
        int alphaInt = alpha;
        Color fill = new Color((byte)Palette.Gold.R, (byte)Palette.Gold.G, (byte)Palette.Gold.B, alpha);

        int badgeSize = 72;
        Raylib.DrawRectangle(x - 6, y - 4, badgeSize, badgeSize, new Color(8, 8, 10, 170));
        Raylib.DrawRectangleLinesEx(new Rectangle(x - 6, y - 4, badgeSize, badgeSize), 3, new Color(20, 20, 26, 220));

        int rSize = 62;
        Raylib.DrawText("R", x + 6, y + 2, rSize, new Color(10, 10, 12, alphaInt));
        Raylib.DrawText("R", x + 4, y, rSize, fill);

        string label = "REPLAY";
        int labelSize = 20;
        int labelX = x - 2;
        int labelY = y + badgeSize + 4;
        Raylib.DrawText(label, labelX + 1, labelY + 1, labelSize, new Color(10, 10, 14, 180));
        Raylib.DrawText(label, labelX, labelY, labelSize, Palette.White);
    }
}
