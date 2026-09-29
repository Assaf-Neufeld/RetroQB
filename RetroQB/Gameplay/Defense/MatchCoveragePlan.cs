using System.Numerics;

namespace RetroQB.Gameplay.Defense;

public enum MatchFamily { None, Cover3, Quarters }
public enum MatchRole { StrongCorner, WeakCorner, MiddleSafety, StrongSafety, WeakSafety, StrongApex, WeakApex, StrongHook, WeakHook, MiddleBack }
public sealed record MatchResponsibility(MatchRole Role, FieldSide Side, ReceiverId? Provisional,
    Vector2 Home, ZoneArea Area, bool Deep) : PassResponsibility;
public sealed record MatchReleaseRule(ReceiverId Receiver, DefenderSlot InitialOwner, int Number, FieldSide Side,
    DefenderSlot? VerticalDestination, IReadOnlyList<DefenderSlot> ShallowDestinations,
    bool LockAll, string VerticalRule, string OutRule, string ShallowRule);
public sealed record MatchRoleBinding(MatchRole Role, DefenderSlot Actor);
public sealed record MatchCoveragePlan(string Check, FieldSide StrongSide, IReadOnlyList<MatchRoleBinding> Roles,
    IReadOnlyList<MatchReleaseRule> Releases)
{
    public DefenderSlot Actor(MatchRole role) => Roles.Single(r => r.Role == role).Actor;
    public bool Trips => Check == "Trips Poach" || Check == "Trips Five Owners";
}

