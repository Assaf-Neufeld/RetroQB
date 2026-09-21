using System.Numerics;
using RetroQB.AI;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public enum DefensivePressure { Four, Edge, Zero }
public sealed record DefensivePlayDefinition(string Id, string Name, CoverageScheme Scheme,
    DefensivePressure Pressure, string Coaching);

public static class DefensivePlaybook
{
    public static IReadOnlyList<DefensivePlayDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new DefensivePlayDefinition("def.cover1", "Cover 1", CoverageScheme.Cover1, DefensivePressure.Four, "Man coverage; one deep helper."),
        new("def.cover2-zone", "Cover 2 Zone", CoverageScheme.Cover2Zone, DefensivePressure.Four, "Guard flats and deep halves."),
        new("def.cover3", "Cover 3", CoverageScheme.Cover3Zone, DefensivePressure.Four, "Three deep; protect underneath."),
        new("def.cover4", "Cover 4", CoverageScheme.Cover4Zone, DefensivePressure.Four, "Four deep; concede short space."),
        new("def.cover2-man", "Cover 2 Man", CoverageScheme.Cover2Man, DefensivePressure.Four, "Man coverage; two deep helpers."),
        new("def.robber", "Robber", CoverageScheme.Robber, DefensivePressure.Four, "One deep; robber attacks inside."),
        new("def.cover3-match", "Cover 3 Match", CoverageScheme.Cover3Match, DefensivePressure.Four, "Match threats from three deep zones."),
        new("def.quarters-match", "Quarters Match", CoverageScheme.QuartersMatch, DefensivePressure.Four, "Match threats from four deep zones."),
        new("def.edge", "Cover 1 Edge Pressure", CoverageScheme.Cover1, DefensivePressure.Edge, "Five rush; single-high man behind it."),
        new("def.zero", "Cover 0 Pressure", CoverageScheme.Cover0, DefensivePressure.Zero, "Six rush; five man matchups, no deep help.")
    });
    public static DefensivePlayDefinition Get(string id) => All.Single(p => p.Id == id);
}

/// <summary>The preview contract deliberately contains no routes, eligibility changes, or offensive call ID.</summary>
public sealed record VisibleReceiver(int Index, ReceiverSlot Slot, Vector2 Position);
public sealed record VisibleOffense(Vector2 Quarterback, IReadOnlyList<VisibleReceiver> Receivers)
{
    public static VisibleOffense From(FormationResult formation) => new(formation.Qb.Position,
        Array.AsReadOnly(formation.Receivers.Select(r => new VisibleReceiver(r.Index, r.Slot, r.Position)).ToArray()));
    internal List<Receiver> CreateReceivers() => Receivers.Select(r => new Receiver(r.Index, r.Slot, r.Position)).ToList();
}

public sealed record DefensiveAssignment(DefenderSlot Slot, DefensivePosition PositionRole, Vector2 Position,
    bool Rush, int ManTarget, CoverageRole Zone, bool Press, float Jitter, float RushLane,
    Vector2 Target, float SpeedBoost, float TackleBoost, float InterceptionBoost, float ShedBoost, bool Star)
{
    public int MatchTarget { get; init; } = -1;
    public string Responsibility => Rush ? "Rush the quarterback" : Zone != CoverageRole.None
        ? MatchTarget >= 0 ? $"Match: receiver {MatchTarget + 1} ({Zone})" : $"Zone: {Zone}"
        : $"Man: receiver {ManTarget + 1}";
    public Defender Create(DefensiveTeamAttributes attributes)
    {
        var defender = new Defender(Position, PositionRole, Slot, attributes)
        {
            IsRusher = Rush, CoverageReceiverIndex = ManTarget, ZoneRole = Zone,
            MatchReceiverIndex = MatchTarget,
            IsPressCoverage = Press, ZoneJitterX = Jitter, RushLaneOffsetX = RushLane
        };
        defender.RestoreModifiers(SpeedBoost, TackleBoost, InterceptionBoost, ShedBoost, Star);
        return defender;
    }
}

