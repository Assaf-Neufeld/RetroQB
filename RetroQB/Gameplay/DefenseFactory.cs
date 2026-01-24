using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public interface IDefenseFactory
{
    DefenseResult CreateDefense(float lineOfScrimmage, float distance, List<Receiver> receivers, Random rng);
}

public sealed class DefenseResult
{
    public List<Defender> Defenders { get; init; } = new();
    public bool IsZoneCoverage { get; init; }
    public List<string> Blitzers { get; init; } = new();
}

public sealed class DefenseFactory : IDefenseFactory
{
    public DefenseResult CreateDefense(float lineOfScrimmage, float distance, List<Receiver> receivers, Random rng)
    {
        var defenders = new List<Defender>();
        var blitzers = new List<string>();

        ResolveCoverageIndices(receivers, out int left, out int leftSlot, out int middle, out int rightSlot, out int right);

        bool useZone = distance > Constants.ManCoverageDistanceThreshold;

        float maxY = Constants.FieldLength - 1f;
        float availableDepth = maxY - lineOfScrimmage;

        float depthScale = availableDepth < 18f ? availableDepth / 18f : 1f;
        depthScale = MathF.Max(depthScale, 0.3f);

        // Defensive line
        float dlDepth = ClampDefenderY(lineOfScrimmage + 2.8f * depthScale, maxY);
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, dlDepth), DefensivePosition.DL) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -5.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.46f, dlDepth), DefensivePosition.DL) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -2.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.54f, dlDepth), DefensivePosition.DL) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 2.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, dlDepth), DefensivePosition.DL) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 5.0f });

        // Linebackers with blitz chance
        bool lblBlitz = rng.NextDouble() < 0.10;
        bool mlbBlitz = rng.NextDouble() < 0.10;
        bool lbrBlitz = rng.NextDouble() < 0.10;

        if (lblBlitz) blitzers.Add("LB");
        if (mlbBlitz) blitzers.Add("MLB");
        if (lbrBlitz) blitzers.Add("LB");

        float lbDepth = ClampDefenderY(lineOfScrimmage + 6.2f * depthScale, maxY);
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.38f, lbDepth), DefensivePosition.LB) { IsRusher = lblBlitz, CoverageReceiverIndex = leftSlot, ZoneRole = CoverageRole.HookLeft, RushLaneOffsetX = -7.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.50f, lbDepth), DefensivePosition.LB) { IsRusher = mlbBlitz, CoverageReceiverIndex = middle, ZoneRole = CoverageRole.HookMiddle, RushLaneOffsetX = 0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.62f, lbDepth), DefensivePosition.LB) { IsRusher = lbrBlitz, CoverageReceiverIndex = rightSlot, ZoneRole = CoverageRole.HookRight, RushLaneOffsetX = 7.0f });

        // DBs
        float baseCbDepth = useZone ? 8.0f : 3.5f;
        float baseSDepth = useZone ? 16.0f : 13.5f;
        float cbDepth = ClampDefenderY(lineOfScrimmage + baseCbDepth * depthScale, maxY);
        float sDepth = ClampDefenderY(lineOfScrimmage + baseSDepth * depthScale, maxY);

        bool leftCbBlitz = !useZone && rng.NextDouble() < 0.05;
        bool rightCbBlitz = !useZone && rng.NextDouble() < 0.05;

        if (leftCbBlitz) blitzers.Add("CB");
        if (rightCbBlitz) blitzers.Add("CB");

        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.18f, cbDepth), DefensivePosition.DB) { IsRusher = leftCbBlitz, CoverageReceiverIndex = left, ZoneRole = CoverageRole.FlatLeft, IsPressCoverage = !useZone, RushLaneOffsetX = -10.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.82f, cbDepth), DefensivePosition.DB) { IsRusher = rightCbBlitz, CoverageReceiverIndex = right, ZoneRole = CoverageRole.FlatRight, IsPressCoverage = !useZone, RushLaneOffsetX = 10.0f });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, sDepth), DefensivePosition.DB) { CoverageReceiverIndex = leftSlot, ZoneRole = CoverageRole.DeepLeft, IsPressCoverage = false });
        defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, sDepth), DefensivePosition.DB) { CoverageReceiverIndex = rightSlot, ZoneRole = CoverageRole.DeepRight, IsPressCoverage = false });

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