/// <summary>Authored match checks consume only the frozen visible surface. Unsupported geometry resolves before preview.</summary>
internal static class MatchCoverageCompiler
{
    public static ResolvedDefensivePlan Compile(OffensiveAlignmentSnapshot offense, DefensiveCallDefinition call,
        OffensiveSurface surface, DefensePackage package, int seed, IEnumerable<DefensiveAssignment> front, DefensiveFront selectedFront = DefensiveFront.Even)
    {
        var left = surface.Threats.Where(t => !t.Backfield && t.Side == FieldSide.Left).OrderBy(t => t.OutsideIn).ToArray();
        var right = surface.Threats.Where(t => !t.Backfield && t.Side == FieldSide.Right).OrderBy(t => t.OutsideIn).ToArray();
        var backs = surface.Threats.Where(t => t.Backfield).OrderBy(t => t.ActorId.Value).ToArray();
        bool trips = left.Length == 3 && right.Length == 1 || right.Length == 3 && left.Length == 1;
        bool ordinary = left.Length is 1 or 2 && right.Length is 1 or 2;
        string? unsupported = surface.Threats.Any(t => t.BunchGroup != null) ? "Bunch/stack: Lock Man"
            : !ordinary && !trips ? "Unsupported distribution: Lock Man"
            : trips && backs.Length != 1 ? "Trips needs one declared back: Lock Man"
            : call.Match == MatchFamily.Quarters && backs.Length > 1 ? "Multiple backs: Lock Man"
            : surface.Threats.Any(t => !t.Backfield && t.Side == FieldSide.Balanced) ? "Unnumbered center threat: Lock Man" : null;
        if (unsupported != null)
        {
            var fallback = DefensivePlanCompiler.Compile(offense, DefensiveCallDefinition.Get(call.Match == MatchFamily.Cover3 ? "def.cover1" : "def.cover2-man"), package, seed, front: selectedFront);
            return new(fallback.Call, offense, surface, package, seed, fallback.Assignments, fallback.PairingCost,
                requestedCall: call, fallbackReason: unsupported, front: selectedFront);
        }
        bool strongLeft = surface.Strength != FieldSide.Right;
        var strongSide = strongLeft ? FieldSide.Left : FieldSide.Right;
        var weakSide = strongLeft ? FieldSide.Right : FieldSide.Left;
        var strong = strongLeft ? left : right; var weak = strongLeft ? right : left;
        var middle = DefensivePersonnelRoles.Middle(package);
        var bindings = new Dictionary<MatchRole, DefenderSlot>
        {
            [MatchRole.StrongCorner] = strongLeft ? DefenderSlot.CB1 : DefenderSlot.CB2,
            [MatchRole.WeakCorner] = strongLeft ? DefenderSlot.CB2 : DefenderSlot.CB1,
            [MatchRole.StrongApex] = DefensivePersonnelRoles.Apex(package, strongLeft),
            [MatchRole.WeakApex] = DefensivePersonnelRoles.Apex(package, !strongLeft)
        };
        if (call.Match == MatchFamily.Cover3)
        { bindings[MatchRole.MiddleSafety] = DefenderSlot.FS; bindings[MatchRole.StrongHook] = middle; bindings[MatchRole.WeakHook] = DefenderSlot.SS; }
        else
        { bindings[MatchRole.StrongSafety] = strongLeft ? DefenderSlot.FS : DefenderSlot.SS; bindings[MatchRole.WeakSafety] = strongLeft ? DefenderSlot.SS : DefenderSlot.FS; bindings[MatchRole.MiddleBack] = middle; }
        var primaries = new Dictionary<MatchRole, ReceiverId>();
        primaries[MatchRole.StrongCorner] = strong[0].ActorId; primaries[MatchRole.WeakCorner] = weak[0].ActorId;
        if (strong.Length >= 2) primaries[trips && call.Match == MatchFamily.Quarters ? MatchRole.StrongSafety : MatchRole.StrongApex] = strong[1].ActorId;
        if (weak.Length >= 2) primaries[MatchRole.WeakApex] = weak[1].ActorId;
        if (trips) primaries[call.Match == MatchFamily.Cover3 ? MatchRole.StrongHook : MatchRole.StrongApex] = strong[2].ActorId;
        if (backs.Length > 0) primaries[call.Match == MatchFamily.Cover3 ? MatchRole.WeakHook : MatchRole.MiddleBack] = backs[0].ActorId;
        if (backs.Length > 1) primaries[MatchRole.StrongHook] = backs[1].ActorId;
        var jobs = front.ToList(); var releases = new List<MatchReleaseRule>();
        foreach (var (role, actor) in bindings)
        {
            FieldSide side = role is MatchRole.MiddleSafety or MatchRole.MiddleBack ? FieldSide.Balanced
                : role.ToString().StartsWith("Strong", StringComparison.Ordinal) ? strongSide : weakSide;
            bool deep = role is MatchRole.StrongCorner or MatchRole.WeakCorner or MatchRole.MiddleSafety or MatchRole.StrongSafety or MatchRole.WeakSafety;
            ReceiverId? primary = primaries.TryGetValue(role, out var id) ? id : null;
            float x = primary is { } owned ? offense.Skills.Single(r => r.Id == owned).Position.X
                : side == FieldSide.Balanced ? surface.CenterX : surface.CenterX + (int)side * (deep ? 9 : 6);
            float depth = role == MatchRole.MiddleSafety ? 14 : role is MatchRole.StrongSafety or MatchRole.WeakSafety ? 11
                : deep ? 7 : 4.5f;
            var home = DefenderMotor.Clamp(new(x, offense.LineOfScrimmage + depth * FieldGeometry.CoverageDepthScale(offense.LineOfScrimmage)));
            var area = new ZoneArea(side == FieldSide.Right ? surface.CenterX : 0, side == FieldSide.Left ? surface.CenterX : Constants.FieldWidth,
                offense.LineOfScrimmage - 2, Constants.FieldLength - Constants.DefenderRadius);
            jobs.Add(new(actor, actor.GetPositionType(), home, new MatchResponsibility(role, side, primary, home, area, deep),
                new(RunRole.Pursuit, side, RunTrigger.ObservedRunCommitment)));
            if (primary == null) continue;
            var threat = surface.Threats.Single(t => t.ActorId == primary);
            bool isStrong = threat.Side == strongSide;
            var apex = bindings[isStrong ? MatchRole.StrongApex : MatchRole.WeakApex];
            DefenderSlot? destination = null; var underneath = new List<DefenderSlot>();
            string vertical, outward, shallow;
            bool locked = threat.Backfield || call.Match == MatchFamily.Quarters && trips && !isStrong;
            if (call.Match == MatchFamily.Cover3)
            {
                underneath.AddRange(new[] { bindings[MatchRole.StrongHook], bindings[MatchRole.WeakHook] });
                if (trips && threat.OutsideIn == 3) underneath = [bindings[MatchRole.WeakHook], bindings[MatchRole.WeakApex]];
                if (threat.OutsideIn == 1) underneath.Add(apex);
                if (threat.OutsideIn == 2) destination = bindings[isStrong ? MatchRole.StrongCorner : MatchRole.WeakCorner];
                vertical = threat.Attached && threat.OutsideIn == 1 ? "M3-07" : trips ? "M3-05" : "M3-01";
                outward = threat.OutsideIn == 1 ? "M3-04" : "M3-02"; shallow = trips && threat.OutsideIn == 3 ? "M3-06" : threat.OutsideIn == 1 ? "M3-04" : "M3-03";
            }
            else
            {
                underneath.Add(bindings[MatchRole.MiddleBack]);
                underneath.Add(bindings[isStrong ? MatchRole.WeakApex : MatchRole.StrongApex]);
                if (threat.OutsideIn == 1) underneath.Insert(0, apex);
                if (threat.OutsideIn == 2 && !trips) destination = bindings[isStrong ? MatchRole.StrongSafety : MatchRole.WeakSafety];
                if (trips && threat.OutsideIn == 3) destination = bindings[MatchRole.WeakSafety];
                vertical = trips ? threat.OutsideIn == 1 && !isStrong ? "QM-07" : "QM-05" : threat.Attached && threat.OutsideIn == 1 ? "QM-09" : "QM-01";
                outward = trips ? "QM-06" : threat.OutsideIn == 1 ? "QM-04" : "QM-02"; shallow = trips ? "QM-06" : threat.OutsideIn == 1 ? "QM-04" : "QM-03";
            }
            releases.Add(new(primary.Value, actor, threat.OutsideIn, threat.Side, destination,
                Array.AsReadOnly(underneath.Where(a => a != actor).Distinct().ToArray()), locked, vertical, outward, shallow));
        }
        var placed = new List<DefensiveAssignment>();
        foreach (var job in jobs.OrderBy(j => j.Pass is RushResponsibility ? 0 : 1).ThenBy(j => j.ActorId))
        {
            var candidates = from y in Enumerable.Range(0, 15) from x in Enumerable.Range(-12, 25)
                let p = job.Alignment + new Vector2(x * .5f, y * .5f)
                where DefensivePlanCompiler.LegalAlignment(p, offense.LineOfScrimmage) && placed.All(a => Vector2.Distance(a.Alignment, p) >= DefensivePlanCompiler.MinimumSpacing)
                orderby MathF.Abs(x) * 2 + y, x select p;
            var position = candidates.Cast<Vector2?>().FirstOrDefault() ?? throw new InvalidOperationException("No legal match alignment.");
            placed.Add(job with { Alignment = position });
        }
        var match = new MatchCoveragePlan(trips ? call.Match == MatchFamily.Cover3 ? "Trips Five Owners" : "Trips Poach" : "Numbered Vertical Match",
            strongSide, Array.AsReadOnly(bindings.Select(b => new MatchRoleBinding(b.Key, b.Value)).ToArray()), Array.AsReadOnly(releases.OrderBy(r => r.Receiver.Value).ToArray()));
        var plan = new ResolvedDefensivePlan(call, offense, surface, package, seed, RunFitCompiler.Bind(surface, placed).Assignments, 0, match: match, front: selectedFront);
        DefensivePlanValidator.ThrowIfInvalid(plan); return plan;
    }
}
