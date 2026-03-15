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
    public bool IsZoneCoverage { get; init; }
    public List<string> Blitzers { get; init; } = new();
    public CoverageScheme Scheme { get; init; }
}

public sealed class DefenseFactory : IDefenseFactory
{
    private enum CoverageUnit
    {
        Linebacker,
        Safety
    }

    /// <summary>
    /// Maximum horizontal jitter (in world units) applied to zone anchors per play.
    /// </summary>
    private const float MaxZoneJitter = 1.8f;

    public DefenseResult CreateDefense(DefensiveContext context, DefensiveCallDecision call, List<Receiver> receivers, Random rng, DefensiveTeamAttributes? teamAttributes = null)
    {
        var attrs = teamAttributes ?? DefensiveTeamAttributes.Default;
        var defenders = new List<Defender>();

        ResolveCoverageIndices(receivers, out int left, out int leftSlot, out int middle, out int rightSlot, out int right);

        // Use the pre-decided scheme and blitz from the coordinator
        CoverageScheme scheme = call.Scheme;
        BlitzDecision blitzDecision = call.Blitz;
        bool hasNickelPackage = UsesNickelPackage(scheme);

        bool useZone = IsZoneScheme(scheme);

        float maxY = Constants.FieldLength - 1f;
        bool isGoalLineSituation = context.LineOfScrimmage >= FieldGeometry.OpponentGoalLine - 2.5f;
        float minY = isGoalLineSituation
            ? MathF.Min(FieldGeometry.OpponentGoalLine + 0.2f, maxY)
            : MathF.Min(context.LineOfScrimmage + 0.35f, maxY);
        float availableDepth = maxY - context.LineOfScrimmage;
        float depthScale = MathF.Max(availableDepth < 18f ? availableDepth / 18f : 1f, 0.3f);

        // Defensive line - DEs on the outside (circular rush), DTs inside (straight rush)
        float dlDepth = ClampDefenderY(context.LineOfScrimmage + GetSituationalDepthOffset(1.8f, 1.2f, depthScale), minY, maxY);
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, dlDepth), DefensivePosition.DE, DefenderSlot.DE1, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -5.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.46f, dlDepth), DefensivePosition.DL, DefenderSlot.DT1, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -2.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.54f, dlDepth), DefensivePosition.DL, DefenderSlot.DT2, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 2.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, dlDepth), DefensivePosition.DE, DefenderSlot.DE2, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 5.0f });

        float lbDepth = ClampDefenderY(context.LineOfScrimmage + GetSituationalDepthOffset(7.6f, 4.8f, depthScale), minY, maxY);
        var lbRoles = GetLbZoneRoles(scheme);

        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, lbDepth), DefensivePosition.LB, DefenderSlot.OLB1, attrs)
        {
            IsRusher = blitzDecision.IsBlitzer(DefenderSlot.OLB1),
            CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Linebacker) ? leftSlot : -1,
            ZoneRole = lbRoles.left,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = -7.0f
        });
        if (!hasNickelPackage)
        {
            defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.50f, lbDepth), DefensivePosition.LB, DefenderSlot.MLB, attrs)
            {
                IsRusher = blitzDecision.IsBlitzer(DefenderSlot.MLB),
                CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Linebacker) ? middle : -1,
                ZoneRole = lbRoles.middle,
                ZoneJitterX = GetJitter(rng),
                RushLaneOffsetX = 0f
            });
        }
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, lbDepth), DefensivePosition.LB, DefenderSlot.OLB2, attrs)
        {
            IsRusher = blitzDecision.IsBlitzer(DefenderSlot.OLB2),
            CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Linebacker) ? rightSlot : -1,
            ZoneRole = lbRoles.right,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = 7.0f
        });

        // DBs - positioning and roles vary by scheme
        var dbConfig = GetDbConfiguration(scheme, context.LineOfScrimmage, depthScale, minY, maxY,
            receivers, left, leftSlot, middle, rightSlot, right, rng);

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
            int nickelTarget = middle >= 0 ? middle : leftSlot >= 0 ? leftSlot : rightSlot;
            float nickelX = dbConfig.NickelX >= 0
                ? dbConfig.NickelX
                : GetReceiverXOrDefault(receivers, nickelTarget, Constants.FieldWidth * 0.50f);

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
            CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Safety) ? leftSlot : -1,
            ZoneRole = dbConfig.LeftSafetyZone,
            IsPressCoverage = false,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = -3.5f
        });
        defenders.Add(new Defender(new Vector2(dbConfig.RightSafetyX, dbConfig.RightSafetyDepth), DefensivePosition.DB, DefenderSlot.SS, attrs)
        {
            IsRusher = blitzDecision.IsBlitzer(DefenderSlot.SS),
            CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Safety) ? rightSlot : -1,
            ZoneRole = dbConfig.RightSafetyZone,
            IsPressCoverage = false,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = 3.5f
        });

        ApplyStarPlayers(defenders, context.Stage);

        return new DefenseResult
        {
            Defenders = defenders,
            IsZoneCoverage = useZone,
            Blitzers = BuildBlitzerSummary(defenders),
            Scheme = scheme
        };
    }

    private static void ApplyStarPlayers(IReadOnlyList<Defender> defenders, SeasonStage stage)
    {
        if (stage == SeasonStage.RegularSeason)
        {
            return;
        }

        if (stage == SeasonStage.Playoff)
        {
            ApplyStarToSlot(defenders, DefenderSlot.FS);
            ApplyStarToSlot(defenders, DefenderSlot.DE1);
            return;
        }

        if (stage == SeasonStage.SuperBowl)
        {
            ApplyStarToSlot(defenders, DefenderSlot.DE1);
            ApplyStarToSlot(defenders, DefenderSlot.DE2);
            ApplyStarToSlot(defenders, DefenderSlot.MLB);
            ApplyStarToSlot(defenders, DefenderSlot.CB1);
            ApplyStarToSlot(defenders, DefenderSlot.FS);
        }
    }

    private static void ApplyStarToSlot(IReadOnlyList<Defender> defenders, DefenderSlot slot)
    {
        Defender? defender = defenders.FirstOrDefault(d => d.Slot == slot);
        if (defender == null)
        {
            return;
        }

        switch (defender.PositionRole)
        {
            case DefensivePosition.DB:
                defender.ApplyStarBoost(speedMultiplier: 1.08f, tackleMultiplier: 1.02f, interceptionMultiplier: 1.40f, blockShedMultiplier: 1.05f);
                break;
            case DefensivePosition.DE:
                defender.ApplyStarBoost(speedMultiplier: 1.10f, tackleMultiplier: 1.10f, interceptionMultiplier: 1.00f, blockShedMultiplier: 1.35f);
                break;
            case DefensivePosition.LB:
                defender.ApplyStarBoost(speedMultiplier: 1.07f, tackleMultiplier: 1.25f, interceptionMultiplier: 1.10f, blockShedMultiplier: 1.20f);
                break;
            default:
                defender.ApplyStarBoost(speedMultiplier: 1.05f, tackleMultiplier: 1.15f, interceptionMultiplier: 1.00f, blockShedMultiplier: 1.25f);
                break;
        }
    }

    private static List<string> BuildBlitzerSummary(IReadOnlyList<Defender> defenders)
    {
        int lbCount = 0;
        int dbCount = 0;

        for (int i = 0; i < defenders.Count; i++)
        {
            Defender defender = defenders[i];
            if (!defender.IsRusher)
            {
                continue;
            }

            if (IsLinebackerSlot(defender.Slot))
            {
                lbCount++;
                continue;
            }

            if (IsDefensiveBackSlot(defender.Slot))
            {
                dbCount++;
                continue;
            }

            // Fallback classification for any non-standard slot mapping.
            switch (defender.PositionRole)
            {
                case DefensivePosition.LB:
                    lbCount++;
                    break;
                case DefensivePosition.DB:
                    dbCount++;
                    break;
            }
        }

        var blitzers = new List<string>(lbCount + dbCount);
        for (int i = 0; i < lbCount; i++)
        {
            blitzers.Add("LB");
        }

        for (int i = 0; i < dbCount; i++)
        {
            blitzers.Add("DB");
        }

        return blitzers;
    }

    private static bool IsLinebackerSlot(DefenderSlot slot)
    {
        return slot is DefenderSlot.MLB or DefenderSlot.OLB1 or DefenderSlot.OLB2;
    }

    private static bool IsDefensiveBackSlot(DefenderSlot slot)
    {
        return slot is DefenderSlot.CB1 or DefenderSlot.CB2 or DefenderSlot.FS or DefenderSlot.SS or DefenderSlot.NB;
    }

    // --- Scheme classification helpers ---

    private static bool IsZoneScheme(CoverageScheme scheme) => scheme switch
    {
        CoverageScheme.Cover2Zone => true,
        CoverageScheme.Cover3Zone => true,
        CoverageScheme.Cover4Zone => true,
        // Cover1 and Cover2Man are hybrid: the zone flag must be true
        // so that defenders with ZoneRole != None use zone logic.
        CoverageScheme.Cover1 => true,
        CoverageScheme.Cover2Man => true,
        _ => false
    };

    private static bool UsesNickelPackage(CoverageScheme scheme)
    {
        return scheme is CoverageScheme.Cover2Zone or CoverageScheme.Cover3Zone or CoverageScheme.Cover4Zone;
    }

    /// <summary>
    /// Returns true if the given unit plays man coverage in this scheme.
    /// </summary>
    private static bool IsManForPosition(CoverageScheme scheme, CoverageUnit unit) => scheme switch
    {
        CoverageScheme.Cover0 => true,
        CoverageScheme.Cover1 => unit != CoverageUnit.Safety,
        CoverageScheme.Cover2Man => unit == CoverageUnit.Linebacker,
        CoverageScheme.Cover2Zone => false,
        CoverageScheme.Cover3Zone => false,
        CoverageScheme.Cover4Zone => false,
        _ => false
    };
    // --- LB zone role assignment per scheme ---

    private static (CoverageRole left, CoverageRole middle, CoverageRole right) GetLbZoneRoles(
        CoverageScheme scheme)
    {
        return scheme switch
        {
            CoverageScheme.Cover0 => (CoverageRole.None, CoverageRole.None, CoverageRole.None),
            CoverageScheme.Cover1 => (CoverageRole.None, CoverageRole.None, CoverageRole.None),
            CoverageScheme.Cover2Man => (CoverageRole.None, CoverageRole.None, CoverageRole.None),
            CoverageScheme.Cover2Zone => (CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight),
            CoverageScheme.Cover3Zone => (CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight),
            CoverageScheme.Cover4Zone => (CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight),
            _ => (CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight)
        };
    }

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
        List<Receiver> receivers, int left, int leftSlot, int middle, int rightSlot, int right,
        Random rng)
    {
        bool hasNickelPackage = UsesNickelPackage(scheme);
        float fw = Constants.FieldWidth;
        bool cover1Press = rng.NextDouble() < 0.6;
        bool cover2ManPress = rng.NextDouble() < 0.4;

        // Common depth calculations
        float pressDepth = ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(3.5f, 2.2f, depthScale), minY, maxY);
        float offCbDepth = ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(8.0f, 5.4f, depthScale), minY, maxY);
        float shallowSafetyDepth = ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(10.0f, 6.8f, depthScale), minY, maxY);
        float deepSafetyDepth = ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(16.0f, 10.5f, depthScale), minY, maxY);

        return scheme switch
        {
            // Cover 0: All man, press, no deep help
            CoverageScheme.Cover0 => new DbConfig(
                leftCbX: GetReceiverXOrDefault(receivers, left, fw * 0.18f),
                leftCbDepth: pressDepth,
                leftCbZone: CoverageRole.None,
                rightCbX: GetReceiverXOrDefault(receivers, right, fw * 0.82f),
                rightCbDepth: pressDepth,
                rightCbZone: CoverageRole.None,
                cbPress: true,
                leftSafetyX: GetReceiverXOrDefault(receivers, leftSlot, fw * 0.40f),
                leftSafetyDepth: shallowSafetyDepth,
                leftSafetyZone: CoverageRole.None,
                rightSafetyX: GetReceiverXOrDefault(receivers, rightSlot, fw * 0.60f),
                rightSafetyDepth: shallowSafetyDepth,
                rightSafetyZone: CoverageRole.None,
                nickelX: hasNickelPackage ? GetReceiverXOrDefault(receivers, middle, fw * 0.50f) : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.None
            ),

            // Cover 1: Man with single-high free safety
            CoverageScheme.Cover1 => new DbConfig(
                leftCbX: GetReceiverXOrDefault(receivers, left, fw * 0.18f),
                leftCbDepth: cover1Press ? pressDepth : offCbDepth,
                leftCbZone: CoverageRole.None,
                rightCbX: GetReceiverXOrDefault(receivers, right, fw * 0.82f),
                rightCbDepth: cover1Press ? pressDepth : offCbDepth,
                rightCbZone: CoverageRole.None,
                cbPress: cover1Press,
                // Free safety stays high in the deep middle.
                leftSafetyX: fw * 0.50f,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepMiddle,
                // Strong safety plays a shallow robber/hook role instead of defaulting at the QB.
                rightSafetyX: GetReceiverXOrDefault(receivers, middle, fw * 0.50f),
                rightSafetyDepth: shallowSafetyDepth,
                rightSafetyZone: CoverageRole.HookMiddle,
                nickelX: hasNickelPackage ? GetReceiverXOrDefault(receivers, middle, fw * 0.50f) : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.None
            ),

            // Cover 2 Zone: Two deep halves, CBs in flat zones, classic two-high
            CoverageScheme.Cover2Zone => new DbConfig(
                leftCbX: hasNickelPackage ? fw * 0.12f : fw * 0.18f,
                leftCbDepth: offCbDepth,
                leftCbZone: CoverageRole.FlatLeft,
                rightCbX: hasNickelPackage ? fw * 0.88f : fw * 0.82f,
                rightCbDepth: offCbDepth,
                rightCbZone: CoverageRole.FlatRight,
                cbPress: false,
                leftSafetyX: hasNickelPackage ? fw * 0.33f : fw * 0.40f,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepLeft,
                rightSafetyX: hasNickelPackage ? fw * 0.67f : fw * 0.60f,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepRight,
                nickelX: hasNickelPackage ? fw * 0.50f : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.HookMiddle
            ),

            // Cover 3 Zone: Three deep thirds, SS drops to flat
            CoverageScheme.Cover3Zone => new DbConfig(
                // CBs line up over the outer WRs, then bail to deep thirds post-snap.
                leftCbX: GetReceiverXOrDefault(receivers, left, fw * 0.18f),
                leftCbDepth: offCbDepth,
                leftCbZone: CoverageRole.DeepLeft,
                rightCbX: GetReceiverXOrDefault(receivers, right, fw * 0.82f),
                rightCbDepth: offCbDepth,
                rightCbZone: CoverageRole.DeepRight,
                cbPress: false,
                // SS roles up to flat, FS takes deep middle
                leftSafetyX: fw * 0.25f,
                leftSafetyDepth: ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(7.0f, 4.6f, depthScale), minY, maxY),
                leftSafetyZone: CoverageRole.FlatLeft,
                rightSafetyX: fw * 0.50f,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepMiddle,
                nickelX: hasNickelPackage ? fw * 0.75f : -1,
                nickelDepth: ClampDefenderY(lineOfScrimmage + GetSituationalDepthOffset(7.0f, 4.6f, depthScale), minY, maxY),
                nickelZone: CoverageRole.FlatRight
            ),

            // Cover 4 Zone: Four deep quarters, NB drops to hook underneath
            CoverageScheme.Cover4Zone => new DbConfig(
                // Corners align over the widest WRs and open to quarter coverage after the snap.
                leftCbX: GetReceiverXOrDefault(receivers, left, fw * 0.18f),
                leftCbDepth: offCbDepth,
                leftCbZone: CoverageRole.DeepLeft,
                rightCbX: GetReceiverXOrDefault(receivers, right, fw * 0.82f),
                rightCbDepth: offCbDepth,
                rightCbZone: CoverageRole.DeepRight,
                cbPress: false,
                // Safeties take inside deep-quarter zones
                leftSafetyX: fw * 0.38f,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepQuarterLeft,
                rightSafetyX: fw * 0.62f,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepQuarterRight,
                nickelX: hasNickelPackage ? fw * 0.50f : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.HookMiddle
            ),

            // Cover 2 Man: Two deep safeties (zone), CBs and LBs play man underneath
            CoverageScheme.Cover2Man => new DbConfig(
                leftCbX: GetReceiverXOrDefault(receivers, left, fw * 0.18f),
                leftCbDepth: cover2ManPress ? pressDepth : offCbDepth,
                leftCbZone: CoverageRole.None,
                rightCbX: GetReceiverXOrDefault(receivers, right, fw * 0.82f),
                rightCbDepth: cover2ManPress ? pressDepth : offCbDepth,
                rightCbZone: CoverageRole.None,
                cbPress: cover2ManPress,
                leftSafetyX: fw * 0.35f,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepLeft,
                rightSafetyX: fw * 0.65f,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepRight,
                nickelX: hasNickelPackage ? GetReceiverXOrDefault(receivers, middle, fw * 0.50f) : -1,
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

    private static void ResolveCoverageIndices(List<Receiver> receivers, out int left, out int leftSlot, out int middle, out int rightSlot, out int right)
    {
        if (receivers.Count == 0)
        {
            left = leftSlot = middle = rightSlot = right = -1;
            return;
        }

        var ordered = receivers
            .Select((receiver, index) => new { receiver.Position.X, index })
            .OrderBy(item => item.X)
            .Select(item => item.index)
            .ToList();

        left = ordered[0];
        right = ordered[^1];
        middle = ordered[ordered.Count / 2];
        leftSlot = ordered.Count > 2 ? ordered[1] : left;
        rightSlot = ordered.Count > 3 ? ordered[^2] : right;
    }

    private static float GetReceiverXOrDefault(IReadOnlyList<Receiver> receivers, int receiverIndex, float fallbackX)
    {
        if (receiverIndex >= 0 && receiverIndex < receivers.Count)
        {
            return receivers[receiverIndex].Position.X;
        }

        return fallbackX;
    }
}
