using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class BlockingAndExchangeTests
{
    [Fact]
    public void DrawWaitsForItsExchangeTimeAndHandsOffOnlyOnce()
    {
        var play = PlayResolver.Resolve(PlaybookBuilder.BuildRunPlays()[9]);
        var field = Instantiate(play);
        var rb = field.Receivers.Single(r => r.Slot == play.BallCarrierSlot);
        rb.Position = field.Qb.Position;
        var controller = new BackfieldController();
        Assert.False(controller.TryHandoff(play, field.Ball, field.Qb, field.Receivers));
        controller.Update(play, field.Ball, field.Qb, field.Receivers, 0.3f);
        Assert.False(controller.TryHandoff(play, field.Ball, field.Qb, field.Receivers));
        controller.Update(play, field.Ball, field.Qb, field.Receivers, 0.3f);
        Assert.True(controller.TryHandoff(play, field.Ball, field.Qb, field.Receivers));
        Assert.Same(rb, field.Ball.Holder);
        Assert.False(controller.TryHandoff(play, field.Ball, field.Qb, field.Receivers));
        Assert.All(play.OpeningLineBlocking, a => Assert.Equal(BlockingJob.PassProtection, a.Job));
        Assert.All(play.LineBlocking, a => Assert.True(a.IsRunBlock));
    }

    [Fact]
    public void PlayActionLocksThrowingThroughFakeAndNeverTransfersPossession()
    {
        var play = PlayAction();
        var field = Instantiate(play);
        var rb = field.Receivers.Single(r => r.Slot == ReceiverSlot.RB1);
        rb.Position = field.Qb.Position + play.Backfield.MeshOffset;
        var controller = new BackfieldController();
        controller.Update(play, field.Ball, field.Qb, field.Receivers, 0.01f);
        Assert.Equal(BackfieldPhase.Faking, controller.Phase);
        Assert.False(controller.AllowsThrow);
        Assert.True(controller.HoldsQuarterback);
        Assert.False(controller.TryHandoff(play, field.Ball, field.Qb, field.Receivers));
        controller.MoveParticipant(rb, field.Qb, 0.1f);
        Assert.Equal(Vector2.Zero, rb.Velocity);
        controller.Update(play, field.Ball, field.Qb, field.Receivers, 0.4f);
        Assert.True(controller.AllowsThrow);
        Assert.True(controller.OpeningComplete);
        Assert.False(controller.ControlsParticipant(rb.Slot));
        Assert.Equal(BackfieldPhase.Released, controller.Phase);
        Assert.Same(field.Qb, field.Ball.Holder);
        Assert.False(rb.HasBall);
    }

    [Fact]
    public void BlockedFakeTimesOutAndANewBallResetsTiming()
    {
        var play = PlayAction();
        var field = Instantiate(play);
        var rb = field.Receivers.Single(r => r.Slot == ReceiverSlot.RB1);
        rb.Position -= new Vector2(0, 20);
        var controller = new BackfieldController();
        controller.Update(play, field.Ball, field.Qb, field.Receivers, 2);
        Assert.Equal(BackfieldPhase.Aborted, controller.Phase);
        Assert.True(controller.AllowsThrow);
        var fresh = Instantiate(play);
        controller.Update(play, fresh.Ball, fresh.Qb, fresh.Receivers, 0.01f);
        Assert.Equal(BackfieldPhase.Approaching, controller.Phase);
        Assert.False(controller.AllowsThrow);
    }

    [Fact]
    public void PossessionChangeCancelsPendingExchange()
    {
        var play = PlayResolver.Resolve(PlaybookBuilder.BuildRunPlays()[9]);
        var field = Instantiate(play);
        var controller = new BackfieldController();
        controller.Update(play, field.Ball, field.Qb, field.Receivers, 0.1f);
        field.Ball.SetInAir(field.Qb.Position, Vector2.UnitY * 10, 20, 10, 0);
        controller.Update(play, field.Ball, field.Qb, field.Receivers, 1);
        Assert.Equal(BackfieldPhase.Completed, controller.Phase);
        Assert.False(controller.TryHandoff(play, field.Ball, field.Qb, field.Receivers));
        Assert.Equal(BallState.InAir, field.Ball.State);
    }

    [Fact]
    public void DelayedReleaseKeepsTargetNumberAndBeginsRouteAfterProtection()
    {
        var source = PlaybookBuilder.BuildPassPlays()[9];
        var assignments = source.Assignments.ToDictionary();
        assignments[ReceiverSlot.TE1] = new(AssignmentRole.Route, RouteType.Go,
            Blocking: new(BlockingJob.PassProtection, new(0, -1)), ReleaseAfterSeconds: 0.3f);
        var manager = ManagerWith(new("test.release", "Release", PlayType.Pass, source.Formation, assignments));
        var field = Instantiate(manager.SelectedPlay);
        var te = field.Receivers.Single(r => r.Slot == ReceiverSlot.TE1);
        var priorities = new ReceiverPriorityManager();
        priorities.AssignPriorities(field.Receivers);
        string number = priorities.GetPriorityLabel(te.Index);
        Assert.NotEqual("-", number);
        Assert.True(te.IsBlocking);
        Assert.True(te.Eligible);
        var controller = new ReceiverUpdateController(new BlockingController());
        TickReceivers(controller, manager, field, 0.15f);
        Assert.True(te.IsBlocking);
        Assert.Equal(0, te.RouteState.StepIndex);
        TickReceivers(controller, manager, field, 0.2f);
        Assert.False(te.IsBlocking);
        Assert.Equal(RoutePhase.Running, te.RouteState.Phase);
        Assert.Equal(number, priorities.GetPriorityLabel(te.Index));
        Assert.True(te.Velocity.Y > 0);
    }

    [Fact]
    public void CatchDuringDelayedReleaseGivesControlToCarrier()
    {
        var source = PlaybookBuilder.BuildPassPlays()[9];
        var assignments = source.Assignments.ToDictionary();
        assignments[ReceiverSlot.TE1] = new(AssignmentRole.Route, Blocking: new(BlockingJob.PassProtection, new(0, -1)), ReleaseAfterSeconds: 2);
        var manager = ManagerWith(new("test.catch-release", "Release", PlayType.Pass, source.Formation, assignments));
        var field = Instantiate(manager.SelectedPlay);
        var te = field.Receivers.Single(r => r.Slot == ReceiverSlot.TE1);
        te.HasBall = true;
        field.Ball.SetHeld(te, BallState.HeldByReceiver);
        var start = te.Position;
        new ReceiverUpdateController(new BlockingController()).UpdateAll(field.Receivers, field.Qb, field.Ball, [], te,
            Vector2.UnitY, false, false, false, manager, 0.1f, _ => { });
        Assert.True(te.Position.Y > start.Y);
    }

    [Theory]
    [InlineData(BlockingJob.Lead)]
    [InlineData(BlockingJob.Pull)]
    public void LeadAndPullFollowApproachBeforeEngagingTheGap(BlockingJob job)
    {
        var assignment = new BlockingAssignment(job, new(5, 3), BlockingAnchor.FieldCenter, new(5, -1));
        var state = new BlockingState();
        var origin = new Vector2(Constants.FieldWidth * 0.5f, 40);
        var defender = DefenderAt(origin + new Vector2(5, 3));
        var decision = BlockingSteering.Decide(assignment, state, origin + new Vector2(0, -4), origin.X, 40,
            origin, [defender], 6);
        Assert.True(decision.FollowingApproach);
        Assert.Null(decision.Target);
        Assert.Equal(origin + new Vector2(5, -1), decision.Landmark);
        decision = BlockingSteering.Decide(assignment, state, decision.Landmark, origin.X, 40, origin, [defender], 6);
        Assert.True(state.ReachedApproach);
        Assert.False(decision.FollowingApproach);
        Assert.Same(defender, decision.Target);
    }

    [Fact]
    public void LeadBlockerTargetsCalledGapInsteadOfNearestQbThreat()
    {
        var qb = new Vector2(Constants.FieldWidth * 0.5f, 38);
        var assignment = new BlockingAssignment(BlockingJob.Lead, new(6, 3), BlockingAnchor.FieldCenter);
        var closeThreat = DefenderAt(qb + new Vector2(0, 1));
        var gapThreat = DefenderAt(new(qb.X + 6, 43));
        var decision = BlockingSteering.Decide(assignment, new(), new(qb.X + 6, 41), qb.X, 40, qb,
            [closeThreat, gapThreat], 5);
        Assert.Same(gapThreat, decision.Target);
    }

    [Fact]
    public void KickOutPrefersTheEdgeDefenderAndReleasesStaleTargets()
    {
        var origin = new Vector2(25, 40);
        var assignment = new BlockingAssignment(BlockingJob.KickOut, new(4, 3), driveDirection: Vector2.UnitX);
        var state = new BlockingState();
        var edge = DefenderAt(new(29.5f, 43), DefensivePosition.DE);
        var linebacker = DefenderAt(new(29, 43), DefensivePosition.LB);
        var decision = BlockingSteering.Decide(assignment, state, new(29, 41), 25, 40, origin, [linebacker, edge], 5);
        Assert.Same(edge, decision.Target);
        edge.Position = new(50, 70);
        decision = BlockingSteering.Decide(assignment, state, new(29, 41), 25, 40, origin, [linebacker, edge], 5);
        Assert.Same(linebacker, decision.Target);
    }

    [Fact]
    public void BlockingAndExchangeLandmarksMirrorWithTheEntirePlay()
    {
        var source = PlaybookBuilder.BuildRunPlays()[4];
        var assignments = source.Assignments.ToDictionary();
        assignments[ReceiverSlot.TE2] = new(AssignmentRole.Block,
            Blocking: new(BlockingJob.KickOut, new(4, 3), approachOffset: new(3, -1), driveDirection: Vector2.UnitX));
        var play = PlayResolver.Resolve(new PlayDefinition("test.mirror", "Mirror", PlayType.Run, source.Formation,
            assignments, source.RunConcept, source.RunningBackSide));
        var flip = play.Flip();
        Assert.Equal(-play.Backfield.MeshOffset.X, flip.Backfield.MeshOffset.X);
        for (int i = 0; i < play.Linemen.Count; i++)
        {
            var a = BlockingSteering.GetLandmarks(play.LineBlocking[i], play.Linemen[i].XFraction * Constants.FieldWidth, 40,
                play.Quarterback.AtLineOfScrimmage(40));
            var b = BlockingSteering.GetLandmarks(flip.LineBlocking[i], flip.Linemen[i].XFraction * Constants.FieldWidth, 40,
                flip.Quarterback.AtLineOfScrimmage(40));
            for (int j = 0; j < a.Count; j++)
            {
                Assert.InRange(MathF.Abs(Constants.FieldWidth - a[j].X - b[j].X), 0, 0.001f);
                Assert.Equal(a[j].Y, b[j].Y);
            }
        }
        Assert.Equal(-play.Assignments[ReceiverSlot.TE2].Blocking!.DriveDirection.X,
            flip.Assignments[ReceiverSlot.TE2].Blocking!.DriveDirection.X);
    }

    [Fact]
    public void LinemenExecuteAssignedPullLandmarkEvenOnAPassCall()
    {
        var source = PlaybookBuilder.BuildPassPlays()[1];
        var jobs = source.Formation.Alignment.Linemen.Select(_ => new BlockingAssignment(BlockingJob.Pull,
            new(5, 3), approachOffset: new(5, -1))).ToArray();
        var play = PlayResolver.Resolve(new PlayDefinition("test.pull", "Pull", PlayType.Pass, source.Formation,
            source.Assignments, lineBlocking: jobs));
        var field = Instantiate(play);
        var first = field.Blockers[0];
        var before = first.Position;
        OffensiveLinemanAI.UpdateBlockers(field.Blockers, [], play, 40, 0.1f, false, _ => { }, null);
        Assert.True(first.Position.X > before.X);
        Assert.True(first.Position.Y < before.Y);
        Assert.False(first.BlockingState.ReachedApproach);
    }

    [Fact]
    public void InvalidReleaseAndExchangePlansFailBeforeSetup()
    {
        var source = PlaybookBuilder.BuildPassPlays()[1];
        Assert.Throws<ArgumentException>(() => new BackfieldSequence(BackfieldAction.PlayAction, ReceiverSlot.RB1, fakeDuration: -1));
        Assert.Throws<ArgumentException>(() => new BackfieldSequence(BackfieldAction.Handoff, ReceiverSlot.WR1));
        Assert.Throws<ArgumentException>(() => new BlockingAssignment(BlockingJob.Lead, new(float.NaN, 3)));
        Assert.Throws<ArgumentException>(() => new PlayDefinition("bad.exchange", "Bad", PlayType.Pass, source.Formation,
            source.Assignments, backfield: new(BackfieldAction.PlayAction, ReceiverSlot.RB1)));
        Assert.Throws<ArgumentException>(() => new PlayDefinition("bad.line", "Bad", PlayType.Pass, source.Formation,
            source.Assignments, lineBlocking: []));
        var assignments = source.Assignments.ToDictionary();
        assignments[ReceiverSlot.TE1] = new(AssignmentRole.Route, ReleaseAfterSeconds: 1);
        Assert.Throws<ArgumentException>(() => new PlayDefinition("bad.release", "Bad", PlayType.Pass, source.Formation, assignments));
    }

    [Fact]
    public void EveryCatalogCallRunsFiniteSimulationAndEveryRunCompletesItsExchange()
    {
        foreach (var definition in PlaybookBuilder.BuildCatalog().Plays)
        foreach (bool flipped in new[] { false, true })
        {
            var manager = new PlayManager();
            manager.SetCallSheet(new PlayCallSheet(manager.Catalog,
                manager.Catalog.Plays.Where(p => p.Family == PlayType.Pass).OrderByDescending(p => p.Id == definition.Id).Take(10).Select(p => p.Id),
                manager.Catalog.Plays.Where(p => p.Family == PlayType.Run).OrderByDescending(p => p.Id == definition.Id).Take(10).Select(p => p.Id)));
            if (definition.Family == PlayType.Pass)
                manager.SelectPassPlay(manager.PassPlays.ToList().FindIndex(p => p.Id == definition.Id), new Random(14));
            else
                manager.SelectRunPlay(manager.RunPlays.ToList().FindIndex(p => p.Id == definition.Id), new Random(14));
            if (flipped) manager.FlipSelectedPlay();
            var play = manager.SelectedPlay;
            Assert.Equal(definition.Id, play.Id);
            var field = Instantiate(play);
            var backfield = new BackfieldController();
            var receivers = new ReceiverUpdateController(new BlockingController());
            var defenders = new[]
            {
                DefenderAt(new(22, 43), DefensivePosition.DE), DefenderAt(new(31, 43), DefensivePosition.DE),
                DefenderAt(new(26, 46), DefensivePosition.LB)
            };
            for (int frame = 0; frame < 180; frame++)
            {
                const float dt = 1f / 60;
                BlockingUtils.ResetDefenderBlockingState(defenders);
                backfield.Update(play, field.Ball, field.Qb, field.Receivers, dt);
                var carrier = field.Ball.Holder as Receiver;
                receivers.UpdateAll(field.Receivers, field.Qb, field.Ball, defenders, carrier,
                    Vector2.UnitY, false, false, false, manager, dt, Clamp, backfield);
                backfield.TryHandoff(play, field.Ball, field.Qb, field.Receivers);
                OffensiveLinemanAI.UpdateBlockers(field.Blockers, defenders, play, 40, dt,
                    field.Ball.State == BallState.HeldByReceiver, Clamp, field.Ball.Holder?.Position,
                    field.Qb.Position, backfield.OpeningComplete);
                Assert.All(field.Receivers.Cast<Entity>().Concat(field.Blockers).Concat(defenders), e =>
                    Assert.True(float.IsFinite(e.Position.X) && float.IsFinite(e.Position.Y), play.Id));
            }
            if (play.Family == PlayType.Run)
            {
                var carrier = Assert.IsType<Receiver>(field.Ball.Holder);
                Assert.Equal(play.BallCarrierSlot, carrier.Slot);
                Assert.Equal(BackfieldPhase.Completed, backfield.Phase);
            }
        }

        static void Clamp(Entity entity) => entity.Position = new(
            Math.Clamp(entity.Position.X, 1, Constants.FieldWidth - 1), Math.Clamp(entity.Position.Y, 1, Constants.FieldLength - 1));
    }

    private static ResolvedPlay PlayAction()
    {
        var source = PlaybookBuilder.BuildPassPlays()[9];
        return PlayResolver.Resolve(new PlayDefinition("test.fake", "Fake", PlayType.Pass, source.Formation, source.Assignments,
            backfield: new(BackfieldAction.PlayAction, ReceiverSlot.RB1, new(0, -0.6f))));
    }

    private static FormationResult Instantiate(ResolvedPlay play)
    {
        var result = new FormationFactory().CreateFormation(play, 40);
        RouteAssigner.AssignRoutes(result.Receivers, play);
        return result;
    }

    private static PlayManager ManagerWith(PlayDefinition play)
    {
        var manager = new PlayManager(new PlayCatalog(PlaybookBuilder.BuildCatalog().Plays.Append(play)));
        manager.SetCallSheet(new PlayCallSheet(manager.Catalog, manager.CallSheet.PassIds.Take(9).Append(play.Id), manager.CallSheet.RunIds));
        manager.SelectPassPlay(9, new Random(1));
        return manager;
    }

    private static void TickReceivers(ReceiverUpdateController controller, PlayManager manager, FormationResult field, float dt) =>
        controller.UpdateAll(field.Receivers, field.Qb, field.Ball, [], null, Vector2.Zero, false, false, false, manager, dt, _ => { });

    private static Defender DefenderAt(Vector2 position, DefensivePosition role = DefensivePosition.LB) =>
        new(position, role, role == DefensivePosition.DE ? DefenderSlot.DE1 : DefenderSlot.MLB);
}
