using System.Numerics;
using System.Text.Json;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Tests;

public sealed class SpotZoneCoverageTests
{
    private sealed class Fixture
    {
        public ResolvedDefensivePlan Plan { get; }
        public CoverageCoordinator Board { get; }
        public Dictionary<ReceiverId, ObservedReceiver> Receivers { get; }
        public Dictionary<DefenderSlot, DefenderMotion> Actors { get; }
        public float Time;
        public Fixture(string call = "def.cover3")
        {
            Plan = DefensiveRuntimeTests.Setup(call, "pass.four-verts").Plan; Board = new(Plan);
            Receivers = Plan.Offense.Skills.ToDictionary(r => r.Id, r => new ObservedReceiver(r.Id, new(26, 55), Vector2.Zero, ReleaseDirection.Unknown, false, false, false, false, 0));
            Actors = Plan.Assignments.ToDictionary(a => a.ActorId, a => new DefenderMotion(a.ActorId, a.Alignment, Vector2.Zero, 10, 1, 1, false));
        }
        public ReceiverId Id(int index) => Plan.Offense.Skills[index].Id;
        public void Receiver(int index, float x, float y, float vx = 0, float vy = 0, bool vertical = false)
        { var id = Id(index); Receivers[id] = new(id, new(x, y), new(vx, vy), vy > 0 ? ReleaseDirection.Upfield : ReleaseDirection.Unknown, vertical, false, false, false, 1); }
        public void Actor(DefenderSlot slot, float x, float y, float vx = 0, float vy = 0)
            => Actors[slot] = Actors[slot] with { Position = new(x, y), Velocity = new(vx, vy) };
        public IReadOnlyList<DefensiveIntent> Step(float dt = .02f, DefenderSlot? human = null)
        {
            Time += dt;
            var visible = new DefensiveSighting(Plan.Offense.Quarterback, new(BallState.HeldByQB, Plan.Offense.Ball, Vector2.Zero, 0, null),
                Receivers.Values.Select(r => new VisibleMotion(r.Id, r.Position, r.Velocity, 1)), Actors.Values);
            var result = Board.Decide(new(Time, visible, Receivers.Values.OrderBy(r => r.Id.Value).ToArray(), false, 0, 0), human);
            Assert.Equal(result.Count(i => i.Primary != null), result.Where(i => i.Primary != null).Select(i => i.Primary).Distinct().Count());
            return result;
        }
    }

