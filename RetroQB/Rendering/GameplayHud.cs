using Raylib_cs;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

/// <summary>Common pixel HUD chrome and measured text, without per-screen font shrinking.</summary>
internal static class GameplayHud
{
    public static void Panel(Rectangle rect, string title)
    {
        Raylib.DrawRectangleRec(new(rect.X + 4, rect.Y + 4, rect.Width, rect.Height), new(0, 0, 0, 110));
        Raylib.DrawRectangleRec(rect, Palette.HudSurface);
        Raylib.DrawRectangleLinesEx(rect, 2, Palette.PanelLine);
        Text(title, (int)rect.X + 14, (int)rect.Y + 14, (int)rect.Width - 28, 22, Palette.White);
        Raylib.DrawLine((int)rect.X + 14, (int)rect.Y + 44, (int)(rect.X + rect.Width) - 14, (int)rect.Y + 44, Palette.PanelLine);
    }

    public static string Fit(string text, int width, int font)
    {
        if (Raylib.MeasureText(text, font) <= width) return text;
        while (text.Length > 0 && Raylib.MeasureText(text + "...", font) > width) text = text[..^1];
        return text + "...";
    }

    public static void Text(string text, int x, int y, int width, int font, Color? color = null)
        => Raylib.DrawText(Fit(text, width, font), x, y, font, color ?? Palette.White);

    public static IReadOnlyList<string> Wrap(string text, int width, int font)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string next = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Raylib.MeasureText(next, font) > width) { lines.Add(line); line = word; }
                else line = next;
            }
            lines.Add(line);
        }
        return lines;
    }
}

internal sealed class HudContent(Rectangle bounds, HudLayout layout, int? bottom = null)
{
    public int X { get; } = (int)bounds.X + 14;
    public int Width { get; } = (int)bounds.Width - 28;
    public int Y { get; set; } = (int)bounds.Y + 56;
    public int Bottom { get; } = bottom ?? (int)(bounds.Y + bounds.Height) - 14;
    public int Body => layout.Body;
    public int Hint => layout.Hint;
    public void Line(string text, Color? color = null, int? size = null)
    {
        int font = size ?? Body;
        if (Y + font > Bottom) return;
        GameplayHud.Text(text, X, Y, Width, font, color);
        Y += font + 4;
    }
    public void Wrapped(string text, Color? color = null, int maxLines = 3, int? size = null)
    {
        int font = size ?? Body;
        var lines = GameplayHud.Wrap(text, Width, font);
        for (int i = 0; i < Math.Min(lines.Count, maxLines); i++)
            Line(i == maxLines - 1 && lines.Count > maxLines ? lines[i] + "..." : lines[i], color, font);
    }
    public void Section(string title)
    {
        Y += 6;
        if (Y + Hint + 6 > Bottom) return;
        Raylib.DrawLine(X, Y, X + Width, Y, Palette.PanelLine); Y += 6;
        Line(title, Palette.Muted, Hint);
    }
    public void Row(string key, string text, bool selected, bool enabled = true)
    {
        if (Y + layout.RowHeight > Bottom) return;
        if (selected)
        {
            Raylib.DrawRectangle(X - 4, Y - 1, Width + 8, layout.RowHeight, Palette.HudSelection);
            Raylib.DrawRectangle(X - 4, Y - 1, 2, layout.RowHeight, Palette.Gold);
        }
        GameplayHud.Text(key, X + 2, Y + 1, 20, Hint, selected ? Palette.Gold : Palette.Muted);
        GameplayHud.Text(text, X + 26, Y + 1, Width - 26, Body, !enabled ? Palette.Muted : selected ? Palette.Gold : Palette.White);
        Y += layout.RowHeight;
    }
}

internal readonly record struct PreparationTimers(bool PlayClock, string? CpuLabel, double CpuSeconds);

internal static class MatchHudPresentation
{
    public static bool Selecting(FullMatchSession session) => session.Replay.CurrentFrame == null
        && session.Clock.Phase == ClockPhase.PreSnap && !session.Drive.Live && session.Drive.LastResult == null;

    public static PreparationTimers Timers(FullMatchSession session)
    {
        if (!Selecting(session)) return default;
        if (session.SpecialTeams is { IsKickoff: true })
            return new(false, session.UserReceivingKick ? "CPU KICK" : null, Math.Max(0, session.SnapRemaining));
        return new(true, session.HumanOnDefense ? "CPU SNAP" : null, Math.Max(0, session.SnapRemaining));
    }

    public static string Status(FullMatchSession session)
    {
        if (session.Replay.CurrentFrame != null) return "REPLAY | RECORDED";
        if ((session.Clock.Suspension & ClockSuspension.FocusLoss) != 0) return "PAUSED | WINDOW INACTIVE";
        if (session.Clock.Suspension != ClockSuspension.None) return "PAUSED";
        if (session.Status != "") return session.Status;
        if (session.Drive.LastResult != null) return "PLAY RESULT";
        if (session.SpecialTeams is { } special) return (special.IsKickoff ? "KICKOFF | " : "PUNT | ") + special.Phase.ToString().ToUpperInvariant();
        if (session.Kick is { } kick) return "FIELD GOAL | " + kick.Phase.ToString().ToUpperInvariant();
        return session.Drive.Live ? "LIVE" : session.HumanOnDefense ? "DEFENSIVE SETUP" : "OFFENSIVE SETUP";
    }
}
