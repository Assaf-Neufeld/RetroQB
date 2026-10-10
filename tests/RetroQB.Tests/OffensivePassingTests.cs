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

public sealed class OffensivePassingTests
{
    [Fact]
    public void NamedPassesHaveCompleteIdentityBoundReadsAndAuthoredGeometry()
    {
        foreach (var play in PlaybookBuilder.BuildCatalog().Plays.Where(p => p.Family == PlayType.Pass && !p.IsWildcard))
        {
            var routes = play.Assignments.Where(p => p.Value.Role == AssignmentRole.Route).ToArray();
            Assert.Equal(routes.Select(p => p.Key).Order(), play.Reads.Select(r => r.Slot).Order());
            Assert.All(routes, p => Assert.NotNull(p.Value.RouteDefinition));
            Assert.All(play.Reads, r => Assert.InRange(r.AfterStep, 0, play.Assignments[r.Slot].RouteDefinition!.Steps.Count));
        }
    }

    [Fact]
    public void DisplayNameChangesDoNotChangeAuthoredConceptBehavior()
    {
        var source = PlaybookBuilder.BuildCatalog()["pass.gun-trips.stick"];
        var renamed = PassingConceptAuthor.Author(source.Id, "Renamed call", source.Formation,
            source.Info with { Concept = "Renamed concept" }, source.Assignments);
        Assert.Equal(source.Reads, renamed.Reads);
        Assert.Equal(source.Protection, renamed.Protection);
        foreach (var slot in source.Assignments.Keys)
        {
            Assert.Equal(source.Assignments[slot].Route, renamed.Assignments[slot].Route);
            Assert.Equal(source.Assignments[slot].RouteDefinition!.Steps, renamed.Assignments[slot].RouteDefinition!.Steps);
        }
    }

    [Fact]
    public void InvalidProgressionsAndReleaseContractsAreRejected()
    {
        var source = PlaybookBuilder.BuildCatalog()["pass.gun-doubles.spacing"];
        PlayDefinition Make(IEnumerable<PassRead> reads, Dictionary<ReceiverSlot, PlayerAssignment>? jobs = null) =>
            new("test.reads", "Reads", PlayType.Pass, source.Formation, jobs ?? source.Assignments.ToDictionary(), reads: reads);
        Assert.Throws<ArgumentException>(() => Make(source.Reads.Skip(1)));
        Assert.Throws<ArgumentException>(() => Make(source.Reads.Append(source.Reads[0])));
        Assert.Throws<ArgumentException>(() => Make(source.Reads.Select((r, i) => i == 0 ? r with { AfterStep = 100 } : r)));
        Assert.Throws<ArgumentException>(() => Make(source.Reads.Select((r, i) => i == 0 ? r with { EarliestSeconds = float.NaN } : r)));
        var jobs = source.Assignments.ToDictionary();
        jobs[ReceiverSlot.RB1] = jobs[ReceiverSlot.RB1] with { ProtectionRelease = new() };
        Assert.Throws<ArgumentException>(() => Make(source.Reads, jobs));
        Assert.Throws<ArgumentException>(() => new PlayDefinition("test.help", "Help", PlayType.Pass,
            source.Formation, source.Assignments, protection: new(ProtectionMode.Balanced, ReceiverSlot.WR1)));
    }

    [Theory]
    [InlineData(false, 30)] [InlineData(true, 30)]
    [InlineData(false, 60)] [InlineData(true, 60)]
    [InlineData(false, 120)] [InlineData(true, 120)]
    public void MeshCrossersMaintainSeparationThroughTheProductionMovementController(bool flipped, int hz)
    {
        var manager = Manager("pass.gun-trips.mesh-wheel", flipped);
        var field = Field(manager, 40);
        var crossers = field.Receivers.Where(r => r.Route == RouteType.Drag).ToArray();
        Assert.Equal(2, crossers.Length);
        var controller = new ReceiverUpdateController(new BlockingController());
        float minimum = float.MaxValue;
        for (int frame = 0; frame < hz * 8; frame++)
        {
            controller.UpdateAll(field.Receivers, field.Qb, field.Ball, [], null, Vector2.Zero,
                false, false, false, manager, 1f / hz, Clamp, blockers: field.Blockers);
            minimum = Math.Min(minimum, Vector2.Distance(crossers[0].Position, crossers[1].Position));
        }
        Assert.True(minimum >= crossers[0].Radius + crossers[1].Radius - .05f, $"separation {minimum}");
        Assert.All(crossers, r => Assert.True(r.RouteState.StepIndex >= 2));
        Assert.True(crossers[0].Position.X > crossers[1].Position.X == !flipped);
    }