    [Theory]
    [InlineData("def.cover2-zone", 2, 5)] [InlineData("def.cover3", 3, 4)]
    [InlineData("def.cover3-match", 3, 4)] [InlineData("def.cover4", 4, 3)]
    public void CompiledShellHasExactlySevenAreaJobsAndDeclaredNeighbors(string call, int deep, int under)
    {
        var f = new Fixture(call); var zones = f.Plan.Assignments.Select(a => a.Pass).OfType<ZoneResponsibility>().ToArray();
        Assert.Empty(DefensivePlanValidator.Validate(f.Plan)); Assert.Equal(deep, zones.Count(z => z.IsDeep)); Assert.Equal(under, zones.Count(z => !z.IsDeep));
        Assert.All(zones, z => Assert.NotEmpty(z.Neighbors));
        Assert.Throws<ArgumentException>(() => DefensivePlanCompiler.Compile(f.Plan.Offense, f.Plan.Call, adjustments: new(switchPairs: true)));
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void DM04_FourVerticalsKeepOutsideOwnersAndBothSeamCarriersUntilOneSafetyAccepts(int hz)
    {
        var f = new Fixture(); f.Receiver(0, 7, 62, vy: 6); f.Receiver(1, 20, 62, vy: 6);
        f.Receiver(2, 33, 62, vy: 6); f.Receiver(3, 46, 62, vy: 6);
        f.Actor(DefenderSlot.FS, 26, 76); var initial = f.Step(1f / hz);
        Assert.Equal(f.Id(0), initial.Single(d => d.Actor == DefenderSlot.CB1).Primary);
        Assert.Equal(f.Id(3), initial.Single(d => d.Actor == DefenderSlot.CB2).Primary);
        Assert.Contains(initial, d => d.Primary == f.Id(1)); Assert.Contains(initial, d => d.Primary == f.Id(2));
        f.Receiver(1, 20, 68, vy: 6, vertical: true); f.Receiver(2, 33, 68, vy: 6, vertical: true);
        var after = f.Step(1f / hz); Assert.Equal(1, f.Board.Current!.Transfers.Count(t => t.Stage == "Commit"));
        Assert.Contains(f.Board.Current.Transfers, t => t.Stage == "Decline" && t.Reason.Contains("DestinationReserved"));
        Assert.Contains(after, d => d.Primary == f.Id(1)); Assert.Contains(after, d => d.Primary == f.Id(2));
        var current = f.Board.Owners.ToArray(); f.Step(1f / hz); Assert.Equal(current, f.Board.Owners.ToArray());
    }

    [Fact]
    public void DM05_Cover2SafetyTakesSeamWhileCornerRetainsFadeAndFlatIsExplicitlyConceded()
    {
        var f = new Fixture("def.cover2-zone");
        var left = f.Plan.Surface.Threats.Where(t => t.Side == FieldSide.Left).OrderBy(t => t.OutsideIn).ToArray();
        int outside = f.Plan.Offense.Skills.ToList().FindIndex(r => r.Id == left[0].ActorId);
        int seam = f.Plan.Offense.Skills.ToList().FindIndex(r => r.Id == left[1].ActorId);
        int flat = Enumerable.Range(0, 5).First(i => i != outside && i != seam);
        f.Receiver(outside, 6, 62, vy: 6); f.Receiver(seam, 18, 62, vy: 6); f.Receiver(flat, 3, 61);
        f.Actor(DefenderSlot.CB1, 6, 74); f.Actor(DefenderSlot.FS, 16, 76); f.Step();
        f.Receiver(outside, 6, 68, vy: 6, vertical: true); f.Receiver(seam, 18, 68, vy: 6, vertical: true);
        var next = f.Step();
        Assert.Equal(f.Id(seam), next.Single(d => d.Actor == DefenderSlot.FS).Primary);
        Assert.Equal(f.Id(outside), next.Single(d => d.Actor == DefenderSlot.CB1).Primary);
        Assert.Contains(f.Board.Current!.Concessions, c => c.Contains($"UnownedWindow:{f.Id(flat).Value}"));
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void DM06_PostTransferCommitsOnceThenOverlapExpiresAndCornerRecovers(int hz)
    {
        var f = new Fixture(); f.Receiver(0, 12, 73, vx: 6, vertical: true); f.Actor(DefenderSlot.FS, 24, 78);
        f.Step(1f / hz); f.Receiver(0, 19, 73, vx: 6, vertical: true);
        var accepted = f.Step(1f / hz); var events = f.Board.Current!.Transfers;
        Assert.Equal(new[] { "Offer", "Reserve", "Accept", "Commit" }, events.Select(t => t.Stage));
        Assert.Equal(f.Id(0), accepted.Single(d => d.Actor == DefenderSlot.FS).Primary);
        Assert.Equal(f.Id(0), accepted.Single(d => d.Actor == DefenderSlot.CB1).Helping);
        for (int i = 0; i < hz / 5 + 1; i++) f.Step(1f / hz);
        var corner = f.Board.Current!.Commitments.Single(c => c.Actor == DefenderSlot.CB1);
        // Corner may cap within the boundary overlap, but no longer follows the transferred post as temporary carry.
        Assert.NotEqual("Accepted;TemporaryHelp", corner.Status); Assert.Null(corner.Primary);
        Assert.DoesNotContain(f.Board.Current.Transfers, t => t.Stage == "Commit");
        Assert.Single(f.Board.Owners);
    }

    [Fact]
    public void DM06_UnreachablePostDestinationDeclinesWithoutDroppingDeepOwnerOrSpammingOffers()
    {
        var f = new Fixture(); f.Receiver(0, 12, 73, vx: 6, vertical: true); f.Actor(DefenderSlot.FS, 48, 100); f.Step();
        f.Receiver(0, 19, 73, vx: 6, vertical: true); f.Step();
        Assert.Contains(f.Board.Current!.Transfers, t => t.Stage == "Decline" && t.Reason.Contains("Unreachable"));
        f.Step(); Assert.Empty(f.Board.Current!.Transfers);
        f.Receiver(0, 24, 73, vx: 6, vertical: true);
        for (int i = 0; i < 40; i++) f.Step();
        Assert.Equal(DefenderSlot.CB1, f.Board.Owners[f.Id(0)]);
        Assert.Contains(f.Board.Current!.Concessions, c => c.Contains("VacatedHomeForVertical"));
    }

    [Fact]
    public void ZONE01_DeclinedUnderneathCarryEndsAtTimeOrLateralLimitWithExplicitConcession()
    {
        foreach (bool lateral in new[] { false, true })
        {
            var f = new Fixture("def.cover4"); f.Receiver(0, 15, 64, vx: 5); f.Step();
            foreach (var slot in f.Actors.Keys.ToArray()) if (slot != DefenderSlot.OLB1) f.Actor(slot, 50, 110);
            f.Receiver(0, lateral ? 23 : 19, 64, vx: 5);
            var results = new List<string>();
            for (int i = 0; i < (lateral ? 1 : 32); i++) { f.Step(); results.AddRange(f.Board.Current!.Concessions); }
            Assert.Contains(results, c => c.Contains("ConcededReceiver"));
            Assert.NotEqual(DefenderSlot.OLB1, f.Board.Owners.GetValueOrDefault(f.Id(0)));
        }
    }

    [Fact]
    public void DEC04_ChainedExchangeCommitsAtomicallyWithoutDuplicateOwnership()
    {
        var f = new Fixture("def.cover4"); f.Receiver(0, 15, 64, vx: 2); f.Receiver(1, 20, 64, vx: -2); f.Step();
        DefenderSlot middle = f.Plan.Package == DefensePackage.Base ? DefenderSlot.MLB : DefenderSlot.NB;
        f.Actor(DefenderSlot.OLB1, 16, 66); f.Actor(middle, 20, 66);
        f.Receiver(0, 19, 64, vx: 2); f.Receiver(1, 16, 64, vx: -2); f.Step();
        var commits = f.Board.Current!.Transfers.Where(t => t.Stage == "Commit").ToArray();
        Assert.Equal(2, commits.Length); Assert.Single(commits.Select(t => t.OwnershipVersion).Distinct());
        Assert.Equal(middle, f.Board.Owners[f.Id(0)]); Assert.Equal(DefenderSlot.OLB1, f.Board.Owners[f.Id(1)]);
    }

    [Fact]
    public void XFER04_DM16_HumanReachabilityUsesVelocityAndRecordsLaterAbandonment()
    {
        var f = new Fixture("def.cover3"); var human = f.Plan.ControlledActor;
        var home = (ZoneResponsibility)f.Plan.Assignments.Single(a => a.ActorId == human).Pass;
        var neighbor = f.Plan.Assignments.First(a => a.Pass is ZoneResponsibility z && !z.IsDeep && z.Area.Left == home.Area.Right);
        var source = (ZoneResponsibility)neighbor.Pass;
        f.Receiver(0, source.Area.Left + 1, 64, vx: -2); f.Step(human: human);
        f.Actor(human, home.Area.Right - 2, 66); f.Receiver(0, home.Area.Right - 1, 64, vx: -2);
        var next = f.Step(human: human);
        Assert.Equal(f.Id(0), next.Single(i => i.Actor == human).Primary);
        Assert.Contains(f.Board.Current!.Transfers, t => t.Stage == "Commit" && t.Destination == human);
        f.Actor(human, 2, 40, vy: -8); f.Step(human: human);
        Assert.True(f.Board.Current!.Commitments.Single(c => c.Actor == human).HumanAbandonment);
        Assert.Contains(f.Board.Current.Concessions, c => c.Contains("HumanAbandonment"));
        var r = f.Receivers[f.Id(0)];
        Assert.False(CoverageCoordinator.Reachable(f.Actors[human], r, home, true));
    }

    [Fact]
    public void Z304_BuzzRequiresPhysicalRotationAndKeepsThreeDeepActors()
    {
        var f = new Fixture("def.cover3-match"); var rotation = f.Plan.Assignments.Single(a => a.Pass is ZoneResponsibility { Rotates: true });
        var z = (ZoneResponsibility)rotation.Pass; Assert.NotEqual(z.Home, rotation.Alignment);
        var next = f.Step(); Assert.Equal("RotatingToCurlFlat", next.Single(i => i.Actor == rotation.ActorId).Reason);
        Assert.Equal(z.Home, next.Single(i => i.Actor == rotation.ActorId).Target);
        f.Actor(rotation.ActorId, z.Home.X, z.Home.Y); next = f.Step();
        Assert.DoesNotContain("Rotating", next.Single(i => i.Actor == rotation.ActorId).Reason);
        Assert.Equal(3, f.Plan.Assignments.Count(a => a.Pass is ZoneResponsibility { IsDeep: true }));
    }

    [Fact]
    public void Z4_OutsideVerticalAndInsideSeamRemainSeparateWhileShallowOutStaysUnderneath()
    {
        var f = new Fixture("def.cover4"); f.Receiver(0, 6, 68, vy: 6); f.Receiver(1, 20, 68, vy: 6); f.Receiver(2, 30, 63, vx: 4);
        var next = f.Step(); Assert.Equal(f.Id(0), next.Single(i => i.Actor == DefenderSlot.CB1).Primary);
        Assert.Equal(f.Id(1), next.Single(i => i.Actor == DefenderSlot.FS).Primary);
        Assert.DoesNotContain(next, i => i.Actor == DefenderSlot.SS && i.Primary == f.Id(2));
        f.Receiver(2, 31, 67, vy: 6, vertical: true); f.Actor(DefenderSlot.SS, 32, 73); next = f.Step();
        Assert.Contains(f.Board.Current!.Transfers, t => t.Stage == "Commit" && t.Receiver == f.Id(2));
        Assert.Equal(f.Id(0), next.Single(i => i.Actor == DefenderSlot.CB1).Primary);
    }

    [Theory]
    [InlineData("def.cover2-zone")] [InlineData("def.cover3")] [InlineData("def.cover3-match")] [InlineData("def.cover4")]
    public void LiveZoneCallCompletesProductionPlayAndRetainsBoardInReplay(string call)
    {
        var result = DefenseRuntimeMatrix.Exercise("pass.four-verts", call, 101, 60, true, DefenseInputMode.Stationary, DefensiveEngineMode.ZoneDevelopment);
        Assert.Null(result.Failure); Assert.NotEmpty(result.Decisions);
        Assert.All(result.Decisions, d => { if (d.Observation.ConfirmedCarrier) { Assert.Null(d.Coverage); Assert.Equal(11, d.Run!.Jobs.Count); } else Assert.NotNull(d.Coverage); });
    }

    [Fact]
    public void FullCatalogBothPackagesFlipsAndGoalLineHaveLegalZonePlans()
    {
        int count = 0;
        foreach (var play in new PlayManager().Catalog.Plays)
        foreach (bool flip in new[] { false, true })
        foreach (float los in new[] { 30f, 109f })
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var call in DefensiveCallDefinition.SpotZones)
        {
            var resolved = PlayResolver.Resolve(play, flip);
            var formation = new FormationFactory().CreateFormation(resolved, los);
            var plan = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(formation, los), call, package);
            Assert.Empty(DefensivePlanValidator.Validate(plan)); count++;
        }
        Assert.True(count >= 3200);
    }

