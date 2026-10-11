using Raylib_cs;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

public static class PlaybookBrowserRenderer
{
    public static void Draw(PlaybookBrowser browser, ResolvedPlay active, bool timed = true)
    {
        var layout = HudLayout.Current;
        GameplayHud.Panel(layout.Left, "FULL PLAYBOOK");
        const string controls = "Up/Down: browse | PgUp/Dn: page\nHome/End: first/last | X: flip\nT: pass/run | ]: formation\nC: category | D: concept\nF: favorites | G: star/unstar\nEnter: select | L/Esc: close\nBackspace: reset filters";
        var lines = GameplayHud.Wrap(controls, layout.PanelWidth - 28, layout.Hint);
        int footerY = layout.ScreenHeight - 30 - (lines.Count + 2) * (layout.Hint + 4) - 24;
        var content = new HudContent(layout.Left, layout, footerY - 4);
        content.Line("ACTIVE: " + active.Name, Palette.Muted, layout.Hint);
        content.Wrapped(browser.Filters, Palette.Muted, 3, layout.Hint);
        content.Section($"{browser.Results.Count} CALLS | {(browser.Results.Count == 0 ? 0 : browser.Cursor + 1)}/{browser.Results.Count}");
        int first = browser.Cursor / PlaybookBrowser.PageSize * PlaybookBrowser.PageSize;
        for (int i = first; i < Math.Min(first + PlaybookBrowser.PageSize, browser.Results.Count); i++)
        {
            var call = browser.Results[i];
            content.Row(browser.Favorites.Contains(call.Id) ? "*" : "", call.Name, i == browser.Cursor);
        }
        content.Section("PREVIEW");
        if (browser.Preview is { } preview)
        {
            content.Wrapped(preview.Name, Palette.Gold, 2);
            content.Line(preview.Definition.Info.Formation + (preview.IsFlipped ? " | FLIPPED" : ""), Palette.Muted, layout.Hint);
            content.Line(preview.Definition.Info.CategoryLabel + " | " + preview.Personnel.DisplayLabel, Palette.Muted, layout.Hint);
            content.Wrapped(preview.Family == PlayType.Pass ? PassingGuidance.Reads(preview) : preview.Definition.Info.Description, maxLines: 2, size: layout.Hint);
            if (!layout.Compact && preview.Family == PlayType.Pass) content.Wrapped(PassingGuidance.Protection(preview), Palette.Muted, 2, layout.Hint);
        }
        else content.Wrapped("No matching calls. Reset filters or turn off Favorites.", Palette.Muted);
        var footer = new HudContent(layout.Left, layout) { Y = footerY };
        footer.Section("BROWSE CONTROLS");
        foreach (string line in lines) footer.Line(line, Palette.Muted, layout.Hint);
        footer.Wrapped(timed ? "Play clock runs while browsing." : "Enter selects the previewed call.", Palette.Orange, 2, layout.Hint);
        if (browser.Favorites.Status != "") footer.Wrapped(browser.Favorites.Status, Palette.Orange, 2, layout.Hint);
    }
}
