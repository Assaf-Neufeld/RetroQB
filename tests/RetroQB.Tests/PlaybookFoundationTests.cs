using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Input;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class PlaybookFoundationTests
{
    [Fact]
    public void HundredPlayCatalogDoesNotChangeHotkeyCapacity()
    {
        var catalog = LargeCatalog();
        var manager = new PlayManager(catalog);
        Assert.Equal(100, catalog.Plays.Count);
        Assert.Equal(10, manager.PassPlays.Count);
        Assert.Equal(10, manager.RunPlays.Count);
        string replacement = "test.pass.39";
        manager.SetCallSheet(new PlayCallSheet(catalog,
            manager.CallSheet.PassIds.Skip(1).Append(replacement), manager.CallSheet.RunIds));
        Assert.True(manager.SelectPassPlay(9, new Random(1)));
        Assert.Equal(replacement, manager.SelectedPlay.Id);
        Assert.Equal(9, manager.SelectedPlayIndex);
        for (int i = 0; i < 30; i++)
        {
            manager.AutoSelectPlayBySituation(new Random(i));
            Assert.Contains(manager.SelectedPlay.Id, manager.CallSheet.PassIds.Concat(manager.CallSheet.RunIds));
        }
    }

    [Fact]
    public void SelectionHistoryAndUsageFollowIdentityWhenHotkeysMove()
    {
        var manager = new PlayManager();
        manager.SelectPassPlay(1, new Random(1));
        string id = manager.SelectedPlay.Id;
        manager.FlipSelectedPlay();
        var selected = manager.SelectedPlay;
        manager.StartPlay();
        manager.StartPlayRecord(false, CoverageScheme.Cover2Zone, []);
        manager.FinalizePlayRecord(PlayOutcome.Incomplete, 0, null, null, false);
        manager.SetCallSheet(new PlayCallSheet(manager.Catalog,
            manager.CallSheet.PassIds.Reverse(), manager.CallSheet.RunIds.Reverse()));
        Assert.Same(selected, manager.SelectedPlay);
        Assert.Equal(8, manager.SelectedPlayIndex);
        Assert.Equal(1, manager.GetCallCount(id));
        var record = Assert.Single(manager.PlayRecords);
        Assert.Equal(id, record.OffensivePlayId);
        Assert.Equal("Mesh", record.OffensivePlayName);
        Assert.True(record.IsFlipped);
        manager.SelectPassPlay(0, new Random(1));
        Assert.Equal(0, manager.GetCallCount(manager.SelectedPlay.Id));
        manager.StartPlay();
        Assert.Equal(1, manager.GetCallCount(manager.SelectedPlay.Id));
        manager.StartNewGame();
        Assert.Equal(0, manager.GetCallCount(id));
    }

    [Fact]
    public void WildcardsAreIdentifiedByIdAndStayResolvedUntilSelectedAgain()
    {
        var manager = new PlayManager();
        var catalogWildcard = manager.Catalog["pass.wildcard"];
        manager.SetCallSheet(new PlayCallSheet(manager.Catalog,
            manager.CallSheet.PassIds.Reverse(), manager.CallSheet.RunIds.Reverse()));
        manager.SelectPassPlay(9, new Random(14));
        var resolved = manager.SelectedPlay;
        Assert.True(resolved.IsWildcard);
        Assert.Same(resolved, manager.PassPlays[9]);
        for (int i = 0; i < 3; i++)
        {
            var formation = Instantiate(resolved);
            Assert.All(formation.Receivers, r => Assert.Equal(resolved.Assignments[r.Slot].Route, r.Route));
            _ = manager.GetSuggestedPlayLabel();
            Assert.Same(resolved, manager.SelectedPlay);
        }
        manager.SelectPassPlay(9, new Random(15));
        Assert.Equal(resolved.Id, manager.SelectedPlay.Id);
        Assert.NotSame(resolved, manager.SelectedPlay);
        Assert.Same(catalogWildcard, manager.Catalog[resolved.Id]);
        Assert.NotSame(catalogWildcard, manager.SelectedPlay.Definition);
    }

    [Fact]
    public void AutomaticWildcardSelectionExecutesTheCandidateThatWasScored()
    {
        var expected = PlayResolver.Resolve(PlaybookBuilder.CreatePassWildcardPlay(new FirstCandidateRandom(14)));
        var manager = new PlayManager();
        Assert.True(manager.AutoSelectPlayBySituation(new FirstCandidateRandom(14)));
        Assert.Equal("pass.wildcard", manager.SelectedPlay.Id);
        Assert.Equal(expected.Formation, manager.SelectedPlay.Formation);
        Assert.Equal(expected.Players, manager.SelectedPlay.Players);
        Assert.Same(manager.SelectedPlay, manager.PassPlays[0]);
    }

    [Fact]
    public void PersonnelIsSharedAcrossDifferentAlignmentsAndQbDepthIsAuthored()
    {
        var baseFormation = FormationCatalog.Get(FormationType.BaseSplit);
        var runFormation = FormationCatalog.Get(FormationType.RunSinglebackTripsRight);
        Assert.Same(baseFormation.Personnel, runFormation.Personnel);
        Assert.NotEqual(baseFormation.AlignmentSlots, runFormation.AlignmentSlots);
        var alignment = new FormationAlignment(new(0.5f, 7), baseFormation.Alignment.SkillPositions, baseFormation.Alignment.Linemen);
        var definition = PlaybookBuilder.BuildPassPlays()[9];
        var deepQb = new FormationDefinition(baseFormation.Type, baseFormation.Personnel, alignment, baseFormation.AlignmentSlots);
        var result = Instantiate(PlayResolver.Resolve(new PlayDefinition("test.qb-depth", "Deep QB", PlayType.Pass,
            deepQb, definition.Assignments)));
        Assert.Equal(33f, result.Qb.Position.Y);
        Assert.Equal(5, result.Blockers.Count);
    }

    [Fact]
    public void AssignmentsFollowPlayerSlotsWhenReceiverIndicesAndOrderChange()
    {
        var play = PlayResolver.Resolve(PlaybookBuilder.BuildPassPlays()[4]);
        var formation = Instantiate(play);
        var reordered = formation.Receivers.AsEnumerable().Reverse()
            .Select((r, index) => new Receiver(index, r.Slot, r.Position)).ToArray();
        RouteAssigner.AssignRoutes(reordered, play);
        foreach (var receiver in reordered)
        {
            var original = formation.Receivers.Single(r => r.Slot == receiver.Slot);
            Assert.Equal(original.Route, receiver.Route);
            Assert.Equal(original.IsBlocking, receiver.IsBlocking);
            Assert.Equal(original.RouteSide, receiver.RouteSide);
        }
        var oldPriorities = new ReceiverPriorityManager();
        var newPriorities = new ReceiverPriorityManager();
        oldPriorities.AssignPriorities(formation.Receivers);
        newPriorities.AssignPriorities(reordered);
        foreach (var receiver in reordered)
            Assert.Equal(oldPriorities.GetPriorityLabel(formation.Receivers.Single(r => r.Slot == receiver.Slot).Index),
                newPriorities.GetPriorityLabel(receiver.Index));
    }

    [Fact]
    public void TightEndsCanHaveIndependentAssignmentsInAHeavyPassFormation()
    {
        var formation = FormationCatalog.Get(FormationType.RunPowerLeft);
        var assignments = formation.Personnel.Slots.ToDictionary(s => s, _ => new PlayerAssignment(AssignmentRole.Route, RouteType.Go));
        assignments[ReceiverSlot.TE1] = new(AssignmentRole.Block);
        assignments[ReceiverSlot.TE2] = new(AssignmentRole.Route, RouteType.OutShallow);
        var play = PlayResolver.Resolve(new PlayDefinition("test.heavy-pass", "Heavy Pass", PlayType.Pass, formation, assignments));
        var entities = Instantiate(play);
        Assert.True(entities.Receivers.Single(r => r.Slot == ReceiverSlot.TE1).IsBlocking);
        Assert.True(entities.Receivers.Single(r => r.Slot == ReceiverSlot.TE2).Eligible);
        Assert.Equal(RouteType.Go, entities.Receivers.Single(r => r.Slot == ReceiverSlot.RB1).Route);
        Assert.Equal(formation.Alignment.Linemen.Count, entities.Blockers.Count);
        Assert.DoesNotContain(ReceiverSlot.TE1, play.Routes.Keys);
        Assert.Contains(ReceiverSlot.TE2, play.Routes.Keys);
        Assert.True(PlaySuggestion.GetPlayWeight(play, new PlaySituation(3, 8, 40, 48)) > 0);
    }

    [Fact]
    public void RunCanUseAPassAlignmentWithoutBeingReplaced()
    {
        var formation = FormationCatalog.Get(FormationType.BaseSplit);
        var assignments = formation.Personnel.Slots.ToDictionary(s => s, s =>
            new PlayerAssignment(s == ReceiverSlot.RB1 ? AssignmentRole.BallCarrier : AssignmentRole.Block));
        var play = PlayResolver.Resolve(new PlayDefinition("test.spread-run", "Spread Run", PlayType.Run,
            formation, assignments, RunConcept.Power, -1));
        var entities = Instantiate(play);
        Assert.Equal(5, entities.Blockers.Count);
        Assert.Equal(3, entities.Receivers.Count(r => r.Slot.IsWideReceiverSlot()));
        Assert.Equal(formation.Alignment.SkillPositions[0].AtLineOfScrimmage(40), entities.Receivers[0].Position);
    }

    [Fact]
    public void HandoffAndMeshMovementUseDesignatedRb2InsteadOfFirstBack()
    {
        var alignment = FormationCatalog.Get(FormationType.RunIForm).Alignment;
        var personnel = new PersonnelPackage("two-backs", [ReceiverSlot.WR1, ReceiverSlot.RB1, ReceiverSlot.TE1, ReceiverSlot.RB2], 6);
        var formation = new FormationDefinition(FormationType.RunIForm, personnel, alignment);
        var assignments = personnel.Slots.ToDictionary(s => s, s =>
            new PlayerAssignment(s == ReceiverSlot.RB2 ? AssignmentRole.BallCarrier : AssignmentRole.Block));
        var definition = new PlayDefinition("test.rb2", "RB2 Run", PlayType.Run, formation, assignments, RunConcept.Dive);
        var manager = ManagerWith(definition);
        manager.SelectRunPlay(9, new Random(1));
        var entities = Instantiate(manager.SelectedPlay);
        var rb1 = entities.Receivers.Single(r => r.Slot == ReceiverSlot.RB1);
        var rb2 = entities.Receivers.Single(r => r.Slot == ReceiverSlot.RB2);
        var execution = new PlayExecutionController(new InputManager(), new BlockingController());
        rb1.Position = entities.Qb.Position;
        rb2.Position = entities.Qb.Position + new Vector2(0, -5);
        execution.TryHandoffToRunningBack(manager, entities.Ball, entities.Qb, entities.Receivers);
        Assert.Equal(BallState.HeldByQB, entities.Ball.State);
        var before = rb2.Position;
        new ReceiverUpdateController(new BlockingController()).UpdateAll(entities.Receivers, entities.Qb, entities.Ball,
            [], null, Vector2.Zero, false, false, false, manager, 0.1f, _ => { });
        Assert.True(rb2.Position.Y > before.Y);
        rb2.Position = entities.Qb.Position;
        execution.TryHandoffToRunningBack(manager, entities.Ball, entities.Qb, entities.Receivers);
        Assert.Same(rb2, entities.Ball.Holder);
        Assert.False(rb1.HasBall);
    }

    [Fact]
    public void ExplicitOutsideAndBackfieldRoutesAreNotSilentlyRewritten()
    {
        var source = PlaybookBuilder.BuildPassPlays()[9];
        var assignments = source.Assignments.ToDictionary();
        assignments[ReceiverSlot.WR1] = new(AssignmentRole.Route, RouteType.OutDeep);
        assignments[ReceiverSlot.RB1] = new(AssignmentRole.Route, RouteType.Go);
        var play = PlayResolver.Resolve(new PlayDefinition("test.routes", "Routes", PlayType.Pass, source.Formation, assignments));
        var entities = Instantiate(play);
        Assert.Equal(RouteType.OutDeep, entities.Receivers.Single(r => r.Slot == ReceiverSlot.WR1).Route);
        Assert.Equal(RouteType.Go, entities.Receivers.Single(r => r.Slot == ReceiverSlot.RB1).Route);
        Assert.Equal(play.Routes.OrderBy(p => p.Key), entities.Receivers.Where(r => r.Eligible)
            .Select(r => new KeyValuePair<ReceiverSlot, RouteType>(r.Slot, r.Route)).OrderBy(p => p.Key));
    }

    [Fact]
    public void FlipsMirrorPlayersLinemenRouteDiagramsAndMovementWithoutChangingIdentity()
    {
        foreach (var definition in PlaybookBuilder.BuildCatalog().Plays)
        {
            var play = PlayResolver.Resolve(definition);
            var flip = play.Flip();
            var restored = flip.Flip();
            Assert.Equal(play.Id, flip.Id);
            Assert.Equal(play.BallCarrierSlot, flip.BallCarrierSlot);
            Assert.Equal(-play.RunningBackSide, flip.RunningBackSide);
            Assert.Equal(play.Players, restored.Players);
            Assert.Equal(play.Linemen, restored.Linemen);
            Assert.Equal(play.Quarterback, restored.Quarterback);
            var left = Instantiate(play);
            var right = Instantiate(flip);
            AssertMirror(left.Qb.Position, right.Qb.Position);
            for (int i = 0; i < left.Blockers.Count; i++)
                AssertMirror(left.Blockers[i].Position, right.Blockers[i].Position);
            for (int i = 0; i < left.Receivers.Count; i++)
            {
                var a = left.Receivers[i];
                var b = right.Receivers[i];
                Assert.Equal(a.Slot, b.Slot);
                Assert.Equal(a.Index, b.Index);
                AssertMirror(a.Position, b.Position);
                Assert.Equal(-a.RouteSide, b.RouteSide);
                if (definition.Family != PlayType.Pass || !a.Eligible) continue;
                var aDiagram = RouteVisualizer.GetRouteWaypoints(a);
                var bDiagram = RouteVisualizer.GetRouteWaypoints(b);
                for (int j = 0; j < aDiagram.Count; j++) AssertMirror(aDiagram[j], bDiagram[j]);
                for (int frame = 0; frame < 180; frame++)
                {
                    RouteRunner.UpdateRoute(a, 1f / 60);
                    RouteRunner.UpdateRoute(b, 1f / 60);
                    a.Update(1f / 60);
                    b.Update(1f / 60);
                    AssertMirror(a.Position, b.Position, 0.001f);
                }
            }
        }
    }

    [Fact]
    public void EveryFormationAndWildcardSupportsCompleteDefensiveSetupAcrossFieldSituations()
    {
        var definitions = PlaybookBuilder.BuildCatalog().Plays.Concat(Enumerable.Range(0, 50)
            .SelectMany(seed => new[] { PlaybookBuilder.CreatePassWildcardPlay(new Random(seed)), PlaybookBuilder.CreateRunWildcardPlay(new Random(seed)) }));
        var seen = new HashSet<FormationType>();
        foreach (var definition in definitions)
        foreach (float los in new[] { 11f, 40f, 109f })
        foreach (bool flipped in new[] { false, true })
        {
            var play = PlayResolver.Resolve(definition, flipped);
            seen.Add(play.Formation);
            var coordinator = new DefensiveCoordinator(new DefensiveMemory());
            var setup = new PlaySetupController(new FormationFactory(), new DefenseFactory(), coordinator, new Random(12));
            var context = new DefensiveContext(los, 1, 3, 0, 0, SeasonStage.SuperBowl, los + 1);
            var result = setup.SetupPlay(play, context, coordinator.DecideCoverage(context, new Random(12)),
                OffensiveTeamAttributes.Default, DefensiveTeamAttributes.Default);
            Assert.Equal(11, result.Defenders.Count);
            Assert.Equal(11, 1 + result.Receivers.Count + result.Blockers.Count);
            Assert.All(result.Receivers, r =>
            {
                Assert.True(float.IsFinite(r.Position.X) && float.IsFinite(r.Position.Y));
                Assert.Equal(play.Assignments[r.Slot].Route, r.Route);
                Assert.Equal(play.Assignments[r.Slot].Role != AssignmentRole.Block, r.Eligible);
            });
        }
        Assert.Equal(Enum.GetValues<FormationType>().Length, seen.Count);
    }

    [Fact]
    public void DefinitionOwnsItsDataAndRejectsIncompleteOrInvalidAssignments()
    {
        var source = PlaybookBuilder.BuildPassPlays()[1];
        var assignments = source.Assignments.ToDictionary();
        var copy = new PlayDefinition("test.copy", "Copy", PlayType.Pass, source.Formation, assignments);
        assignments[ReceiverSlot.WR1] = new(AssignmentRole.Block);
        Assert.Equal(AssignmentRole.Route, copy.Assignments[ReceiverSlot.WR1].Role);
        assignments.Remove(ReceiverSlot.TE1);
        Assert.Throws<ArgumentException>(() => new PlayDefinition("test.missing", "Missing", PlayType.Pass, source.Formation, assignments));
        assignments = source.Assignments.ToDictionary();
        assignments[ReceiverSlot.RB2] = new(AssignmentRole.Route);
        Assert.Throws<ArgumentException>(() => new PlayDefinition("test.extra", "Extra", PlayType.Pass, source.Formation, assignments));
        assignments = source.Assignments.ToDictionary();
        assignments[ReceiverSlot.WR1] = new(AssignmentRole.Route, RouteSide: 2);
        Assert.Throws<ArgumentException>(() => new PlayDefinition("test.direction", "Direction", PlayType.Pass, source.Formation, assignments));
        var run = PlaybookBuilder.BuildRunPlays()[1];
        var noCarrier = run.Assignments.ToDictionary(p => p.Key, p => new PlayerAssignment(AssignmentRole.Block));
        Assert.Throws<ArgumentException>(() => new PlayDefinition("test.no-carrier", "No Carrier", PlayType.Run, run.Formation, noCarrier, RunConcept.Dive));
        var twoCarriers = run.Assignments.ToDictionary();
        twoCarriers[ReceiverSlot.TE1] = new(AssignmentRole.BallCarrier);
        Assert.Throws<ArgumentException>(() => new PlayDefinition("test.two-carriers", "Two Carriers", PlayType.Run, run.Formation, twoCarriers, RunConcept.Dive));
    }

    [Fact]
    public void CatalogAndFormationValidationFailBeforeSetup()
    {
        var catalog = PlaybookBuilder.BuildCatalog();
        Assert.Throws<ArgumentException>(() => new PlayCatalog(catalog.Plays.Append(catalog.Plays[0])));
        var sheet = PlayCallSheet.Default(catalog);
        Assert.Throws<ArgumentException>(() => new PlayCallSheet(catalog, sheet.PassIds.Take(9), sheet.RunIds));
        Assert.Throws<ArgumentException>(() => new PlayCallSheet(catalog, sheet.PassIds.Skip(1).Append(sheet.RunIds[0]), sheet.RunIds));
        Assert.Throws<ArgumentException>(() => new PlayCallSheet(catalog, sheet.PassIds.Skip(1).Append("unknown"), sheet.RunIds));
        Assert.Throws<ArgumentException>(() => new PlayCallSheet(catalog, Enumerable.Repeat(sheet.PassIds[0], 10), sheet.RunIds));
        Assert.Throws<ArgumentException>(() => FormationCatalog.Get((FormationType)999));
        Assert.Throws<ArgumentException>(() => new PersonnelPackage("bad", [ReceiverSlot.WR1, ReceiverSlot.WR1], 8));
        Assert.Throws<ArgumentException>(() => new PersonnelPackage("too-many", [ReceiverSlot.WR1, ReceiverSlot.WR2, ReceiverSlot.WR3, ReceiverSlot.WR4, ReceiverSlot.RB1], 6));
        Assert.Throws<ArgumentException>(() => new FormationAlignment(new(0.5f, 1.6f), [new(0.5f, 1.6f)], []));
        Assert.Throws<ArgumentException>(() => new FormationAlignment(new(float.NaN, 1.6f), [], []));
        var formation = catalog.Plays[0].Formation;
        Assert.Throws<ArgumentException>(() => new FormationDefinition(formation.Type, formation.Personnel,
            new FormationAlignment(formation.Alignment.Quarterback, [], formation.Alignment.Linemen)));
    }

    private static FormationResult Instantiate(ResolvedPlay play)
    {
        var result = new FormationFactory().CreateFormation(play, 40);
        RouteAssigner.AssignRoutes(result.Receivers, play);
        return result;
    }

    private static void AssertMirror(Vector2 a, Vector2 b, float tolerance = 0.0001f)
    {
        Assert.InRange(MathF.Abs(Constants.FieldWidth - a.X - b.X), 0, tolerance);
        Assert.InRange(MathF.Abs(a.Y - b.Y), 0, tolerance);
    }

    private static PlayCatalog LargeCatalog()
    {
        var original = PlaybookBuilder.BuildCatalog();
        var pass = original["pass.mesh"];
        var run = original["run.hb-dive"];
        return new PlayCatalog(original.Plays.Concat(Enumerable.Range(0, 40).SelectMany(i => new[]
        {
            new PlayDefinition($"test.pass.{i}", "Pass variant", PlayType.Pass, pass.Formation, pass.Assignments),
            new PlayDefinition($"test.run.{i}", "Run variant", PlayType.Run, run.Formation, run.Assignments, run.RunConcept)
        })));
    }

    private static PlayManager ManagerWith(PlayDefinition play)
    {
        var manager = new PlayManager(new PlayCatalog(PlaybookBuilder.BuildCatalog().Plays.Append(play)));
        manager.SetCallSheet(new PlayCallSheet(manager.Catalog, manager.CallSheet.PassIds,
            manager.CallSheet.RunIds.Take(9).Append(play.Id)));
        return manager;
    }

    private sealed class FirstCandidateRandom(int seed) : Random(seed)
    {
        public override double NextDouble() => 0;
    }
}
