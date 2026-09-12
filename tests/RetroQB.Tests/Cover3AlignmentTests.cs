using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class Cover3AlignmentTests
{
    [Theory]
    [InlineData(CoverageScheme.Cover3Zone, false)]
    [InlineData(CoverageScheme.Cover3Zone, true)]
    [InlineData(CoverageScheme.Cover3Match, false)]
    [InlineData(CoverageScheme.Cover3Match, true)]
    public void BlockingAssignmentsDoNotChangePreSnapShell(CoverageScheme scheme, bool flipped)
    {
        foreach (var definition in PlaybookBuilder.BuildCatalog().Plays)
        {
            var play = PlayResolver.Resolve(definition, flipped);
            var formation = new FormationFactory().CreateFormation(play, 30f);
            var expected = Create(scheme, formation.Receivers);
            RouteAssigner.AssignRoutes(formation.Receivers, play);
            var actual = Create(scheme, formation.Receivers);

            Assert.Equal(11, actual.Defenders.Count);
            Assert.Equal(expected.Defenders.Select(d => (d.Slot, d.Position)),
                actual.Defenders.Select(d => (d.Slot, d.Position)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GunDoublesStretchKeepsCornersOverWideoutsAndThreeDeepRoles(bool flipped)
    {
        var definition = PlaybookBuilder.BuildCatalog().Plays.First(p =>
            p.Formation.Type == FormationType.GunDoubles && p.RunConcept == RunConcept.Stretch);
        var play = PlayResolver.Resolve(definition, flipped);
        var formation = new FormationFactory().CreateFormation(play, 30f);
        RouteAssigner.AssignRoutes(formation.Receivers, play);
        var defense = Create(CoverageScheme.Cover3Zone, formation.Receivers);
        var wideouts = formation.Receivers.Where(r => r.PositionRole == OffensivePosition.WR)
            .OrderBy(r => r.Position.X).ToList();
        var left = defense.Defenders.Single(d => d.Slot == DefenderSlot.CB1);
        var right = defense.Defenders.Single(d => d.Slot == DefenderSlot.CB2);
        Assert.InRange(MathF.Abs(left.Position.X - wideouts[0].Position.X), 0f, 1f);
        Assert.InRange(MathF.Abs(right.Position.X - wideouts[^1].Position.X), 0f, 1f);
        Assert.Equal(CoverageRole.DeepLeft, left.ZoneRole);
        Assert.Equal(CoverageRole.DeepRight, right.ZoneRole);
        Assert.Equal(CoverageRole.DeepMiddle, defense.Defenders.Single(d => d.Slot == DefenderSlot.FS).ZoneRole);
        Assert.Contains(defense.Defenders, d => d.Slot == DefenderSlot.NB);
        Assert.Single(defense.Defenders, d => d.ZoneRole == CoverageRole.FlatLeft);
        Assert.Single(defense.Defenders, d => d.ZoneRole == CoverageRole.FlatRight);
    }

    private static DefenseResult Create(CoverageScheme scheme, List<Receiver> receivers) =>
        new DefenseFactory().CreateDefense(
            new DefensiveContext(30f, 10f, 1, 0, 0, SeasonStage.RegularSeason),
            new DefensiveCallDecision(scheme, BlitzDecision.None), default, receivers, new Random(42));
}
