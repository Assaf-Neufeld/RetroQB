using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

/// <summary>
/// Situational context passed to the defense factory for scheme selection.
/// </summary>
public readonly record struct DefensiveContext(
    float LineOfScrimmage,
    float Distance,
    int Down,
    int Score,
    int AwayScore,
    SeasonStage Stage);

public interface IDefenseFactory
{
    DefenseResult CreateDefense(DefensiveContext context, DefensiveCallDecision call, List<Receiver> receivers, Random rng, DefensiveTeamAttributes? teamAttributes = null);
}

public sealed class DefenseResult
{
    public List<Defender> Defenders { get; init; } = new();
    public bool UsesZoneResponsibilities { get; init; }
    public bool IsUnderneathManCoverage { get; init; }
    public List<string> Blitzers { get; init; } = new();
    public CoverageScheme Scheme { get; init; }
}

public sealed class DefenseFactory : IDefenseFactory
{
    /// <summary>
    /// Maximum horizontal jitter (in world units) applied to zone anchors per play.
    /// </summary>
    private const float MaxZoneJitter = 1.8f;
    private const float DbMinX = 0.75f;

    public DefenseResult CreateDefense(DefensiveContext context, DefensiveCallDecision call, List<Receiver> receivers, Random rng, DefensiveTeamAttributes? teamAttributes = null)
    {
        var attrs = teamAttributes ?? DefensiveTeamAttributes.Default;
        var defenders = new List<Defender>();

        ResolveCoverageIndices(receivers, out int left, out int leftSlot, out int middle, out int rightSlot, out int right);
        OffensiveSurface surface = DefensiveSurfaceAnalyzer.Analyze(receivers, context.LineOfScrimmage);

        // Use the pre-decided scheme and blitz from the coordinator
        CoverageScheme scheme = call.Scheme;
        BlitzDecision blitzDecision = call.Blitz;
        bool hasNickelPackage = ShouldUseNickelPackage(surface);

        bool usesZoneResponsibilities = CoverageSchemePolicies.UsesZoneResponsibilities(scheme);
        bool isUnderneathManCoverage = CoverageSchemePolicies.IsUnderneathManCoverage(scheme);

        float maxY = Constants.FieldLength - 1f;
        bool isGoalLineSituation = context.LineOfScrimmage >= FieldGeometry.OpponentGoalLine - 2.5f;
        float minY = isGoalLineSituation
            ? MathF.Min(FieldGeometry.OpponentGoalLine + 0.2f, maxY)
            : MathF.Min(context.LineOfScrimmage + 0.35f, maxY);
        float availableDepth = maxY - context.LineOfScrimmage;
        float depthScale = MathF.Max(availableDepth < 18f ? availableDepth / 18f : 1f, 0.3f);
        float frontCenterX = GetFrontCenterX(surface);
        float leftBoxX = GetBoxAlignmentX(receivers, surface, leftSide: true, Constants.FieldWidth * 0.40f);
        float rightBoxX = GetBoxAlignmentX(receivers, surface, leftSide: false, Constants.FieldWidth * 0.60f);
        float mikeX = GetMikeAlignmentX(surface, leftBoxX, rightBoxX);

        // Defensive line - DEs on the outside (circular rush), DTs inside (straight rush)
        float dlDepth = ClampDefenderY(context.LineOfScrimmage + GetSituationalDepthOffset(1.8f, 1.2f, depthScale), minY, maxY);
        defenders.Add(new Defender(new Vector2(ClampDbX(frontCenterX - 5.3f), dlDepth), DefensivePosition.DE, DefenderSlot.DE1, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -5.0f });
        defenders.Add(new Defender(new Vector2(ClampDbX(frontCenterX - 2.1f), dlDepth), DefensivePosition.DL, DefenderSlot.DT1, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -2.0f });
        defenders.Add(new Defender(new Vector2(ClampDbX(frontCenterX + 2.1f), dlDepth), DefensivePosition.DL, DefenderSlot.DT2, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 2.0f });
        defenders.Add(new Defender(new Vector2(ClampDbX(frontCenterX + 5.3f), dlDepth), DefensivePosition.DE, DefenderSlot.DE2, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 5.0f });

        float lbDepth = ClampDefenderY(context.LineOfScrimmage + GetSituationalDepthOffset(7.6f, 4.8f, depthScale), minY, maxY);
        CoverageRoleSet lbRoles = CoverageSchemePolicies.GetLbRoles(scheme, hasNickelPackage);

        defenders.Add(new Defender(new Vector2(leftBoxX, lbDepth), DefensivePosition.LB, DefenderSlot.OLB1, attrs)
        {
            IsRusher = blitzDecision.IsBlitzer(DefenderSlot.OLB1),
            CoverageReceiverIndex = CoverageSchemePolicies.IsManForUnit(scheme, CoverageUnitType.Linebacker) ? leftSlot : -1,
            ZoneRole = lbRoles.Left,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = -7.0f
        });
        if (!hasNickelPackage)
        {
            defenders.Add(new Defender(new Vector2(mikeX, lbDepth), DefensivePosition.LB, DefenderSlot.MLB, attrs)
            {
                IsRusher = blitzDecision.IsBlitzer(DefenderSlot.MLB),
                CoverageReceiverIndex = CoverageSchemePolicies.IsManForUnit(scheme, CoverageUnitType.Linebacker) ? middle : -1,
                ZoneRole = lbRoles.Middle,
                ZoneJitterX = GetJitter(rng),
                RushLaneOffsetX = 0f
            });
        }
        defenders.Add(new Defender(new Vector2(rightBoxX, lbDepth), DefensivePosition.LB, DefenderSlot.OLB2, attrs)
        {
            IsRusher = blitzDecision.IsBlitzer(DefenderSlot.OLB2),
            CoverageReceiverIndex = CoverageSchemePolicies.IsManForUnit(scheme, CoverageUnitType.Linebacker) ? rightSlot : -1,
            ZoneRole = lbRoles.Right,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = 7.0f
        });

        // DBs - positioning and roles vary by scheme
        var dbConfig = GetDbConfiguration(scheme, context.LineOfScrimmage, depthScale, minY, maxY,
            receivers, surface, hasNickelPackage, rng);

        // Cornerbacks
        defenders.Add(new Defender(new Vector2(dbConfig.LeftCbX, dbConfig.LeftCbDepth), DefensivePosition.DB, DefenderSlot.CB1, attrs)
        {
            IsRusher = blitzDecision.IsBlitzer(DefenderSlot.CB1),
            CoverageReceiverIndex = left,
            ZoneRole = dbConfig.LeftCbZone,
            IsPressCoverage = dbConfig.CbPress,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = -10.0f
        });
        defenders.Add(new Defender(new Vector2(dbConfig.RightCbX, dbConfig.RightCbDepth), DefensivePosition.DB, DefenderSlot.CB2, attrs)
        {
            IsRusher = blitzDecision.IsBlitzer(DefenderSlot.CB2),
            CoverageReceiverIndex = right,
            ZoneRole = dbConfig.RightCbZone,
            IsPressCoverage = dbConfig.CbPress,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = 10.0f
        });

        // Nickel DB (extra DB replacing MLB in nickel packages)
        if (hasNickelPackage)
        {
            int nickelTarget = GetNickelTarget(surface);
            float nickelX = dbConfig.NickelX >= 0
                ? dbConfig.NickelX
                : GetReceiverXOrDefault(receivers, nickelTarget, GetMiddleFieldAlignmentX(surface));

            defenders.Add(new Defender(new Vector2(nickelX, dbConfig.NickelDepth), DefensivePosition.DB, DefenderSlot.NB, attrs)
            {
                IsRusher = blitzDecision.IsBlitzer(DefenderSlot.NB),
                CoverageReceiverIndex = nickelTarget,
                ZoneRole = dbConfig.NickelZone,
                IsPressCoverage = false,
                ZoneJitterX = GetJitter(rng),
                RushLaneOffsetX = 0f
            });
        }

        // Safeties
        defenders.Add(new Defender(new Vector2(dbConfig.LeftSafetyX, dbConfig.LeftSafetyDepth), DefensivePosition.DB, DefenderSlot.FS, attrs)
        {
            IsRusher = blitzDecision.IsBlitzer(DefenderSlot.FS),
            CoverageReceiverIndex = CoverageSchemePolicies.IsManForUnit(scheme, CoverageUnitType.Safety) ? leftSlot : -1,
            ZoneRole = dbConfig.LeftSafetyZone,
            IsPressCoverage = false,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = -3.5f
        });
        defenders.Add(new Defender(new Vector2(dbConfig.RightSafetyX, dbConfig.RightSafetyDepth), DefensivePosition.DB, DefenderSlot.SS, attrs)
        {
            IsRusher = blitzDecision.IsBlitzer(DefenderSlot.SS),
            CoverageReceiverIndex = CoverageSchemePolicies.IsManForUnit(scheme, CoverageUnitType.Safety) ? rightSlot : -1,
            ZoneRole = dbConfig.RightSafetyZone,
            IsPressCoverage = false,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = 3.5f
        });

        ApplyUniqueCoverageAssignments(scheme, defenders, receivers);

        DefensePostProcessor.ApplyStarPlayers(defenders, context.Stage);

        return new DefenseResult
        {
            Defenders = defenders,
            UsesZoneResponsibilities = usesZoneResponsibilities,
            IsUnderneathManCoverage = isUnderneathManCoverage,
            Blitzers = DefensePostProcessor.BuildBlitzerSummary(defenders),
            Scheme = scheme
        };
    }

