using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

/// <summary>Pure bounded compiler. No RNG draws, route reads, physical simulation, or legacy repair passes.</summary>
public static class DefensivePlanCompiler
{
    public const float MinimumSpacing = 2.25f;
    private static readonly DefenderSlot[] Front = [DefenderSlot.DE1, DefenderSlot.DT1, DefenderSlot.DT2, DefenderSlot.DE2];

    public static ResolvedDefensivePlan Compile(OffensiveAlignmentSnapshot offense, DefensiveCallDefinition call,
        DefensePackage? package = null, int resolutionSeed = 101, ManCoverageAdjustments? adjustments = null)
    {
        ArgumentNullException.ThrowIfNull(offense); ArgumentNullException.ThrowIfNull(call);
        adjustments ??= ManCoverageAdjustments.None;
        if (!DefensiveCallDefinition.All.Contains(call))
            throw new NotSupportedException("Only implemented versioned calls may be compiled.");
        var surface = OffensiveSurface.Analyze(offense);
        var personnel = package ?? surface.SuggestedPackage;
        if (!Enum.IsDefined(personnel)) throw new ArgumentOutOfRangeException(nameof(package));
        var active = ActiveSlots(personnel);
        var jobs = new Dictionary<DefenderSlot, DefensiveAssignment>();
        var gapPool = surface.Gaps.ToList();
        foreach (var slot in Front)
        {
            LineGap gap = slot switch
            {
                DefenderSlot.DE1 => gapPool.MinBy(g => g.X)!,
                DefenderSlot.DE2 => gapPool.MaxBy(g => g.X)!,
                DefenderSlot.DT1 => gapPool.Where(g => !g.Outside).MinBy(g => g.X)!,
                _ => gapPool.Where(g => !g.Outside).MaxBy(g => g.X)!
            };
            gapPool.Remove(gap); AddRush(slot, gap);
        }
        if (call.Zone != SpotZoneFamily.None)
        {
            if (!adjustments.Equivalent(ManCoverageAdjustments.None)) throw new ArgumentException("Man adjustments cannot modify spot zones.");
            return SpotZoneCompiler.Compile(offense, call, surface, personnel, resolutionSeed, jobs.Values);
        }
        if (call.RushCount >= 5)
        {
            var gap = gapPool.MinBy(g => g.X)!; gapPool.Remove(gap); AddRush(DefenderSlot.OLB1, gap);
        }
        if (call.RushCount == 6)
        {
            var gap = gapPool.MaxBy(g => g.X)!;
            AddRush(personnel == DefensePackage.Base ? DefenderSlot.MLB : DefenderSlot.OLB2, gap);
        }
        if (call.Shell == ManShell.TwoHigh)
        {
            AddHelp(DefenderSlot.FS, HelperRole.DeepLeft);
            AddHelp(DefenderSlot.SS, HelperRole.DeepRight);
        }
        else if (call.Shell != ManShell.Zero)
        {
            AddHelp(DefenderSlot.FS, HelperRole.DeepMiddle);
            if (call.Shell == ManShell.Robber) AddHelp(DefenderSlot.SS, HelperRole.Robber);
            // Cover 1's seventh coverage player is the called low helper, not an extra primary.
            else if (call.RushCount == 4) AddHelp(DefenderSlot.SS, HelperRole.LowMiddle);
        }
        var candidates = active.Where(s => !jobs.ContainsKey(s)).ToArray();
        var threats = offense.Skills.OrderBy(p => p.Id.Value).ToArray();
        if (candidates.Length < threats.Length) throw new InvalidOperationException("The called shell/rush budget leaves an uncovered threat.");
        if (adjustments.Shadows.Select(s => s.Defender).Distinct().Count() != adjustments.Shadows.Count
            || adjustments.Shadows.Select(s => s.Receiver).Distinct().Count() != adjustments.Shadows.Count
            || adjustments.Shadows.Any(s => !candidates.Contains(s.Defender) || !threats.Any(t => t.Id == s.Receiver)))
            throw new ArgumentException("Shadow assignments must use distinct available primary defenders and visible targets; reserved shell/rush actors cannot be borrowed.");
        if (adjustments.Bracket is { } bracket && (!threats.Any(t => t.Id == bracket.Receiver)
            || !jobs.TryGetValue(bracket.Helper, out var helperJob) || helperJob.Pass is not HelpResponsibility { Role: HelperRole.LowMiddle or HelperRole.Robber }))
            throw new ArgumentException("A bracket requires a visible target and an existing low/robber helper; primary and deep-shell budgets cannot be consumed.");
        var costs = new double[threats.Length, candidates.Length];
        for (int t = 0; t < threats.Length; t++)
        for (int d = 0; d < candidates.Length; d++)
            costs[t, d] = adjustments.Shadows.Any(s => s.Defender == candidates[d] && s.Receiver != threats[t].Id
                || s.Receiver == threats[t].Id && s.Defender != candidates[d]) ? double.PositiveInfinity : PairingCost(candidates[d], threats[t], surface);
        var pairing = ExactManMatcher.Solve(costs);
        for (int t = 0; t < threats.Length; t++)
        {
            var slot = candidates[pairing.CandidateIndices[t]];
            var target = threats[t]; var role = surface.Threats.Single(r => r.ActorId == target.Id);
            float leverage = target.Position.X < surface.CenterX ? .5f : target.Position.X > surface.CenterX ? -.5f : 0;
            float depth = slot.GetPositionType() == DefensivePosition.DB ? 4.5f : 3.5f;
            jobs.Add(slot, new(slot, slot.GetPositionType(), Position(target.Position.X + leverage, depth),
                new ManResponsibility(target.Id), new(RunRole.Pursuit, role.Side, RunTrigger.ObservedRunCommitment)));
        }
        foreach (var extra in active.Where(s => !jobs.ContainsKey(s))) AddHelp(extra, HelperRole.LowMiddle);
        if (adjustments.Spy is { } spy)
        {
            if (!jobs.TryGetValue(spy, out var spyJob) || spyJob.Pass is not HelpResponsibility { Role: HelperRole.LowMiddle or HelperRole.Robber }
                || adjustments.Bracket?.Helper == spy)
                throw new ArgumentException("A spy consumes an existing low helper; it cannot also be a bracket, receiver primary, rusher or deep safety.");
            jobs[spy] = spyJob with { Pass = new HelpResponsibility(HelperRole.Spy) };
        }
        if (adjustments.Bracket is { } binding)
        {
            var helper = jobs[binding.Helper];
            jobs[binding.Helper] = helper with { Pass = ((HelpResponsibility)helper.Pass) with { BracketTarget = binding.Receiver } };
        }
        // Resolve physical spacing after ownership is final. Prefer depth changes to lateral travel.
        var placed = new List<DefensiveAssignment>();
        foreach (var job in jobs.Values.OrderBy(j => j.Pass is RushResponsibility ? 0 : j.Pass is ManResponsibility ? 1 : 2).ThenBy(j => j.ActorId))
        {
            Vector2 p = job.Alignment;
            if (placed.Any(a => Vector2.Distance(a.Alignment, p) < MinimumSpacing))
            {
                Vector2? best = null; float bestCost = float.PositiveInfinity;
                for (int y = 0; y <= 12; y++)
                for (int x = -8; x <= 8; x++)
                {
                    var candidate = p + new Vector2(x * .5f, y * .5f);
                    if (!LegalAlignment(candidate, offense.LineOfScrimmage)
                        || placed.Any(a => Vector2.Distance(a.Alignment, candidate) < MinimumSpacing)) continue;
                    float score = MathF.Abs(x) * 2 + y;
                    if (score < bestCost) { best = candidate; bestCost = score; }
                }
                p = best ?? throw new InvalidOperationException("No legal separated alignment for the compiled ownership.");
            }
            placed.Add(job with { Alignment = p });
        }
        var plan = new ResolvedDefensivePlan(call, offense, surface, personnel, resolutionSeed, RunFitCompiler.Bind(surface, placed).Assignments, pairing.TotalCost, adjustments);
        DefensivePlanValidator.ThrowIfInvalid(plan);
        return plan;

        Vector2 Position(float x, float depth) => new(Math.Clamp(x, Constants.DefenderRadius, Constants.FieldWidth - Constants.DefenderRadius),
            Math.Clamp(offense.LineOfScrimmage + depth * FieldGeometry.CoverageDepthScale(offense.LineOfScrimmage),
                offense.LineOfScrimmage + .1f, Constants.FieldLength - Constants.DefenderRadius));
        void AddRush(DefenderSlot slot, LineGap gap)
        {
            var contain = slot == DefenderSlot.DE1 ? ContainRole.LeftEdge : slot == DefenderSlot.DE2 ? ContainRole.RightEdge : ContainRole.None;
            jobs.Add(slot, new(slot, slot.GetPositionType(), Position(gap.X, 1.5f), new RushResponsibility(gap.Index, contain),
                new(contain == ContainRole.None ? RunRole.Gap : RunRole.Force, gap.Side, RunTrigger.ObservedRunCommitment, gap.Index)));
        }
        void AddHelp(DefenderSlot slot, HelperRole role)
        {
            float x = role == HelperRole.DeepLeft ? surface.CenterX - 10 : role == HelperRole.DeepRight ? surface.CenterX + 10 : surface.CenterX;
            bool deep = role is HelperRole.DeepLeft or HelperRole.DeepMiddle or HelperRole.DeepRight;
            jobs.Add(slot, new(slot, slot.GetPositionType(), Position(x, deep ? 14 : role == HelperRole.Robber ? 7 : 6),
                new HelpResponsibility(role), new(deep ? RunRole.DeepSupport : RunRole.Cutback,
                    x < surface.CenterX ? FieldSide.Left : x > surface.CenterX ? FieldSide.Right : FieldSide.Balanced, RunTrigger.ObservedRunCommitment)));
        }
    }

