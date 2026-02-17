using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
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
    DefenseResult CreateDefense(DefensiveContext context, List<Receiver> receivers, Random rng, DefensiveTeamAttributes? teamAttributes = null);
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

    public DefenseResult CreateDefense(DefensiveContext context, List<Receiver> receivers, Random rng, DefensiveTeamAttributes? teamAttributes = null)
    {
        var attrs = teamAttributes ?? DefensiveTeamAttributes.Default;
        var defenders = new List<Defender>();
        var blitzers = new List<string>();

        ResolveCoverageIndices(receivers, out int left, out int leftSlot, out int middle, out int rightSlot, out int right);
        int wrCount = receivers.Count(receiver => receiver.PositionRole == OffensivePosition.WR);
        bool useNickel = wrCount >= 3;

        // Select coverage scheme based on game situation
        CoverageScheme scheme = CoverageSchemeSelector.SelectScheme(
            context.Down, context.Distance, context.LineOfScrimmage,
            context.Score, context.AwayScore, context.Stage, rng);

        bool useZone = IsZoneScheme(scheme);

        float maxY = FieldGeometry.OpponentGoalLine - 1f;
        float availableDepth = maxY - context.LineOfScrimmage;
        float depthScale = MathF.Max(availableDepth < 18f ? availableDepth / 18f : 1f, 0.3f);

        // Defensive line - DEs on the outside (circular rush), DTs inside (straight rush)
        float dlDepth = ClampDefenderY(context.LineOfScrimmage + 1.8f * depthScale, maxY);
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, dlDepth), DefensivePosition.DE, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -5.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.46f, dlDepth), DefensivePosition.DL, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -2.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.54f, dlDepth), DefensivePosition.DL, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 2.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, dlDepth), DefensivePosition.DE, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 5.0f });

        // Linebackers - blitz chance varies by scheme
        float lbBlitzChance = GetLbBlitzChance(scheme, attrs, context);
        bool lblBlitz = rng.NextDouble() < lbBlitzChance;
        bool lbrBlitz = rng.NextDouble() < lbBlitzChance;
        bool mlbBlitz = !useNickel && rng.NextDouble() < lbBlitzChance;

        if (lblBlitz) blitzers.Add("LB");
        if (lbrBlitz) blitzers.Add("LB");
        if (mlbBlitz) blitzers.Add("MLB");

        float lbDepth = ClampDefenderY(context.LineOfScrimmage + 7.6f * depthScale, maxY);
        var lbRoles = GetLbZoneRoles(scheme);

        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, lbDepth), DefensivePosition.LB, attrs)
        {
            IsRusher = lblBlitz,
            CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Linebacker) ? leftSlot : -1,
            ZoneRole = lbRoles.left,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = -7.0f
        });
        if (!useNickel)
        {
            defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.50f, lbDepth), DefensivePosition.LB, attrs)
            {
                IsRusher = mlbBlitz,
                CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Linebacker) ? middle : -1,
                ZoneRole = lbRoles.middle,
                ZoneJitterX = GetJitter(rng),
                RushLaneOffsetX = 0f
            });
        }
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, lbDepth), DefensivePosition.LB, attrs)
        {
            IsRusher = lbrBlitz,
            CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Linebacker) ? rightSlot : -1,
            ZoneRole = lbRoles.right,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = 7.0f
        });

        // DBs - positioning and roles vary by scheme
        var dbConfig = GetDbConfiguration(scheme, context.LineOfScrimmage, depthScale, maxY,
            receivers, left, leftSlot, middle, rightSlot, right, useNickel, rng);

        float cbBlitzChance = GetCbBlitzChance(scheme, attrs, context);
        bool leftCbBlitz = rng.NextDouble() < cbBlitzChance;
        bool rightCbBlitz = rng.NextDouble() < cbBlitzChance;

        if (leftCbBlitz) blitzers.Add("CB");
        if (rightCbBlitz) blitzers.Add("CB");

        // Cornerbacks
        defenders.Add(new Defender(new Vector2(dbConfig.LeftCbX, dbConfig.LeftCbDepth), DefensivePosition.DB, attrs)
        {
            IsRusher = leftCbBlitz,
            CoverageReceiverIndex = left,
            ZoneRole = dbConfig.LeftCbZone,
            IsPressCoverage = dbConfig.CbPress,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = -10.0f
        });
        defenders.Add(new Defender(new Vector2(dbConfig.RightCbX, dbConfig.RightCbDepth), DefensivePosition.DB, attrs)
        {
            IsRusher = rightCbBlitz,
            CoverageReceiverIndex = right,
            ZoneRole = dbConfig.RightCbZone,
            IsPressCoverage = dbConfig.CbPress,
            ZoneJitterX = GetJitter(rng),
            RushLaneOffsetX = 10.0f
        });

        // Nickel DB (extra DB replacing MLB in nickel packages)
        if (useNickel)
        {
            int nickelTarget = middle >= 0 ? middle : leftSlot >= 0 ? leftSlot : rightSlot;
            float nickelX = dbConfig.NickelX >= 0
                ? dbConfig.NickelX
                : GetReceiverXOrDefault(receivers, nickelTarget, Constants.FieldWidth * 0.50f);

            defenders.Add(new Defender(new Vector2(nickelX, dbConfig.NickelDepth), DefensivePosition.DB, attrs)
            {
                CoverageReceiverIndex = nickelTarget,
                ZoneRole = dbConfig.NickelZone,
                IsPressCoverage = false,
                ZoneJitterX = GetJitter(rng),
                RushLaneOffsetX = 0f
            });
        }

        // Safeties
        defenders.Add(new Defender(new Vector2(dbConfig.LeftSafetyX, dbConfig.LeftSafetyDepth), DefensivePosition.DB, attrs)
        {
            CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Safety) ? leftSlot : -1,
            ZoneRole = dbConfig.LeftSafetyZone,
            IsPressCoverage = false,
            ZoneJitterX = GetJitter(rng)
        });
        defenders.Add(new Defender(new Vector2(dbConfig.RightSafetyX, dbConfig.RightSafetyDepth), DefensivePosition.DB, attrs)
        {
            CoverageReceiverIndex = IsManForPosition(scheme, CoverageUnit.Safety) ? rightSlot : -1,
            ZoneRole = dbConfig.RightSafetyZone,
            IsPressCoverage = false,
            ZoneJitterX = GetJitter(rng)
        });

        return new DefenseResult
        {
            Defenders = defenders,
            IsZoneCoverage = useZone,
            Blitzers = blitzers,
            Scheme = scheme
        };
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

    // --- Blitz chance helpers ---

    private static float GetLbBlitzChance(CoverageScheme scheme, DefensiveTeamAttributes attrs, DefensiveContext context)
    {
        float baseChance = scheme switch
        {
            CoverageScheme.Cover0 => 0.35f,
            CoverageScheme.Cover1 => 0.15f,
            CoverageScheme.Cover2Man => 0.12f,
            CoverageScheme.Cover2Zone => 0.08f,
            CoverageScheme.Cover3Zone => 0.08f,
            CoverageScheme.Cover4Zone => 0.05f,
            _ => 0.10f
        };

        float situationalScale = GetBlitzSituationalMultiplier(context);
        float chance = baseChance * attrs.BlitzFrequency * situationalScale;
        return Math.Clamp(chance, 0f, 0.70f);
    }

    private static float GetCbBlitzChance(CoverageScheme scheme, DefensiveTeamAttributes attrs, DefensiveContext context)
    {
        float baseChance = scheme switch
        {
            CoverageScheme.Cover0 => 0.20f,
            CoverageScheme.Cover1 => 0.05f,
            _ => 0f // zone schemes don't blitz CBs
        };

        float situationalScale = GetBlitzSituationalMultiplier(context);
        float chance = baseChance * attrs.BlitzFrequency * situationalScale;
        return Math.Clamp(chance, 0f, 0.35f);
    }

    private static float GetBlitzSituationalMultiplier(DefensiveContext context)
    {
        float stageScale = context.Stage switch
        {
            SeasonStage.RegularSeason => 0.82f,
            SeasonStage.Playoff => 1.00f,
            SeasonStage.SuperBowl => 1.15f,
            _ => 1.00f
        };

        float downDistanceScale = 1f;
        if (context.Down >= 3 && context.Distance >= 7f)
        {
            downDistanceScale *= 1.15f;
        }
        else if (context.Down >= 3 && context.Distance <= 2f)
        {
            downDistanceScale *= 0.92f;
        }

        bool isRedZone = context.LineOfScrimmage >= FieldGeometry.OpponentGoalLine - 15f;
        float fieldScale = isRedZone ? 0.94f : 1f;

        int defenseLead = context.AwayScore - context.Score;
        float scoreScale = defenseLead switch
        {
            <= -8 => 1.10f,
            >= 8 => 0.92f,
            _ => 1f
        };

        return stageScale * downDistanceScale * fieldScale * scoreScale;
    }

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
            CoverageScheme.Cover3Zone => (CoverageRole.FlatLeft, CoverageRole.HookMiddle, CoverageRole.FlatRight),
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
        CoverageScheme scheme, float lineOfScrimmage, float depthScale, float maxY,
        List<Receiver> receivers, int left, int leftSlot, int middle, int rightSlot, int right,
        bool useNickel, Random rng)
    {
        float fw = Constants.FieldWidth;
        bool cover1Press = rng.NextDouble() < 0.6;
        bool cover2ManPress = rng.NextDouble() < 0.4;

        // Common depth calculations
        float pressDepth = ClampDefenderY(lineOfScrimmage + 3.5f * depthScale, maxY);
        float offCbDepth = ClampDefenderY(lineOfScrimmage + 8.0f * depthScale, maxY);
        float shallowSafetyDepth = ClampDefenderY(lineOfScrimmage + 10.0f * depthScale, maxY);
        float deepSafetyDepth = ClampDefenderY(lineOfScrimmage + 16.0f * depthScale, maxY);

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
                nickelX: useNickel ? GetReceiverXOrDefault(receivers, middle, fw * 0.50f) : -1,
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
                // Strong safety plays man on slot
                leftSafetyX: GetReceiverXOrDefault(receivers, leftSlot, fw * 0.40f),
                leftSafetyDepth: shallowSafetyDepth,
                leftSafetyZone: CoverageRole.None,
                // Free safety plays deep middle zone
                rightSafetyX: fw * 0.50f,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepMiddle,
                nickelX: useNickel ? GetReceiverXOrDefault(receivers, middle, fw * 0.50f) : -1,
                nickelDepth: shallowSafetyDepth,
                nickelZone: CoverageRole.None
            ),

            // Cover 2 Zone: Two deep halves, CBs in flat zones, classic two-high
            CoverageScheme.Cover2Zone => new DbConfig(
                leftCbX: useNickel ? fw * 0.12f : fw * 0.18f,
                leftCbDepth: offCbDepth,
                leftCbZone: CoverageRole.FlatLeft,
                rightCbX: useNickel ? fw * 0.88f : fw * 0.82f,
                rightCbDepth: offCbDepth,
                rightCbZone: CoverageRole.FlatRight,
                cbPress: false,
                leftSafetyX: useNickel ? fw * 0.33f : fw * 0.40f,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepLeft,
                rightSafetyX: useNickel ? fw * 0.67f : fw * 0.60f,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepRight,
                nickelX: useNickel ? fw * 0.50f : -1,
                nickelDepth: deepSafetyDepth,
                nickelZone: CoverageRole.DeepMiddle
            ),

            // Cover 3 Zone: Three deep thirds, SS drops to flat
            CoverageScheme.Cover3Zone => new DbConfig(
                // CBs drop to deep thirds
                leftCbX: fw * 0.22f,
                leftCbDepth: deepSafetyDepth,
                leftCbZone: CoverageRole.DeepLeft,
                rightCbX: fw * 0.78f,
                rightCbDepth: deepSafetyDepth,
                rightCbZone: CoverageRole.DeepRight,
                cbPress: false,
                // SS roles up to flat, FS takes deep middle
                leftSafetyX: fw * 0.25f,
                leftSafetyDepth: ClampDefenderY(lineOfScrimmage + 7.0f * depthScale, maxY),
                leftSafetyZone: CoverageRole.FlatLeft,
                rightSafetyX: fw * 0.50f,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepMiddle,
                nickelX: useNickel ? fw * 0.75f : -1,
                nickelDepth: ClampDefenderY(lineOfScrimmage + 7.0f * depthScale, maxY),
                nickelZone: CoverageRole.FlatRight
            ),

            // Cover 4 Zone: Four deep quarters, strong underneath help from LBs
            CoverageScheme.Cover4Zone => new DbConfig(
                leftCbX: fw * 0.20f,
                leftCbDepth: deepSafetyDepth,
                leftCbZone: CoverageRole.DeepLeft,
                rightCbX: fw * 0.80f,
                rightCbDepth: deepSafetyDepth,
                rightCbZone: CoverageRole.DeepRight,
                cbPress: false,
                // Safeties take inside deep-quarter zones
                leftSafetyX: fw * 0.38f,
                leftSafetyDepth: deepSafetyDepth,
                leftSafetyZone: CoverageRole.DeepQuarterLeft,
                rightSafetyX: fw * 0.62f,
                rightSafetyDepth: deepSafetyDepth,
                rightSafetyZone: CoverageRole.DeepQuarterRight,
                nickelX: useNickel ? fw * 0.50f : -1,
                nickelDepth: deepSafetyDepth,
                nickelZone: CoverageRole.DeepMiddle
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
                nickelX: useNickel ? GetReceiverXOrDefault(receivers, middle, fw * 0.50f) : -1,
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

    private static float ClampDefenderY(float y, float maxY)
    {
        return MathF.Min(y, maxY);
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
