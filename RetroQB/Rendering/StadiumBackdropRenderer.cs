using Raylib_cs;
using RetroQB.Core;
using System.Numerics;

namespace RetroQB.Rendering;

internal sealed partial class StadiumBackdropRenderer
{
    private static readonly Color[] SkinPalette =
    [
        new(225, 176, 137, 255), new(183, 128, 88, 255),
        new(130, 82, 57, 255), new(92, 59, 46, 255), new(243, 204, 166, 255)
    ];

    private static readonly Color[] NeutralCrowdPalette =
    [
        new Color(178, 182, 190, 255),
        new Color(132, 138, 150, 255),
        new Color(94, 100, 114, 255),
        new Color(214, 210, 196, 255)
    ];

    public void Draw(Color homeTeamColor, Color awayTeamColor, SeasonStage stage, CrowdBackdropState crowdState)
    {
        Rectangle rect = Constants.FieldRect;
        int screenW = Raylib.GetScreenWidth();
        StadiumTier tier = StadiumTier.For(stage);
        StadiumLighting lighting = StadiumLighting.For(stage);

        DrawArenaShell(rect, homeTeamColor, awayTeamColor);

        int margin = 6;
        int bleacherWidth = Math.Max(24, (int)(rect.Width * tier.BleacherWidthFactor));
        int sidelineBuffer = Math.Max(14, (int)(rect.Width * tier.SidelineBufferFactor));
        int leftBleacherX = (int)Math.Max(margin, rect.X - bleacherWidth - sidelineBuffer);
        int rightBleacherX = (int)Math.Min(screenW - margin - bleacherWidth, rect.X + rect.Width + sidelineBuffer);

        Color bleacherBase = lighting.Concrete;
        Color bleacherEdge = lighting.Rail;
        Color homeAccent = CreateAccentColor(homeTeamColor, 0.82f, 18);
        Color awayAccent = CreateAccentColor(awayTeamColor, 0.82f, 18);
        int topLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 90f);
        int bottomLimit = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 10f);

        DrawBowlShell(rect, leftBleacherX, rightBleacherX, bleacherWidth, homeAccent, awayAccent, lighting, tier.Attendance);
        DrawBleachersColumn(rect, leftBleacherX, bleacherWidth, isLeftSide: true, bleacherBase, bleacherEdge);
        DrawBleachersColumn(rect, rightBleacherX, bleacherWidth, isLeftSide: false, bleacherBase, bleacherEdge);
        DrawCrowdSections(rect, leftBleacherX, bleacherWidth, rightBleacherX, homeAccent, awayAccent, crowdState, tier.Attendance);
        DrawExcitedCrowdFlares(leftBleacherX, rightBleacherX, bleacherWidth, homeAccent, awayAccent, crowdState);
        DrawExcitedCrowdProps(leftBleacherX, rightBleacherX, bleacherWidth, homeAccent, awayAccent, crowdState);
        DrawStandStructure(leftBleacherX, bleacherWidth, true, homeAccent, lighting);
        DrawStandStructure(rightBleacherX, bleacherWidth, false, awayAccent, lighting);
        DrawRibbonBoards(leftBleacherX, rightBleacherX, bleacherWidth, topLimit, bottomLimit, homeAccent, awayAccent);
        DrawFieldEdgeShadow(rect, leftBleacherX, rightBleacherX, bleacherWidth);
        DrawFloodlights(rect, leftBleacherX, rightBleacherX, bleacherWidth, lighting.Trim, lighting.Trim, tier, lighting);
    }

    private static void DrawArenaShell(Rectangle field, Color homeColor, Color awayColor)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();
        int leftPanelEdge = (int)(Constants.OuterMargin + Constants.SidePanelWidth + 3);
        int rightPanelEdge = screenW - (int)(Constants.OuterMargin + Constants.ScoreboardPanelWidth + 3);

        Raylib.DrawRectangleGradientV(0, 0, screenW, screenH, Palette.Cabinet, Palette.Background);

        // Recessed bay behind the stadium. At ultrawide sizes this turns dead space into cabinet art.
        int bayX = leftPanelEdge;
        int bayWidth = Math.Max(0, rightPanelEdge - leftPanelEdge);
        Raylib.DrawRectangle(bayX, 0, bayWidth, screenH, new Color(3, 5, 9, 225));

        Color grid = new(24, 40, 52, 80);
        for (int x = bayX + 24; x < bayX + bayWidth; x += 48)
        {
            Raylib.DrawLine(x, 0, x, screenH, grid);
        }
        for (int y = 24; y < screenH; y += 48)
        {
            Raylib.DrawLine(bayX, y, bayX + bayWidth, y, grid);
        }

        // Team-colored light rails frame the play surface without competing with it.
        int railLeft = (int)field.X - 18;
        int railRight = (int)(field.X + field.Width + 14);
        Color homeRail = new(homeColor.R, homeColor.G, homeColor.B, (byte)105);
        Color awayRail = new(awayColor.R, awayColor.G, awayColor.B, (byte)105);
        Raylib.DrawRectangle(railLeft, 0, 4, screenH, homeRail);
        Raylib.DrawRectangle(railRight, 0, 4, screenH, awayRail);
        Raylib.DrawRectangle(railLeft - 4, 0, 12, screenH, new Color(homeColor.R, homeColor.G, homeColor.B, (byte)20));
        Raylib.DrawRectangle(railRight - 4, 0, 12, screenH, new Color(awayColor.R, awayColor.G, awayColor.B, (byte)20));

        DrawBayTicks(bayX, bayWidth, 10, homeColor);
        DrawBayTicks(bayX, bayWidth, screenH - 14, awayColor);
    }

    private static void DrawBayTicks(int x, int width, int y, Color color)
    {
        int center = x + width / 2;
        for (int i = -4; i <= 4; i++)
        {
            int tickWidth = i == 0 ? 34 : 16;
            int tickX = center + (i * 42) - (tickWidth / 2);
            Raylib.DrawRectangle(tickX, y, tickWidth, 2, new Color(color.R, color.G, color.B, (byte)110));
        }
    }

    public void DrawChantOverlay(Color homeTeamColor, SeasonStage stage, CrowdBackdropState crowdState)
    {
        Rectangle rect = Constants.FieldRect;
        int screenW = Raylib.GetScreenWidth();

        int margin = 6;
        StadiumTier tier = StadiumTier.For(stage);
        int bleacherWidth = Math.Max(24, (int)(rect.Width * tier.BleacherWidthFactor));
        int sidelineBuffer = Math.Max(14, (int)(rect.Width * tier.SidelineBufferFactor));
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
        CrowdBackdropState crowdState,
        float attendance)
    {
        GetSeatingBounds(rect, out int topLimit, out int splitTop, out int splitBottom, out int bottomLimit);

        int upperHeight = splitTop - topLimit;
        int lowerHeight = bottomLimit - splitBottom;

        DrawCrowdBlock(leftBleacherX + 3, topLimit + 6, bleacherWidth - 6, upperHeight - 10, homeAccent, awayAccent, CrowdMix.HomeHeavy, alignFromBottom: true, crowdState, attendance);
        DrawCrowdBlock(leftBleacherX + 3, splitBottom + 4, bleacherWidth - 6, lowerHeight - 8, homeAccent, awayAccent, CrowdMix.BalancedHome, alignFromBottom: false, crowdState, attendance);
        DrawCrowdBlock(rightBleacherX + 3, topLimit + 6, bleacherWidth - 6, upperHeight - 10, homeAccent, awayAccent, CrowdMix.BalancedHome, alignFromBottom: true, crowdState, attendance);
        DrawCrowdBlock(rightBleacherX + 3, splitBottom + 4, bleacherWidth - 6, lowerHeight - 8, homeAccent, awayAccent, CrowdMix.HomeHeavy, alignFromBottom: false, crowdState, attendance);
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
        CrowdBackdropState crowdState,
        float attendance)
    {
        if (width < 12 || height < 12)
        {
            return;
        }

        float time = crowdState.AnimationTime;
        // Attendance controls how tightly rows are packed. Never remove isolated
        // spectators: random empty seats read as holes rather than a smaller crowd.
        int rowSpacing = attendance >= 0.95f ? 7 : attendance >= 0.82f ? 8 : 10;
        int colSpacing = attendance >= 0.95f ? 5 : attendance >= 0.82f ? 6 : 7;
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
            float fieldY = Math.Clamp(((Constants.FieldRect.Y + Constants.FieldRect.Height - rowY)
                / Constants.FieldRect.Height * Constants.FieldLength - Constants.EndZoneDepth) / 100f, 0f, 1f);
            float sectionPulse = CrowdReaction.GetSectionPulse(crowdState, fieldY,
                x > Constants.FieldRect.X + Constants.FieldRect.Width / 2f);

            for (int colX = x + 2 + stagger; colX <= x + width - 5; colX += colSpacing)
            {
                int seed = Hash(colX, rowY, rowIndex);
                CrowdFanVisual fanVisual = SelectCrowdVisual(seed, homeCrowd, awayCrowd, mix);
                float fanEnergy = GetFanEnergy(fanVisual.Affiliation, crowdState);
                bool celebrating = fanVisual.Affiliation == (crowdState.ReactionForHome ? CrowdAffiliation.Home : CrowdAffiliation.Away);
                float pulse = sectionPulse * (celebrating ? 1f : fanVisual.Affiliation == CrowdAffiliation.Neutral ? 0.35f : 0f);
                float phase = (seed & 31) * 0.21f;
                float bobSpeed = 2.3f + (fanEnergy * 2.4f);
                float swaySpeed = 1.2f + (fanEnergy * 1.7f);
                int bobAmplitude = fanEnergy >= 0.72f ? 2 : 1;
                int bob = MathF.Sin((time * bobSpeed) + phase) > (0.6f - (fanEnergy * 0.38f)) ? bobAmplitude : 0;
                int swayMagnitude = fanEnergy >= 0.7f ? 2 : 1;
                int swayDirection = ((seed >> 2) & 1) == 0 ? 1 : -1;
                int sway = MathF.Cos((time * swaySpeed) + phase) > (0.8f - (fanEnergy * 0.3f)) ? swayMagnitude * swayDirection : 0;
                Color crowdColor = ApplyExcitementTint(fanVisual.Color, fanVisual.Affiliation, fanEnergy, crowdState);
                int drawY = rowY - bob - (int)(pulse * 2f);
                int drawX = colX + sway;
                DrawSpectator(drawX, drawY, seed, crowdColor, fanEnergy, time, phase, pulse);
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

    private static void DrawSpectator(int x, int y, int seed, Color shirt, float energy, float time, float phase, float reaction = 0f)
    {
        // Stable variants: no per-frame randomness or changing faces.
        uint variant = (uint)seed;
        Color skin = SkinPalette[(int)((variant >> 5) % (uint)SkinPalette.Length)];
        int outfit = (int)((variant >> 10) % 4);
        bool seated = ((variant >> 14) & 3) == 0 || energy < 0.4f;
        int bodyHeight = seated ? 2 : 3;
        if (seated) y += 1;
        Raylib.DrawRectangle(x, y + bodyHeight, 3, 1, new Color(38, 41, 52, 255));
        Raylib.DrawRectangle(x, y, 3, bodyHeight, shirt);
        Raylib.DrawRectangle(x + 1, y - 2, 2, 2, skin);

        if (outfit == 0) // Cap with a tiny brim.
            Raylib.DrawRectangle(x, y - 3, 4, 1, AdjustColor(shirt, 32));
        else if (outfit == 1)
            Raylib.DrawRectangle(x + 1, y - 3, 2, 1, new Color(47, 32, 26, 255));
        else if (outfit == 2) // Contrasting scarf.
            Raylib.DrawRectangle(x, y, 3, 1, new Color(214, 206, 181, 255));
        else
            Raylib.DrawPixel(x + 1, y + 1, AdjustColor(shirt, 48));

        bool cheering = !seated && ((energy > 0.5f && MathF.Sin(time * 3f + phase) > 0.15f) || reaction > 0.25f);
        if (cheering)
        {
            Raylib.DrawRectangle(x - 1, y - 1, 1, 2, shirt);
            Raylib.DrawPixel(x - 1, y - 2, skin);
            if (energy > 0.78f || reaction > 0.5f)
            {
                Raylib.DrawRectangle(x + 3, y - 1, 1, 2, shirt);
                Raylib.DrawPixel(x + 3, y - 2, skin);
            }
        }
    }

    private static void DrawFloodlights(
        Rectangle rect,
        int leftBleacherX,
        int rightBleacherX,
        int bleacherWidth,
        Color homeAccent,
        Color awayAccent,
        StadiumTier tier, StadiumLighting lighting)
    {
        int outerLeft = leftBleacherX - (tier.HasCornerStands ? Math.Clamp((int)(bleacherWidth * 0.6f), 16, 28) : 5);
        int outerRight = rightBleacherX + bleacherWidth + (tier.HasCornerStands ? Math.Clamp((int)(bleacherWidth * 0.6f), 16, 28) : 5);
        int top = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 92f);
        int bottom = (int)Constants.WorldToScreenY(Constants.EndZoneDepth + 8f);

        DrawLightTower(outerLeft, top, (int)rect.X, top + 38, pointsRight: true, homeAccent, tier.LightRows, lighting);
        DrawLightTower(outerRight, bottom, (int)(rect.X + rect.Width), bottom - 38, pointsRight: false, awayAccent, tier.LightRows, lighting);

        if (tier.LightTowerCount >= 4)
        {
            DrawLightTower(outerRight, top, (int)(rect.X + rect.Width), top + 38, pointsRight: false, awayAccent, tier.LightRows, lighting);
            DrawLightTower(outerLeft, bottom, (int)rect.X, bottom - 38, pointsRight: true, homeAccent, tier.LightRows, lighting);
        }

        if (tier.LightTowerCount >= 6)
        {
            int midfield = (top + bottom) / 2;
            DrawLightTower(outerLeft - 3, midfield, (int)rect.X, midfield, pointsRight: true, homeAccent, tier.LightRows, lighting);
            DrawLightTower(outerRight + 3, midfield, (int)(rect.X + rect.Width), midfield, pointsRight: false, awayAccent, tier.LightRows, lighting);
        }
    }

    private static void DrawLightTower(int x, int y, int beamTargetX, int beamTargetY, bool pointsRight, Color accent, int lightRows, StadiumLighting lighting)
    {
        int direction = pointsRight ? 1 : -1;
        int poleX = x - (direction * 3);
        int poleTop = y - 16;
        int poleBottom = y + 17;
        Color steel = new(82, 92, 108, 255);
        Color dimSteel = new(40, 48, 62, 255);

        Raylib.DrawLine(poleX - 3, poleBottom, poleX, poleTop, dimSteel);
        Raylib.DrawLine(poleX + 3, poleBottom, poleX, poleTop, steel);
        for (int braceY = poleTop + 7; braceY < poleBottom; braceY += 8)
        {
            Raylib.DrawLine(poleX - 2, braceY, poleX + 2, braceY + 5, steel);
        }

        int bankWidth = lightRows >= 2 ? 18 : 14;
        int bankHeight = lightRows >= 2 ? 13 : 8;
        int bankX = pointsRight ? x - 3 : x - bankWidth + 3;
        int bankY = y - bankHeight / 2;

        Color beam = new(lighting.Lamp.R, lighting.Lamp.G, lighting.Lamp.B, (byte)(lightRows >= 2 ? 24 : 15));
        Vector2 beamTop = new(bankX + (pointsRight ? bankWidth : 0), bankY + 2);
        Vector2 beamBottom = new(bankX + (pointsRight ? bankWidth : 0), bankY + bankHeight - 2);
        Vector2 target = new(beamTargetX, beamTargetY);
        // Raylib culls triangles with the opposite winding. Mirror the vertex
        // order as well as the geometry so both sides cast a visible beam.
        if (pointsRight)
        {
            Raylib.DrawTriangle(beamBottom, target, beamTop, beam);
        }
        else
        {
            Raylib.DrawTriangle(beamTop, target, beamBottom, beam);
        }

        Raylib.DrawRectangle(bankX, bankY, bankWidth, bankHeight, new Color(22, 28, 38, 255));
        Raylib.DrawRectangleLines(bankX, bankY, bankWidth, bankHeight, accent);
        for (int row = 0; row < lightRows; row++)
        {
            int bulbY = bankY + 3 + (row * 6);
            for (int bulbX = bankX + 3; bulbX <= bankX + bankWidth - 3; bulbX += 5)
            {
                Raylib.DrawCircle(bulbX, bulbY, 4f, new Color(lighting.Lamp.R, lighting.Lamp.G, lighting.Lamp.B, (byte)35));
                Raylib.DrawCircle(bulbX, bulbY, 2f, lighting.Lamp);
            }
        }
    }

    private static void DrawExcitedCrowdFlares(int leftBleacherX, int rightBleacherX, int bleacherWidth, Color homeAccent, Color awayAccent, CrowdBackdropState crowdState)
    {
        float homeEnergy = Math.Clamp(crowdState.HomeEnergy, 0f, 1f);
        float awayEnergy = Math.Clamp(crowdState.AwayEnergy, 0f, 1f);

        if (homeEnergy < 0.58f && awayEnergy < 0.58f)
        {
            return;
        }

        GetSeatingBounds(Constants.FieldRect, out int topLimit, out int splitTop, out int splitBottom, out int bottomLimit);

        DrawBleacherFlares(leftBleacherX + 4, bleacherWidth - 8, topLimit + 6, splitTop - topLimit - 10, homeAccent, homeEnergy, 11);
        DrawBleacherFlares(leftBleacherX + 4, bleacherWidth - 8, splitBottom + 4, bottomLimit - splitBottom - 8, homeAccent, homeEnergy * 0.92f, 23);
        DrawBleacherFlares(rightBleacherX + 4, bleacherWidth - 8, topLimit + 6, splitTop - topLimit - 10, awayAccent, awayEnergy, 37);
        DrawBleacherFlares(rightBleacherX + 4, bleacherWidth - 8, splitBottom + 4, bottomLimit - splitBottom - 8, awayAccent, awayEnergy * 0.92f, 53);
    }

    private static void DrawBleacherFlares(int x, int width, int y, int height, Color flareColor, float energy, int salt)
    {
        if (width < 12 || height < 12 || energy < 0.58f)
        {
            return;
        }

        float time = (float)Raylib.GetTime();
        int timeBucket = (int)MathF.Floor(time * (2.6f + (energy * 2.8f)));
        int flareSlots = energy >= 0.88f ? 4 : energy >= 0.72f ? 3 : 2;

        for (int i = 0; i < flareSlots; i++)
        {
            int seed = Hash(x + (i * 17), y + (i * 29), salt ^ timeBucket);
            float appearRoll = Math.Abs(seed % 100) / 100f;
            float appearThreshold = 0.56f - ((energy - 0.58f) * 0.65f);
            if (appearRoll > appearThreshold)
            {
                continue;
            }

            float life = Math.Abs((seed >> 3) % 100) / 100f;
            float flareStrength = Math.Clamp(0.55f + (energy * 0.55f) + (life * 0.25f), 0f, 1f);
            int flareX = x + 6 + Math.Abs((seed >> 5) % Math.Max(8, width - 12));
            int flareBaseY = y + height - 6 - Math.Abs((seed >> 9) % Math.Max(6, height - 10));
            int popOffset = (int)(MathF.Sin((time * 9.5f) + (seed & 15)) * (2f + (flareStrength * 5f)));
            int flareY = flareBaseY - (int)(flareStrength * 9f) + popOffset;
            DrawSingleFlare(flareX, flareY, flareColor, flareStrength, seed);
        }
    }

    private static void DrawExcitedCrowdProps(int leftBleacherX, int rightBleacherX, int bleacherWidth, Color homeAccent, Color awayAccent, CrowdBackdropState crowdState)
    {
        float homeEnergy = Math.Clamp(crowdState.HomeEnergy, 0f, 1f);
        float awayEnergy = Math.Clamp(crowdState.AwayEnergy, 0f, 1f);

        if (homeEnergy < 0.46f && awayEnergy < 0.46f)
        {
            return;
        }

        GetSeatingBounds(Constants.FieldRect, out int topLimit, out int splitTop, out int splitBottom, out int bottomLimit);

        DrawBleacherProps(leftBleacherX + 4, bleacherWidth - 8, topLimit + 8, splitTop - topLimit - 12, homeAccent, homeEnergy, 71);
        DrawBleacherProps(leftBleacherX + 4, bleacherWidth - 8, splitBottom + 6, bottomLimit - splitBottom - 10, homeAccent, homeEnergy * 0.95f, 89);
        DrawBleacherProps(rightBleacherX + 4, bleacherWidth - 8, topLimit + 8, splitTop - topLimit - 12, awayAccent, awayEnergy, 107);
        DrawBleacherProps(rightBleacherX + 4, bleacherWidth - 8, splitBottom + 6, bottomLimit - splitBottom - 10, awayAccent, awayEnergy * 0.95f, 131);
    }

    private static void DrawBleacherProps(int x, int width, int y, int height, Color teamColor, float energy, int salt)
    {
        if (width < 12 || height < 12 || energy < 0.46f)
        {
            return;
        }

        float time = (float)Raylib.GetTime();
        int propCount = energy >= 0.88f ? 7 : energy >= 0.72f ? 5 : energy >= 0.58f ? 3 : 2;
        int maxX = Math.Max(8, width - 10);
        int maxY = Math.Max(8, height - 10);

        for (int i = 0; i < propCount; i++)
        {
            int seed = Hash(x + (i * 31), y + (i * 17), salt);
            int px = x + 5 + Math.Abs((seed >> 2) % maxX);
            int py = y + 5 + Math.Abs((seed >> 8) % maxY);
            float phase = ((seed >> 4) & 31) * 0.27f;
            bool drawFlag = ((seed >> 1) & 1) == 0;

            if (drawFlag)
            {
                DrawFlagProp(px, py, teamColor, energy, time, phase, seed);
            }
            else
            {
                DrawTowelProp(px, py, teamColor, energy, time, phase, seed);
            }
        }
    }

    private static void DrawFlagProp(int x, int y, Color teamColor, float energy, float time, float phase, int seed)
    {
        int poleHeight = 7 + Math.Abs((seed >> 6) % 4);
        float wave = MathF.Sin((time * (4.4f + (energy * 2.4f))) + phase);
        int flagWidth = 5 + (int)(energy * 3f);
        int flagHeight = 4 + Math.Abs((seed >> 9) % 3);
        int tipOffset = (int)(wave * (2f + (energy * 2f)));
        Color poleColor = new((byte)210, (byte)210, (byte)220, (byte)220);
        Color flagColor = new(
            (byte)Math.Clamp(teamColor.R + 30, 0, 255),
            (byte)Math.Clamp(teamColor.G + 30, 0, 255),
            (byte)Math.Clamp(teamColor.B + 30, 0, 255),
            (byte)240);

        Raylib.DrawLine(x, y, x, y - poleHeight, poleColor);

        System.Numerics.Vector2 anchor = new(x, y - poleHeight);
        System.Numerics.Vector2 top = new(x + flagWidth, y - poleHeight + tipOffset);
        System.Numerics.Vector2 bottom = new(x + flagWidth - 1, y - poleHeight + flagHeight + tipOffset);
        Raylib.DrawTriangle(anchor, top, bottom, flagColor);
        Raylib.DrawLineEx(anchor, top, 1f, AdjustColor(flagColor, 18));
        Raylib.DrawLineEx(anchor, bottom, 1f, AdjustColor(flagColor, -8));
    }

    private static void DrawTowelProp(int x, int y, Color teamColor, float energy, float time, float phase, int seed)
    {
        float wave = MathF.Sin((time * (6.1f + (energy * 3.1f))) + phase);
        int handleHeight = 3 + Math.Abs((seed >> 7) % 2);
        int towelWidth = 4 + Math.Abs((seed >> 10) % 3);
        int towelHeight = 5 + Math.Abs((seed >> 12) % 3);
        int offsetX = (int)(wave * (2f + (energy * 2f)));
        int offsetY = (int)(MathF.Cos((time * 4.8f) + phase) * (1f + energy));
        Color towelColor = new((byte)242, (byte)242, (byte)246, (byte)230);
        Color accentStripe = new(
            (byte)Math.Clamp(teamColor.R + 10, 0, 255),
            (byte)Math.Clamp(teamColor.G + 10, 0, 255),
            (byte)Math.Clamp(teamColor.B + 10, 0, 255),
            (byte)210);

        Raylib.DrawLine(x, y, x, y - handleHeight, new Color(220, 220, 230, 180));
        Rectangle towelRect = new(x + offsetX, y - handleHeight - towelHeight + offsetY, towelWidth, towelHeight);
        Raylib.DrawRectangleRec(towelRect, towelColor);
        Raylib.DrawRectangle((int)towelRect.X, (int)towelRect.Y + towelHeight / 2, towelWidth, 1, accentStripe);
        Raylib.DrawRectangleLinesEx(towelRect, 1f, new Color(255, 255, 255, 180));
    }

    private static void DrawSingleFlare(int x, int y, Color color, float strength, int seed)
    {
        int glowRadius = 4 + (int)(strength * 6f);
        int coreRadius = Math.Max(2, glowRadius / 2);
        int alpha = (int)(110 + (strength * 115f));
        Color glow = new(
            (byte)Math.Clamp(color.R + 20, 0, 255),
            (byte)Math.Clamp(color.G + 20, 0, 255),
            (byte)Math.Clamp(color.B + 20, 0, 255),
            (byte)Math.Clamp(alpha, 0, 255));
        Color core = new(
            (byte)Math.Clamp(color.R + 55, 0, 255),
            (byte)Math.Clamp(color.G + 55, 0, 255),
            (byte)Math.Clamp(color.B + 55, 0, 255),
            (byte)255);

        Raylib.DrawCircle(x, y, glowRadius + 3, new Color(glow.R, glow.G, glow.B, (byte)Math.Clamp(alpha / 3, 0, 255)));
        Raylib.DrawCircle(x, y, glowRadius, glow);
        Raylib.DrawCircle(x, y, coreRadius, core);

        int rayLength = glowRadius + 4 + Math.Abs((seed >> 2) % 4);
        Color rayColor = new(glow.R, glow.G, glow.B, (byte)Math.Clamp(alpha - 20, 0, 255));
        Raylib.DrawLineEx(new System.Numerics.Vector2(x - rayLength, y), new System.Numerics.Vector2(x + rayLength, y), 1.5f, rayColor);
        Raylib.DrawLineEx(new System.Numerics.Vector2(x, y - rayLength), new System.Numerics.Vector2(x, y + rayLength), 1.5f, rayColor);

        if (((seed >> 1) & 1) == 0)
        {
            Raylib.DrawLineEx(new System.Numerics.Vector2(x - (rayLength - 2), y - (rayLength - 2)), new System.Numerics.Vector2(x + (rayLength - 2), y + (rayLength - 2)), 1f, rayColor);
            Raylib.DrawLineEx(new System.Numerics.Vector2(x - (rayLength - 2), y + (rayLength - 2)), new System.Numerics.Vector2(x + (rayLength - 2), y - (rayLength - 2)), 1f, rayColor);
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

    private readonly record struct StadiumTier(
        float BleacherWidthFactor,
        float SidelineBufferFactor,
        float Attendance,
        int LightTowerCount,
        int LightRows,
        bool HasCornerStands)
    {
        public static StadiumTier For(SeasonStage stage) => stage switch
        {
            SeasonStage.RegularSeason => new StadiumTier(
                BleacherWidthFactor: 0.14f,
                SidelineBufferFactor: 0.08f,
                Attendance: 0.74f,
                LightTowerCount: 2,
                LightRows: 1,
                HasCornerStands: true),
            SeasonStage.Playoff => new StadiumTier(
                BleacherWidthFactor: 0.17f,
                SidelineBufferFactor: 0.085f,
                Attendance: 0.90f,
                LightTowerCount: 4,
                LightRows: 1,
                HasCornerStands: true),
            SeasonStage.SuperBowl => new StadiumTier(
                BleacherWidthFactor: 0.20f,
                SidelineBufferFactor: 0.09f,
                Attendance: 0.98f,
                LightTowerCount: 6,
                LightRows: 2,
                HasCornerStands: true),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };
    }
}

public readonly record struct CrowdBackdropState(
    float HomeEnergy,
    float AwayEnergy,
    float OverallEnergy,
    string HomeChantText,
    float HomeChantStrength,
    float HomeChantFieldX,
    float HomeChantFieldY,
    float ReactionAge = -1f,
    float ReactionStrength = 0f,
    float ReactionFieldY = 0.5f,
    bool ReactionForHome = true,
    float AnimationTime = 0f);