    private static bool ShouldUseNickelPackage(OffensiveSurface surface) => surface.IsSpread && !surface.IsHeavy;


    // --- DB configuration per scheme ---

    private readonly struct DbConfig
    {
        // Cornerbacks
        public readonly float LeftCbX, LeftCbDepth;
        public readonly float RightCbX, RightCbDepth;
        public readonly CoverageRole LeftCbZone, RightCbZone;
        public readonly bool CbPress;
        // Safeties
        public readonly float LeftSafetyX, LeftSafetyDepth;
        public readonly float RightSafetyX, RightSafetyDepth;
        public readonly CoverageRole LeftSafetyZone, RightSafetyZone;
        // Nickel
        public readonly float NickelX, NickelDepth;
        public readonly CoverageRole NickelZone;

        public DbConfig(
            float leftCbX, float leftCbDepth, CoverageRole leftCbZone,
            float rightCbX, float rightCbDepth, CoverageRole rightCbZone,
            bool cbPress,
            float leftSafetyX, float leftSafetyDepth, CoverageRole leftSafetyZone,
            float rightSafetyX, float rightSafetyDepth, CoverageRole rightSafetyZone,
            float nickelX, float nickelDepth, CoverageRole nickelZone)
        {
            LeftCbX = leftCbX; LeftCbDepth = leftCbDepth; LeftCbZone = leftCbZone;
            RightCbX = rightCbX; RightCbDepth = rightCbDepth; RightCbZone = rightCbZone;
            CbPress = cbPress;
            LeftSafetyX = leftSafetyX; LeftSafetyDepth = leftSafetyDepth; LeftSafetyZone = leftSafetyZone;
            RightSafetyX = rightSafetyX; RightSafetyDepth = rightSafetyDepth; RightSafetyZone = rightSafetyZone;
            NickelX = nickelX; NickelDepth = nickelDepth; NickelZone = nickelZone;
        }
    }

