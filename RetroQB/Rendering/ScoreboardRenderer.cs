using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

/// <summary>
/// Renders the stats scoreboard panel on the right side of the screen.
/// </summary>
public sealed class ScoreboardRenderer
{
    private readonly record struct SummaryLine(string Text, int FontSize, Color Color, int Indent);
    private readonly record struct SummaryBlock(IReadOnlyList<SummaryLine> Lines, int Height, int PlayNumber);

    private static int ScoreboardY => (int)Constants.OuterMargin;
    private static int ScoreboardWidth => (int)Constants.ScoreboardPanelWidth;
    private static int ScoreboardHeight => Raylib.GetScreenHeight() - (int)(Constants.OuterMargin * 2);

    public void Draw(PlayManager play, string resultText, GameState state, OffensiveTeamAttributes offensiveTeam, DefensiveTeamAttributes defensiveTeam, GameStatsSnapshot stats, SeasonStage stage, int driveSummaryScrollOffsetFromLatest)
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

        // Stage indicator
        string stageText = stage.GetDisplayName();
        int stageNum = stage.GetStageNumber();
        Color stageColor = stage switch
        {
            SeasonStage.RegularSeason => Palette.Lime,
            SeasonStage.Playoff => Palette.Yellow,
            SeasonStage.SuperBowl => Palette.Gold,
            _ => Palette.White
        };
        Raylib.DrawRectangle(contentX - 4, contentY - 4, ScoreboardWidth - 24, 26, new Color(18, 26, 40, 220));
        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 26, stageColor);
        string stageDisplay = $"STAGE {stageNum}/3: {stageText}";
        int stageWidth = Raylib.MeasureText(stageDisplay, 14);
        Raylib.DrawText(stageDisplay, contentX + (ScoreboardWidth - 24 - stageWidth) / 2, contentY, 14, stageColor);
        contentY += 30;

        // Score block
        Raylib.DrawRectangleLines(contentX - 4, contentY - 4, ScoreboardWidth - 24, 48, panelAccent);
        string homeLabel = string.IsNullOrWhiteSpace(offensiveTeam.Name) ? "HOME" : offensiveTeam.Name.ToUpperInvariant();
        string awayLabel = string.IsNullOrWhiteSpace(defensiveTeam.Name) ? "AWAY" : defensiveTeam.Name.ToUpperInvariant();
        Raylib.DrawText(homeLabel, contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        Raylib.DrawText($"{play.Score}", x + ScoreboardWidth - 54, contentY - 2, 22, offensiveTeam.PrimaryColor);
        Raylib.DrawText(awayLabel, contentX + 6, contentY + 24, 14, defensiveTeam.SecondaryColor);
        Raylib.DrawText($"{play.AwayScore}", x + ScoreboardWidth - 54, contentY + 20, 22, defensiveTeam.PrimaryColor);
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

        if (stats.Qb.Sacks > 0)
        {
            Raylib.DrawText("SACKS/YDS LOST", contentX + 2, contentY, 12, panelAccent);
            DrawRightText($"{stats.Qb.Sacks} / -{stats.Qb.SackYardsLost}", qbCol4Right, contentY, 12, panelText);
            contentY += 16;
        }

        // Receiving
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 6;
        Raylib.DrawRectangle(contentX - 2, contentY, innerWidth + 4, 18, new Color(18, 26, 40, 220));
        Raylib.DrawText("RECEIVING", contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        contentY += 22;

        int recCol1Right = contentX + (int)(innerWidth * 0.42f);
        int recCol2Right = contentX + (int)(innerWidth * 0.60f);
        int recCol3Right = contentX + (int)(innerWidth * 0.78f);
        int recCol4Right = contentX + innerWidth;
        DrawRightText("TGT", recCol1Right, contentY, 12, panelAccent);
        DrawRightText("REC", recCol2Right, contentY, 12, panelAccent);
        DrawRightText("YDS", recCol3Right, contentY, 12, panelAccent);
        DrawRightText("TD", recCol4Right, contentY, 12, panelAccent);
        contentY += 14;

        foreach (var receiver in stats.Receivers)
        {
            Raylib.DrawText($"{receiver.Label}", contentX + 6, contentY, 14, panelText);
            DrawRightText($"{receiver.Targets}", recCol1Right, contentY, 14, panelText);
            DrawRightText($"{receiver.Receptions}", recCol2Right, contentY, 14, panelText);
            DrawRightText($"{receiver.Yards}", recCol3Right, contentY, 14, panelText);
            DrawRightText($"{receiver.Tds}", recCol4Right, contentY, 14, panelText);
            contentY += 16;
        }

        // Running
        contentY += 4;
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 6;
        Raylib.DrawRectangle(contentX - 2, contentY, innerWidth + 4, 18, new Color(18, 26, 40, 220));
        Raylib.DrawText("RUNNING", contentX + 6, contentY + 2, 14, offensiveTeam.SecondaryColor);
        contentY += 22;
        DrawRightText("ATT", recCol1Right, contentY, 12, panelAccent);
        DrawRightText("YDS", recCol2Right, contentY, 12, panelAccent);
        DrawRightText("TD", recCol3Right, contentY, 12, panelAccent);
        contentY += 14;
        Raylib.DrawText("QB", contentX + 2, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.RushAttempts}", recCol1Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.RushYards}", recCol2Right, contentY, 14, panelText);
        DrawRightText($"{stats.Qb.RushTds}", recCol3Right, contentY, 14, panelText);
        contentY += 16;
        Raylib.DrawText("RB", contentX + 2, contentY, 14, panelText);
        DrawRightText($"{stats.Rb.Attempts}", recCol1Right, contentY, 14, panelText);
        DrawRightText($"{stats.Rb.Yards}", recCol2Right, contentY, 14, panelText);
        DrawRightText($"{stats.Rb.Tds}", recCol3Right, contentY, 14, panelText);
        contentY += 22;

        // Drive Summary Section
        Raylib.DrawLine(contentX - 2, contentY, contentX + ScoreboardWidth - 30, contentY, panelAccent);
        contentY += 8;
        Raylib.DrawText("DRIVE SUMMARY", contentX + 2, contentY, 16, offensiveTeam.SecondaryColor);
        contentY += 22;

        int summaryFooterY = ScoreboardY + ScoreboardHeight - 58;
        int summaryContentMaxHeight = Math.Max(0, summaryFooterY - contentY - 4);

        if (play.PlayRecords.Count == 0)
        {
            Raylib.DrawText("No plays yet", contentX + 6, contentY, 14, panelText);
            contentY = summaryFooterY + 18;
        }
        else
        {
            int textMaxWidth = innerWidth - 14;

            List<SummaryBlock> summaryBlocks = BuildDriveSummaryBlocks(play.PlayRecords, textMaxWidth, panelText);
            int clampedOffset = Math.Clamp(driveSummaryScrollOffsetFromLatest, 0, summaryBlocks.Count - 1);
            List<SummaryBlock> visibleBlocks = GetVisibleDriveSummaryBlocks(summaryBlocks, clampedOffset, summaryContentMaxHeight);

            if (visibleBlocks.Count == 0)
            {
                Raylib.DrawText("Summary area too small", contentX + 6, contentY, 12, new Color(165, 190, 220, 255));
                contentY = summaryFooterY + 18;
                string fullRangeScrollHint = $"Plays 1-{summaryBlocks.Count} of {summaryBlocks.Count}  Wheel/PgUp/PgDn";
                Raylib.DrawText(fullRangeScrollHint, contentX + 6, summaryFooterY, 11, new Color(165, 190, 220, 255));
                goto DrawResultTicker;
            }

            foreach (SummaryBlock block in visibleBlocks)
            {
                foreach (SummaryLine line in block.Lines)
                {
                    Raylib.DrawText(line.Text, contentX + line.Indent, contentY, line.FontSize, line.Color);
                    contentY += line.FontSize + 2;
                }
                contentY += 4;
            }

            SummaryBlock firstBlock = visibleBlocks[0];
            SummaryBlock lastBlock = visibleBlocks[^1];
            string visibleRangeScrollHint = $"Plays {firstBlock.PlayNumber}-{lastBlock.PlayNumber} of {summaryBlocks.Count}  Wheel/PgUp/PgDn";
            Raylib.DrawText(visibleRangeScrollHint, contentX + 6, summaryFooterY, 11, new Color(165, 190, 220, 255));
            contentY = summaryFooterY + 18;
        }

DrawResultTicker:
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

    private static List<SummaryBlock> BuildDriveSummaryBlocks(IReadOnlyList<PlayRecord> playRecords, int maxWidth, Color panelText)
    {
        var blocks = new List<SummaryBlock>(playRecords.Count);

        foreach (PlayRecord record in playRecords)
        {
            var lines = new List<SummaryLine>();
            lines.Add(new SummaryLine($"#{record.PlayNumber}: {record.GetSituationText()}", 14, Palette.Yellow, 4));

            foreach (string line in WrapTextToWidth(record.GetPlayCallText(), 12, maxWidth))
            {
                lines.Add(new SummaryLine(line, 12, panelText, 8));
            }

            if (record.Blitzers.Count > 0)
            {
                string blitzText = $"Blitz: {string.Join(", ", record.Blitzers)}";
                foreach (string line in WrapTextToWidth(blitzText, 12, maxWidth))
                {
                    lines.Add(new SummaryLine(line, 12, Palette.Orange, 8));
                }
            }

            Color resultColor = GetDriveSummaryResultColor(record, panelText);
            foreach (string line in WrapTextToWidth(record.GetResultText(), 12, maxWidth))
            {
                lines.Add(new SummaryLine(line, 12, resultColor, 8));
            }

            int height = 4;
            foreach (SummaryLine line in lines)
            {
                height += line.FontSize + 2;
            }
            height += 4;

            blocks.Add(new SummaryBlock(lines, height, record.PlayNumber));
        }

        return blocks;
    }

    private static List<SummaryBlock> GetVisibleDriveSummaryBlocks(IReadOnlyList<SummaryBlock> blocks, int scrollOffsetFromLatest, int maxHeight)
    {
        var visibleBlocks = new List<SummaryBlock>();
        if (blocks.Count == 0 || maxHeight <= 0)
        {
            return visibleBlocks;
        }

        int latestIndex = Math.Clamp(blocks.Count - 1 - scrollOffsetFromLatest, 0, blocks.Count - 1);
        int usedHeight = 0;

        for (int index = latestIndex; index >= 0; index--)
        {
            SummaryBlock block = blocks[index];
            if (visibleBlocks.Count > 0 && usedHeight + block.Height > maxHeight)
            {
                break;
            }

            visibleBlocks.Insert(0, block);
            usedHeight += block.Height;

            if (usedHeight >= maxHeight)
            {
                break;
            }
        }

        return visibleBlocks;
    }

    private static Color GetDriveSummaryResultColor(PlayRecord record, Color panelText)
    {
        return record.Outcome switch
        {
            PlayOutcome.Touchdown => Palette.Gold,
            PlayOutcome.Interception => Palette.Red,
            PlayOutcome.Incomplete => Palette.Orange,
            PlayOutcome.Turnover => Palette.Red,
            _ when record.Gain >= 10 => Palette.Lime,
            _ when record.Gain < 0 => Palette.Orange,
            _ => panelText
        };
    }

    private static List<string> WrapTextToWidth(string text, int fontSize, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string> { string.Empty };
        }

        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string current = string.Empty;

        foreach (string word in words)
        {
            string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (Raylib.MeasureText(candidate, fontSize) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }

            if (Raylib.MeasureText(word, fontSize) > maxWidth)
            {
                string segment = string.Empty;
                foreach (char ch in word)
                {
                    string next = segment + ch;
                    if (Raylib.MeasureText(next, fontSize) <= maxWidth)
                    {
                        segment = next;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(segment))
                        {
                            lines.Add(segment);
                        }

                        segment = ch.ToString();
                    }
                }

                current = segment;
            }
            else
            {
                current = word;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        return lines;
    }
}