public sealed class ResolvedDefensivePlay
{
    private readonly ResolvedDefensivePlay? _unmirrored;
    public DefensivePlayDefinition Definition { get; }
    public IReadOnlyList<DefensiveAssignment> Assignments { get; }
    public DefenderSlot ControlledSlot => Assignments.Any(a => a.Slot == DefenderSlot.MLB) ? DefenderSlot.MLB : DefenderSlot.OLB1;
    public bool UsesZones => Assignments.Any(a => a.Zone != CoverageRole.None);
    public bool UnderneathMan => CoverageSchemePolicies.IsUnderneathManCoverage(Definition.Scheme);
    public ResolvedDefensivePlay(DefensivePlayDefinition definition, IEnumerable<DefensiveAssignment> assignments,
        ResolvedDefensivePlay? unmirrored = null)
    { Definition = definition; Assignments = Array.AsReadOnly(assignments.ToArray()); _unmirrored = unmirrored; }
    public DefenseResult CreateDefense(DefensiveTeamAttributes attributes) => new()
    {
        Defenders = Assignments.Select(a => a.Create(attributes)).ToList(), UsesZoneResponsibilities = UsesZones,
        IsUnderneathManCoverage = UnderneathMan, Scheme = Definition.Scheme,
        Blitzers = Assignments.Where(a => a.Rush && a.PositionRole is not (DefensivePosition.DE or DefensivePosition.DL))
            .Select(a => a.Slot.ToString()).ToList()
    };
    public ResolvedDefensivePlay Mirror()
    {
        if (_unmirrored != null) return _unmirrored;
        static Vector2 Flip(Vector2 p) => new(Constants.FieldWidth - p.X, p.Y);
        static CoverageRole FlipZone(CoverageRole zone) => zone switch
        {
            CoverageRole.DeepLeft => CoverageRole.DeepRight, CoverageRole.DeepRight => CoverageRole.DeepLeft,
            CoverageRole.DeepQuarterLeft => CoverageRole.DeepQuarterRight, CoverageRole.DeepQuarterRight => CoverageRole.DeepQuarterLeft,
            CoverageRole.FlatLeft => CoverageRole.FlatRight, CoverageRole.FlatRight => CoverageRole.FlatLeft,
            CoverageRole.HookLeft => CoverageRole.HookRight, CoverageRole.HookRight => CoverageRole.HookLeft, _ => zone
        };
        return new(Definition, Assignments.Select(a => a with
        { Position = Flip(a.Position), Target = Flip(a.Target), Zone = FlipZone(a.Zone), Jitter = -a.Jitter, RushLane = -a.RushLane }), this);
    }
}

