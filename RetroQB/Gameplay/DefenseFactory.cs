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
        bool mlbBlitz = rng.NextDouble() < baseBlitzChance;
        bool lbrBlitz = rng.NextDouble() < baseBlitzChance;

        if (lblBlitz) blitzers.Add("LB");
        if (mlbBlitz) blitzers.Add("MLB");
        if (lbrBlitz) blitzers.Add("LB");

        float lbDepth = ClampDefenderY(lineOfScrimmage + 7.6f * depthScale, maxY);
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.38f, lbDepth), DefensivePosition.LB, attrs) { IsRusher = lblBlitz, CoverageReceiverIndex = leftSlot, ZoneRole = CoverageRole.HookLeft, RushLaneOffsetX = -7.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.50f, lbDepth), DefensivePosition.LB, attrs) { IsRusher = mlbBlitz, CoverageReceiverIndex = middle, ZoneRole = CoverageRole.HookMiddle, RushLaneOffsetX = 0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.62f, lbDepth), DefensivePosition.LB, attrs) { IsRusher = lbrBlitz, CoverageReceiverIndex = rightSlot, ZoneRole = CoverageRole.HookRight, RushLaneOffsetX = 7.0f });

        // DBs
        float baseCbDepth = useZone ? 8.0f : 3.5f;
        float baseSDepth = useZone ? 16.0f : 10.0f;
        float cbDepth = ClampDefenderY(lineOfScrimmage + baseCbDepth * depthScale, maxY);
        float sDepth = ClampDefenderY(lineOfScrimmage + baseSDepth * depthScale, maxY);

        float cbBlitzChance = !useZone ? 0.05f * attrs.BlitzFrequency : 0f;
        bool leftCbBlitz = rng.NextDouble() < cbBlitzChance;
        bool rightCbBlitz = rng.NextDouble() < cbBlitzChance;

        if (leftCbBlitz) blitzers.Add("CB");
        if (rightCbBlitz) blitzers.Add("CB");

        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.18f, cbDepth), DefensivePosition.DB, attrs) { IsRusher = leftCbBlitz, CoverageReceiverIndex = left, ZoneRole = CoverageRole.FlatLeft, IsPressCoverage = !useZone, RushLaneOffsetX = -10.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.82f, cbDepth), DefensivePosition.DB, attrs) { IsRusher = rightCbBlitz, CoverageReceiverIndex = right, ZoneRole = CoverageRole.FlatRight, IsPressCoverage = !useZone, RushLaneOffsetX = 10.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, sDepth), DefensivePosition.DB, attrs) { CoverageReceiverIndex = leftSlot, ZoneRole = CoverageRole.DeepLeft, IsPressCoverage = false });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, sDepth), DefensivePosition.DB, attrs) { CoverageReceiverIndex = rightSlot, ZoneRole = CoverageRole.DeepRight, IsPressCoverage = false });

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
}
