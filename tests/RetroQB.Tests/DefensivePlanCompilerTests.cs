using System.Numerics;
using System.Text.Json;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Gameplay.Defense;
using RetroQB.Routes;
using PlanAssignment = RetroQB.Gameplay.Defense.DefensiveAssignment;
using Surface = RetroQB.Gameplay.Defense.OffensiveSurface;

namespace RetroQB.Tests;

public sealed class DefensivePlanCompilerTests
{
    private static FormationResult Formation(string id = "pass.mesh", bool flip = false, float los = 60)
        => new FormationFactory().CreateFormation(PlayResolver.Resolve(new PlayManager().Catalog[id], flip), los);
    private static ResolvedDefensivePlan Compile(FormationResult formation, string call = "def.cover2-man", DefensePackage? package = null, float los = 60)
        => DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(formation, los), DefensiveCallDefinition.Get(call), package);

    [Theory]
    [InlineData(11f)] [InlineData(60f)] [InlineData(109f)]
    public void ALIGN01_05_06_07_AllCatalogFlipsPackagesAndBudgetsAreLegal(float los)
    {
        var catalog = new PlayManager().Catalog;
        Assert.Equal(108, catalog.Plays.Count);
        int count = 0; bool heavy = false;
        foreach (var definition in catalog.Plays)
        foreach (bool flip in new[] { false, true })
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var call in DefensiveCallDefinition.Supported)
        {
            var formation = new FormationFactory().CreateFormation(PlayResolver.Resolve(definition, flip), los);
            var plan = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(formation, los), call, package);
            Assert.Empty(DefensivePlanValidator.Validate(plan));
            Assert.Equal(formation.Receivers.Count, plan.Assignments.Count(a => a.Pass is ManResponsibility));
            Assert.Equal(DefensivePersonnelRoles.Controlled(package), plan.ControlledActor);
            Assert.Equal(call.RushCount, plan.Assignments.Count(a => a.Pass is RushResponsibility));
            heavy |= formation.Receivers.Count < 5; count++;
        }
        Assert.True(heavy); Assert.Equal(3240, count);
    }

    [Fact]
    public void DM01_FivePrimariesTwoSafetiesAndExplicitLegalLinebackerMismatch()
    {
        var plan = Compile(Formation(), package: DefensePackage.Nickel);
        var primaries = plan.Assignments.Where(a => a.Pass is ManResponsibility).ToArray();
        Assert.Equal(5, primaries.Length);
        Assert.Equal(5, primaries.Select(a => ((ManResponsibility)a.Pass).Target).Distinct().Count());
        Assert.Equal(new HelpResponsibility(HelperRole.DeepLeft), plan.Assignments.Single(a => a.ActorId == DefenderSlot.FS).Pass);
        Assert.Equal(new HelpResponsibility(HelperRole.DeepRight), plan.Assignments.Single(a => a.ActorId == DefenderSlot.SS).Pass);
        Assert.Contains(primaries, a => a.PositionRole == DefensivePosition.LB
            && plan.Offense.Skills.Single(p => p.Id == ((ManResponsibility)a.Pass).Target).Slot.IsWideReceiverSlot());
        foreach (var owner in primaries)
        {
            var target = plan.Offense.Skills.Single(p => p.Id == ((ManResponsibility)owner.Pass).Target);
            Assert.InRange(MathF.Abs(owner.Alignment.X - target.Position.X), 0, 4.5f);
        }
    }

    [Fact]
    public void DM01_ActorIdentitySurvivesShuffledNoncontiguousIds()
    {
        var formation = Formation();
        var original = OffensiveAlignmentSnapshot.From(formation, 60);
        var skills = original.Skills.Select((p, i) => p with { Id = new(101 + i * 13) }).ToArray();
        var a = new OffensiveAlignmentSnapshot(original.Ball, original.Quarterback, 60, skills, original.Linemen);
        var b = new OffensiveAlignmentSnapshot(original.Ball, original.Quarterback, 60, skills.Reverse(), original.Linemen.Reverse());
        var p = DefensivePlanCompiler.Compile(a, DefensiveCallDefinition.Get("def.cover2-man"));
        var q = DefensivePlanCompiler.Compile(b, p.Call);
        Assert.Equal(p.Assignments, q.Assignments); Assert.Equal(p.Surface.Threats, q.Surface.Threats);
        Assert.Equal(skills.Select(s => s.Id.Value).Order(), p.Assignments.Select(j => j.Pass).OfType<ManResponsibility>().Select(m => m.Target.Value).Order());
        // Caller-owned collections cannot mutate the snapshot after compilation.
        skills[0] = skills[0] with { Position = Vector2.Zero };
        Assert.NotEqual(Vector2.Zero, a.Skills[0].Position);
        Assert.Throws<NotSupportedException>(() => ((IList<PlanAssignment>)p.Assignments).Clear());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ALIGN03_DM02_TwinsAndClosedTeAreBoundToVisibleGeometry(bool flip)
    {
        var plan = Compile(Formation("pass.pistol-twins.scissors", flip), "def.cover1", DefensePackage.Base);
        var te = plan.Offense.Skills.Single(p => p.Slot == ReceiverSlot.TE1);
        var role = plan.Surface.Threats.Single(t => t.ActorId == te.Id);
        Assert.True(role.Attached); Assert.Equal(flip ? FieldSide.Left : FieldSide.Right, role.Side);
        Assert.Contains(plan.Assignments, a => a.Pass == new ManResponsibility(te.Id));
        Assert.Equal(flip ? FieldSide.Right : FieldSide.Left, plan.Surface.Strength);
        foreach (var a in plan.Assignments.Where(a => a.Pass is ManResponsibility))
            Assert.InRange(MathF.Abs(a.Alignment.X - plan.Offense.Skills.Single(p => p.Id == ((ManResponsibility)a.Pass).Target).Position.X), 0, 4.5f);
    }

    [Fact]
    public void ALIGN02_TripsRanksReserveWeakReceiverAndBackWithExplicitLockCheck()
    {
        var plan = Compile(Formation("pass.gun-trips.mills"), "def.cover1");
        Assert.Equal(FieldSide.Right, plan.Surface.Strength);
        var strong = plan.Surface.Threats.Where(t => t.Side == FieldSide.Right && !t.Backfield).OrderBy(t => t.OutsideIn).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, strong.Select(t => t.OutsideIn));
        Assert.Equal("LockAllPrimaries", plan.TripsCheck);
        var owners = plan.Assignments.Select(a => a.Pass).OfType<ManResponsibility>().Select(a => a.Target).ToArray();
        Assert.All(plan.Surface.Threats.Where(t => t.Backfield || t.Side == FieldSide.Left), t => Assert.Contains(t.ActorId, owners));
    }

    [Fact]
    public void ALIGN01_BalancedAceKeepsCornersAndUnderneathOwnersLocal()
    {
        var plan = Compile(Formation("pass.ace.mills"), "def.cover2-man", DefensePackage.Base);
        Assert.Equal(FieldSide.Balanced, plan.Surface.Strength);
        var expected = new Dictionary<DefenderSlot, ReceiverSlot>
        {
            [DefenderSlot.CB1] = ReceiverSlot.WR1, [DefenderSlot.CB2] = ReceiverSlot.WR2,
            [DefenderSlot.OLB1] = ReceiverSlot.TE1, [DefenderSlot.OLB2] = ReceiverSlot.TE2,
            [DefenderSlot.MLB] = ReceiverSlot.RB1
        };
        foreach (var pair in expected)
        {
            var target = Assert.IsType<ManResponsibility>(plan.Assignments.Single(a => a.ActorId == pair.Key).Pass).Target;
            Assert.Equal(pair.Value, plan.Offense.Skills.Single(p => p.Id == target).Slot);
        }
    }

    [Fact]
    public void ALIGN05_FiveDetachedThreatsSuggestNickelButRetainExplicitBase()
    {
        var snapshot = OffensiveAlignmentSnapshot.From(Formation(), 60);
        var detached = new OffensiveAlignmentSnapshot(snapshot.Ball, snapshot.Quarterback, 60,
            snapshot.Skills.Select(p => p.Slot == ReceiverSlot.TE1 ? p with { Position = new(43, 59) } : p), snapshot.Linemen);
        var call = DefensiveCallDefinition.Get("def.cover2-man");
        var suggested = DefensivePlanCompiler.Compile(detached, call);
        Assert.Equal(5, suggested.Surface.DetachedCount); Assert.Equal(DefensePackage.Nickel, suggested.Package);
        var retained = DefensivePlanCompiler.Compile(detached, call, DefensePackage.Base);
        Assert.Equal(DefensePackage.Base, retained.Package); Assert.Empty(DefensivePlanValidator.Validate(retained));
        Assert.Equal(5, retained.Assignments.Count(a => a.Pass is ManResponsibility));
    }

    [Fact]
    public void BunchGroupsAreMetadataAndNeverAdvertiseAnUnimplementedSwitch()
    {
        var plan = Compile(Formation("pass.bunch-quick"), "def.cover1");
        Assert.Contains(plan.Surface.Threats, t => t.BunchGroup != null);
        Assert.Equal("LockActorIds", plan.BunchCheck);
    }

    [Fact]
    public void ALIGN06_HeavyUsesActualLineGapsAndAssignsSurplusHelper()
    {
        var plan = Compile(Formation("run.hb-dive"), "def.cover2-man", DefensePackage.Base);
        Assert.Equal(6, plan.Offense.Linemen.Count); Assert.Equal(4, plan.Offense.Skills.Count);
        Assert.Equal(7, plan.Surface.Gaps.Count);
        Assert.Equal(3, plan.Assignments.Count(a => a.Pass is HelpResponsibility));
        var shifted = new OffensiveAlignmentSnapshot(plan.Offense.Ball, plan.Offense.Quarterback, 60, plan.Offense.Skills,
            plan.Offense.Linemen.Select(l => l with { Position = l.Position + new Vector2(1, 0) }));
        var other = DefensivePlanCompiler.Compile(shifted, plan.Call, plan.Package);
        Assert.Equal(plan.Surface.Gaps.Select(g => g.X + 1), other.Surface.Gaps.Select(g => g.X));
    }

    [Fact]
    public void DM18_HiddenRoutesBlocksAndEligibilityCannotChangeThePlan()
    {
        var formation = Formation(); var before = Compile(formation);
        foreach (var r in formation.Receivers) { r.IsBlocking = true; r.Eligible = false; r.Route = RouteType.Flat; }
        var after = Compile(formation);
        Assert.True(before.Offense.SameGeometry(after.Offense));
        Assert.Equal(before.Assignments, after.Assignments);
    }

    [Fact]
    public void ALIGN07_FlipRecompilesAndTwoFlipsRestoreWithoutRandomDraws()
    {
        var play = PlayResolver.Resolve(new PlayManager().Catalog["pass.pistol-twins.scissors"]);
        ResolvedDefensivePlan Build(ResolvedPlay p) => Compile(new FormationFactory().CreateFormation(p, 60), "def.cover1");
        var original = Build(play); var flipped = Build(play.Flip()); var restored = Build(play.Flip().Flip());
        Assert.NotEqual(original.Surface.Strength, flipped.Surface.Strength);
        Assert.Equal(original.ControlledActor, flipped.ControlledActor);
        Assert.Equal(original.Assignments, restored.Assignments);
        Assert.Equal(original.ResolutionSeed, restored.ResolutionSeed);
    }

    [Fact]
    public void ExactSolverBeatsGreedyAndRejectsAnImpossibleGraph()
    {
        // Greedy would take cost 1 for row 0 and 100 for row 1. Global optimum is 4.
        var result = ExactManMatcher.Solve(new double[,] { { 1, 2 }, { 2, 100 } });
        Assert.Equal(4, result.TotalCost); Assert.Equal(new[] { 1, 0 }, result.CandidateIndices);
        Assert.Throws<InvalidOperationException>(() => ExactManMatcher.Solve(new double[,] { { 1, double.PositiveInfinity }, { 2, double.PositiveInfinity } }));
    }

    [Fact]
    public void ExactSolverMatchesIndependentBruteForceAcrossCostMatrices()
    {
        var random = new Random(202);
        for (int sample = 0; sample < 30; sample++)
        {
            var costs = new double[5, 7];
            for (int t = 0; t < 5; t++) for (int d = 0; d < 7; d++) costs[t, d] = random.Next(30);
            double Brute(int row, HashSet<int> used)
            {
                if (row == 5) return 0;
                double best = double.PositiveInfinity;
                for (int d = 0; d < 7; d++) if (used.Add(d)) { best = Math.Min(best, costs[row, d] + Brute(row + 1, used)); used.Remove(d); }
                return best;
            }
            Assert.Equal(Brute(0, new()), ExactManMatcher.Solve(costs).TotalCost);
        }
    }

    [Fact]
    public void PreviewInstantiationPreservesPlanRatingsAndCannotEnterLegacySetup()
    {
        var play = PlayResolver.Resolve(new PlayManager().Catalog["pass.mesh"]);
        var plan = Compile(Formation());
        var setup = new PlaySetupController(new FormationFactory(), new DefenseFactory(), new DefensiveCoordinator(new()), new Random(99));
        var a = setup.SetupPreview(play, plan, OffensiveTeamAttributes.Default, DefensiveTeamPresets.ScarletGuard);
        var b = setup.SetupPreview(play, plan, OffensiveTeamAttributes.Default, DefensiveTeamPresets.ScarletGuard);
        Assert.Same(plan, a.Plan); Assert.Equal(plan.Assignments.Select(j => j.Alignment), a.Defenders.Select(d => d.Position));
        Assert.Equal(a.Defenders.Select(d => d.Position), b.Defenders.Select(d => d.Position));
        foreach (var d in a.Defenders)
        {
            var reference = new Defender(d.Position, d.PositionRole, d.Slot, DefensiveTeamPresets.ScarletGuard);
            Assert.Equal(reference.Speed, d.Speed); Assert.Equal(reference.TackleMultiplier, d.TackleMultiplier);
            Assert.Equal(-1, d.CoverageReceiverIndex); // Typed ownership never leaks into legacy list-index fields.
        }
        a.Defenders[0].Position = Vector2.Zero;
        Assert.NotEqual(Vector2.Zero, plan.Assignments[0].Alignment);
        Assert.False(typeof(PlaySetupResult).IsAssignableFrom(a.GetType()));
        Assert.Throws<ArgumentException>(() => setup.SetupPreview(play.Flip(), plan, OffensiveTeamAttributes.Default, DefensiveTeamAttributes.Default));
    }

    [Fact]
    public void ValidatorRejectsCorruptOwnershipBudgetsGeometrySlotsAndJobs()
    {
        var plan = Compile(Formation());
        var primary = plan.Assignments.First(a => a.Pass is ManResponsibility);
        void Reject(IEnumerable<PlanAssignment> assignments) => Assert.NotEmpty(DefensivePlanValidator.Validate(
            new(plan.Call, plan.Offense, plan.Surface, plan.Package, plan.ResolutionSeed, assignments, 0)));
        Reject(plan.Assignments.Skip(1));
        Reject(plan.Assignments.Select(a => a == primary ? a with { Pass = new ManResponsibility(new(9999)) } : a));
        Reject(plan.Assignments.Select(a => a == primary ? a with { Alignment = new(float.NaN, 60) } : a));
        Reject(plan.Assignments.Select(a => a == primary ? a with { Pass = null! } : a));
        Reject(plan.Assignments.Select(a => a == primary ? a with { Run = null! } : a));
        Reject(plan.Assignments.Select(a => a.ActorId == DefenderSlot.FS ? a with { Pass = new HelpResponsibility(HelperRole.LowMiddle) } : a));
        Reject(plan.Assignments.Select(a => a == primary ? a with { ActorId = DefenderSlot.DT1 } : a));
        Assert.Throws<NotSupportedException>(() => DefensiveCallDefinition.Get("def.quarters-match"));
        Assert.Throws<NotSupportedException>(() => DefensivePlanCompiler.Compile(plan.Offense, plan.Call with { RushCount = 6 }));
    }

    [Fact]
    public void ALIGN07_CacheUsesVisibleGeometryAndEverySupportedAdjustment()
    {
        var formation = Formation(); var snapshot = OffensiveAlignmentSnapshot.From(formation, 60);
        var call = DefensiveCallDefinition.Get("def.cover1"); var cache = new DefensivePlanCache();
        var first = cache.Resolve(snapshot, call);
        foreach (var r in formation.Receivers) r.IsBlocking = true;
        Assert.Same(first, cache.Resolve(OffensiveAlignmentSnapshot.From(formation, 60), call));
        Assert.NotSame(first, cache.Resolve(snapshot, call, resolutionSeed: 202));
        var basePlan = cache.Resolve(snapshot, call, DefensePackage.Base);
        Assert.Equal(DefensePackage.Base, basePlan.Package);
        Assert.NotSame(basePlan, cache.Resolve(snapshot, call, DefensePackage.Nickel));
        var otherCall = cache.Resolve(snapshot, DefensiveCallDefinition.Get("def.cover2-man"));
        Assert.NotEqual(first.Call, otherCall.Call);
        cache.Clear(); Assert.NotSame(otherCall, cache.Resolve(snapshot, otherCall.Call));
    }

    [Fact]
    public void SnapshotRejectsDuplicateIdsAndNonFiniteCoordinates()
    {
        var s = OffensiveAlignmentSnapshot.From(Formation(), 60);
        Assert.Throws<ArgumentException>(() => new OffensiveAlignmentSnapshot(s.Ball, s.Quarterback, 60,
            s.Skills.Select(p => p with { Id = new(1) }), s.Linemen));
        Assert.Throws<ArgumentException>(() => new OffensiveAlignmentSnapshot(new(float.PositiveInfinity, 0), s.Quarterback, 60, s.Skills, s.Linemen));
    }
}