public static class DefensivePlayResolver
{
    public static ResolvedDefensivePlay Resolve(DefensivePlayDefinition definition, VisibleOffense offense,
        DefensiveContext context, DefensiveTeamAttributes attributes, Random random)
    {
        var receivers = offense.CreateReceivers();
        var personnel = DefensivePersonnelPolicy.Create(receivers, context);
        var blitzers = new HashSet<DefenderSlot>();
        if (definition.Pressure == DefensivePressure.Edge) blitzers.Add(DefenderSlot.OLB1);
        if (definition.Pressure == DefensivePressure.Zero)
        { blitzers.Add(personnel.UsesNickel ? DefenderSlot.OLB2 : DefenderSlot.MLB); blitzers.Add(DefenderSlot.OLB1); }
        var result = new DefenseFactory().CreateDefense(context, new(definition.Scheme, new(blitzers)),
            personnel, receivers, random, attributes);
        var defenders = result.Defenders;
        // Allocate all five visible receiving threats before any help responsibility.
        // Rushing a linebacker therefore explicitly transfers its old man matchup.
        if (CoverageSchemePolicies.IsUnderneathManCoverage(definition.Scheme))
        {
            foreach (var d in defenders.Where(d => !d.IsRusher))
            { d.ZoneRole = CoverageRole.None; }
            var fs = defenders.Single(d => d.Slot == DefenderSlot.FS);
            var ss = defenders.Single(d => d.Slot == DefenderSlot.SS);
            if (definition.Scheme != CoverageScheme.Cover0) fs.ZoneRole = CoverageRole.DeepMiddle;
            if (definition.Scheme == CoverageScheme.Cover2Man)
            { fs.ZoneRole = CoverageRole.DeepLeft; ss.ZoneRole = CoverageRole.DeepRight; }
            if (definition.Scheme == CoverageScheme.Robber) ss.ZoneRole = CoverageRole.Robber;
            var available = defenders.Where(d => !d.IsRusher && d.ZoneRole == CoverageRole.None).ToList();
            var remainingTargets = receivers.Select(r => r.Index).ToHashSet();
            // Keep the factory's personnel matchups wherever the selected shell permits them.
            foreach (var d in available.ToArray())
            {
                if (remainingTargets.Remove(d.CoverageReceiverIndex)) available.Remove(d);
                else d.CoverageReceiverIndex = -1;
            }
            foreach (var r in receivers.Where(r => remainingTargets.Contains(r.Index)).OrderBy(r => r.Position.X))
            {
                var d = available.MinBy(d => Vector2.DistanceSquared(d.Position, r.Position))
                    ?? throw new InvalidOperationException("Pressure package lacks a replacement matchup.");
                d.CoverageReceiverIndex = r.Index; available.Remove(d);
            }
            foreach (var helper in available) helper.ZoneRole = CoverageRole.HookMiddle;
        }
        foreach (var d in defenders)
        {
            if (d.IsRusher) { d.ZoneRole = CoverageRole.None; d.CoverageReceiverIndex = -1; }
            else if (d.ZoneRole != CoverageRole.None) d.CoverageReceiverIndex = -1;
            else if (d.CoverageReceiverIndex < 0) d.ZoneRole = CoverageRole.HookMiddle;
        }
        var qb = new Quarterback(offense.Quarterback); var ball = new Ball(qb.Position);
        ball.SetHeld(qb, BallState.HeldByQB);
        // Replacement matchups are final now. Position man DBs over that actual target,
        // including nickel/safety replacements taking a tight end after pressure changes.
        float manDbDepth = defenders.Single(d => d.Slot == DefenderSlot.CB1).Position.Y;
        Vector2 Alignment(Defender d) => CoverageSchemePolicies.IsUnderneathManCoverage(definition.Scheme)
            && !d.IsRusher && d.ZoneRole == CoverageRole.None
            && d.PositionRole == DefensivePosition.DB && d.CoverageReceiverIndex >= 0
                ? new Vector2(receivers.Single(r => r.Index == d.CoverageReceiverIndex).Position.X, manDbDepth)
                : d.Position;
        return new(definition, defenders.Select(d => new DefensiveAssignment(d.Slot, d.PositionRole, Alignment(d),
            d.IsRusher, d.CoverageReceiverIndex, d.ZoneRole, d.IsPressCoverage, d.ZoneJitterX, d.RushLaneOffsetX,
            d.IsRusher ? qb.Position : d.MatchReceiverIndex >= 0 ? receivers.Single(r => r.Index == d.MatchReceiverIndex).Position
                : d.ZoneRole != CoverageRole.None ? ZoneCoverage.GetZoneTarget(d, receivers, context.LineOfScrimmage)
                : receivers.Single(r => r.Index == d.CoverageReceiverIndex).Position,
            d.SpeedMultiplier, d.TackleMultiplier, d.InterceptionMultiplier, d.BlockShedMultiplier, d.IsStarPlayer)
            { MatchTarget = d.MatchReceiverIndex }));
    }
}
