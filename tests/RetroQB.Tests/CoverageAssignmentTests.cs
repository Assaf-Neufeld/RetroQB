using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class CoverageAssignmentTests
{
    private static readonly DefensiveContext Context = new(30, 10, 1, 0, 0, SeasonStage.RegularSeason);

    [Theory]
    [InlineData(CoverageScheme.QuartersMatch)]
    [InlineData(CoverageScheme.Cover3Match)]
    public void FormerMatchShellsKeepEveryDefenderInALocalZone(CoverageScheme scheme)
    {
        foreach (bool flipped in new[] { false, true })
        foreach (var definition in PlaybookBuilder.BuildCatalog().Plays)
        {
            var formation = new FormationFactory().CreateFormation(PlayResolver.Resolve(definition, flipped), 30);
            var defense = Create(scheme, formation.Receivers);
            var coverage = defense.Defenders.Where(d => !d.IsRusher).ToList();
            Assert.All(coverage, defender =>
            {
                Assert.NotEqual(CoverageRole.None, defender.ZoneRole);
                Assert.Equal(-1, defender.CoverageReceiverIndex);
                Assert.Equal(-1, defender.MatchReceiverIndex);
            });

            float middle = Constants.FieldWidth * .5f;
            Assert.All(coverage.Where(d => d.ZoneRole is CoverageRole.DeepLeft or CoverageRole.DeepQuarterLeft
                or CoverageRole.FlatLeft or CoverageRole.HookLeft), d => Assert.True(d.Position.X < middle));
            Assert.All(coverage.Where(d => d.ZoneRole is CoverageRole.DeepRight or CoverageRole.DeepQuarterRight
                or CoverageRole.FlatRight or CoverageRole.HookRight), d => Assert.True(d.Position.X > middle));
        }
    }

    [Theory]
    [InlineData(CoverageScheme.Cover2Man)]
    [InlineData(CoverageScheme.Robber)]
    [InlineData(CoverageScheme.Cover1)]
    [InlineData(CoverageScheme.Cover0)]
    public void FactoryKeepsCornersOnTheirPlannedWideouts(CoverageScheme scheme)
    {
        var receivers = new List<Receiver>
        {
            new(0, ReceiverSlot.WR1, new(7, 30)),
            new(1, ReceiverSlot.WR2, new(16, 29)),
            new(2, ReceiverSlot.TE1, new(21, 30)),
            new(3, ReceiverSlot.TE2, new(34, 30)),
            new(4, ReceiverSlot.RB1, new(Constants.FieldWidth / 2, 25))
        };
        var defense = Create(scheme, receivers);
        foreach (var corner in defense.Defenders.Where(d => d.Slot is DefenderSlot.CB1 or DefenderSlot.CB2))
        {
            var target = receivers.Single(r => r.Index == corner.CoverageReceiverIndex);
            Assert.Equal(OffensivePosition.WR, target.PositionRole);
            Assert.Equal(target.Position.X, corner.AlignmentPosition.X);
        }
    }

    [Theory]
    [InlineData("def.cover2-man")]
    [InlineData("def.robber")]
    [InlineData("def.cover1")]
    [InlineData("def.edge")]
    [InlineData("def.zero")]
    public void ResolvedManDbsAlignOverTheirFinalAssignment(string callId)
    {
        bool tightEndCoveredByDb = false;
        foreach (bool flipped in new[] { false, true })
        foreach (var definition in PlaybookBuilder.BuildCatalog().Plays)
        {
            var formation = new FormationFactory().CreateFormation(PlayResolver.Resolve(definition, flipped), 30);
            var result = DefensivePlayResolver.Resolve(DefensivePlaybook.Get(callId), VisibleOffense.From(formation),
                Context, DefensiveTeamAttributes.Default, new Random(42));
            foreach (var db in result.Assignments.Where(d => d.PositionRole == DefensivePosition.DB && d.ManTarget >= 0))
            {
                var receiver = formation.Receivers.Single(r => r.Index == db.ManTarget);
                tightEndCoveredByDb |= receiver.IsTightEnd;
                Assert.Equal(receiver.Position.X, db.Position.X);
                Assert.InRange(db.Position.Y - Context.LineOfScrimmage, .35f, 8f);
                var spawned = result.CreateDefense(DefensiveTeamAttributes.Default).Defenders.Single(d => d.Slot == db.Slot);
                Assert.Equal(db.Position, spawned.AlignmentPosition);
            }
        }
        // Pressure must exercise the replacement-DB path; normal calls may keep TEs on LBs.
        if (callId is "def.edge" or "def.zero") Assert.True(tightEndCoveredByDb);
    }

    private static DefenseResult Create(CoverageScheme scheme, List<Receiver> receivers) =>
        new DefenseFactory().CreateDefense(Context, new(scheme, BlitzDecision.None), default, receivers, new Random(42));
}