    [Theory]
    [InlineData(40, false)] [InlineData(105, false)] [InlineData(109, true)]
    public void FloodKeepsThreeDistinctLevelsOnOneSidelineAndMatchesItsPreview(float los, bool flipped)
    {
        var manager = Manager("pass.gun-doubles.sail", flipped);
        var field = Field(manager, los);
        var clear = field.Receivers.Single(r => r.Route == RouteType.Go);
        var corner = field.Receivers.Single(r => r.Route == RouteType.Corner);
        var flat = field.Receivers.Where(r => r.Route == RouteType.Flat)
            .OrderBy(r => Math.Abs(RouteGeometry.GetPath(r).Points[^1].X - RouteGeometry.GetPath(corner).Points[^1].X)).First();
        var deep = RouteVisualizer.GetRouteWaypoints(clear)[^1];
        var mid = RouteVisualizer.GetRouteWaypoints(corner)[^1];
        var shallow = RouteVisualizer.GetRouteWaypoints(flat)[^1];
        Assert.True(shallow.Y < mid.Y && mid.Y < deep.Y);
        Assert.InRange(Math.Abs(deep.X - shallow.X), 0, 4);
        Assert.InRange(Math.Abs(mid.X - shallow.X), 0, 4);
        Assert.Equal(RouteGeometry.GetPath(corner).Points, RouteVisualizer.GetRouteWaypoints(corner));
        Assert.Equal(flipped ? ProtectionMode.SlideLeft : ProtectionMode.SlideRight, manager.SelectedPlay.Protection.Mode);
        Assert.All(manager.SelectedPlay.LineBlocking, b => Assert.True(flipped ? b.TargetOffset.X < 0 : b.TargetOffset.X > 0));
    }

    [Fact]
    public void ReadOrderDoesNotRenumberTargetKeysAndCannotThrowAnUndevelopedPrimary()
    {
        var manager = Manager("pass.gun-doubles.drive");
        var field = Field(manager, 40);
        var targets = new ReceiverPriorityManager(); targets.AssignPriorities(field.Receivers);
        var primary = field.Receivers.Single(r => r.Slot == manager.SelectedPlay.Reads[0].Slot);
        Assert.Equal(ReceiverSlot.WR2, primary.Slot);
        Assert.Equal("2", targets.GetPriorityLabel(primary.Index));
        var qb = new QuarterbackAI();
        var read = new ReceiverRead(primary.Index, new(40, 50), Vector2.Zero, true, false, Developed: false);
        var view = new QuarterbackObservation(new(20, 35), 40, true, [read], []);
        Assert.Null(qb.Decide(view, .3f).ReceiverIndex);
        Assert.Null(qb.Decide(view with { Defenders = [new(21, 36)] }, .3f).ReceiverIndex);
        Assert.Equal(primary.Index, qb.Decide(view with { Reads = [read with { Developed = true }] }, .3f).ReceiverIndex);
    }

    [Theory]
    [InlineData("pass.i-pro.mills", 7)]
    [InlineData("pass.split-backs.mills", 6)]
    public void MaxProtectionSacrificesRoutesAndCoversDistinctRushersBeforeHelping(string id, int count)
    {
        var manager = Manager(id); var field = Field(manager, 40);
        Assert.Equal(ProtectionMode.MaxProtect, manager.SelectedPlay.Protection.Mode);
        Assert.Equal(count, field.Blockers.Count + field.Receivers.Count(r => r.IsBlocking));
        Assert.Equal(11 - count - 1, field.Receivers.Count(r => r.Eligible));
        var rush = Rush(40, count);
        var map = PassProtectionPlanner.AssignAll(field.Blockers, field.Receivers, rush, manager.SelectedPlay, true);
        Assert.Equal(count, map.Count);
        Assert.Equal(count, map.Values.Distinct().Count());
        var reverse = PassProtectionPlanner.AssignAll(field.Blockers.AsEnumerable().Reverse().ToArray(),
            field.Receivers.AsEnumerable().Reverse().ToArray(), rush.AsEnumerable().Reverse().ToArray(), manager.SelectedPlay, true);
        Assert.All(map, p => Assert.Same(p.Value, reverse[p.Key]));
    }