    internal static DefenderSlot[] ActiveSlots(DefensePackage package) => Enum.GetValues<DefenderSlot>()
        .Where(s => s != (package == DefensePackage.Base ? DefenderSlot.NB : DefenderSlot.MLB)).ToArray();

    internal static bool LegalAlignment(Vector2 p, float los) => OffensiveAlignmentSnapshot.InField(p)
        && p.X >= Constants.DefenderRadius && p.X <= Constants.FieldWidth - Constants.DefenderRadius
        && p.Y >= los + .05f && p.Y <= Constants.FieldLength - Constants.DefenderRadius;

    internal static double PairingCost(DefenderSlot slot, VisibleSkillPlayer player, OffensiveSurface surface)
    {
        var threat = surface.Threats.Single(t => t.ActorId == player.Id);
        float side = slot is DefenderSlot.CB1 or DefenderSlot.OLB1 ? -1 : slot is DefenderSlot.CB2 or DefenderSlot.OLB2 ? 1
            : slot == DefenderSlot.NB ? surface.Strength == FieldSide.Left ? -1 : 1 : 0;
        float offset = slot is DefenderSlot.CB1 or DefenderSlot.CB2 ? 17 : slot == DefenderSlot.NB ? 10 : 6;
        float home = surface.CenterX + side * offset;
        bool db = slot.GetPositionType() == DefensivePosition.DB;
        // Locality and travel matter; personnel preference is soft so legal LB/WR mismatches remain visible.
        return Math.Abs(player.Position.X - home)
            + (!db && !threat.Attached && !threat.Backfield ? 14 : 0)
            + (db && threat.Backfield ? 10 : 0)
            + (db && threat.Attached ? 4 : 0)
            + (side != 0 && (int)threat.Side != 0 && Math.Sign(side) != (int)threat.Side ? 4 : 0);
    }
}

