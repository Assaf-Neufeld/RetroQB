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
    public void SlotUsesNickelAndLinebackersStayWithNearbyBackAndTightEnd(string call, bool flipped)
    {
        var receivers = new List<Receiver>
        {
            new(0, ReceiverSlot.WR1, new(8, 30)), new(1, ReceiverSlot.WR2, new(15, 29)),
            new(2, ReceiverSlot.WR3, new(45, 30)), new(3, ReceiverSlot.TE1, new(33, 28.7f)),
            new(4, ReceiverSlot.RB1, new(23, 24))
        };
        if (flipped) foreach (var receiver in receivers) receiver.Position = new(Constants.FieldWidth - receiver.Position.X, receiver.Position.Y);
        var result = Resolve(call, receivers);
        Assert.Equal(1, result.Assignments.Single(a => a.Slot == DefenderSlot.NB).ManTarget);
        Assert.Equal(receivers[1].Position.X, result.Assignments.Single(a => a.Slot == DefenderSlot.NB).Position.X);
        Assert.Equal(4, result.Assignments.Single(a => a.Slot == (flipped ? DefenderSlot.OLB2 : DefenderSlot.OLB1)).ManTarget);
        Assert.Equal(3, result.Assignments.Single(a => a.Slot == (flipped ? DefenderSlot.OLB1 : DefenderSlot.OLB2)).ManTarget);
        AssertAllCovered(result, receivers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosedSideCornerAlignsAndMatchesSecondTightEndInPreviewAndLivePlay(bool flipped)
    {
        var receivers = new List<Receiver>
        {
            new(0, ReceiverSlot.WR1, new(9, 30)), new(1, ReceiverSlot.WR2, new(16, 29)),
            new(2, ReceiverSlot.TE1, new(20, 28.7f)), new(3, ReceiverSlot.TE2, new(33, 30)),
            new(4, ReceiverSlot.RB1, new(Constants.FieldWidth / 2, 23))
        };
        if (flipped) foreach (var receiver in receivers) receiver.Position = new(Constants.FieldWidth - receiver.Position.X, receiver.Position.Y);
        var result = Resolve("def.quarters-match", receivers);
        var job = result.Assignments.Single(a => a.MatchTarget == 3);
        Assert.Equal(flipped ? DefenderSlot.CB1 : DefenderSlot.CB2, job.Slot);
        Assert.Equal(receivers[3].Position.X, job.Position.X);
        Assert.Equal(receivers[3].Position, job.Target);
        Assert.Contains("Match:", job.Responsibility);
        AssertAllCovered(result, receivers);
        var defender = result.CreateDefense(DefensiveTeamAttributes.Default).Defenders.Single(d => d.Slot == job.Slot);
        Assert.Equal(3, defender.MatchReceiverIndex);
        // Carry the TE even when another receiver becomes the deeper threat in this lane.
        receivers[3].Position += new Vector2(flipped ? 3 : -3, 12);
        receivers[3].Velocity = new(0, 6);
        receivers[1].Position = new(receivers[3].Position.X + 5, 53);
        var target = ZoneCoverage.GetZoneTarget(defender, receivers, 30);
        Assert.Equal(receivers[3].Position.X, target.X);
        Assert.True(target.Y > receivers[3].Position.Y);
        Assert.True(target.Y < receivers[1].Position.Y);
        var mirrored = result.Mirror().CreateDefense(DefensiveTeamAttributes.Default).Defenders.Single(d => d.Slot == job.Slot);
        Assert.Equal(3, mirrored.MatchReceiverIndex);
        Assert.Equal(Constants.FieldWidth - job.Position.X, mirrored.AlignmentPosition.X);
    }

    [Theory]
    [InlineData("def.quarters-match", 4)]
    [InlineData("def.cover3-match", 3)]
    public void EveryCatalogFormationHasFiveExplicitTargetsAndRetainsDeepShell(string call, int deepCount)
    {
        foreach (bool flipped in new[] { false, true })
        foreach (var play in PlaybookBuilder.BuildCatalog().Plays)
        {
            var formation = new FormationFactory().CreateFormation(PlayResolver.Resolve(play, flipped), 30);
            var result = Resolve(call, formation.Receivers);
            AssertAllCovered(result, formation.Receivers);
            Assert.Equal(deepCount, result.Assignments.Count(a => a.Zone.IsDeepZone()));
        }
    }

    private static void AssertAllCovered(ResolvedDefensivePlay result, List<Receiver> receivers) =>
        Assert.Equal(receivers.Select(r => r.Index).Order(), result.Assignments
            .Select(a => a.MatchTarget >= 0 ? a.MatchTarget : a.ManTarget).Where(i => i >= 0).Order());

    private static ResolvedDefensivePlay Resolve(string call, List<Receiver> receivers) =>
        DefensivePlayResolver.Resolve(DefensivePlaybook.Get(call),
            new(new(Constants.FieldWidth / 2, 26), receivers.Select(r => new VisibleReceiver(r.Index, r.Slot, r.Position)).ToList()),
            Context, DefensiveTeamAttributes.Default, new Random(42));
}