    [Theory]
    [InlineData("def.cover2-zone")] [InlineData("def.cover3")] [InlineData("def.cover3-match")] [InlineData("def.cover4")]
    public void ZoneGoalLineFlipsAndBothHumanRolesProduceLegalTargets(string call)
    {
        foreach (bool human in new[] { true, false })
        {
            var result = DefenseRuntimeMatrix.Exercise("pass.pistol-twins.scissors", call, 202, 120, human,
                DefenseInputMode.Abandonment, DefensiveEngineMode.ZoneDevelopment, yard: 98, flipped: true);
            Assert.Null(result.Failure);
            Assert.All(result.Decisions.SelectMany(d => d.Decisions), i => { Assert.InRange(i.Target.X, 1.1f, 52.2f); Assert.InRange(i.Target.Y, 1.1f, 118.9f); });
        }
    }

    [Fact]
    public void SameSeedProducesIdenticalProductionBoardsAndDecisions()
    {
        var a = DefenseRuntimeMatrix.Exercise("pass.mesh", "def.cover3", 202, 60, true, DefenseInputMode.AssignmentFollowing, DefensiveEngineMode.ZoneDevelopment);
        var b = DefenseRuntimeMatrix.Exercise("pass.mesh", "def.cover3", 202, 60, true, DefenseInputMode.AssignmentFollowing, DefensiveEngineMode.ZoneDevelopment);
        Assert.Null(a.Failure); Assert.Null(b.Failure);
        Assert.Equal(JsonSerializer.Serialize(a, DefenseRedesignMatrix.Json), JsonSerializer.Serialize(b, DefenseRedesignMatrix.Json));
    }

