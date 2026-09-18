using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class ExpandedPlaybookTests
{
    [Fact]
    public void AdditionalPersonnelAppearInReceivingStatistics()
    {
        var stats = new RetroQB.Stats.StatisticsTracker();
        foreach (var slot in new[] { ReceiverSlot.TE2, ReceiverSlot.RB2, ReceiverSlot.FB })
        {
            stats.RecordTarget(slot);
            stats.RecordCompletion(slot);
            stats.RecordPassYards(slot, 8, false);
            Assert.Contains(stats.BuildSnapshot().Receivers, r => r.Label == slot.GetLabel() && r.Yards == 8 && r.Receptions == 1);
        }
    }
    [Fact]
    public void ProductionCatalogHasOneHundredCallsAndRealPersonnelVariety()
    {
        var catalog = PlaybookBuilder.BuildCatalog();
        Assert.Equal(100, catalog.Plays.Count);
        Assert.Equal(50, catalog.Plays.Count(p => p.Family == PlayType.Pass));
        Assert.Equal(50, catalog.Plays.Count(p => p.Family == PlayType.Run));
        Assert.Equal(100, catalog.Plays.Select(p => p.Id).Distinct().Count());
        foreach (string personnel in new[] { "11", "12", "21", "20" })
        // The offensive catalog contains pass/run formations; kicks use their own setup.
        foreach (var family in new[] { PlayType.Pass, PlayType.Run })
            Assert.Contains(catalog.Plays, p => p.Personnel.Id == personnel && p.Family == family);
        Assert.Equal(8, ExpandedFormations.All.Select(f => f.Formation.Type).Distinct().Count());
        Assert.Contains(catalog.Plays, p => p.Personnel.Slots.Contains(ReceiverSlot.FB) &&
            p.Assignments[ReceiverSlot.FB].Blocking?.Job == BlockingJob.Lead);
        Assert.Contains(catalog.Plays, p => p.Assignments.Values.Any(a => a.Blocking?.Job == BlockingJob.KickOut));
        Assert.Contains(ExpandedFormations.All, f => f.Formation.Alignment.SkillPositions[
            f.Formation.AlignmentSlots.ToList().IndexOf(ReceiverSlot.TE1)].XFraction < .5f);
    }

    [Fact]
    public void SheetsAreDistinctStableAndSituationSensitiveWithWholeCatalogReachable()
    {
        var catalog = PlaybookBuilder.BuildCatalog();
        var situations = new[] { new PlaySituation(1, 10, 35, 45), new PlaySituation(3, 2, 60, 62),
            new PlaySituation(3, 15, 50, 65), new PlaySituation(2, 4, FieldGeometry.OpponentGoalLine - 4, FieldGeometry.OpponentGoalLine) };
        var seen = new HashSet<string>();
        foreach (var situation in situations)
        {
            var first = SituationalCallSheet.Build(catalog, situation, 12);
            var again = SituationalCallSheet.Build(catalog, situation, 12);
            Assert.Equal(first.PassIds, again.PassIds);
            Assert.Equal(first.RunIds, again.RunIds);
            for (int seed = 0; seed < 180; seed++)
            {
                var sheet = SituationalCallSheet.Build(catalog, situation, seed);
                Assert.Equal(10, sheet.PassIds.Distinct().Count());
                Assert.Equal(10, sheet.RunIds.Distinct().Count());
                Assert.All(sheet.PassIds, id => Assert.Equal(PlayType.Pass, catalog[id].Family));
                Assert.All(sheet.RunIds, id => Assert.Equal(PlayType.Run, catalog[id].Family));
                seen.UnionWith(sheet.PassIds.Concat(sheet.RunIds));
            }
        }
        Assert.Equal(100, seen.Count);
        Assert.NotEqual(SituationalCallSheet.Build(catalog, situations[0], 1).PassIds,
            SituationalCallSheet.Build(catalog, situations[3], 1).PassIds);
        var shortSheet = SituationalCallSheet.Build(catalog, situations[1], 1);
        var longSheet = SituationalCallSheet.Build(catalog, situations[2], 1);
        Assert.Contains(shortSheet.RunIds, id => catalog[id].RunConcept == RunConcept.Dive);
        Assert.Contains(longSheet.RunIds, id => catalog[id].RunConcept == RunConcept.Draw);
        Assert.Contains(shortSheet.PassIds, id => catalog[id].Info.Category == PlayCategory.Quick);
    }

    [Fact]
    public void RefreshIsOncePerSnapAndFlipKeepsCallSheetStable()
    {
        var manager = new PlayManager();
        Assert.True(manager.EnsureSituationCallSheet());
        manager.SelectPassPlay(3, new Random(1));
        string id = manager.SelectedPlay.Id;
        var sheet = manager.CallSheet;
        manager.FlipSelectedPlay();
        Assert.False(manager.EnsureSituationCallSheet());
        Assert.Same(sheet, manager.CallSheet);
        Assert.Equal(id, manager.SelectedPlay.Id);
        Assert.True(manager.SelectedPlay.IsFlipped);
        manager.StartPlay();
        manager.ResolvePlay(manager.LineOfScrimmage, true, false, false, false);
        Assert.True(manager.EnsureSituationCallSheet());
        Assert.Equal(1, manager.GetCallCount(id));
    }

    [Fact]
    public void EveryNewRouteIsAuthoredAndExecutesFiniteGeometry()
    {
        var catalog = PlaybookBuilder.BuildCatalog();
        foreach (var type in new[] { RouteType.Hitch, RouteType.Curl, RouteType.Comeback, RouteType.Corner,
            RouteType.Wheel, RouteType.Angle, RouteType.Drag, RouteType.Seam })
        {
            Assert.Contains(catalog.Plays, p => p.Assignments.Values.Any(a => a.Route == type));
            foreach (int side in new[] { -1, 1 })
            {
                var receiver = new Receiver(0, ReceiverSlot.WR1, new Vector2(26, 30)) { Route = type, RouteSide = side };
                var path = RouteGeometry.GetPath(receiver);
                for (int frame = 0; frame < 900; frame++)
                {
                    RouteRunner.UpdateRoute(receiver, 1f / 60);
                    receiver.Position += receiver.Velocity / 60;
                }
                Assert.Equal(path.Definition.Steps.Count, receiver.RouteState.StepIndex);
                Assert.True(float.IsFinite(receiver.Position.X) && float.IsFinite(receiver.Position.Y));
                Assert.True(path.Points.Count >= 2);
                Assert.All(path.Points, p => Assert.True(float.IsFinite(p.X) && float.IsFinite(p.Y)));
            }
        }
    }

    [Fact]
    public void AuthoredPlayActionsFinishFakeAndKeepQuarterbackPossession()
    {
        foreach (var definition in PlaybookBuilder.BuildCatalog().Plays.Where(p => p.Backfield.Action == BackfieldAction.PlayAction))
        foreach (bool flipped in new[] { false, true })
        {
            var play = PlayResolver.Resolve(definition, flipped);
            var field = new FormationFactory().CreateFormation(play, 40);
            RouteAssigner.AssignRoutes(field.Receivers, play);
            var backfield = new BackfieldController();
            var participant = field.Receivers.Single(r => r.Slot == play.Backfield.Participant);
            bool sawFake = false;
            for (int frame = 0; frame < 180; frame++)
            {
                backfield.Update(play, field.Ball, field.Qb, field.Receivers, 1f / 60);
                sawFake |= backfield.Phase == BackfieldPhase.Faking;
                backfield.MoveParticipant(participant, field.Qb, 1f / 60);
                participant.Position += participant.Velocity / 60;
            }
            Assert.True(sawFake, play.Id);
            Assert.Equal(BackfieldPhase.Released, backfield.Phase);
            Assert.Same(field.Qb, field.Ball.Holder);
            Assert.True(backfield.AllowsThrow);
        }
    }
}
