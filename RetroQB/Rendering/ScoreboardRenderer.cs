using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

/// <summary>
/// Renders the stats scoreboard panel on the right side of the screen.
/// </summary>
public sealed class ScoreboardRenderer
{
    private static int ScoreboardY => (int)Constants.OuterMargin;
    private static int ScoreboardWidth => (int)Constants.ScoreboardPanelWidth;
    private static int ScoreboardHeight => Raylib.GetScreenHeight() - (int)(Constants.OuterMargin * 2);

    public void Draw(PlayManager play, string resultText, GameState state, OffensiveTeamAttributes offensiveTeam, GameStatsSnapshot stats)
    {
        int x = Raylib.GetScreenWidth() - ScoreboardWidth - (int)Constants.OuterMargin;
        int y = ScoreboardY;

        Color panelBg = new(10, 16, 28, 235);
        Color panelBorder = new(36, 90, 150, 255);
        Color panelHeader = new(20, 28, 45, 255);
        Color panelAccent = new(30, 110, 170, 255);
        Color panelText = Palette.EndZoneText;

        // Outer shell
        Raylib.DrawRectangle(x, y, ScoreboardWidth, ScoreboardHeight, panelBg);
        Raylib.DrawRectangleLines(x, y, ScoreboardWidth, ScoreboardHeight, panelBorder);

        // Header bar
        Raylib.DrawRectangle(x + 2, y + 2, ScoreboardWidth - 4, 22, panelHeader);
        Raylib.DrawText("STATS BOARD", x + 10, y + 5, 14, offensiveTeam.PrimaryColor);

        int contentX = x + 12;
        int contentY = y + 30;
        int innerWidth = ScoreboardWidth - 24;

        void DrawRightText(string text, int rightX, int drawY, int fontSize, Color color)
        {
            int textWidth = Raylib.MeasureText(text, fontSize);
            Raylib.DrawText(text, rightX - textWidth, drawY, fontSize, color);
        }

        // Score block
        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 48, panelAccent);
        string homeLabel = string.IsNullOrWhiteSpace(offensiveTeam.Name) ? "HOME" : offensiveTeam.Name.ToUpperInvariant();
        Raylib.DrawText(homeLabel, contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        Raylib.DrawText($"{play.Score}", x + ScoreboardWidth - 54, contentY - 2, 22, offensiveTeam.PrimaryColor);
        Raylib.DrawText("AWAY", contentX + 6, contentY + 24, 14, panelText);
        Raylib.DrawText($"{play.AwayScore}", x + ScoreboardWidth - 54, contentY + 20, 22, Palette.Red);
        contentY += 54;

        // Down & Distance block
        string downOrdinal = play.Down switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            4 => "4th",
            _ => $"{play.Down}th"
        };

