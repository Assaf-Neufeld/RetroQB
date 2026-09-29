using System.Numerics;

namespace RetroQB.Gameplay.Defense;

public static class DefensivePlanValidator
{
    public static IReadOnlyList<string> Validate(ResolvedDefensivePlan plan)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(plan.Package) || !DefensiveCallDefinition.All.Contains(plan.Call)) errors.Add("Unsupported package/call version.");
        if (!Enum.IsDefined(plan.Front)) errors.Add("Unknown front.");
        if (plan.Front != DefensiveFront.Even && plan.Pressure.Selection.Kind != PressureKind.Base) errors.Add("Pressure requires Even front.");
        var expected = DefensivePlanCompiler.ActiveSlots(plan.Package);
        if (plan.Assignments.Count != 11 || !plan.Assignments.Select(a => a.ActorId).Order().SequenceEqual(expected.Order()))
            errors.Add("Defense must contain exactly the eleven selected roster identities.");
        if (!plan.ControlledActor.IsLinebacker() || !plan.Assignments.Any(a => a.ActorId == plan.ControlledActor))
            errors.Add("Controlled linebacker is missing.");
        foreach (var job in plan.Assignments)
        {
            if (job.PositionRole != job.ActorId.GetPositionType()) errors.Add($"{job.ActorId}: wrong personnel type.");
            if (!DefensivePlanCompiler.LegalAlignment(job.Alignment, plan.Offense.LineOfScrimmage)) errors.Add($"{job.ActorId}: illegal alignment.");
            if (job.Pass is not (ManResponsibility or HelpResponsibility or RushResponsibility or ZoneResponsibility or MatchResponsibility)) errors.Add($"{job.ActorId}: missing/unsupported pass job.");
            if (job.Run == null || !Enum.IsDefined(job.Run.Role) || !Enum.IsDefined(job.Run.Side) || !Enum.IsDefined(job.Run.Trigger))
                errors.Add($"{job.ActorId}: invalid conditional run job.");
            else if (job.Run.GapIndex is { } runGap && !plan.Surface.Gaps.Any(g => g.Index == runGap)) errors.Add($"{job.ActorId}: unknown run gap.");
        }
        var owners = plan.Assignments.Select(a => a.Pass).OfType<ManResponsibility>().Select(m => m.Target.Value).Order();
        if (plan.Call.Zone == SpotZoneFamily.None && plan.Match == null && !owners.SequenceEqual(plan.Offense.Skills.Select(p => p.Id.Value).Order())) errors.Add("Every potential receiver needs exactly one primary man owner.");
        var rushers = plan.Assignments.Where(a => a.Pass is RushResponsibility).ToArray();
        int rushBudget = plan.Pressure.Selection.Kind == PressureKind.ZoneFive ? 5 : plan.Call.RushCount;
        if (rushers.Length != rushBudget) errors.Add("Wrong rush budget.");
        if (plan.Pressure.Drops.Count != (plan.Pressure.Selection.Kind is PressureKind.ZoneFive or PressureKind.Simulated ? 1 : 0)) errors.Add("Wrong declared drop budget.");
        foreach (var drop in plan.Pressure.Drops)
            if (!plan.Assignments.Any(a => a.ActorId == drop.Actor && a.Pass is ZoneResponsibility)
                || !OffensiveAlignmentSnapshot.InField(drop.Home)) errors.Add("Invalid declared drop.");
        var rushJobs = rushers.Select(a => (RushResponsibility)a.Pass).ToArray();
        if (rushJobs.Select(r => r.GapIndex).Distinct().Count() != rushJobs.Length
            || rushJobs.Any(r => !plan.Surface.Gaps.Any(g => g.Index == r.GapIndex) || !Enum.IsDefined(r.Contain))) errors.Add("Invalid/duplicate rush entry.");
        if (rushJobs.Count(r => r.Contain == ContainRole.LeftEdge) != 1
            || rushJobs.Count(r => r.Contain == ContainRole.RightEdge) != 1) errors.Add("Named edge contain missing.");
        foreach (var entry in plan.Pressure.Entries)
            if (!OffensiveAlignmentSnapshot.InField(entry.Entry) || entry.AlternateGap is { } alternate
                && (rushJobs.Any(r => r.GapIndex == alternate) || !plan.Surface.Gaps.Any(g => g.Index == alternate && !g.Outside)
                    || Math.Abs(alternate - entry.Gap) != 1 || entry.Contain != ContainRole.None)) errors.Add("Illegal pressure alternate lane.");
        if (plan.Pressure.Conditional is { } conditional)
        {
            var owner = plan.Assignments.SingleOrDefault(a => a.ActorId == conditional.Actor);
            if (rushers.Any(a => a.ActorId == conditional.Actor) || rushJobs.Any(r => r.GapIndex == conditional.Gap)
                || !plan.Surface.Gaps.Any(g => g.Index == conditional.Gap && !g.Outside)
                || (conditional.ReleaseKey is { } key ? owner?.Pass != new ManResponsibility(key)
                    : owner?.Pass is not HelpResponsibility { Role: HelperRole.LowMiddle, BracketTarget: null })) errors.Add("Invalid conditional pressure or preceding coverage job.");
        }
        if (plan.Pressure.Stunt is { } stunt && (stunt.Penetrator == stunt.Looper
            || !plan.Pressure.Entries.Any(e => e.Actor == stunt.Penetrator && e.Contain == ContainRole.None)
            || !plan.Pressure.Entries.Any(e => e.Actor == stunt.Looper && e.Contain == ContainRole.None)
            || rushJobs.Any(r => r.GapIndex == stunt.FallbackGap)
            || !plan.Surface.Gaps.Any(g => g.Index == stunt.FallbackGap && !g.Outside))) errors.Add("Invalid stunt participants/fallback.");
        if (plan.Call.Match != MatchFamily.None)
        {
            var matchJobs = plan.Assignments.Where(a => a.Pass is MatchResponsibility).ToArray();
            var matchOwners = matchJobs.Select(a => ((MatchResponsibility)a.Pass).Provisional).Where(id => id != null).Select(id => id!.Value.Value).Order();
            if (plan.Match == null || matchJobs.Length != 7 || plan.Match.Roles.Count != 7
                || plan.Match.Roles.Select(b => b.Actor).Distinct().Count() != 7
                || plan.Match.Roles.Select(b => b.Role).Distinct().Count() != 7
                || !matchOwners.SequenceEqual(plan.Offense.Skills.Select(s => s.Id.Value).Order())) errors.Add("Invalid match roles/initial ownership.");
            if (plan.Match != null)
            {
                if (!plan.Match.Releases.Select(r => r.Receiver.Value).Order().SequenceEqual(plan.Offense.Skills.Select(s => s.Id.Value).Order())) errors.Add("Match release table must account for every receiver.");
                foreach (var rule in plan.Match.Releases)
                    if (!matchJobs.Any(a => a.ActorId == rule.InitialOwner && ((MatchResponsibility)a.Pass).Provisional == rule.Receiver)
                        || rule.VerticalDestination is { } dest && !matchJobs.Any(a => a.ActorId == dest)
                        || rule.ShallowDestinations.Any(d => d == rule.InitialOwner || !matchJobs.Any(a => a.ActorId == d))) errors.Add("Illegal match release binding.");
                foreach (var binding in plan.Match.Roles)
                    if (!matchJobs.Any(a => a.ActorId == binding.Actor && ((MatchResponsibility)a.Pass).Role == binding.Role)) errors.Add("Match role disagrees with actor job.");
            }
        }
        else if (plan.Call.Zone == SpotZoneFamily.None)
        {
        if (plan.Assignments.Any(a => a.Pass is ZoneResponsibility)) errors.Add("Man call contains zone jobs.");
        var helpers = plan.Assignments.Where(a => a.Pass is HelpResponsibility).ToArray();
        if (helpers.Length != 11 - plan.Call.RushCount - plan.Offense.Skills.Count) errors.Add("Wrong helper budget.");
        if (helpers.Any(a => !Enum.IsDefined(((HelpResponsibility)a.Pass).Role))) errors.Add("Unknown helper role.");
        foreach (var helper in helpers)
        {
            var help = (HelpResponsibility)helper.Pass;
            if (help.BracketTarget is { } target && (help.Role is not (HelperRole.LowMiddle or HelperRole.Robber)
                || !plan.Offense.Skills.Any(r => r.Id == target) || plan.Adjustments.Bracket != new BracketAssignment(helper.ActorId, target)))
                errors.Add("Invalid or undeclared bracket helper.");
        }
        foreach (var shadow in plan.Adjustments.Shadows)
            if (!plan.Assignments.Any(a => a.ActorId == shadow.Defender && a.Pass == new ManResponsibility(shadow.Receiver))) errors.Add("Declared shadow was not preserved.");
        if (plan.Adjustments.Bracket is { } bracket && !helpers.Any(a => a.ActorId == bracket.Helper && ((HelpResponsibility)a.Pass).BracketTarget == bracket.Receiver))
            errors.Add("Declared bracket helper missing.");
        int deep = helpers.Count(a => ((HelpResponsibility)a.Pass).Role is HelperRole.DeepLeft or HelperRole.DeepMiddle or HelperRole.DeepRight);
        if (deep != plan.Call.RequiredDeepHelpers) errors.Add("Wrong deep shell budget.");
        void Require(DefenderSlot slot, HelperRole role)
        {
            if (!helpers.Any(a => a.ActorId == slot && ((HelpResponsibility)a.Pass).Role == role)) errors.Add($"Reserved helper {slot}/{role} missing.");
        }
        if (plan.Call.Shell == ManShell.TwoHigh) { Require(DefenderSlot.FS, HelperRole.DeepLeft); Require(DefenderSlot.SS, HelperRole.DeepRight); }
        else if (plan.Call.Shell != ManShell.Zero) Require(DefenderSlot.FS, HelperRole.DeepMiddle);
        if (helpers.Count(a => ((HelpResponsibility)a.Pass).Role == HelperRole.Spy) != (plan.Adjustments.Spy == null ? 0 : 1)
            || plan.Adjustments.Spy is { } spy && !helpers.Any(a => a.ActorId == spy && ((HelpResponsibility)a.Pass).Role == HelperRole.Spy)) errors.Add("Spy must consume its declared helper.");
        if (plan.Call.Shell == ManShell.Robber) Require(DefenderSlot.SS, plan.Adjustments.Spy == DefenderSlot.SS ? HelperRole.Spy : HelperRole.Robber);
        if (plan.Call.Shell == ManShell.SingleHigh && plan.Call.RushCount == 4) Require(DefenderSlot.SS, plan.Adjustments.Spy == DefenderSlot.SS ? HelperRole.Spy : HelperRole.LowMiddle);
        }
        else
        {
            var zones = plan.Assignments.Where(a => a.Pass is ZoneResponsibility).ToArray();
            int expectedDeep = plan.Call.Zone == SpotZoneFamily.Cover2 ? 2 : plan.Call.Zone == SpotZoneFamily.Cover4 ? 4 : 3;
            if (zones.Length != 11 - rushBudget || zones.Count(a => ((ZoneResponsibility)a.Pass).IsDeep) != expectedDeep)
                errors.Add("Wrong zone shell/underneath budget.");
            if (plan.Assignments.Any(a => a.Pass is ManResponsibility or HelpResponsibility)) errors.Add("Spot call contains man jobs.");
            foreach (var a in zones)
            {
                var z = (ZoneResponsibility)a.Pass;
                if (!Enum.IsDefined(z.Role) || !OffensiveAlignmentSnapshot.InField(z.Home)
                    || !float.IsFinite(z.Area.Left + z.Area.Right + z.Area.Shallow + z.Area.Deep)
                    || z.Area.Left < 0 || z.Area.Right > Constants.FieldWidth || z.Area.Left >= z.Area.Right
                    || z.Area.Shallow >= z.Area.Deep || !z.Area.Contains(z.Home)) errors.Add("Illegal zone area/home.");
                if (z.Neighbors.Distinct().Count() != z.Neighbors.Count || z.Neighbors.Any(n => n == a.ActorId || !zones.Any(b => b.ActorId == n)))
                    errors.Add("Illegal zone neighbor.");
            }
            if (zones.Count(a => ((ZoneResponsibility)a.Pass).Rotates) != (plan.Call.Zone == SpotZoneFamily.Buzz ? 1 : 0)) errors.Add("Wrong rotation budget.");
        }
        if (!plan.Fits.Gaps.Select(g => g.GapIndex).Order().SequenceEqual(plan.Surface.Gaps.Select(g => g.Index).Order())
            || plan.Fits.Gaps.Select(g => g.Owner).Distinct().Count() != plan.Fits.Gaps.Count) errors.Add("Each actual line gap needs one distinct conditional owner.");
        if (plan.Assignments.Count(a => a.Run?.Role == RunRole.DeepSupport) != 1
            || plan.Assignments.Count(a => a.Run is { Role: RunRole.Cutback, Side: FieldSide.Left }) != 1
            || plan.Assignments.Count(a => a.Run is { Role: RunRole.Cutback, Side: FieldSide.Right }) != 1
            || plan.Assignments.Count(a => a.Run is { Role: RunRole.Force, Side: FieldSide.Left }) != 1
            || plan.Assignments.Count(a => a.Run is { Role: RunRole.Force, Side: FieldSide.Right }) != 1) errors.Add("Named force/cutback/last-support structure missing.");
        for (int i = 0; i < plan.Assignments.Count; i++)
        for (int j = i + 1; j < plan.Assignments.Count; j++)
            if (Vector2.Distance(plan.Assignments[i].Alignment, plan.Assignments[j].Alignment) < DefensivePlanCompiler.MinimumSpacing - .0001f)
                errors.Add($"Overlapping alignment: {plan.Assignments[i].ActorId}/{plan.Assignments[j].ActorId}.");
        return errors.AsReadOnly();
    }

    public static void ThrowIfInvalid(ResolvedDefensivePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var errors = Validate(plan);
        if (errors.Count != 0) throw new InvalidOperationException(string.Join(" ", errors));
    }
}
