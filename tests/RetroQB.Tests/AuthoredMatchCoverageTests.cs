using System.Numerics;
using System.Text.Json;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class AuthoredMatchCoverageTests
{
    private sealed class Fixture
    {
        public ResolvedDefensivePlan Plan { get; }
        public CoverageCoordinator Coordinator { get; }
        public Dictionary<ReceiverId, ObservedReceiver> Receivers { get; }
        public Dictionary<DefenderSlot, DefenderMotion> Actors { get; }
        private float _seconds;
        public Fixture(MatchFamily family, bool trips = false, bool closed = false, float los = 60)
        {
            var (_, offense, _) = DefensiveRuntimeTests.Setup(offense: "pass.gun-doubles.pa-cross", los: los);
            float[] xs = trips ? [7, 30, 38, 46, 26] : [8, 18, 36, 46, 26];
            for (int i = 0; i < offense.Receivers.Count; i++) offense.Receivers[i].Position = new(xs[i], los - (i == 4 ? 6 : 0));
            var original = OffensiveAlignmentSnapshot.From(offense, los);
            ReceiverSlot[] slots = [ReceiverSlot.WR1, ReceiverSlot.WR2, ReceiverSlot.WR3, ReceiverSlot.WR4, ReceiverSlot.RB1];
            var snapshot = new OffensiveAlignmentSnapshot(original.Ball, original.Quarterback, los,
                original.Skills.Select(s => s with { Slot = slots[s.Id.Value] }), original.Linemen);
            if (closed)
                snapshot = new(snapshot.Ball, snapshot.Quarterback, los,
                    snapshot.Skills.Where(s => s.Id.Value != 1).Select(s => s.Id.Value == 0 ? s with { Slot = ReceiverSlot.TE1, Position = new(snapshot.Linemen.Min(l => l.Position.X) - 1.5f, los) } : s),
                    snapshot.Linemen.Append(new(5, new(snapshot.Linemen.Max(l => l.Position.X) + 1.5f, los))));
            Plan = DefensivePlanCompiler.Compile(snapshot, DefensiveCallDefinition.Matches.Single(c => c.Match == family));
            Assert.NotNull(Plan.Match);
            Coordinator = new(Plan);
            Receivers = Plan.Offense.Skills.ToDictionary(r => r.Id, r => new ObservedReceiver(r.Id, r.Position, Vector2.Zero, ReleaseDirection.Unknown, false, false, false, false, 1));
            Actors = Plan.Assignments.ToDictionary(a => a.ActorId, a => new DefenderMotion(a.ActorId, a.Alignment, Vector2.Zero, 8, .65f, 1, false));
        }
        public MatchReleaseRule Rule(int receiver) => Plan.Match!.Releases.Single(r => r.Receiver.Value == receiver);
        public void Motion(int receiver, Vector2 position, Vector2 velocity, ReleaseDirection release, bool vertical = false)
            => Receivers[new(receiver)] = Receivers[new(receiver)] with { Position = position, Velocity = velocity, Release = release, Vertical = vertical };
        public void Ready(DefenderSlot actor, int receiver)
            => Actors[actor] = Actors[actor] with { Position = Receivers[new(receiver)].Position + Receivers[new(receiver)].Velocity * .75f + new Vector2(0, 2), Contact = false };
        public IReadOnlyList<DefensiveIntent> Step(float dt = .2f, DefenderSlot? human = null, BallState ball = BallState.HeldByQB)
        {
            _seconds += dt;
            var sight = new DefensiveSighting(Plan.Offense.Quarterback, new(ball, Plan.Offense.Quarterback, Vector2.Zero, 0, null),
                Receivers.Values.Select(r => new VisibleMotion(r.Id, r.Position, r.Velocity, .6f)), Actors.Values);
            var intents = Coordinator.Decide(new(_seconds, sight, Receivers.Values.OrderBy(r => r.Id.Value).ToArray(), false, 0, 0), human);
            Assert.Equal(Plan.Offense.Skills.Count, intents.Count(i => i.Primary != null));
            Assert.Equal(Plan.Offense.Skills.Count, intents.Where(i => i.Primary != null).Select(i => i.Primary).Distinct().Count());
            return intents;
        }
    }

    [Fact]
    public void VersionedCatalogCompilesEverySurfaceOrNamesFallbackWithoutChangingHistoricalCalls()
    {
        int matched = 0, fallback = 0;
        foreach (var play in new PlayManager().Catalog.Plays)
        foreach (bool flipped in new[] { false, true })
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var call in DefensiveCallDefinition.Matches)
        foreach (float los in new[] { 30f, 109f })
        {
            var f = new FormationFactory().CreateFormation(PlayResolver.Resolve(play, flipped), los);
            var p = DefensivePlanCompiler.Compile(OffensiveAlignmentSnapshot.From(f, los), call, package);
            Assert.Empty(DefensivePlanValidator.Validate(p)); Assert.Equal(call, p.RequestedCall);
            if (p.Match != null)
            {
                matched++; Assert.Equal(7, p.Match.Roles.Select(r => r.Actor).Distinct().Count());
                Assert.Equal(p.Offense.Skills.Count, p.Match.Releases.Count);
            }
            else
            {
                fallback++; Assert.NotNull(p.FallbackReason); Assert.Contains(p.Call.Id, new[] { "def.cover1", "def.cover2-man" });
                var actors = p.Assignments.Select(a => new Defender(a.Alignment, a.PositionRole, a.ActorId)).ToList();
                Assert.Contains("fallback", DefensivePlanPresentation.Project(p, actors).Definition.Name);
            }
        }
        Assert.True(matched > 0 && fallback > 0);
        Assert.Equal(SpotZoneFamily.Buzz, DefensiveCallDefinition.Get("def.cover3-match").Zone);
        Assert.Equal("Cover 3 Buzz", DefensivePlaybook.Get("def.cover3-match").Name);
        Assert.Equal("Quarters Zone", DefensivePlaybook.Get("def.quarters-match").Name);
        Assert.All(DefensiveCallDefinition.Matches, c => Assert.Equal(2, c.Version));
    }

    [Theory] [InlineData(MatchFamily.Cover3)] [InlineData(MatchFamily.Quarters)]
    public void VisibleNumberingAndRoleBindingsIgnoreEnumerationAndRetainClosedSide(MatchFamily family)
    {
        var f = new Fixture(family, closed: true); var p = f.Plan;
        var snapshot = new OffensiveAlignmentSnapshot(p.Offense.Ball, p.Offense.Quarterback, p.Offense.LineOfScrimmage, p.Offense.Skills.Reverse(), p.Offense.Linemen.Reverse());
        var repeated = DefensivePlanCompiler.Compile(snapshot, p.Call, p.Package);
        Assert.Equal(JsonSerializer.Serialize(p, DefenseRedesignMatrix.Json), JsonSerializer.Serialize(repeated, DefenseRedesignMatrix.Json));
        var closed = f.Rule(0); Assert.Equal(1, closed.Number);
        f.Motion(0, new(15, 68), new(0, 6), ReleaseDirection.Upfield, true);
        var intents = f.Step(); Assert.Equal(closed.InitialOwner, intents.Single(i => i.Primary == new ReceiverId(0)).Actor);
        Assert.Contains(family == MatchFamily.Cover3 ? "M3-07" : "QM-09", intents.Single(i => i.Primary == new ReceiverId(0)).Rule);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void Cover3LocksVerticalsAcrossCutsAndRetainsBlockingBackOwner(int hz)
    {
        var f = new Fixture(MatchFamily.Cover3);
        foreach (int id in new[] { 0, 1, 2, 3 }) f.Motion(id, new(f.Receivers[new(id)].Position.X, 68), new(0, 6), ReleaseDirection.Upfield, true);
        var original = f.Step(1f / hz).Where(i => i.Primary != null).ToDictionary(i => i.Primary!.Value, i => i.Actor);
        f.Motion(1, new(38, 72), Vector2.Zero, ReleaseDirection.Unknown);
        for (int i = 0; i < hz; i++)
        {
            var decisions = f.Step(1f / hz);
            Assert.All(decisions.Where(d => d.Primary != null), d => Assert.Equal(original[d.Primary!.Value], d.Actor));
        }
        Assert.Contains(f.Coordinator.Current!.Commitments, c => c.Primary == new ReceiverId(1) && c.Status.Contains("MatchedActorLock"));
        Assert.Equal(f.Rule(4).InitialOwner, f.Coordinator.Owners[new(4)]);
    }

    [Theory] [InlineData(MatchFamily.Cover3)] [InlineData(MatchFamily.Quarters)]
    public void ShallowCrosserCannotStealBackOwnerAndCarriesPastHomeWhenHelpRefuses(MatchFamily family)
    {
        var f = new Fixture(family); var rule = f.Rule(1);
        f.Motion(1, new(30, 62), new(6, 0), ReleaseDirection.Inward);
        foreach (var dest in rule.ShallowDestinations) f.Actors[dest] = f.Actors[dest] with { Contact = true };
        bool declined = false;
        for (int i = 0; i < 10; i++) { f.Step(); declined |= f.Coordinator.Current!.Transfers.Any(t => t.Stage == "Decline"); }
        Assert.Equal(rule.InitialOwner, f.Coordinator.Owners[new(1)]);
        Assert.Equal(f.Rule(4).InitialOwner, f.Coordinator.Owners[new(4)]);
        Assert.True(declined);
        Assert.Contains(f.Coordinator.Current!.Concessions, c => c.Contains("VacatedUnderneathArea"));
    }

    [Theory] [InlineData(MatchFamily.Cover3)] [InlineData(MatchFamily.Quarters)]
    public void ShortOutStaysWithApexThenWheelTransfersOnlyToReachableDeclaredSafety(MatchFamily family)
    {
        var f = new Fixture(family); var rule = f.Rule(1);
        f.Motion(0, new(8, 69), new(0, 6), ReleaseDirection.Upfield, true);
        f.Motion(1, new(12, 63), new(-5, 0), ReleaseDirection.Outward);
        f.Step(); Assert.Equal(rule.InitialOwner, f.Coordinator.Owners[new(1)]);
        f.Motion(1, new(12, 69), new(0, 6), ReleaseDirection.Upfield, true);
        if (family == MatchFamily.Quarters)
        {
            var safety = rule.VerticalDestination!.Value;
            f.Actors[safety] = f.Actors[safety] with { Position = new(50, 100) };
            var pending = f.Step(); Assert.Equal(rule.InitialOwner, f.Coordinator.Owners[new(1)]);
            Assert.DoesNotContain(pending, i => i.Actor == safety && i.Primary != null);
            f.Ready(safety, 1); f.Step(); Assert.Equal(safety, f.Coordinator.Owners[new(1)]);
            f.Motion(1, new(34, 72), new(6, 0), ReleaseDirection.Inward); f.Step();
            Assert.Equal(safety, f.Coordinator.Owners[new(1)]);
        }
        else { f.Step(); Assert.Equal(rule.InitialOwner, f.Coordinator.Owners[new(1)]); }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Cover3ShortOutsideVerticalInsideChainCommitsCompletelyOrKeepsBothOwners(bool refuse)
    {
        var f = new Fixture(MatchFamily.Cover3); var outside = f.Rule(0); var inside = f.Rule(1);
        f.Motion(0, new(15, 63), new(5, 0), ReleaseDirection.Inward);
        f.Motion(1, new(18, 69), new(0, 6), ReleaseDirection.Upfield, true);
        var hook = outside.ShallowDestinations.First(d => !f.Coordinator.Owners.Values.Contains(d));
        f.Ready(hook, 0); f.Ready(outside.InitialOwner, 1);
        if (refuse) f.Actors[outside.InitialOwner] = f.Actors[outside.InitialOwner] with { Contact = true };
        f.Step();
        Assert.Equal(refuse ? outside.InitialOwner : hook, f.Coordinator.Owners[new(0)]);
        Assert.Equal(refuse ? inside.InitialOwner : outside.InitialOwner, f.Coordinator.Owners[new(1)]);
        Assert.Equal(refuse ? 0 : 2, f.Coordinator.Current!.Transfers.Count(t => t.Stage == "Commit"));
    }

    [Theory] [InlineData(MatchFamily.Cover3)] [InlineData(MatchFamily.Quarters)]
    public void TripsPrebindsFiveOwnersAndWeakHelperNeverDropsBackOrIsolatedReceiver(MatchFamily family)
    {
        var f = new Fixture(family, trips: true); var match = f.Plan.Match!;
        Assert.Equal(5, f.Coordinator.Owners.Count);
        Assert.Equal(family == MatchFamily.Quarters ? match.Actor(MatchRole.StrongSafety) : match.Actor(MatchRole.StrongApex), f.Coordinator.Owners[new(2)]);
        Assert.Equal(family == MatchFamily.Quarters ? match.Actor(MatchRole.StrongApex) : match.Actor(MatchRole.StrongHook), f.Coordinator.Owners[new(1)]);
        foreach (int id in new[] { 0, 1, 2, 3 }) f.Motion(id, new(f.Receivers[new(id)].Position.X, 68), new(0, 6), ReleaseDirection.Upfield, true);
        if (family == MatchFamily.Quarters) f.Ready(match.Actor(MatchRole.WeakSafety), 1);
        f.Step();
        Assert.Equal(f.Rule(0).InitialOwner, f.Coordinator.Owners[new(0)]);
        Assert.Equal(f.Rule(4).InitialOwner, f.Coordinator.Owners[new(4)]);
        if (family == MatchFamily.Quarters)
        {
            Assert.Equal(match.Actor(MatchRole.WeakSafety), f.Coordinator.Owners[new(1)]);
            f.Motion(0, new(7, 90), new(0, 8), ReleaseDirection.Upfield, true);
            var decisions = f.Step(); Assert.Equal(match.Actor(MatchRole.WeakSafety), f.Coordinator.Owners[new(1)]);
            Assert.DoesNotContain(decisions, i => i.Actor == match.Actor(MatchRole.WeakSafety) && i.Helping == new ReceiverId(0));
        }
        else Assert.Contains(f.Step(), i => i.Actor == match.Actor(MatchRole.MiddleSafety) && i.Helping == new ReceiverId(1));
    }

    [Theory] [InlineData(MatchFamily.Cover3)] [InlineData(MatchFamily.Quarters)]
    public void TripsShallowUsesFreeWeakApexWhileBackRemainsAccountedFor(MatchFamily family)
    {
        var f = new Fixture(family, trips: true); var apex = f.Plan.Match!.Actor(MatchRole.WeakApex);
        f.Motion(1, new(24, 62), new(-5, 0), ReleaseDirection.Inward); f.Ready(apex, 1);
        f.Step(); Assert.Equal(apex, f.Coordinator.Owners[new(1)]);
        Assert.Equal(f.Rule(4).InitialOwner, f.Coordinator.Owners[new(4)]);
        if (family == MatchFamily.Quarters)
        {
            f.Motion(1, new(22, 68), new(0, 6), ReleaseDirection.Upfield, true);
            var safety = f.Plan.Match.Actor(MatchRole.WeakSafety); f.Ready(safety, 1); f.Step();
            Assert.Equal(safety, f.Coordinator.Owners[new(1)]);
        }
    }

    [Fact]
    public void HumanAcceptanceUsesActualStateAndAbandonmentNeverChangesPrimaryByItself()
    {
        var f = new Fixture(MatchFamily.Quarters); var safety = f.Rule(1).VerticalDestination!.Value;
        f.Motion(1, new(18, 68), new(0, 6), ReleaseDirection.Upfield, true);
        f.Actors[safety] = f.Actors[safety] with { Position = new(50, 100) };
        f.Step(human: safety); Assert.Equal(f.Rule(1).InitialOwner, f.Coordinator.Owners[new(1)]);
        f.Ready(safety, 1); var before = f.Actors[safety].Position; f.Step(human: safety);
        Assert.Equal(safety, f.Coordinator.Owners[new(1)]); Assert.Equal(before, f.Actors[safety].Position);
        f.Actors[safety] = f.Actors[safety] with { Position = new(50, 100), Velocity = new(0, -8) };
        f.Step(human: safety); Assert.Equal(safety, f.Coordinator.Owners[new(1)]);
        Assert.Contains(f.Coordinator.Current!.Commitments, c => c.Actor == safety && c.HumanAbandonment);
    }

    [Fact]
    public void NumberKeysPreviewReplayAndFallbackRetainVersionedMeaning()
    {
        var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: "pass.gun-trips.mills", engine: DefensiveEngineMode.MatchDevelopment);
        var session = new DefensivePlaySession(drive, 101); var offense = drive.Actors.Receivers[0];
        session.Update(.01f, new(Call: 7));
        Assert.Equal("def.quarters-match.v2", drive.CompiledPlan!.RequestedCall.Id); Assert.Same(offense, drive.Actors.Receivers[0]);
        var preview = drive.CompiledPlan; drive.Execution.Control = new(true) { AutomateDefense = true }; drive.Snap();
        for (int i = 0; drive.Live && i < 1800; i++) drive.Update(1f / 60);
        Assert.False(drive.Live); Assert.Same(preview, drive.LastReplay!.MatchContext!.CompiledPlan);
        Assert.Equal(preview.Call.Id, drive.LastReplayDefense!.Definition.Id);
        Assert.NotEqual("Quarters Zone", drive.LastReplayDefense.Definition.Name);
    }

    [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void QuartersTwoByTwoSafetiesAcceptVerticalsAndCornersKeepTheirOutsideReceivers(int hz)
    {
        var f = new Fixture(MatchFamily.Quarters);
        foreach (int id in new[] { 0, 1, 2, 3 }) f.Motion(id, new(f.Receivers[new(id)].Position.X, 68), new(0, 6), ReleaseDirection.Upfield, true);
        f.Ready(f.Rule(1).VerticalDestination!.Value, 1); f.Ready(f.Rule(2).VerticalDestination!.Value, 2);
        f.Step(1f / hz);
        Assert.Equal(f.Rule(1).VerticalDestination, f.Coordinator.Owners[new(1)]);
        Assert.Equal(f.Rule(2).VerticalDestination, f.Coordinator.Owners[new(2)]);
        var owners = f.Coordinator.Owners.ToDictionary();
        f.Motion(1, new(25, 71), new(5, -2), ReleaseDirection.Inward);
        f.Motion(2, new(27, 70), Vector2.Zero, ReleaseDirection.Unknown);
        for (int i = 0; i < hz; i++)
        {
            var frame = f.Step(1f / hz);
            Assert.All(frame.Where(d => d.Primary != null), d => Assert.Equal(owners[d.Primary!.Value], d.Actor));
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void QuartersShallowOutsideTransfersOnlyAfterInsideVerticalHasAnOwner(bool blocked)
    {
        var f = new Fixture(MatchFamily.Quarters); var corner = f.Rule(0).InitialOwner; var apex = f.Rule(1).InitialOwner;
        var safety = f.Rule(1).VerticalDestination!.Value;
        f.Motion(0, new(15, 63), new(5, 0), ReleaseDirection.Inward);
        f.Motion(1, new(18, 69), new(0, 6), ReleaseDirection.Upfield, true);
        f.Ready(safety, 1); f.Ready(apex, 0);
        if (blocked) f.Actors[apex] = f.Actors[apex] with { Contact = true };
        var intents = f.Step();
        Assert.Equal(safety, f.Coordinator.Owners[new(1)]);
        Assert.Equal(blocked ? corner : apex, f.Coordinator.Owners[new(0)]);
        if (!blocked) Assert.Contains(intents, i => i.Actor == corner && i.Primary == null && i.Helping == new ReceiverId(1));
    }

    [Fact]
    public void ThrowStopsNewTransactionsAndRuntimeSuspensionFreezesMatchState()
    {
        var f = new Fixture(MatchFamily.Quarters);
        f.Motion(1, new(18, 68), new(0, 6), ReleaseDirection.Upfield, true); f.Ready(f.Rule(1).VerticalDestination!.Value, 1);
        f.Step(ball: BallState.InAir);
        Assert.Equal(f.Rule(1).InitialOwner, f.Coordinator.Owners[new(1)]);
        Assert.Empty(f.Coordinator.Current!.Transfers);
        var runtime = new DefensiveRuntime(f.Plan);
        var sight = new DefensiveSighting(f.Plan.Offense.Quarterback, new(BallState.HeldByQB, f.Plan.Offense.Ball, Vector2.Zero, 0, null),
            f.Plan.Offense.Skills.Select(r => new VisibleMotion(r.Id, r.Position, Vector2.Zero, .6f)), f.Actors.Values);
        var before = runtime.Decide(sight, .1f);
        Assert.Same(before, runtime.Decide(sight, 10, suspended: true));
        Assert.Equal(5, before!.Decisions.Count(d => d.Primary != null));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void QuartersWheelAcceptancePrecedesWithdrawalOfExistingSafetyHelp(bool trips)
    {
        var f = new Fixture(MatchFamily.Quarters, trips);
        int wheel = 1, capped = trips ? 2 : 0;
        var safety = f.Rule(wheel).VerticalDestination!.Value;
        f.Motion(capped, new(f.Receivers[new(capped)].Position.X, 69), new(0, 6), ReleaseDirection.Upfield, true);
        f.Motion(wheel, new(trips ? 35 : 12, 63), new(trips ? 5 : -5, 0), ReleaseDirection.Outward);
        var first = f.Step(); Assert.Equal(new ReceiverId(capped), first.Single(i => i.Actor == safety).Helping);
        f.Motion(wheel, new(trips ? 35 : 12, 70), new(0, 6), ReleaseDirection.Upfield, true);
        f.Actors[safety] = f.Actors[safety] with { Position = new(50, 100) };
        var pending = f.Step(); Assert.Equal(new ReceiverId(capped), pending.Single(i => i.Actor == safety).Helping);
        Assert.Equal(f.Rule(wheel).InitialOwner, f.Coordinator.Owners[new(wheel)]);
        f.Ready(safety, wheel); var accepted = f.Step();
        Assert.Equal(new ReceiverId(wheel), accepted.Single(i => i.Actor == safety).Primary);
        Assert.Null(accepted.Single(i => i.Actor == safety).Helping);
    }

    [Theory] [InlineData("def.cover3-match.v2")] [InlineData("def.quarters-match.v2")]
    public void HiddenRouteAndBlockingFlagsCannotChangeMatchDecisions(string call)
    {
        var (plan, offense, actors) = DefensiveRuntimeTests.Setup(call, "pass.gun-doubles.pa-cross");
        Assert.NotNull(plan.Match); var first = new DefensiveRuntime(plan); var second = new DefensiveRuntime(plan);
        for (int tick = 0; tick < 120; tick++)
        {
            foreach (var receiver in offense.Receivers) { receiver.Position += new Vector2(0, .1f); receiver.Velocity = new(0, 6); }
            var a = first.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / 60);
            foreach (var receiver in offense.Receivers)
            { receiver.Route = RouteType.Wheel; receiver.IsBlocking = !receiver.IsBlocking; receiver.Eligible = !receiver.Eligible; receiver.RouteStart = Vector2.Zero; }
            var b = second.Decide(DefensiveRuntimeTests.Sight(offense, actors), 1f / 60);
            Assert.Equal(JsonSerializer.Serialize(a, DefenseRedesignMatrix.Json), JsonSerializer.Serialize(b, DefenseRedesignMatrix.Json));
        }
    }
}
