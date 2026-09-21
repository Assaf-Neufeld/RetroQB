using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class MatchCoverageTests
{
    private static readonly DefensiveContext Context = new(30, 10, 1, 0, 0, SeasonStage.RegularSeason);

    [Theory]
    [InlineData("def.quarters-match", false)]
    [InlineData("def.quarters-match", true)]
    [InlineData("def.cover3-match", false)]
    [InlineData("def.cover3-match", true)]
    public void SlotUsesNickelAndEveryCoverageDefenderKeepsALocalZone(string call, bool flipped)
    {
        var receivers = new List<Receiver>
        {
            new(0, ReceiverSlot.WR1, new(8, 30)), new(1, ReceiverSlot.WR2, new(15, 29)),
            new(2, ReceiverSlot.WR3, new(45, 30)), new(3, ReceiverSlot.TE1, new(33, 28.7f)),
            new(4, ReceiverSlot.RB1, new(23, 24))
        };
        if (flipped) foreach (var receiver in receivers) receiver.Position = new(Constants.FieldWidth - receiver.Position.X, receiver.Position.Y);
        var result = Resolve(call, receivers);
        Assert.Contains(result.Assignments, assignment => assignment.Slot == DefenderSlot.NB);
        Assert.All(result.Assignments.Where(assignment => !assignment.Rush), assignment =>
        {
            Assert.NotEqual(CoverageRole.None, assignment.Zone);
            Assert.Equal(-1, assignment.ManTarget);
            Assert.Equal(-1, assignment.MatchTarget);
            AssertLocalSide(assignment);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosedSideCornerProtectsItsOwnDeepLane(bool flipped)
    {
        var receivers = new List<Receiver>
        {
            new(0, ReceiverSlot.WR1, new(9, 30)), new(1, ReceiverSlot.WR2, new(16, 29)),
            new(2, ReceiverSlot.TE1, new(20, 28.7f)), new(3, ReceiverSlot.TE2, new(33, 30)),
            new(4, ReceiverSlot.RB1, new(Constants.FieldWidth / 2, 23))
        };
        if (flipped) foreach (var receiver in receivers) receiver.Position = new(Constants.FieldWidth - receiver.Position.X, receiver.Position.Y);
        var result = Resolve("def.quarters-match", receivers);
        var slot = flipped ? DefenderSlot.CB1 : DefenderSlot.CB2;
        var job = result.Assignments.Single(a => a.Slot == slot);
        Assert.Equal(-1, job.MatchTarget);
        Assert.True(job.Zone.IsDeepZone());
        AssertLocalSide(job);
        var defender = result.CreateDefense(DefensiveTeamAttributes.Default).Defenders.Single(d => d.Slot == job.Slot);
        Assert.Equal(-1, defender.MatchReceiverIndex);
        // A crossing route cannot drag the corner out of its original half.
        receivers[3].Position += new Vector2(flipped ? 3 : -3, 12);
        receivers[3].Velocity = new(0, 6);
        receivers[1].Position = new(receivers[3].Position.X + 5, 53);
        var target = ZoneCoverage.GetZoneTarget(defender, receivers, 30);
        var bounds = ZoneCoverage.GetZoneBounds(defender, 30);
        Assert.InRange(target.X, bounds.XMin, bounds.XMax);
        var mirrored = result.Mirror().CreateDefense(DefensiveTeamAttributes.Default).Defenders.Single(d => d.Slot == job.Slot);
        Assert.Equal(-1, mirrored.MatchReceiverIndex);
        Assert.Equal(Constants.FieldWidth - job.Position.X, mirrored.AlignmentPosition.X);
    }

    [Theory]
    [InlineData("def.quarters-match", 4)]
    [InlineData("def.cover3-match", 3)]
    public void EveryCatalogFormationUsesLocalZonesAndRetainsDeepShell(string call, int deepCount)
    {
        foreach (bool flipped in new[] { false, true })
        foreach (var play in PlaybookBuilder.BuildCatalog().Plays)
        {
            var formation = new FormationFactory().CreateFormation(PlayResolver.Resolve(play, flipped), 30);
            var result = Resolve(call, formation.Receivers);
            Assert.DoesNotContain(result.Assignments, assignment => assignment.ManTarget >= 0 || assignment.MatchTarget >= 0);
            Assert.All(result.Assignments.Where(assignment => !assignment.Rush), AssertLocalSide);
            Assert.Equal(deepCount, result.Assignments.Count(a => a.Zone.IsDeepZone()));
        }
    }

    private static void AssertLocalSide(DefensiveAssignment assignment)
    {
        float middle = Constants.FieldWidth * .5f;
        if (assignment.Zone is CoverageRole.DeepLeft or CoverageRole.DeepQuarterLeft
            or CoverageRole.FlatLeft or CoverageRole.HookLeft) Assert.True(assignment.Position.X < middle);
        if (assignment.Zone is CoverageRole.DeepRight or CoverageRole.DeepQuarterRight
            or CoverageRole.FlatRight or CoverageRole.HookRight) Assert.True(assignment.Position.X > middle);
        if (assignment.Zone is CoverageRole.DeepMiddle or CoverageRole.HookMiddle or CoverageRole.Robber)
            Assert.InRange(assignment.Position.X, middle - Constants.FieldWidth * .10f, middle + Constants.FieldWidth * .10f);
    }

    private static ResolvedDefensivePlay Resolve(string call, List<Receiver> receivers) =>
        DefensivePlayResolver.Resolve(DefensivePlaybook.Get(call),
            new(new(Constants.FieldWidth / 2, 26), receivers.Select(r => new VisibleReceiver(r.Index, r.Slot, r.Position)).ToList()),
            Context, DefensiveTeamAttributes.Default, new Random(42));
}
