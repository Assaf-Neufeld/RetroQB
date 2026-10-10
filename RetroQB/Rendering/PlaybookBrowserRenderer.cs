using Raylib_cs;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

public static class PlaybookBrowserRenderer
{
    public static void Draw(PlaybookBrowser browser, ResolvedPlay active, bool timed = true)
    {
        int width = Math.Max(280, (int)Constants.FieldRect.X - 8), height = Raylib.GetScreenHeight();
        float scale = Math.Min(1, height / 900f);
        Raylib.DrawRectangle(0, 0, width, height, new Color(12, 20, 30, 255));
        void Text(string text, int y, int size = 15, Color? color = null)
        {
            int font = Math.Max(11, (int)(size * scale));
            while (font > 11 && text.Split('\n').Any(line => Raylib.MeasureText(line, font) > width - 28)) font--;
            text = string.Join('\n', text.Split('\n').Select(line => {
                while (line.Length > 0 && Raylib.MeasureText(line, font) > width - 28) line = line[..^1];
                return line;
            }));
            Raylib.DrawText(text, 14, (int)(y * scale), font, color ?? Palette.White);
        }
        Text("FULL PLAYBOOK", 22, 23, Palette.Gold);
        Text("ACTIVE: " + active.Definition.Info.Formation + "\n" + active.Name, 58, 14, Palette.Cyan);
        Text(browser.Filters, 92, 14);
        Text($"{browser.Results.Count} calls | {(browser.Results.Count == 0 ? 0 : browser.Cursor + 1)}/{browser.Results.Count}", 163, 13, Palette.Gold);
        int first = browser.Cursor / PlaybookBrowser.PageSize * PlaybookBrowser.PageSize;
        for (int i = first; i < Math.Min(first + PlaybookBrowser.PageSize, browser.Results.Count); i++)
        {
            var call = browser.Results[i];
            string prefix = (i == browser.Cursor ? "> " : "  ") + (browser.Favorites.Contains(call.Id) ? "* " : "");
            Text(prefix + call.Name, 195 + (i - first) * 23, 14, i == browser.Cursor ? Palette.Gold : Palette.White);
        }
        if (browser.Preview is { } preview)
        {
            Text("PREVIEW: " + preview.Definition.Info.Formation, 482, 14, Palette.Cyan);
            Text(preview.Definition.Info.CategoryLabel + (preview.IsFlipped ? " | flipped" : ""), 508, 14);
            Text(preview.Id, 533, 12);
            Text(preview.Family == PlayType.Pass ? PassingGuidance.Reads(preview) : "Follow the highlighted run and blocks.", 560, 13, Palette.Gold);
            Text(preview.Family == PlayType.Pass ? PassingGuidance.Protection(preview) : preview.Definition.Info.Description, 591, 12);
        }
        else Text("No matching calls. Cycle a filter\nor turn off Favorites to see more.", 482, 14, Palette.Gold);
        Text("Up/Down: browse | PgUp/PgDn: page\nHome/End: first/last | X: flip preview\nT: pass/run | ]: formation\nC: category | D: concept\nF: favorites view | G: star/unstar\nEnter: select | L / Esc: close", 640, 14);
        Text("Backspace: reset all filters\n" + (timed ? "Play clock continues while browsing." : "Enter selects the previewed call."), 790, 13, Palette.Gold);
        if (browser.Favorites.Status != "") Text(browser.Favorites.Status, 829, 12);
    }
}