    private static DbConfig GetDbConfiguration(
        CoverageScheme scheme, float lineOfScrimmage, float depthScale, float minY, float maxY,
        List<Receiver> receivers, OffensiveSurface surface, bool hasNickelPackage,
        Random rng)
    {
        float fw = Constants.FieldWidth;
        bool cover1Press = rng.NextDouble() < 0.6;
        bool robberPress = rng.NextDouble() < 0.78;
        bool cover2ManPress = rng.NextDouble() < 0.4;

        // Common depth calculations
        float pressDepth = ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(3.5f, 2.2f, depthScale), minY, maxY);
        float offCbDepth = ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(8.0f, 5.4f, depthScale), minY, maxY);
        float shallowSafetyDepth = ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(10.0f, 6.8f, depthScale), minY, maxY);
        float deepSafetyDepth = ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(16.0f, 10.5f, depthScale), minY, maxY);
        float robberDepth = ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(7.0f, 4.6f, depthScale), minY, maxY);

        float leftOutsideX = GetOutsideAlignmentX(receivers, surface, leftSide: true, fw * 0.18f, boundaryShade: 0.28f);
        float rightOutsideX = GetOutsideAlignmentX(receivers, surface, leftSide: false, fw * 0.82f, boundaryShade: 0.28f);
        float leftFlatX = GetOutsideAlignmentX(receivers, surface, leftSide: true, fw * 0.14f, boundaryShade: 0.95f);
        float rightFlatX = GetOutsideAlignmentX(receivers, surface, leftSide: false, fw * 0.86f, boundaryShade: 0.95f);
        float leftApexX = GetApexAlignmentX(receivers, surface, leftSide: true, fw * 0.28f);
        float rightApexX = GetApexAlignmentX(receivers, surface, leftSide: false, fw * 0.72f);
        float leftDeepHalfX = GetDeepHalfAlignmentX(receivers, surface, leftSide: true, fw * 0.35f);
        float rightDeepHalfX = GetDeepHalfAlignmentX(receivers, surface, leftSide: false, fw * 0.65f);
        float leftQuarterX = GetQuarterAlignmentX(receivers, surface, leftSide: true, fw * 0.40f);
        float rightQuarterX = GetQuarterAlignmentX(receivers, surface, leftSide: false, fw * 0.60f);
        float middleFieldX = GetMiddleFieldAlignmentX(surface);
        float strongInsideX = GetStrongInsideAlignmentX(receivers, surface);
        float robberX = GetRobberAlignmentX(surface, middleFieldX, strongInsideX);
        float matchNickelX = GetMatchNickelAlignmentX(surface, middleFieldX, strongInsideX);

        return scheme switch
        {
            // Cover 0: All man, press, no deep help
            CoverageScheme.Cover0 => new DbConfig(
                leftCbX: leftOutsideX,
                leftCbDepth: pressDepth,
                leftCbZone: CoverageRole.None,
                rightCbX: rightOutsideX,
                rightCbDepth: pressDepth,
                rightCbZone: CoverageRole.None,
                cbPress: true,
                leftSafetyX: leftApexX,
                leftSafetyDepth: shallowSafetyDepth,
                leftSafetyZone: CoverageRole.None,
                rightSafetyX: rightApexX,
                rightSafetyDepth: shallowSafetyDepth,
                rightSafetyZone: CoverageRole.None,
                nickelX: hasNickelPackage ? strongInsideX : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.None
            ),

            // Cover 1: Man with single-high free safety
            CoverageScheme.Cover1 => new DbConfig(
                leftCbX: leftOutsideX,
                leftCbDepth: cover1Press ? pressDepth : offCbDepth,
                leftCbZone: CoverageRole.None,
                rightCbX: rightOutsideX,
                rightCbDepth: cover1Press ? pressDepth : offCbDepth,
                rightCbZone: CoverageRole.None,
                cbPress: cover1Press,
                // Free safety stays high in the deep middle.
                leftSafetyX: middleFieldX,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepMiddle,
                // Strong safety plays a shallow robber/hook role instead of defaulting at the QB.
                rightSafetyX: robberX,
                rightSafetyDepth: shallowSafetyDepth,
                rightSafetyZone: CoverageRole.HookMiddle,
                nickelX: hasNickelPackage ? strongInsideX : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.None
            ),

            // Cover 2 Zone: Two deep halves, CBs in flat zones, classic two-high
            CoverageScheme.Cover2Zone => new DbConfig(
                leftCbX: leftFlatX,
                leftCbDepth: offCbDepth,
                leftCbZone: CoverageRole.FlatLeft,
                rightCbX: rightFlatX,
                rightCbDepth: offCbDepth,
                rightCbZone: CoverageRole.FlatRight,
                cbPress: false,
                leftSafetyX: leftDeepHalfX,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepLeft,
                rightSafetyX: rightDeepHalfX,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepRight,
                nickelX: hasNickelPackage ? middleFieldX : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.HookMiddle
            ),

            // Cover 3 Zone: Three deep thirds, SS drops to flat
            CoverageScheme.Cover3Zone => new DbConfig(
                // CBs line up over the outer WRs, then bail to deep thirds post-snap.
                leftCbX: leftOutsideX,
                leftCbDepth: offCbDepth,
                leftCbZone: CoverageRole.DeepLeft,
                rightCbX: rightOutsideX,
                rightCbDepth: offCbDepth,
                rightCbZone: CoverageRole.DeepRight,
                cbPress: false,
                // Nickel looks can widen both flats; base looks roll one safety down to strength.
                leftSafetyX: hasNickelPackage ? leftApexX : strongInsideX,
                leftSafetyDepth: hasNickelPackage ? robberDepth : robberDepth,
                leftSafetyZone: hasNickelPackage ? CoverageRole.FlatLeft : CoverageRole.Robber,
                rightSafetyX: middleFieldX,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepMiddle,
                nickelX: hasNickelPackage ? rightApexX : -1,
                nickelDepth: robberDepth,
                nickelZone: CoverageRole.FlatRight
            ),

            // Cover 4 Zone: Four deep quarters, NB drops to hook underneath
            CoverageScheme.Cover4Zone => new DbConfig(
                // Corners align over the widest WRs and open to quarter coverage after the snap.
                leftCbX: leftOutsideX,
                leftCbDepth: offCbDepth,
                leftCbZone: CoverageRole.DeepLeft,
                rightCbX: rightOutsideX,
                rightCbDepth: offCbDepth,
                rightCbZone: CoverageRole.DeepRight,
                cbPress: false,
                // Safeties take inside deep-quarter zones
                leftSafetyX: leftQuarterX,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepQuarterLeft,
                rightSafetyX: rightQuarterX,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepQuarterRight,
                nickelX: hasNickelPackage ? middleFieldX : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.HookMiddle
            ),

            // Cover 3 Match: three-deep shell with a shallow robber helping on crossers.
            CoverageScheme.Cover3Match => new DbConfig(
                leftCbX: leftOutsideX,
                leftCbDepth: offCbDepth,
                leftCbZone: CoverageRole.DeepLeft,
                rightCbX: rightOutsideX,
                rightCbDepth: offCbDepth,
                rightCbZone: CoverageRole.DeepRight,
                cbPress: false,
                leftSafetyX: middleFieldX,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepMiddle,
                rightSafetyX: robberX,
                rightSafetyDepth: ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(8.2f, 5.6f, depthScale), minY, maxY),
                rightSafetyZone: CoverageRole.Robber,
                nickelX: hasNickelPackage ? matchNickelX : -1f,
                nickelDepth: shallowSafetyDepth,
                nickelZone: hasNickelPackage ? CoverageRole.HookMiddle : CoverageRole.None
            ),

            // Quarters Match: four deep defenders with linebackers carrying underneath routes.
            CoverageScheme.QuartersMatch => new DbConfig(
                leftCbX: leftOutsideX,
                leftCbDepth: offCbDepth,
                leftCbZone: CoverageRole.DeepLeft,
                rightCbX: rightOutsideX,
                rightCbDepth: offCbDepth,
                rightCbZone: CoverageRole.DeepRight,
                cbPress: false,
                leftSafetyX: leftQuarterX,
                leftSafetyDepth: ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(14.2f, 9.4f, depthScale), minY, maxY),
                leftSafetyZone: CoverageRole.DeepQuarterLeft,
                rightSafetyX: rightQuarterX,
                rightSafetyDepth: ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(14.2f, 9.4f, depthScale), minY, maxY),
                rightSafetyZone: CoverageRole.DeepQuarterRight,
                nickelX: hasNickelPackage ? middleFieldX : -1f,
                nickelDepth: shallowSafetyDepth,
                nickelZone: hasNickelPackage ? CoverageRole.HookMiddle : CoverageRole.None
            ),

            // Cover 2 Man: Two deep safeties (zone), CBs and LBs play man underneath
            CoverageScheme.Cover2Man => new DbConfig(
                leftCbX: leftOutsideX,
                leftCbDepth: cover2ManPress ? pressDepth : offCbDepth,
                leftCbZone: CoverageRole.None,
                rightCbX: rightOutsideX,
                rightCbDepth: cover2ManPress ? pressDepth : offCbDepth,
                rightCbZone: CoverageRole.None,
                cbPress: cover2ManPress,
                leftSafetyX: leftDeepHalfX,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepLeft,
                rightSafetyX: rightDeepHalfX,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepRight,
                nickelX: hasNickelPackage ? strongInsideX : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.None
            ),

            // Robber: tight outside man with a low-hole safety poaching the middle.
            CoverageScheme.Robber => new DbConfig(
                leftCbX: leftOutsideX,
                leftCbDepth: robberPress ? pressDepth : offCbDepth,
                leftCbZone: CoverageRole.None,
                rightCbX: rightOutsideX,
                rightCbDepth: robberPress ? pressDepth : offCbDepth,
                rightCbZone: CoverageRole.None,
                cbPress: robberPress,
                leftSafetyX: middleFieldX,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepMiddle,
                rightSafetyX: robberX,
                rightSafetyDepth: robberDepth,
                rightSafetyZone: CoverageRole.Robber,
                nickelX: hasNickelPackage ? strongInsideX : -1f,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.None
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(scheme))
        };
    }

    // --- Jitter ---

    private static float GetJitter(Random rng)
    {
        return ((float)rng.NextDouble() * 2f - 1f) * MaxZoneJitter;
    }

    // --- Utility ---

    private static float ClampDefenderY(float y, float minY, float maxY)
    {
        return Math.Clamp(y, minY, maxY);
    }

    private static float GetSituationalDepthOffset(float baseOffset, float minOffset, float depthScale)
    {
        return minOffset + (baseOffset - minOffset) * depthScale;
    }

    private static void ResolveCoverageIndices(IReadOnlyList<Receiver> receivers, out int left, out int leftSlot, out int middle, out int rightSlot, out int right)
    {
        var coverageReceivers = receivers
            .Where(receiver => receiver.Eligible)
            .ToList();

        if (coverageReceivers.Count == 0)
        {
            coverageReceivers = receivers.ToList();
        }

        if (coverageReceivers.Count == 0)
        {
            left = leftSlot = middle = rightSlot = right = -1;
            return;
        }

        var ordered = coverageReceivers
            .OrderBy(receiver => receiver.Position.X)
            .Select(item => item.Index)
            .ToList();

        left = ordered[0];
        right = ordered[^1];
        middle = ordered.Count >= 3 ? ordered[ordered.Count / 2] : -1;
        leftSlot = ordered.Count >= 4 ? ordered[1] : -1;
        rightSlot = ordered.Count >= 4 ? ordered[^2] : -1;
    }

    private static float GetReceiverXOrDefault(IReadOnlyList<Receiver> receivers, int receiverIndex, float fallbackX)
    {
        if (receiverIndex >= 0 && receiverIndex < receivers.Count)
        {
            return receivers[receiverIndex].Position.X;
        }

        return fallbackX;
    }

    private static int GetNickelTarget(OffensiveSurface surface)
    {
        int strongInside = GetStrongInsideReceiverIndex(surface);
        if (strongInside >= 0)
        {
            return strongInside;
        }

        if (surface.MiddleReceiverIndex >= 0)
        {
            return surface.MiddleReceiverIndex;
        }

        return surface.Strength == FormationStrength.Left
            ? surface.LeftWideReceiverIndex
            : surface.RightWideReceiverIndex;
    }

    private static int GetStrongInsideReceiverIndex(OffensiveSurface surface)
    {
        if (surface.IsHeavy)
        {
            return surface.Strength switch
            {
                FormationStrength.Left when surface.LeftInsideReceiverIndex >= 0 => surface.LeftInsideReceiverIndex,
                FormationStrength.Right when surface.RightInsideReceiverIndex >= 0 => surface.RightInsideReceiverIndex,
                _ => -1
            };
        }

        return surface.Strength switch
        {
            FormationStrength.Left when surface.LeftInsideReceiverIndex >= 0 => surface.LeftInsideReceiverIndex,
            FormationStrength.Right when surface.RightInsideReceiverIndex >= 0 => surface.RightInsideReceiverIndex,
            _ when surface.MiddleReceiverIndex >= 0 => surface.MiddleReceiverIndex,
            _ when surface.LeftInsideReceiverIndex >= 0 => surface.LeftInsideReceiverIndex,
            _ => surface.RightInsideReceiverIndex
        };
    }

    private static float GetOutsideAlignmentX(
        IReadOnlyList<Receiver> receivers,
        OffensiveSurface surface,
        bool leftSide,
        float fallbackX,
        float boundaryShade)
    {
        int receiverIndex = leftSide ? surface.LeftWideReceiverIndex : surface.RightWideReceiverIndex;
        float receiverX = GetReceiverXOrDefault(receivers, receiverIndex, fallbackX);
        return ShadeTowardBoundary(receiverX, leftSide, boundaryShade);
    }

    private static float GetInsideAlignmentX(
        IReadOnlyList<Receiver> receivers,
        OffensiveSurface surface,
        bool leftSide,
        float fallbackX)
    {
        int receiverIndex = leftSide ? surface.LeftInsideReceiverIndex : surface.RightInsideReceiverIndex;
        if (receiverIndex < 0)
        {
            receiverIndex = surface.MiddleReceiverIndex;
        }

        return ClampDbX(GetReceiverXOrDefault(receivers, receiverIndex, fallbackX));
    }

    private static float GetApexAlignmentX(
        IReadOnlyList<Receiver> receivers,
        OffensiveSurface surface,
        bool leftSide,
        float fallbackX)
    {
        float outsideX = GetOutsideAlignmentX(receivers, surface, leftSide, fallbackX, boundaryShade: 0f);
        float insideX = GetInsideAlignmentX(receivers, surface, leftSide, fallbackX);

        if (MathF.Abs(outsideX - insideX) < 1.25f)
        {
            return insideX;
        }

        float influence = surface.IsHeavy ? 0.78f : 0.58f;
        return ClampDbX(Blend(outsideX, insideX, influence));
    }

    private static float GetDeepHalfAlignmentX(
        IReadOnlyList<Receiver> receivers,
        OffensiveSurface surface,
        bool leftSide,
        float fallbackX)
    {
        float shellX = fallbackX;
        float apexX = GetApexAlignmentX(receivers, surface, leftSide, fallbackX);
        float outsideX = GetOutsideAlignmentX(receivers, surface, leftSide, fallbackX, boundaryShade: 0f);
        float laneX = Blend(outsideX, apexX, 0.50f);
        float influence = surface.IsHeavy ? 0.18f : surface.IsSpread ? 0.55f : 0.35f;
        return ClampDbX(Blend(shellX, laneX, influence));
    }

    private static float GetQuarterAlignmentX(
        IReadOnlyList<Receiver> receivers,
        OffensiveSurface surface,
        bool leftSide,
        float fallbackX)
    {
        float shellX = fallbackX;
        float insideX = GetInsideAlignmentX(receivers, surface, leftSide, fallbackX);
        float influence = surface.IsHeavy ? 0.24f : surface.IsSpread ? 0.72f : 0.50f;
        return ClampDbX(Blend(shellX, insideX, influence));
    }

    private static float GetMiddleFieldAlignmentX(OffensiveSurface surface)
    {
        if (!surface.IsSpread)
        {
            // Keep shells neutral unless offense is truly spread detached across the field.
            return Constants.FieldWidth * 0.50f;
        }

        float influence = 0.45f;
        return ClampDbX(Blend(Constants.FieldWidth * 0.50f, surface.CenterX, influence));
    }

    private static float GetStrongInsideAlignmentX(IReadOnlyList<Receiver> receivers, OffensiveSurface surface)
    {
        int receiverIndex = GetStrongInsideReceiverIndex(surface);
        float fallbackX = surface.Strength switch
        {
            FormationStrength.Left => Constants.FieldWidth * 0.44f,
            FormationStrength.Right => Constants.FieldWidth * 0.56f,
            _ => Constants.FieldWidth * 0.50f
        };

        return ClampDbX(GetReceiverXOrDefault(receivers, receiverIndex, fallbackX));
    }

    private static float GetFrontCenterX(OffensiveSurface surface)
    {
        float middleFieldX = GetMiddleFieldAlignmentX(surface);
        if (!surface.IsSpread)
        {
            // Prevent full-front lateral drifting in tight/base formations.
            return middleFieldX;
        }

        float strengthOffset = surface.Strength switch
        {
            FormationStrength.Left => -0.85f,
            FormationStrength.Right => 0.85f,
            _ => 0f
        };

        float countBiasScale = 0.22f;
        float countBias = (surface.RightDetachedCount - surface.LeftDetachedCount) * countBiasScale;
        return ClampDbX(middleFieldX + strengthOffset + countBias);
    }

    private static float GetBoxAlignmentX(
        IReadOnlyList<Receiver> receivers,
        OffensiveSurface surface,
        bool leftSide,
        float fallbackX)
    {
        if (!surface.IsSpread)
        {
            return ClampDbX(fallbackX);
        }

        float apexX = GetApexAlignmentX(receivers, surface, leftSide, fallbackX);
        float insideX = GetInsideAlignmentX(receivers, surface, leftSide, fallbackX);
        int detachedCount = leftSide ? surface.LeftDetachedCount : surface.RightDetachedCount;
        bool isStrengthSide = (leftSide && surface.Strength == FormationStrength.Left)
            || (!leftSide && surface.Strength == FormationStrength.Right);

        float targetX = surface.IsHeavy ? insideX : detachedCount >= 2 ? apexX : insideX;
        float influence = surface.IsHeavy
            ? isStrengthSide ? 0.34f : 0.18f
            : detachedCount >= 2 ? 0.70f : detachedCount == 1 ? 0.48f : 0.28f;

        if (isStrengthSide && !surface.IsHeavy)
        {
            influence += 0.10f;
        }

        return ClampDbX(Blend(fallbackX, targetX, influence));
    }

    private static float GetMikeAlignmentX(OffensiveSurface surface, float leftBoxX, float rightBoxX)
    {
        float baseX = GetMiddleFieldAlignmentX(surface);
        if (!surface.IsSpread)
        {
            return baseX;
        }

        float boxMidX = Blend(leftBoxX, rightBoxX, 0.50f);
        float influence = surface.IsHeavy
            ? surface.Strength == FormationStrength.Balanced ? 0.20f : 0.32f
            : surface.Strength == FormationStrength.Balanced ? 0.35f : 0.55f;
        return ClampDbX(Blend(baseX, boxMidX, influence));
    }

    private static float GetRobberAlignmentX(OffensiveSurface surface, float middleFieldX, float strongInsideX)
    {
        if (!surface.IsSpread)
        {
            return middleFieldX;
        }

        int maxDetachedSurfaceCount = Math.Max(surface.LeftDetachedCount, surface.RightDetachedCount);
        float influence = maxDetachedSurfaceCount >= 3
            ? 0.58f
            : surface.IsHeavy ? 0.18f : surface.IsSpread ? 0.42f : 0.30f;

        if (surface.Strength == FormationStrength.Balanced)
        {
            influence *= 0.7f;
        }

        return ClampDbX(Blend(middleFieldX, strongInsideX, influence));
    }

    private static float GetMatchNickelAlignmentX(OffensiveSurface surface, float middleFieldX, float strongInsideX)
    {
        if (!surface.IsSpread)
        {
            return middleFieldX;
        }

        int maxDetachedSurfaceCount = Math.Max(surface.LeftDetachedCount, surface.RightDetachedCount);
        float influence = surface.IsHeavy ? 0.24f : maxDetachedSurfaceCount >= 3 ? 0.82f : surface.IsSpread ? 0.62f : 0.45f;
        return ClampDbX(Blend(middleFieldX, strongInsideX, influence));
    }

    private static float ShadeTowardBoundary(float x, bool leftSide, float amount)
    {
        return ClampDbX(x + (leftSide ? -amount : amount));
    }

    private static float ClampDbX(float x)
    {
        return Math.Clamp(x, DbMinX, Constants.FieldWidth - DbMinX);
    }

    private static float Blend(float start, float end, float weight)
    {
        return start + (end - start) * weight;
    }

    private static void ApplyUniqueCoverageAssignments(CoverageScheme scheme, List<Defender> defenders, IReadOnlyList<Receiver> receivers)
    {
        var coverageReceivers = receivers
            .Where(receiver => receiver.Eligible)
            .ToList();

        if (coverageReceivers.Count == 0)
        {
            return;
        }

        var candidates = GetCoverageAssignmentCandidates(scheme, defenders);
        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            candidate.Defender.CoverageReceiverIndex = -1;
            candidate.Defender.ZoneRole = candidate.OriginalZoneRole;
        }

        var remainingCandidates = new List<CoverageAssignmentCandidate>(candidates);
        var availableReceivers = coverageReceivers.Select(receiver => receiver.Index).ToHashSet();

        while (remainingCandidates.Count > 0 && availableReceivers.Count > 0)
        {
            CoverageAssignmentCandidate? bestCandidate = null;
            int bestReceiverIndex = -1;
            float bestDistSq = float.MaxValue;

            foreach (CoverageAssignmentCandidate candidate in remainingCandidates)
            {
                foreach (int receiverIndex in availableReceivers)
                {
                    float distSq = Vector2.DistanceSquared(candidate.Defender.Position, receivers[receiverIndex].Position);
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestCandidate = candidate;
                        bestReceiverIndex = receiverIndex;
                    }
                }
            }

            if (bestCandidate is null || bestReceiverIndex < 0)
            {
                break;
            }

            bestCandidate.Defender.CoverageReceiverIndex = bestReceiverIndex;
            if (bestCandidate.ClearZoneRoleOnAssignment)
            {
                bestCandidate.Defender.ZoneRole = CoverageRole.None;
            }

            remainingCandidates.Remove(bestCandidate);
            availableReceivers.Remove(bestReceiverIndex);
        }

        foreach (CoverageAssignmentCandidate candidate in remainingCandidates)
        {
            candidate.Defender.CoverageReceiverIndex = -1;
            candidate.Defender.ZoneRole = candidate.OriginalZoneRole;
        }
    }

    private static List<CoverageAssignmentCandidate> GetCoverageAssignmentCandidates(CoverageScheme scheme, IReadOnlyList<Defender> defenders)
    {
        return scheme switch
        {
            CoverageScheme.Cover0 or CoverageScheme.Cover1 or CoverageScheme.Cover2Man or CoverageScheme.Robber => defenders
                .Where(defender => !defender.IsRusher
                    && defender.CoverageReceiverIndex >= 0
                    && defender.ZoneRole == CoverageRole.None)
                .Select(defender => new CoverageAssignmentCandidate(defender, defender.ZoneRole, ClearZoneRoleOnAssignment: false))
                .ToList(),

            CoverageScheme.Cover3Match or CoverageScheme.QuartersMatch => defenders
                .Where(defender => !defender.IsRusher
                    && defender.PositionRole is DefensivePosition.LB or DefensivePosition.DB
                    && !IsDeepZoneRole(defender.ZoneRole))
                .Select(defender => new CoverageAssignmentCandidate(defender, defender.ZoneRole, ClearZoneRoleOnAssignment: true))
                .ToList(),

            _ => new List<CoverageAssignmentCandidate>()
        };
    }

    private sealed record CoverageAssignmentCandidate(
        Defender Defender,
        CoverageRole OriginalZoneRole,
        bool ClearZoneRoleOnAssignment);

    private static bool IsDeepZoneRole(CoverageRole role)
    {
        return role is CoverageRole.DeepLeft
            or CoverageRole.DeepMiddle
            or CoverageRole.DeepRight
            or CoverageRole.DeepQuarterLeft
            or CoverageRole.DeepQuarterRight;
    }
}
