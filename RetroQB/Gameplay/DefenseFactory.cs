using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public interface IDefenseFactory
{
    DefenseResult CreateDefense(float lineOfScrimmage, float distance, List<Receiver> receivers, Random rng, DefensiveTeamAttributes? teamAttributes = null);
}

public sealed class DefenseResult
{
    public List<Defender> Defenders { get; init; } = new();
    public bool IsZoneCoverage { get; init; }
    public List<string> Blitzers { get; init; } = new();
}

public sealed class DefenseFactory : IDefenseFactory
{
    public DefenseResult CreateDefense(float lineOfScrimmage, float distance, List<Receiver> receivers, Random rng, DefensiveTeamAttributes? teamAttributes = null)
    {
        var attrs = teamAttributes ?? DefensiveTeamAttributes.Default;
        var defenders = new List<Defender>();
        var blitzers = new List<string>();

        ResolveCoverageIndices(receivers, out int left, out int leftSlot, out int middle, out int rightSlot, out int right);
        int wrCount = receivers.Count(receiver => receiver.PositionRole == OffensivePosition.WR);
        bool useNickel = wrCount >= 3;

        // Distance-based man coverage probability: 95% at 1 yard, 30% at 10 yards
        float manChance = GetManCoverageChance(distance);
        bool useZone = rng.NextDouble() >= manChance;

        float maxY = Constants.FieldLength - 1f;
        float availableDepth = maxY - lineOfScrimmage;

        float depthScale = availableDepth < 18f ? availableDepth / 18f : 1f;
        depthScale = MathF.Max(depthScale, 0.3f);

        // Defensive line
        float dlDepth = ClampDefenderY(lineOfScrimmage + 2.8f * depthScale, maxY);
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, dlDepth), DefensivePosition.DL, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -5.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.46f, dlDepth), DefensivePosition.DL, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -2.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.54f, dlDepth), DefensivePosition.DL, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 2.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, dlDepth), DefensivePosition.DL, attrs) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 5.0f });

        // Linebackers with blitz chance (adjusted by team's blitz frequency)
        float baseBlitzChance = 0.10f * attrs.BlitzFrequency;
        bool lblBlitz = rng.NextDouble() < baseBlitzChance;
        bool lbrBlitz = rng.NextDouble() < baseBlitzChance;
        bool mlbBlitz = !useNickel && rng.NextDouble() < baseBlitzChance;

        if (lblBlitz) blitzers.Add("LB");
        if (lbrBlitz) blitzers.Add("LB");
        if (mlbBlitz) blitzers.Add("MLB");

        float lbDepth = ClampDefenderY(lineOfScrimmage + 7.6f * depthScale, maxY);
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, lbDepth), DefensivePosition.LB, attrs) { IsRusher = lblBlitz, CoverageReceiverIndex = leftSlot, ZoneRole = CoverageRole.HookLeft, RushLaneOffsetX = -7.0f });
        if (!useNickel)
        {
            defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.50f, lbDepth), DefensivePosition.LB, attrs) { IsRusher = mlbBlitz, CoverageReceiverIndex = middle, ZoneRole = CoverageRole.HookMiddle, RushLaneOffsetX = 0f });
        }
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, lbDepth), DefensivePosition.LB, attrs) { IsRusher = lbrBlitz, CoverageReceiverIndex = rightSlot, ZoneRole = CoverageRole.HookRight, RushLaneOffsetX = 7.0f });

        // DBs
        float baseCbDepth = useZone ? 8.0f : 3.5f;
        float baseSDepth = useZone ? 16.0f : 10.0f;
        float cbDepth = ClampDefenderY(lineOfScrimmage + baseCbDepth * depthScale, maxY);
        float sDepth = ClampDefenderY(lineOfScrimmage + baseSDepth * depthScale, maxY);

        bool alignManNickel = useNickel && !useZone;
        bool useNickelZone = useNickel && useZone;
        float leftCbDepth = cbDepth;
        float rightCbDepth = cbDepth;
        float leftSafetyDepth = sDepth;
        float rightSafetyDepth = sDepth;
        CoverageRole leftCbZoneRole = CoverageRole.FlatLeft;
        CoverageRole rightCbZoneRole = CoverageRole.FlatRight;
        CoverageRole leftSafetyZoneRole = CoverageRole.DeepLeft;
        CoverageRole rightSafetyZoneRole = CoverageRole.DeepRight;
        float leftCbX = alignManNickel
            ? GetReceiverXOrDefault(receivers, left, Constants.FieldWidth * 0.18f)
            : useNickelZone ? Constants.FieldWidth * 0.12f : Constants.FieldWidth * 0.18f;
        float rightCbX = alignManNickel
            ? GetReceiverXOrDefault(receivers, right, Constants.FieldWidth * 0.82f)
            : useNickelZone ? Constants.FieldWidth * 0.88f : Constants.FieldWidth * 0.82f;
        float leftSafetyX = alignManNickel
            ? GetReceiverXOrDefault(receivers, leftSlot, Constants.FieldWidth * 0.40f)
            : useNickelZone ? Constants.FieldWidth * 0.33f : Constants.FieldWidth * 0.40f;
        float rightSafetyX = alignManNickel
            ? GetReceiverXOrDefault(receivers, rightSlot, Constants.FieldWidth * 0.60f)
            : useNickelZone ? Constants.FieldWidth * 0.67f : Constants.FieldWidth * 0.60f;

        float cbBlitzChance = !useZone ? 0.05f * attrs.BlitzFrequency : 0f;
        bool leftCbBlitz = rng.NextDouble() < cbBlitzChance;
        bool rightCbBlitz = rng.NextDouble() < cbBlitzChance;

        if (leftCbBlitz) blitzers.Add("CB");
        if (rightCbBlitz) blitzers.Add("CB");

        defenders.Add(new Defender(new Vector2(leftCbX, leftCbDepth), DefensivePosition.DB, attrs) { IsRusher = leftCbBlitz, CoverageReceiverIndex = left, ZoneRole = leftCbZoneRole, IsPressCoverage = !useZone, RushLaneOffsetX = -10.0f });
        defenders.Add(new Defender(new Vector2(rightCbX, rightCbDepth), DefensivePosition.DB, attrs) { IsRusher = rightCbBlitz, CoverageReceiverIndex = right, ZoneRole = rightCbZoneRole, IsPressCoverage = !useZone, RushLaneOffsetX = 10.0f });

        if (useNickel)
        {
            int nickelTarget = middle >= 0 ? middle : leftSlot >= 0 ? leftSlot : rightSlot;
            float nickelX = alignManNickel
                ? GetReceiverXOrDefault(receivers, nickelTarget, Constants.FieldWidth * 0.50f)
                : useNickelZone ? Constants.FieldWidth * 0.50f : (nickelTarget >= 0 ? receivers[nickelTarget].Position.X : Constants.FieldWidth * 0.50f);
            float nickelDepth = sDepth;

            defenders.Add(new Defender(new Vector2(nickelX, nickelDepth), DefensivePosition.DB, attrs)
            {
                CoverageReceiverIndex = nickelTarget,
                ZoneRole = useZone ? CoverageRole.DeepMiddle : CoverageRole.None,
                IsPressCoverage = false,
                RushLaneOffsetX = 0f
            });
        }

        defenders.Add(new Defender(new Vector2(leftSafetyX, leftSafetyDepth), DefensivePosition.DB, attrs) { CoverageReceiverIndex = leftSlot, ZoneRole = leftSafetyZoneRole, IsPressCoverage = false });
        defenders.Add(new Defender(new Vector2(rightSafetyX, rightSafetyDepth), DefensivePosition.DB, attrs) { CoverageReceiverIndex = rightSlot, ZoneRole = rightSafetyZoneRole, IsPressCoverage = false });

        return new DefenseResult
        {
            Defenders = defenders,
            IsZoneCoverage = useZone,
            Blitzers = blitzers
        };
    }

    private static float ClampDefenderY(float y, float maxY)
    {
        return MathF.Min(y, maxY);
    }

    private static float GetManCoverageChance(float distance)
    {
        const float nearDistance = 1f;
        const float farDistance = 10f;
        const float nearManChance = 0.95f;
        const float farManChance = 0.30f;

        if (distance <= nearDistance)
            return nearManChance;

        if (distance >= farDistance)
            return farManChance;

        float t = (distance - nearDistance) / (farDistance - nearDistance);
        return nearManChance + (farManChance - nearManChance) * t;
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
