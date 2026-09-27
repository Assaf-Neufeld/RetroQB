using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public sealed record ThreatRole(ReceiverId ActorId, FieldSide Side, int OutsideIn,
    bool Attached, bool Backfield, int? BunchGroup);
public sealed record LineGap(int Index, float X, FieldSide Side, bool Outside);

/// <summary>Immutable surface labels compiled from actual positions; these are not recomputed during a play.</summary>
public sealed class OffensiveSurface
{
    public FieldSide Strength { get; }
    public bool ClosedLeft { get; }
    public bool ClosedRight { get; }
    public float CenterX { get; }
    public IReadOnlyList<ThreatRole> Threats { get; }
    public IReadOnlyList<LineGap> Gaps { get; }
    public int DetachedCount => Threats.Count(t => !t.Attached && !t.Backfield);
    public DefensePackage SuggestedPackage => DetachedCount >= 3 ? DefensePackage.Nickel : DefensePackage.Base;

    private OffensiveSurface(float center, FieldSide strength, IEnumerable<ThreatRole> threats, IEnumerable<LineGap> gaps)
    {
        CenterX = center; Strength = strength;
        Threats = Array.AsReadOnly(threats.OrderBy(t => t.ActorId.Value).ToArray());
        Gaps = Array.AsReadOnly(gaps.ToArray());
        ClosedLeft = Threats.Any(t => t.Attached && t.Side == FieldSide.Left);
        ClosedRight = Threats.Any(t => t.Attached && t.Side == FieldSide.Right);
    }

    public static OffensiveSurface Analyze(OffensiveAlignmentSnapshot offense)
    {
        var line = offense.Linemen.OrderBy(p => p.Position.X).ThenBy(p => p.Id).ToArray();
        float center = offense.Ball.X, left = line[0].Position.X, right = line[^1].Position.X;
        var roles = offense.Skills.Select(p =>
        {
            bool back = offense.LineOfScrimmage - p.Position.Y >= 3f;
            bool attached = !back && p.Slot.IsTightEndSlot() && p.Position.X >= left - 3f && p.Position.X <= right + 3f;
            var side = p.Position.X < center - .25f ? FieldSide.Left : p.Position.X > center + .25f ? FieldSide.Right : FieldSide.Balanced;
            return new ThreatRole(p.Id, side, 0, attached, back, null);
        }).ToDictionary(t => t.ActorId);
        foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
        {
            int number = 0;
            foreach (var p in offense.Skills.Where(p => roles[p.Id].Side == side && !roles[p.Id].Backfield)
                .OrderByDescending(p => MathF.Abs(p.Position.X - center)).ThenBy(p => p.Id.Value))
                roles[p.Id] = roles[p.Id] with { OutsideIn = ++number };
        }
        // Connected close-release groups. Detection is metadata only; P3 owns switching.
        int group = 0; var visited = new HashSet<ReceiverId>();
        foreach (var start in offense.Skills.Where(p => !roles[p.Id].Backfield).OrderBy(p => p.Id.Value))
        {
            if (!visited.Add(start.Id)) continue;
            var members = new List<VisibleSkillPlayer> { start };
            for (int i = 0; i < members.Count; i++)
            foreach (var candidate in offense.Skills.Where(p => !roles[p.Id].Backfield && !visited.Contains(p.Id)))
                if (roles[candidate.Id].Side == roles[start.Id].Side
                    && MathF.Abs(candidate.Position.X - members[i].Position.X) <= 3.5f
                    && MathF.Abs(candidate.Position.Y - members[i].Position.Y) <= 2.5f)
                { visited.Add(candidate.Id); members.Add(candidate); }
            if (members.Count < 2) continue;
            foreach (var p in members) roles[p.Id] = roles[p.Id] with { BunchGroup = group };
            group++;
        }
        int l = roles.Values.Count(t => t.Side == FieldSide.Left && !t.Backfield);
        int r = roles.Values.Count(t => t.Side == FieldSide.Right && !t.Backfield);
        var strength = l == r ? FieldSide.Balanced : l > r ? FieldSide.Left : FieldSide.Right;
        var gaps = new List<LineGap>();
        Add(left - 2f, true);
        for (int i = 1; i < line.Length; i++) Add((line[i - 1].Position.X + line[i].Position.X) / 2, false);
        Add(right + 2f, true);
        return new(center, strength, roles.Values, gaps);
        void Add(float x, bool outside) => gaps.Add(new(gaps.Count, x, x < center ? FieldSide.Left : FieldSide.Right, outside));
    }
}