internal sealed record ManPairing(IReadOnlyList<int> CandidateIndices, double TotalCost);

/// <summary>Exact minimum-cost injective assignment. At most seven candidates: O(targets * 2^candidates * candidates).</summary>
internal static class ExactManMatcher
{
    internal static ManPairing Solve(double[,] costs)
    {
        int targets = costs.GetLength(0), candidates = costs.GetLength(1);
        if (candidates > 7 || targets > candidates) throw new ArgumentException("Unsupported man assignment size.");
        foreach (double entryCost in costs) if (double.IsNaN(entryCost) || double.IsNegativeInfinity(entryCost)) throw new ArgumentException("Invalid cost.");
        var memo = new Dictionary<(int, int), (double Cost, int Choice)>();
        double Search(int t, int mask)
        {
            if (t == targets) return 0;
            if (memo.TryGetValue((t, mask), out var entry)) return entry.Cost;
            double best = double.PositiveInfinity; int choice = -1;
            for (int d = 0; d < candidates; d++)
            {
                if ((mask & (1 << d)) != 0) continue;
                double total = costs[t, d] + Search(t + 1, mask | (1 << d));
                // Stable candidate order resolves exact ties, independent of enumeration order or seed.
                if (total < best) { best = total; choice = d; }
            }
            memo[(t, mask)] = (best, choice); return best;
        }
        double cost = Search(0, 0);
        if (!double.IsFinite(cost)) throw new InvalidOperationException("No complete legal man assignment exists.");
        int used = 0; var result = new int[targets];
        for (int t = 0; t < targets; t++) { result[t] = memo[(t, used)].Choice; used |= 1 << result[t]; }
        return new(Array.AsReadOnly(result), cost);
    }
}
