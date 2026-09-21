using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

/// <summary>A broadcast matchup card shown once before each game's opening drive.</summary>
public static class PregameRenderer
{
    public static void Draw(OffensiveTeamAttributes offense, DefensiveTeamAttributes defense, SeasonStage stage, string rulesText = "FIRST TO 21 WINS")
    {
        float scale = Math.Min(1.25f, Math.Min((Raylib.GetScreenWidth() - 48) / 920f,
            (Raylib.GetScreenHeight() - 48) / 540f));
        var frame = OverlayChromeRenderer.DrawWindowCentered((int)(920 * scale), (int)(540 * scale),
            Palette.Gold, OverlayVariant.Hero, 32, 32);
        int X(float x) => frame.X + (int)(x * scale);
        int Y(float y) => frame.Y + (int)(y * scale);
        void Box(float x, float y, float w, float h, Color color) =>
            Raylib.DrawRectangle(X(x), Y(y), (int)(w * scale), (int)(h * scale), color);
        void Text(string text, float x, float y, float width, int size, Color color)
        {
            int font = Math.Max(1, (int)(size * scale));
            int available = (int)(width * scale);
            while (font > 1 && Raylib.MeasureText(text, font) > available) font--;
            Raylib.DrawText(text, X(x) + (available - Raylib.MeasureText(text, font)) / 2, Y(y), font, color);
        }
        void Team(string name, string label, float x, Color primary, Color secondary, bool faceLeft)
        {
            Box(x, 146, 352, 174, Palette.Cabinet);
            Box(x, 146, 352, 5, primary);
            Text(label, x, 164, 352, 14, Palette.Muted);
            // Pixel helmet in each team's colors, facing the center of the card.
            float cx = x + 176;
            void Pixel(float px, float py, float w, float h, Color color) =>
                Box(faceLeft ? cx - px - w : cx + px, py, w, h, color);
            Pixel(-39, 199, 62, 10, primary);
            Pixel(-49, 209, 82, 37, primary);
            Pixel(-39, 246, 49, 18, primary);
            Pixel(-14, 199, 10, 47, secondary);
            Pixel(18, 232, 24, 10, Palette.Ink);
            Pixel(31, 242, 7, 24, secondary);
            Pixel(9, 259, 29, 7, secondary);
            Text(name.ToUpperInvariant(), x + 12, 282, 328, 28, Palette.White);
        }

        Text("RETROQB  /  GAME DAY", 32, 30, 856, 17, Palette.Gold);
        Text(stage.GetDisplayName(), 32, 59, 856, 38, Palette.White);
        string stakes = stage switch
        {
            SeasonStage.RegularSeason => "THE ROAD TO THE TITLE STARTS HERE",
            SeasonStage.Playoff => "WIN AND YOU'RE SUPER BOWL BOUND",
            _ => "ONE WIN FROM THE CHAMPIONSHIP"
        };
        Text($"GAME {stage.GetStageNumber()} OF 3  /  {stakes}", 32, 109, 856, 15, Palette.Muted);
        Team(offense.Name, "YOUR TEAM", 32, offense.PrimaryColor, offense.SecondaryColor, false);
        Team(defense.Name, "OPPONENT", 536, defense.PrimaryColor, defense.SecondaryColor, true);
        Text("VS", 392, 208, 136, 44, Palette.Gold);

        var report = OpponentScoutingReport.FromTeam(defense);
        var opponent = TeamCatalog.Opponents.SingleOrDefault(t => t.Name == defense.Name);
        Box(32, 338, 856, 106, Palette.Cabinet);
        Box(32, 338, 4, 106, defense.PrimaryColor);
        if (opponent == null)
        {
            Text("OPPONENT'S TOP STRENGTH", 48, 351, 824, 13, Palette.Muted);
            Text(report.Strength, 48, 373, 824, 24, Palette.Gold);
            Text(report.Tip, 48, 411, 824, 17, Palette.White);
        }
        else
        {
            Text($"THEIR OFFENSE: {opponent.Offense.Description}", 48, 348, 824, 17, Palette.White);
            Text($"THEIR DEFENSE: {opponent.Defense.Description}", 48, 373, 824, 17, Palette.White);
            Text(RosterStars.Summary(opponent), 48, 402, 824, 15, Palette.Gold);
            Text($"CALLING: {(int)(opponent.Tendencies.PassPreference * 100)}% PASS / {(int)MathF.Round((1 - opponent.Tendencies.PassPreference) * 100)}% RUN BEFORE SITUATIONAL ADJUSTMENTS", 48, 425, 824, 11, Palette.Muted);
        }
        Text("PRESS ENTER TO TAKE THE FIELD", 32, 468, 856, 23, Palette.White);
        Text(rulesText, 32, 505, 856, 13, Palette.Muted);
    }
}