        float yardLine = play.GetYardLineDisplay(play.LineOfScrimmage);
        float toGoal = 100f - yardLine;
        string distanceText = toGoal <= play.Distance
            ? "Goal"
            : play.Distance < 1f
                ? "Inches"
                : $"{play.Distance:F0}";

        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 28, panelAccent);
        Raylib.DrawText($"{downOrdinal} & {distanceText}", contentX + 6, contentY, 18, Palette.Lime);
        contentY += 30;

        // Ball position block
        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 24, panelAccent);
        Raylib.DrawText($"BALL ON {yardLine:F0}", contentX + 6, contentY - 1, 14, panelText);
        contentY += 28;

        // Passing
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 6;
        Raylib.DrawRectangle(contentX - 2, contentY, innerWidth + 4, 18, new Color(18, 26, 40, 220));
        Raylib.DrawText("PASSING", contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        contentY += 22;

        int qbCol1Right = contentX + (int)(innerWidth * 0.52f);
        int qbCol2Right = contentX + (int)(innerWidth * 0.72f);
        int qbCol3Right = contentX + (int)(innerWidth * 0.86f);
        int qbCol4Right = contentX + innerWidth;
        DrawRightText("CMP/ATT", qbCol1Right, contentY, 12, panelAccent);
        DrawRightText("YDS", qbCol2Right, contentY, 12, panelAccent);
        DrawRightText("TD", qbCol3Right, contentY, 12, panelAccent);
        DrawRightText("INT", qbCol4Right, contentY, 12, panelAccent);
        contentY += 16;
        Raylib.DrawText("QB", contentX + 2, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.Completions}/{stats.Qb.Attempts}", qbCol1Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.PassYards}", qbCol2Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.PassTds}", qbCol3Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.Interceptions}", qbCol4Right, contentY, 14, panelText);
        contentY += 22;

        // Receiving
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 6;
        Raylib.DrawRectangle(contentX - 2, contentY, innerWidth + 4, 18, new Color(18, 26, 40, 220));
        Raylib.DrawText("RECEIVING", contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        contentY += 22;

        int recCol1Right = contentX + (int)(innerWidth * 0.46f);
        int recCol2Right = contentX + (int)(innerWidth * 0.72f);
        int recCol3Right = contentX + innerWidth;
        Raylib.DrawText("REC", contentX + 2, contentY, 12, panelAccent);
        DrawRightText("YDS", recCol2Right, contentY, 12, panelAccent);
        DrawRightText("TD", recCol3Right, contentY, 12, panelAccent);
        contentY += 14;

        foreach (var receiver in stats.Receivers)
        {
            Raylib.DrawText($"{receiver.Label}", contentX + 6, contentY, 14, panelText);
            DrawRightText($"{receiver.Receptions}", recCol1Right, contentY, 14, panelText);
            DrawRightText($"{receiver.Yards}", recCol2Right, contentY, 14, panelText);
            DrawRightText($"{receiver.Tds}", recCol3Right, contentY, 14, panelText);
            contentY += 16;
        }

        // Running
        contentY += 4;
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 6;
        Raylib.DrawRectangle(contentX - 2, contentY, innerWidth + 4, 18, new Color(18, 26, 40, 220));
        Raylib.DrawText("RUNNING", contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        contentY += 22;
        Raylib.DrawText("QB", contentX + 2, contentY, 14, panelText);
        DrawRightText("YDS", recCol2Right, contentY - 2, 12, panelAccent);
        DrawRightText("TD", recCol3Right, contentY - 2, 12, panelAccent);
        contentY += 16;
        DrawRightText($"{stats.Qb.RushYards}", recCol2Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.RushTds}", recCol3Right, contentY, 14, panelText);
        contentY += 16;
        Raylib.DrawText("RB", contentX + 2, contentY, 14, panelText);
        DrawRightText($"{stats.Rb.Yards}", recCol2Right, contentY, 14, panelText);
        DrawRightText($"{stats.Rb.Tds}", recCol3Right, contentY, 14, panelText);
        contentY += 22;

        // Drive Summary Section
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 8;
        Raylib.DrawText("DRIVE SUMMARY", contentX + 2, contentY, 16, offensiveTeam.SecondaryColor);
        contentY += 22;

        // Show all plays in chronological order (oldest first)
        if (play.PlayRecords.Count == 0)
        {
            Raylib.DrawText("No plays yet", contentX + 6, contentY, 14, panelText);
        }
        else
        {
            for (int i = 0; i < play.PlayRecords.Count; i++)
            {
                var record = play.PlayRecords[i];
                
                // Play number and situation
                string situationLine = $"#{record.PlayNumber}: {record.GetSituationText()}";
                Raylib.DrawText(situationLine, contentX + 4, contentY, 14, Palette.Yellow);
                contentY += 16;
                
                // Play call
                string playCallLine = record.GetPlayCallText();
                string? blitzLine = null;
                int blitzIndex = playCallLine.IndexOf(" (");
                if (blitzIndex >= 0)
                {
                    blitzLine = playCallLine.Substring(blitzIndex + 2).TrimEnd(')');
                    if (blitzLine.EndsWith(" blitz", StringComparison.OrdinalIgnoreCase))
                        blitzLine = blitzLine[..^6];
                    playCallLine = playCallLine.Substring(0, blitzIndex);
                }

                if (playCallLine.Length > 32)
                    playCallLine = playCallLine.Substring(0, 29) + "...";
                Raylib.DrawText(playCallLine, contentX + 8, contentY, 12, panelText);
                contentY += 14;

                if (!string.IsNullOrWhiteSpace(blitzLine))
                {
                    string blitzText = $"Blitz: {blitzLine}";
                    if (blitzText.Length > 32)
                        blitzText = blitzText.Substring(0, 29) + "...";
                    Raylib.DrawText(blitzText, contentX + 8, contentY, 12, Palette.Orange);
                    contentY += 14;
                }
                
                // Result
                string resultLine = record.GetResultText();
                Color resultColor = record.Outcome switch
                {
                    PlayOutcome.Touchdown => Palette.Gold,
                    PlayOutcome.Interception => Palette.Red,
                    PlayOutcome.Incomplete => Palette.Orange,
                    PlayOutcome.Turnover => Palette.Red,
                    _ when record.Gain >= 10 => Palette.Lime,
                    _ when record.Gain < 0 => Palette.Orange,
                    _ => panelText
                };
                if (resultLine.Length > 35)
                    resultLine = resultLine.Substring(0, 32) + "...";
                Raylib.DrawText(resultLine, contentX + 8, contentY, 12, resultColor);
                contentY += 18;
            }
        }

        // Last play result ticker (at the bottom)
        if (!string.IsNullOrWhiteSpace(resultText))
        {
            contentY += 4;
            Color resultTickerColor = resultText.Contains("TD") || resultText.Contains("1ST") ? Palette.Gold :
                               resultText.Contains("INT") || resultText.Contains("TURN") ? Palette.Red :
                               resultText.Contains("Incomplete") ? Palette.Orange :
                               panelText;

            Raylib.DrawRectangle(contentX - 4, contentY - 4, ScoreboardWidth - 24, 26, panelHeader);
            Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 26, panelAccent);
            Raylib.DrawText(resultText, contentX + 6, contentY - 1, 14, resultTickerColor);
        }
    }
}