    [Fact]
    public void ZoneRuntimeKeepsHumanIdleAndFreezesBoardForPauseWhistleAndReplay()
    {
        var input = new ScriptedInput(); var drive = new DefensiveDrive(input, 101, fixedCall: "pass.mesh", engine: DefensiveEngineMode.ZoneDevelopment);
        Assert.True(drive.SelectCompiledDefenseForDevelopment("def.cover3")); Assert.True(drive.Snap());
        var start = drive.Linebacker.Position;
        for (int i = 0; i < 12; i++) drive.Update(1f / 60);
        Assert.Equal(start, drive.Linebacker.Position);
        var runtime = drive.Execution.DefenseRuntime!; var snapshot = runtime.Current!;
        string saved = JsonSerializer.Serialize(snapshot, DefenseRedesignMatrix.Json);
        runtime.Decide(snapshot.Observation.Visible, 1, suspended: true); Assert.Same(snapshot, runtime.Current);
        for (int i = 0; drive.Live && i < 1800; i++) drive.Update(1f / 60);
        Assert.False(drive.Live); Assert.True(runtime.Stopped); Assert.NotNull(drive.LastReplay);
        Assert.Contains(drive.LastReplay!.Frames, f => f.DefensiveDecision?.Coverage != null);
        Assert.Equal(saved, JsonSerializer.Serialize(snapshot, DefenseRedesignMatrix.Json));
        var stopped = runtime.Current; runtime.Decide(snapshot.Observation.Visible, 1); Assert.Same(stopped, runtime.Current);
    }

