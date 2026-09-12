using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class RedZoneDefenseTests
{
    [Theory]
    [InlineData(20f)]
    [InlineData(10f)]
    [InlineData(5f)]
    [InlineData(1f)]
    public void DeepDefendersCarryEndZoneThreatsAcrossSchemes(float yardsToGoal)
    {
        float los = 110f - yardsToGoal;
        foreach (var scheme in Enum.GetValues<CoverageScheme>())
        {
            var defense = Create(scheme, los, Spread(los));
            foreach (var defender in defense.Defenders.Where(d => d.ZoneRole.IsDeepZone()))
            {
                float x = ZoneCoverage.GetZoneAnchor(defender, los).X;
                var receiver = new Receiver(0, ReceiverSlot.WR1, new(x, 115f)) { Velocity = new(0, 7f) };
                Vector2 target = ZoneCoverage.GetZoneTarget(defender, [receiver], los);
                Assert.InRange(target.Y, 115f, 119.5f);

                receiver.Position = new(x, 119f);
                target = ZoneCoverage.GetZoneTarget(defender, [receiver], los);
                Assert.InRange(target.Y, 119f, 119.5f);
            }
        }
    }

    [Fact]
    public void UnthreatenedSafetyKeepsEndZoneAlignmentWithoutDroppingToBackLine()
    {
        var defender = new Defender(new(15f, 114f), DefensivePosition.DB, DefenderSlot.FS)
        { ZoneRole = CoverageRole.DeepLeft };
        Assert.Equal(114f, ZoneCoverage.GetZoneTarget(defender, [], 105f).Y);
    }

    [Theory]
    [InlineData(60f)]
    [InlineData(90f)]
    [InlineData(100f)]
    [InlineData(105f)]
    [InlineData(109f)]
    public void ZonesRemainOrderedAndInsidePlayableField(float los)
    {
        foreach (var role in Enum.GetValues<CoverageRole>().Where(r => r != CoverageRole.None))
        {
            var defender = new Defender(new(26f, los + 6f), DefensivePosition.DB, DefenderSlot.NB) { ZoneRole = role };
            var bounds = ZoneCoverage.GetZoneBounds(defender, los);
            Assert.InRange(bounds.YMin, los, 119.5f);
            Assert.InRange(bounds.YMax, bounds.YMin, 119.5f);
            var receiver = new Receiver(0, ReceiverSlot.WR1, new(bounds.XMin + 1f, 119f));
            Assert.InRange(ZoneCoverage.GetZoneTarget(defender, [receiver], los).Y, los, 119.5f);
        }
    }

    [Fact]
    public void UnderneathCoverageProtectsShortWindowInsteadOfTakingFullFieldDrop()
    {
        const float los = 109f;
        foreach (var role in new[] { CoverageRole.FlatLeft, CoverageRole.HookMiddle, CoverageRole.Robber })
        {
            var defender = new Defender(new(20f, 112f), DefensivePosition.LB, DefenderSlot.OLB1) { ZoneRole = role };
            var receiver = new Receiver(0, ReceiverSlot.WR1, new(20f, 110f)) { Velocity = new(7f, 0f) };
            var target = ZoneCoverage.GetZoneTarget(defender, [receiver], los);
            Assert.InRange(target.Y, los, 112f);
        }
    }

    [Fact]
    public void NickelAnchorCanRemainInsideEndZone()
    {
        var nickel = new Defender(new(26f, 113f), DefensivePosition.DB, DefenderSlot.NB)
        { ZoneRole = CoverageRole.HookMiddle };
        Assert.InRange(ZoneCoverage.GetZoneAnchor(nickel, 109f).Y, 113f, 119.5f);
    }

    [Theory]
    [InlineData(107.5f)]
    [InlineData(109f)]
    public void GoalLineFrontStaysNearSnapAndSpreadKeepsNickel(float los)
    {
        var defense = Create(CoverageScheme.Cover2Zone, los, Spread(los));
        Assert.Contains(defense.Defenders, d => d.Slot == DefenderSlot.NB);
        Assert.DoesNotContain(defense.Defenders, d => d.Slot == DefenderSlot.MLB);
        Assert.All(defense.Defenders.Where(d => d.PositionRole is DefensivePosition.DL or DefensivePosition.DE),
            d => Assert.InRange(d.Position.Y, los + .35f, los + 1f));
        Assert.Contains(defense.Defenders, d => d.ZoneRole.IsDeepZone() && d.Position.Y > 110f);
    }

    [Fact]
    public void HeavyGoalLineFormationKeepsBasePersonnel()
    {
        const float los = 109f;
        List<Receiver> receivers = [
            new(0, ReceiverSlot.WR1, new(6f, los)),
            new(1, ReceiverSlot.TE1, new(21f, los)),
            new(2, ReceiverSlot.TE2, new(32f, los)),
            new(3, ReceiverSlot.RB1, new(26f, los - 5f)),
            new(4, ReceiverSlot.FB, new(26f, los - 3f))];
        var defense = Create(CoverageScheme.Cover2Zone, los, receivers);
        Assert.Contains(defense.Defenders, d => d.Slot == DefenderSlot.MLB);
        Assert.DoesNotContain(defense.Defenders, d => d.Slot == DefenderSlot.NB);
    }

    private static DefenseResult Create(CoverageScheme scheme, float los, List<Receiver> receivers) =>
        new DefenseFactory().CreateDefense(
            new DefensiveContext(los, 110f - los, 1, 0, 0, SeasonStage.RegularSeason, 110f),
            new DefensiveCallDecision(scheme, BlitzDecision.None), default, receivers, new Random(42));

    private static List<Receiver> Spread(float los) => [
        new(0, ReceiverSlot.WR1, new(6f, los)),
        new(1, ReceiverSlot.WR2, new(17f, los)),
        new(2, ReceiverSlot.WR3, new(36f, los)),
        new(3, ReceiverSlot.WR4, new(47f, los)),
        new(4, ReceiverSlot.RB1, new(26f, los - 5f))];
}