    [Fact]
    public void DelayedReleaseKeepsImmediateThreatPastTimeoutThenReleasesWhenSafe()
    {
        var manager = Manager("pass.i-pro.stick"); var field = Field(manager, 40);
        var back = field.Receivers.Single(r => r.Slot == ReceiverSlot.FB);
        var threat = new Defender(back.Position + new Vector2(1, 1), DefensivePosition.LB, DefenderSlot.MLB) { IsRusher = true };
        var controller = new ReceiverUpdateController(new BlockingController());
        for (int frame = 0; frame < 180; frame++)
        {
            threat.Position = back.Position + new Vector2(1, 1);
            controller.UpdateAll(field.Receivers, field.Qb, field.Ball, [threat], null, Vector2.Zero,
                false, false, false, manager, 1f / 60, Clamp);
            Assert.True(back.IsBlocking);
        }
        Assert.True(back.Eligible);
        threat.IsRusher = false;
        controller.UpdateAll(field.Receivers, field.Qb, field.Ball, [threat], null, Vector2.Zero,
            false, false, false, manager, 1f / 60, Clamp, blockers: field.Blockers);
        Assert.False(back.IsBlocking);
        Assert.Null(back.BlockingState.Target);
        Assert.Equal(RoutePhase.Running, back.RouteState.Phase);
    }

    [Theory]
    [InlineData(false, 30)] [InlineData(true, 60)] [InlineData(false, 120)]
    public void SharedProtectionPicksUpEdgeOverloadAndDelayedRushWithoutAccidentalDoubles(bool flipped, int hz)
    {
        var manager = Manager("pass.gun-doubles.scissors", flipped); var field = Field(manager, 40);
        var defenders = Rush(40, 7);
        var late = defenders[^1]; late.IsRusher = false;
        var input = new StillInput(); var execution = new PlayExecutionController(input, new BlockingController());
        var contact = new HashSet<Defender>();
        for (int frame = 0; frame < hz * 3; frame++)
        {
            if (frame == hz) late.IsRusher = true;
            foreach (var defender in defenders.Where(d => d.IsRusher))
            {
                defender.Velocity = Vector2.Normalize(field.Qb.Position - defender.Position) * 4;
                defender.Position += defender.Velocity / hz;
            }
            execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, defenders, field.Blockers, manager,
                false, false, false, Clamp, 1f / hz);
            foreach (var defender in defenders.Where(d => d.IsBeingBlocked)) contact.Add(defender);
            var targets = field.Blockers.Select(b => (Actor: (Entity)b, b.BlockingState.Target))
                .Concat(field.Receivers.Where(r => r.IsBlocking).Select(r => (Actor: (Entity)r, r.BlockingState.Target)))
                .Where(p => p.Target != null).ToArray();
            foreach (var doubled in targets.GroupBy(p => p.Target).Where(g => g.Count() > 1))
                Assert.Contains(doubled, p => p.Actor is Receiver r && r.Slot == manager.SelectedPlay.Protection.HelpSlot);
        }
        Assert.Contains(late, contact);
        Assert.True(contact.Count >= 6, $"Contacted {contact.Count} rushers at {hz}Hz");
    }

    private static List<Defender> Rush(float los, int count)
    {
        DefenderSlot[] slots = [DefenderSlot.DE1, DefenderSlot.DT1, DefenderSlot.DT2, DefenderSlot.DE2,
            DefenderSlot.MLB, DefenderSlot.OLB1, DefenderSlot.OLB2];
        return Enumerable.Range(0, count).Select(i => new Defender(new(Constants.FieldWidth * .5f + (i - 3) * 2.3f, los + 1),
            i < 4 ? DefensivePosition.DL : DefensivePosition.LB, slots[i]) { IsRusher = true }).ToList();
    }

    private static PlayManager Manager(string id, bool flipped = false)
    {
        var manager = new PlayManager(); manager.SelectCatalogPlay(id, new Random(1));
        if (flipped) manager.FlipSelectedPlay();
        return manager;
    }
    private static FormationResult Field(PlayManager manager, float los)
    {
        manager.StartNewDrive(new DriveStart(los - FieldGeometry.EndZoneDepth, Distance: Math.Min(10, FieldGeometry.OpponentGoalLine - los)));
        var field = new FormationFactory().CreateFormation(manager.SelectedPlay, manager.LineOfScrimmage);
        RouteAssigner.AssignRoutes(field.Receivers, manager.SelectedPlay); return field;
    }
    private sealed class StillInput : IPlayerMovementInput
    {
        public Vector2 GetMovementDirection() => Vector2.Zero;
        public bool IsSprintHeld() => false;
    }
    private static void Clamp(Entity entity) => entity.Position = new(
        Math.Clamp(entity.Position.X, entity.Radius, Constants.FieldWidth - entity.Radius),
        Math.Clamp(entity.Position.Y, entity.Radius, Constants.FieldLength - entity.Radius));
}
