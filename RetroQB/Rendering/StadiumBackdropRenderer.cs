using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Rendering;

internal sealed class StadiumBackdropRenderer
{
    private static readonly Color[] NeutralCrowdPalette =
    [
        new Color(178, 182, 190, 255),
        new Color(132, 138, 150, 255),
        new Color(94, 100, 114, 255),
        new Color(214, 210, 196, 255)
    ];

    public void Draw(Color homeTeamColor, Color awayTeamColor, CrowdBackdropState crowdState)
    {
        Rectangle rect = Constants.FieldRect;
        int screenW = Raylib.GetScreenWidth();

        int margin = 6;
        int bleacherWidth = Math.Max(24, (int)(rect.Width * 0.14f));
        int sidelineBuffer = Math.Max(16, (int)(rect.Width * 0.08f));
        int leftBleacherX = (int)Math.Max(margin, rect.X - bleacherWidth - sidelineBuffer);
        int rightBleacherX = (int)Math.Min(screenW - margin - bleacherWidth, rect.X + rect.Width + sidelineBuffer);
        int stadiumLeft = leftBleacherX;
        int stadiumRight = rightBleacherX + bleacherWidth;

        Color bleacherBase = new Color(45, 45, 55, 255);
        Color bleacherEdge = new Color(70, 70, 85, 255);
        Color homeAccent = CreateAccentColor(homeTeamColor, 0.82f, 18);
        Color awayAccent = CreateAccentColor(awayTeamColor, 0.82f, 18);
        int topLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 90f);
        int bottomLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 10f);

        DrawBleachersColumn(rect, leftBleacherX, bleacherWidth, isLeftSide: true, bleacherBase, bleacherEdge);
        DrawBleachersColumn(rect, rightBleacherX, bleacherWidth, isLeftSide: false, bleacherBase, bleacherEdge);
        DrawBleacherSideExtension(leftBleacherX, bleacherWidth, bleacherBase, bleacherEdge, homeAccent, awayAccent, isLeftSide: true);
        DrawBleacherSideExtension(rightBleacherX, bleacherWidth, bleacherBase, bleacherEdge, awayAccent, homeAccent, isLeftSide: false);
        DrawUpperDeckBands(rect, stadiumLeft, stadiumRight, bleacherEdge, homeAccent, awayAccent);
        DrawCrowdSections(rect, leftBleacherX, bleacherWidth, rightBleacherX, homeAccent, awayAccent, crowdState);
        DrawRibbonBoards(leftBleacherX, rightBleacherX, bleacherWidth, topLimit, bottomLimit, homeAccent, awayAccent);
        DrawFieldEdgeShadow(rect, leftBleacherX, rightBleacherX, bleacherWidth);
    }

    public void DrawChantOverlay(Color homeTeamColor, CrowdBackdropState crowdState)
    {
        Rectangle rect = Constants.FieldRect;
        int screenW = Raylib.GetScreenWidth();

        int margin = 6;
        int bleacherWidth = Math.Max(24, (int)(rect.Width * 0.14f));
        int sidelineBuffer = Math.Max(16, (int)(rect.Width * 0.08f));
        int leftBleacherX = (int)Math.Max(margin, rect.X - bleacherWidth - sidelineBuffer);
        int rightBleacherX = (int)Math.Min(screenW - margin - bleacherWidth, rect.X + rect.Width + sidelineBuffer);
        Color homeAccent = CreateAccentColor(homeTeamColor, 0.82f, 18);

        DrawHomeCrowdChant(leftBleacherX, rightBleacherX, bleacherWidth, homeAccent, crowdState);
    }

    private static void DrawBleachersColumn(Rectangle rect, int x, int width, bool isLeftSide, Color baseColor, Color edgeColor)
    {
        int topLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 90f);
        int bottomLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 10f);
        int height = Math.Max(0, bottomLimit - topLimit);

        if (height <= 0)
        {
            return;
        }

        int midfieldY = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 50f);
        int splitY = Math.Clamp(midfieldY, topLimit + 1, bottomLimit - 1);

        int concourseHeight = Math.Clamp(height / 14, 8, 14);
        int splitTop = Math.Clamp(splitY - concourseHeight / 2, topLimit + 1, bottomLimit - 2);
        int splitBottom = Math.Clamp(splitTop + concourseHeight, splitTop + 1, bottomLimit - 1);

        int upperY = topLimit;
        int upperHeight = splitTop - upperY;
        int lowerY = splitBottom;
        int lowerHeight = bottomLimit - lowerY;

        Color upperBase = AdjustColor(baseColor, 8);
        Color lowerBase = AdjustColor(baseColor, 2);
        Color concourseBase = AdjustColor(baseColor, -8);
        Color rail = AdjustColor(edgeColor, 10);
        Color tierLine = AdjustColor(edgeColor, -10);
        Color frontFace = AdjustColor(edgeColor, 8);

        Raylib.DrawRectangle(x, upperY, width, upperHeight, upperBase);
        Raylib.DrawRectangle(x, lowerY, width, lowerHeight, lowerBase);
        Raylib.DrawRectangle(x, splitTop, width, splitBottom - splitTop, concourseBase);
        Raylib.DrawRectangleLines(x, topLimit, width, height, edgeColor);
        Raylib.DrawRectangle(x, splitTop - 1, width, 2, rail);
        Raylib.DrawRectangle(x, splitBottom - 1, width, 2, rail);
        Raylib.DrawRectangle(x, topLimit - 4, width, 3, AdjustColor(edgeColor, -12));
        Raylib.DrawRectangle(x, bottomLimit, width, 3, frontFace);

        int innerEdgeX = isLeftSide ? x + width - 3 : x + 1;
        Raylib.DrawRectangle(innerEdgeX, topLimit + 1, 2, height - 2, AdjustColor(edgeColor, 2));

        DrawTierLines(x, upperY, width, upperHeight, tierLine);
        DrawTierLines(x, lowerY, width, lowerHeight, tierLine);

        int accentHeight = Math.Clamp(height / 36, 1, 2);
        Raylib.DrawRectangle(x + 3, splitTop - accentHeight - 1, width - 6, accentHeight, AdjustColor(upperBase, -4));
        Raylib.DrawRectangle(x + 3, splitBottom + 1, width - 6, accentHeight, AdjustColor(lowerBase, -4));
    }

    private static void DrawTierLines(int x, int y, int width, int height, Color lineColor)
    {
        if (height < 10 || width < 8)
        {
            return;
        }

        int rows = Math.Clamp(height / 18, 2, 5);
        for (int i = 1; i < rows; i++)
        {
            int rowY = y + (height * i) / rows;
            Raylib.DrawLine(x + 2, rowY, x + width - 3, rowY, lineColor);
        }
    }

    private static void DrawCrowdSections(
        Rectangle rect,
        int leftBleacherX,
        int bleacherWidth,
        int rightBleacherX,
        Color homeAccent,
        Color awayAccent,
        CrowdBackdropState crowdState)
    {
        GetSeatingBounds(rect, out int topLimit, out int splitTop, out int splitBottom, out int bottomLimit);

        int upperHeight = splitTop - topLimit;
        int lowerHeight = bottomLimit - splitBottom;

        DrawCrowdBlock(leftBleacherX + 3, topLimit + 6, bleacherWidth - 6, upperHeight - 10, homeAccent, awayAccent, CrowdMix.HomeHeavy, alignFromBottom: true, crowdState);
        DrawCrowdBlock(leftBleacherX + 3, splitBottom + 4, bleacherWidth - 6, lowerHeight - 8, homeAccent, awayAccent, CrowdMix.BalancedHome, alignFromBottom: false, crowdState);
        DrawCrowdBlock(rightBleacherX + 3, topLimit + 6, bleacherWidth - 6, upperHeight - 10, homeAccent, awayAccent, CrowdMix.BalancedHome, alignFromBottom: true, crowdState);
        DrawCrowdBlock(rightBleacherX + 3, splitBottom + 4, bleacherWidth - 6, lowerHeight - 8, homeAccent, awayAccent, CrowdMix.HomeHeavy, alignFromBottom: false, crowdState);
    }

    private static void DrawCrowdBlock(
        int x,
        int y,
        int width,
        int height,
        Color homeCrowd,
        Color awayCrowd,
        CrowdMix mix,
        bool alignFromBottom,
        CrowdBackdropState crowdState)
    {
        if (width < 12 || height < 12)
        {
            return;
        }

        float time = (float)Raylib.GetTime();
        float overallEnergy = Math.Clamp(crowdState.OverallEnergy, 0f, 1f);
        int rowSpacing = overallEnergy >= 0.78f ? 9 : 10;
        int colSpacing = overallEnergy >= 0.86f ? 6 : 7;
        int startRowY = alignFromBottom
            ? y + height - 8
            : y + 2;
        int endRowY = alignFromBottom
            ? y + 2
            : y + height - 8;
        int rowStep = alignFromBottom ? -rowSpacing : rowSpacing;
        int rowIndex = 0;

        for (int rowY = startRowY;
            alignFromBottom ? rowY >= endRowY : rowY <= endRowY;
            rowY += rowStep, rowIndex++)
        {
            int stagger = (rowIndex % 2) * 3;

            for (int colX = x + 2 + stagger; colX <= x + width - 5; colX += colSpacing)
            {
                int seed = Hash(colX, rowY, rowIndex);
                CrowdFanVisual fanVisual = SelectCrowdVisual(seed, homeCrowd, awayCrowd, mix);
                float fanEnergy = GetFanEnergy(fanVisual.Affiliation, crowdState);
                float phase = (seed & 31) * 0.21f;
                float bobSpeed = 2.3f + (fanEnergy * 2.4f);
                float swaySpeed = 1.2f + (fanEnergy * 1.7f);
                int bobAmplitude = fanEnergy >= 0.72f ? 2 : 1;
                int bob = MathF.Sin((time * bobSpeed) + phase) > (0.6f - (fanEnergy * 0.38f)) ? bobAmplitude : 0;
                int swayMagnitude = fanEnergy >= 0.7f ? 2 : 1;
                int swayDirection = ((seed >> 2) & 1) == 0 ? 1 : -1;
                int sway = MathF.Cos((time * swaySpeed) + phase) > (0.8f - (fanEnergy * 0.3f)) ? swayMagnitude * swayDirection : 0;
                Color crowdColor = ApplyExcitementTint(fanVisual.Color, fanVisual.Affiliation, fanEnergy, crowdState);
                Color headColor = AdjustColor(crowdColor, 12 + (int)(fanEnergy * 20f));
                int drawY = rowY - bob;
                int drawX = colX + sway;

                Raylib.DrawRectangle(drawX, drawY, 3, 4, crowdColor);
                Raylib.DrawRectangle(drawX + 1, drawY - 2, 2, 2, headColor);

                int armThreshold = fanEnergy >= 0.78f ? 2 : fanEnergy >= 0.52f ? 1 : 0;
                if (((seed >> 3) & 3) <= armThreshold)
                {
                    int armDir = (seed & 1) == 0 ? -1 : 3;
                    int armHeight = fanEnergy >= 0.75f ? 3 : 2;
                    Raylib.DrawRectangle(drawX + armDir, drawY + 1 - bob, 1, armHeight, AdjustColor(crowdColor, 24));

                    if (fanEnergy >= 0.88f)
                    {
                        int secondArmDir = armDir < 0 ? 3 : -1;
                        Raylib.DrawRectangle(drawX + secondArmDir, drawY + 1 - bob, 1, 2, AdjustColor(crowdColor, 20));
                    }
                }
            }
        }
    }

    private static CrowdFanVisual SelectCrowdVisual(int seed, Color homeCrowd, Color awayCrowd, CrowdMix mix)
    {
        int roll = Math.Abs(seed % 20);

        return mix switch
        {
            CrowdMix.HomeHeavy => roll switch
            {
                <= 10 => new CrowdFanVisual(homeCrowd, CrowdAffiliation.Home),
                <= 14 => new CrowdFanVisual(awayCrowd, CrowdAffiliation.Away),
                _ => new CrowdFanVisual(NeutralCrowdPalette[roll % NeutralCrowdPalette.Length], CrowdAffiliation.Neutral)
            },
            CrowdMix.BalancedHome => roll switch
            {
                <= 8 => new CrowdFanVisual(homeCrowd, CrowdAffiliation.Home),
                <= 12 => new CrowdFanVisual(awayCrowd, CrowdAffiliation.Away),
                _ => new CrowdFanVisual(NeutralCrowdPalette[roll % NeutralCrowdPalette.Length], CrowdAffiliation.Neutral)
            },
            _ => roll switch
            {
                <= 6 => new CrowdFanVisual(homeCrowd, CrowdAffiliation.Home),
                <= 12 => new CrowdFanVisual(awayCrowd, CrowdAffiliation.Away),
                _ => new CrowdFanVisual(NeutralCrowdPalette[(roll + 1) % NeutralCrowdPalette.Length], CrowdAffiliation.Neutral)
            }
        };
    }

    private static float GetFanEnergy(CrowdAffiliation affiliation, CrowdBackdropState crowdState)
    {
        float overall = Math.Clamp(crowdState.OverallEnergy, 0f, 1f);
        float teamEnergy = affiliation switch
        {
            CrowdAffiliation.Home => crowdState.HomeEnergy,
            CrowdAffiliation.Away => crowdState.AwayEnergy,
            _ => overall
        };

        float baseline = affiliation == CrowdAffiliation.Neutral ? 0.22f : 0.18f;
        float overallShare = affiliation == CrowdAffiliation.Neutral ? 0.7f : 0.45f;
        return Math.Clamp(baseline + (overall * overallShare) + (teamEnergy * 0.75f), 0f, 1f);
    }

    private static Color ApplyExcitementTint(Color color, CrowdAffiliation affiliation, float fanEnergy, CrowdBackdropState crowdState)
    {
        int brighten = (int)(fanEnergy * 22f);
        int darken = affiliation switch
        {
            CrowdAffiliation.Home when crowdState.HomeEnergy < crowdState.AwayEnergy => (int)((crowdState.AwayEnergy - crowdState.HomeEnergy) * 18f),
            CrowdAffiliation.Away when crowdState.AwayEnergy < crowdState.HomeEnergy => (int)((crowdState.HomeEnergy - crowdState.AwayEnergy) * 18f),
            _ => 0
        };

        return AdjustColor(color, brighten - darken);
    }

    private static void DrawUpperDeckBands(Rectangle rect, int stadiumLeft, int stadiumRight, Color edgeColor, Color homeAccent, Color awayAccent)
    {
        int upperY = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 95f);
        int lowerY = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 5f) - 12;
        int deckHeight = 10;

        Color baseBand = new Color(28, 30, 38, 220);
        Color rail = AdjustColor(edgeColor, 16);

        Raylib.DrawRectangle(stadiumLeft, upperY, stadiumRight - stadiumLeft, deckHeight, baseBand);
        Raylib.DrawRectangle(stadiumLeft, lowerY, stadiumRight - stadiumLeft, deckHeight, baseBand);
        Raylib.DrawRectangle(stadiumLeft, upperY + deckHeight - 2, stadiumRight - stadiumLeft, 2, rail);
        Raylib.DrawRectangle(stadiumLeft, lowerY, stadiumRight - stadiumLeft, 2, rail);

        int segmentWidth = Math.Max(18, (stadiumRight - stadiumLeft) / 6);
        for (int i = 0; i < 6; i++)
        {
            int segmentX = stadiumLeft + (i * segmentWidth);
            Color accent = i % 2 == 0 ? homeAccent : awayAccent;
            Raylib.DrawRectangle(segmentX + 2, upperY + 2, segmentWidth - 4, 3, accent);
            Raylib.DrawRectangle(segmentX + 2, lowerY + 5, segmentWidth - 4, 3, accent);
        }
    }

    private static void DrawHomeCrowdChant(int leftBleacherX, int rightBleacherX, int bleacherWidth, Color homeAccent, CrowdBackdropState crowdState)
    {
        if (string.IsNullOrWhiteSpace(crowdState.HomeChantText) || crowdState.HomeChantStrength <= 0.01f)
        {
            return;
        }

        GetSeatingBounds(Constants.FieldRect, out int topLimit, out int splitTop, out int splitBottom, out int bottomLimit);

        float time = (float)Raylib.GetTime();
        float strength = Math.Clamp(crowdState.HomeChantStrength, 0f, 1f);
        int fontSize = 18 + (int)(strength * 10f);
        int textWidth = Raylib.MeasureText(crowdState.HomeChantText, fontSize);
        float fieldX = Math.Clamp(crowdState.HomeChantFieldX, 0f, 1f);
        float fieldY = Math.Clamp(crowdState.HomeChantFieldY, 0f, 1f);
        int fieldScreenY = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + (fieldY * 100f));
        bool useLeftBleacher = fieldX <= 0.5f;
        int bleacherX = useLeftBleacher ? leftBleacherX : rightBleacherX;
        int centerBias = useLeftBleacher
            ? (int)(fieldX * bleacherWidth * 0.22f)
            : (int)((1f - fieldX) * bleacherWidth * 0.22f);
        int baseX = bleacherX + Math.Max(4, (bleacherWidth - textWidth) / 2) + (useLeftBleacher ? centerBias : -centerBias);

        int upperMinY = topLimit + 8;
        int upperMaxY = splitTop - fontSize - 8;
        int lowerMinY = splitBottom + 8;
        int lowerMaxY = bottomLimit - fontSize - 8;
        int baseY = fieldScreenY <= splitTop
            ? Math.Clamp(fieldScreenY - (fontSize / 2), upperMinY, Math.Max(upperMinY, upperMaxY))
            : Math.Clamp(fieldScreenY - (fontSize / 2), lowerMinY, Math.Max(lowerMinY, lowerMaxY));

        int bounce = (int)(MathF.Sin(time * 7.2f) * (2f + (strength * 3f)));
        int alpha = (int)(110 + (strength * 145f));
        Color outline = new((byte)18, (byte)12, (byte)24, (byte)Math.Clamp(alpha, 0, 255));
        Color fill = new(
            (byte)Math.Clamp(homeAccent.R + 35, 0, 255),
            (byte)Math.Clamp(homeAccent.G + 35, 0, 255),
            (byte)Math.Clamp(homeAccent.B + 35, 0, 255),
            (byte)Math.Clamp(alpha, 0, 255));
        Color background = new((byte)14, (byte)16, (byte)24, (byte)Math.Clamp(150 + (int)(strength * 70f), 0, 255));
        Color backgroundBorder = new(
            (byte)Math.Clamp(homeAccent.R + 20, 0, 255),
            (byte)Math.Clamp(homeAccent.G + 20, 0, 255),
            (byte)Math.Clamp(homeAccent.B + 20, 0, 255),
            (byte)Math.Clamp(180 + (int)(strength * 60f), 0, 255));
        int padX = 10;
        int padY = 6;
        Rectangle backgroundRect = new(baseX - padX, baseY + bounce - padY, textWidth + (padX * 2), fontSize + (padY * 2));
        Rectangle shadowRect = new(backgroundRect.X + 2, backgroundRect.Y + 2, backgroundRect.Width, backgroundRect.Height);

        Raylib.DrawRectangleRec(shadowRect, new Color(0, 0, 0, (int)Math.Clamp(70 + (strength * 60f), 0f, 255f)));
        Raylib.DrawRectangleRec(backgroundRect, background);
        Raylib.DrawRectangleLinesEx(backgroundRect, 2f, backgroundBorder);

        for (int ox = -2; ox <= 2; ox++)
        {
            for (int oy = -2; oy <= 2; oy++)
            {
                if (ox == 0 && oy == 0)
                {
                    continue;
                }

                Raylib.DrawText(crowdState.HomeChantText, baseX + ox, baseY + oy + bounce, fontSize, outline);
            }
        }

        Raylib.DrawText(crowdState.HomeChantText, baseX, baseY + bounce, fontSize, fill);
    }

    private static void DrawBleacherSideExtension(
        int bleacherX,
        int bleacherWidth,
        Color baseColor,
        Color edgeColor,
        Color primaryAccent,
        Color secondaryAccent,
        bool isLeftSide)
    {
        GetSeatingBounds(Constants.FieldRect, out int topLimit, out int splitTop, out int splitBottom, out int bottomLimit);

        int bodyHeight = Math.Max(42, bottomLimit - topLimit - 4);
        int bodyY = topLimit + 2;
        int extensionWidth = Math.Clamp((int)(bleacherWidth * 0.82f), 20, 34);
        int bodyX = isLeftSide ? bleacherX - extensionWidth + 2 : bleacherX + bleacherWidth - 2;
        int bodyRadiusX = Math.Max(8, extensionWidth / 2);
        int bodyRadiusY = Math.Max(16, bodyHeight / 2);
        int ellipseCenterX = isLeftSide ? bodyX + extensionWidth - 2 : bodyX + 2;
        int ellipseCenterY = bodyY + (bodyHeight / 2);

        Color shell = AdjustColor(baseColor, 10);
        Color shellShadow = AdjustColor(baseColor, -8);
        Color trim = AdjustColor(edgeColor, 14);
        Color panelBase = AdjustColor(baseColor, 2);

        Raylib.DrawEllipse(ellipseCenterX + (isLeftSide ? -2 : 2), ellipseCenterY + 3, bodyRadiusX, bodyRadiusY, shellShadow);
        Raylib.DrawEllipse(ellipseCenterX, ellipseCenterY, bodyRadiusX, bodyRadiusY, shell);
        Raylib.DrawEllipseLines(ellipseCenterX, ellipseCenterY, bodyRadiusX, bodyRadiusY, trim);

        int fasciaInset = 4;
        int fasciaX = bodyX + fasciaInset;
        int fasciaY = splitTop - 12;
        int fasciaWidth = Math.Max(8, extensionWidth - (fasciaInset * 2));
        int fasciaHeight = Math.Max(30, splitBottom - splitTop + 28);

        Raylib.DrawRectangle(fasciaX, fasciaY, fasciaWidth, fasciaHeight, panelBase);
        Raylib.DrawRectangle(fasciaX, fasciaY, fasciaWidth, 2, trim);
        Raylib.DrawRectangle(fasciaX, fasciaY + fasciaHeight - 2, fasciaWidth, 2, AdjustColor(trim, -8));

        int panelCount = 2;
        int panelGap = 4;
        int panelWidth = Math.Max(6, (fasciaWidth - (panelGap * (panelCount + 1))) / panelCount);
        for (int i = 0; i < panelCount; i++)
        {
            int panelX = fasciaX + panelGap + (i * (panelWidth + panelGap));
            Color accent = i % 2 == 0 ? primaryAccent : secondaryAccent;
            Raylib.DrawRectangle(panelX, fasciaY + 5, panelWidth, Math.Max(10, fasciaHeight - 10), accent);
        }

        int joinX = isLeftSide ? bleacherX - 2 : bleacherX + bleacherWidth;
        Raylib.DrawRectangle(joinX, bodyY + 4, 4, bodyHeight - 8, AdjustColor(trim, 4));
    }

    private static void DrawRibbonBoards(int leftBleacherX, int rightBleacherX, int bleacherWidth, int topLimit, int bottomLimit, Color homeAccent, Color awayAccent)
    {
        int ribbonHeight = 4;
        int upperRibbonY = topLimit + 18;
        int lowerRibbonY = bottomLimit - 22;

        DrawRibbonBoard(leftBleacherX + 2, upperRibbonY, bleacherWidth - 4, ribbonHeight, homeAccent, awayAccent);
        DrawRibbonBoard(rightBleacherX + 2, upperRibbonY, bleacherWidth - 4, ribbonHeight, awayAccent, homeAccent);
        DrawRibbonBoard(leftBleacherX + 2, lowerRibbonY, bleacherWidth - 4, ribbonHeight, awayAccent, homeAccent);
        DrawRibbonBoard(rightBleacherX + 2, lowerRibbonY, bleacherWidth - 4, ribbonHeight, homeAccent, awayAccent);
    }

    private static void DrawRibbonBoard(int x, int y, int width, int height, Color primary, Color secondary)
    {
        if (width < 8 || height < 2)
        {
            return;
        }

        Raylib.DrawRectangle(x, y, width, height, new Color(20, 22, 28, 230));

        int segmentWidth = Math.Max(6, width / 5);
        for (int i = 0; i < 5; i++)
        {
            int segmentX = x + (i * segmentWidth);
            Color color = i % 2 == 0 ? primary : secondary;
            Raylib.DrawRectangle(segmentX, y + 1, Math.Min(segmentWidth - 1, x + width - segmentX), height - 2, color);
        }
    }

    private static void DrawFieldEdgeShadow(Rectangle rect, int leftBleacherX, int rightBleacherX, int bleacherWidth)
    {
        int shadowWidth = Math.Max(10, bleacherWidth / 3);
        Color edgeShadow = new(8, 10, 14, 70);

        Raylib.DrawRectangleGradientH(leftBleacherX + bleacherWidth - shadowWidth, (int)rect.Y, shadowWidth, (int)rect.Height, edgeShadow, new Color(0, 0, 0, 0));
        Raylib.DrawRectangleGradientH((int)rect.X + (int)rect.Width, (int)rect.Y, shadowWidth, (int)rect.Height, new Color(0, 0, 0, 0), edgeShadow);
        Raylib.DrawRectangleGradientH((int)rect.X - shadowWidth, (int)rect.Y, shadowWidth, (int)rect.Height, edgeShadow, new Color(0, 0, 0, 0));
        Raylib.DrawRectangleGradientH(rightBleacherX, (int)rect.Y, shadowWidth, (int)rect.Height, new Color(0, 0, 0, 0), edgeShadow);
    }

    private static void GetSeatingBounds(Rectangle rect, out int topLimit, out int splitTop, out int splitBottom, out int bottomLimit)
    {
        topLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 90f);
        bottomLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 10f);
        int height = Math.Max(0, bottomLimit - topLimit);

        int midfieldY = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 50f);
        int splitY = Math.Clamp(midfieldY, topLimit + 1, bottomLimit - 1);
        int concourseHeight = Math.Clamp(height / 14, 8, 14);
        splitTop = Math.Clamp(splitY - concourseHeight / 2, topLimit + 1, bottomLimit - 2);
        splitBottom = Math.Clamp(splitTop + concourseHeight, splitTop + 1, bottomLimit - 1);
    }

    private static Color CreateAccentColor(Color color, float tintFactor, int brighten)
    {
        Color tinted = Tint(color, tintFactor);
        return AdjustColor(tinted, brighten);
    }

    private static Color Tint(Color color, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        return new Color(
            (byte)Math.Clamp((int)(color.R * factor), 0, 255),
            (byte)Math.Clamp((int)(color.G * factor), 0, 255),
            (byte)Math.Clamp((int)(color.B * factor), 0, 255),
            color.A);
    }

    private static int Hash(int x, int y, int salt)
    {
        unchecked
        {
            int hash = x;
            hash = (hash * 397) ^ y;
            hash = (hash * 397) ^ salt;
            return hash;
        }
    }

    private static Color AdjustColor(Color color, int delta)
    {
        int r = Math.Clamp(color.R + delta, 0, 255);
        int g = Math.Clamp(color.G + delta, 0, 255);
        int b = Math.Clamp(color.B + delta, 0, 255);
        return new Color((byte)r, (byte)g, (byte)b, color.A);
    }

    private enum CrowdMix
    {
        HomeHeavy,
        BalancedHome,
        AwayHeavy
    }

    private enum CrowdAffiliation
    {
        Home,
        Away,
        Neutral
    }

    private readonly record struct CrowdFanVisual(Color Color, CrowdAffiliation Affiliation);
}

public readonly record struct CrowdBackdropState(
    float HomeEnergy,
    float AwayEnergy,
    float OverallEnergy,
    string HomeChantText,
    float HomeChantStrength,
    float HomeChantFieldX,
    float HomeChantFieldY);