    [Fact]
    public void ZoneDecisionsCannotReadHiddenRoutesOrBlockingFlags()
    {
        var (plan, offense, defenders) = DefensiveRuntimeTests.Setup("def.cover3");
        var a = new DefensiveRuntime(plan); var b = new DefensiveRuntime(plan);
        for (int i = 0; i < 60; i++)
        {
            foreach (var r in offense.Receivers) r.Position += new Vector2(.01f, .08f);
            a.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / 60);
            foreach (var r in offense.Receivers) { r.IsBlocking = !r.IsBlocking; r.Eligible = !r.Eligible; r.Route = RetroQB.Routes.RouteType.Wheel; }
            b.Decide(DefensiveRuntimeTests.Sight(offense, defenders), 1f / 60);
            Assert.Equal(JsonSerializer.Serialize(a.Current, DefenseRedesignMatrix.Json), JsonSerializer.Serialize(b.Current, DefenseRedesignMatrix.Json));
        }
    }

    [Fact]
    public void XFER04_AwayVelocityAndContactCanInvalidateOtherwiseNearbyAcceptance()
    {
        var f = new Fixture("def.cover4"); var z = (ZoneResponsibility)f.Plan.Assignments.Single(a => a.ActorId == DefenderSlot.OLB1).Pass;
        f.Receiver(0, 10, 65); var r = f.Receivers[f.Id(0)];
        var actor = f.Actors[DefenderSlot.OLB1] with { Position = new(10, 62), Speed = 10 };
        Assert.True(CoverageCoordinator.Reachable(actor, r, z, true));
        Assert.False(CoverageCoordinator.Reachable(actor with { Velocity = new(0, -10) }, r, z, true));
        Assert.False(CoverageCoordinator.Reachable(actor with { Contact = true }, r, z, true));
    }

    [Theory] [InlineData("def.cover2-zone")] [InlineData("def.cover3")]
    public void FloodKeepsDeepPrimaryIntermediateContestAndDeclaredShortConcession(string call)
    {
        var f = new Fixture(call); f.Receiver(0, 7, 62, vy: 6); f.Receiver(1, 12, 65, vx: -2); f.Receiver(2, 4, 61);
        var next = f.Step(); f.Receiver(0, 7, 73, vy: 6, vertical: true); f.Actor(DefenderSlot.FS, 10, 80); next = f.Step();
        Assert.Contains(next, i => i.Primary == f.Id(0));
        Assert.Contains(next, i => i.Primary == f.Id(1));
        Assert.DoesNotContain(next, i => ((f.Plan.Assignments.Single(a => a.ActorId == i.Actor).Pass as ZoneResponsibility)?.IsDeep ?? false) && i.Primary == f.Id(2));
    }

    [Fact]
    public void Z403_ShallowCrossersNeverCollapseEmptyQuarterStructure()
    {
        var f = new Fixture("def.cover4"); f.Receiver(0, 12, 64, vx: 4); f.Receiver(1, 30, 65, vx: -4);
        for (int i = 0; i < 30; i++)
        {
            var next = f.Step();
            foreach (var job in f.Plan.Assignments.Where(a => a.Pass is ZoneResponsibility { IsDeep: true }))
            {
                var intent = next.Single(d => d.Actor == job.ActorId);
                Assert.Null(intent.Primary); Assert.True(intent.Target.Y >= ((ZoneResponsibility)job.Pass).Home.Y);
            }
        }
    }

    [Fact]
    public void PostThatTurnsHorizontalStillNeedsAcceptedTransferAndCanCrossBackWithoutDuplicateOwners()
    {
        var f = new Fixture(); f.Receiver(0, 12, 73, vy: 6); f.Step();
        f.Receiver(0, 19, 73, vx: 2); f.Actor(DefenderSlot.FS, 22, 77); f.Step();
        Assert.Equal(DefenderSlot.FS, f.Board.Owners[f.Id(0)]);
        f.Receiver(0, 15, 73, vx: -2); f.Actor(DefenderSlot.CB1, 13, 77); f.Step();
        Assert.Equal(DefenderSlot.CB1, f.Board.Owners[f.Id(0)]);
        Assert.Single(f.Board.Current!.Transfers, t => t.Stage == "Commit");
    }

    [Fact]
    public void DeclinedOfferReevaluatesChangedReachabilityAndOccupiedDestinationCannotDropItsPrimary()
    {
        var f = new Fixture(); f.Receiver(0, 12, 73, vx: 2); f.Actor(DefenderSlot.FS, 49, 100); f.Step();
        f.Receiver(0, 19, 73, vx: 2); f.Step(); Assert.Equal(DefenderSlot.CB1, f.Board.Owners[f.Id(0)]);
        f.Actor(DefenderSlot.FS, 22, 77); f.Step(); Assert.Equal(DefenderSlot.FS, f.Board.Owners[f.Id(0)]);
        f.Receiver(1, 12, 73, vx: 2); f.Step(); f.Receiver(1, 19, 73, vx: 2); f.Step();
        Assert.Contains(f.Board.Current!.Transfers, t => t.Stage == "Decline" && t.Reason.Contains("Occupied"));
        Assert.Equal(DefenderSlot.CB1, f.Board.Owners[f.Id(1)]); Assert.Equal(DefenderSlot.FS, f.Board.Owners[f.Id(0)]);
    }

    [Fact]
    public void Z201_CornerKeepsDeclinedFadeBeyondOrdinaryUnderneathLimitAndOnlyReachableHookReplacesFlat()
    {
        foreach (bool reachable in new[] { true, false })
        {
            var f = new Fixture("def.cover2-zone"); f.Receiver(0, 6, 62, vy: 6); f.Receiver(1, 6, 61);
            f.Actor(DefenderSlot.FS, 49, 110); f.Actor(DefenderSlot.OLB1, reachable ? 9 : 35, reachable ? 65 : 95);
            f.Step(); f.Receiver(0, 6, 78, vy: 6, vertical: true);
            for (int i = 0; i < 60; i++) f.Step();
            Assert.Equal(DefenderSlot.CB1, f.Board.Owners[f.Id(0)]);
            if (reachable)
            {
                Assert.Equal(DefenderSlot.OLB1, f.Board.Owners[f.Id(1)]);
                Assert.Contains(f.Board.Current!.Concessions, c => c.Contains("VacatedHookForFlatReplacement"));
            }
            else Assert.False(f.Board.Owners.ContainsKey(f.Id(1)));
            Assert.Contains(f.Board.Current!.Concessions, c => c.Contains("VacatedHomeForVertical"));
        }
    }
}
