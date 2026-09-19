using RetroQB.AI;
using RetroQB.Routes;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;

namespace RetroQB.Tests;

public sealed class DefensivePlaybookTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    public void CALL01_EveryCallCoversEveryCatalogFormationWithOneJobPerSlot(int callIndex)
    {
        var factory = new FormationFactory();
        var call = DefensivePlaybook.All[callIndex];
        var catalog = new PlayManager().Catalog;
        bool baseSeen = false, nickelSeen = false;
        foreach (var definition in catalog.Plays)
        {
            var play = PlayResolver.Resolve(definition);
            var visible = VisibleOffense.From(factory.CreateFormation(play, 30));
            var result = DefensivePlayResolver.Resolve(call, visible, new(30, 10, 1, 0, 0, SeasonStage.RegularSeason),
                DefensiveTeamAttributes.Default, new Random(101));
            baseSeen |= result.ControlledSlot == DefenderSlot.MLB;
            nickelSeen |= result.ControlledSlot == DefenderSlot.OLB1;
            Assert.Equal(11, result.Assignments.Count);
            Assert.Equal(11, result.Assignments.Select(a => a.Slot).Distinct().Count());
            Assert.Equal(callIndex == 8 ? 5 : callIndex == 9 ? 6 : 4, result.Assignments.Count(a => a.Rush));
            foreach (var job in result.Assignments)
            {
                Assert.Equal(1, (job.Rush ? 1 : 0) + (job.Zone != CoverageRole.None ? 1 : 0) + (job.ManTarget >= 0 ? 1 : 0));
                if (job.ManTarget >= 0) Assert.Contains(visible.Receivers, r => r.Index == job.ManTarget);
                Assert.True(float.IsFinite(job.Position.X) && float.IsFinite(job.Position.Y));
                Assert.True(float.IsFinite(job.Target.X) && float.IsFinite(job.Target.Y));
            }
            if (result.UnderneathMan)
            {
                Assert.Equal(visible.Receivers.Select(r => r.Index).Order(), result.Assignments.Where(a => a.ManTarget >= 0).Select(a => a.ManTarget).Order());
                Assert.Equal(callIndex == 9 ? 0 : callIndex == 4 ? 2 : 1,
                    result.Assignments.Count(a => a.Zone is CoverageRole.DeepLeft or CoverageRole.DeepMiddle or CoverageRole.DeepRight));
            }
            Assert.Same(result, result.Mirror().Mirror());
            Assert.Equal(result.ControlledSlot, result.Mirror().ControlledSlot);
        }
        Assert.True(baseSeen && nickelSeen);
    }

    [Theory]
    [InlineData("run.hb-dive", 2)] [InlineData("pass.mesh", 0)] [InlineData("pass.mesh", 8)] [InlineData("run.hb-dive", 9)]
    public void CALL03_ResolvedSetupRetainsPreviewWithoutFurtherRandomness(string id, int callIndex)
    {
        var plays = new PlayManager(); plays.SelectCatalogPlay(id, new Random(101));
        var formation = new FormationFactory().CreateFormation(plays.SelectedPlay, 30);
        var random = new CountingRandom(101);
        var result = DefensivePlayResolver.Resolve(DefensivePlaybook.All[callIndex], VisibleOffense.From(formation),
            new(30, 10, 1, 0, 0, SeasonStage.RegularSeason), DefensiveTeamAttributes.Default, random);
        int calls = random.Calls;
        var setup = new PlaySetupController(new FormationFactory(), new DefenseFactory(), new DefensiveCoordinator(new()), random);
        for (int i = 0; i < 3; i++)
        {
            var actors = setup.SetupPlay(plays.SelectedPlay, 30, result, OffensiveTeamAttributes.Default, DefensiveTeamAttributes.Default);
            Assert.Equal(result.Assignments.Select(a => a.Position), actors.Defenders.Select(d => d.Position));
            Assert.Equal(result.Assignments.Select(a => a.Jitter), actors.Defenders.Select(d => d.ZoneJitterX));
            Assert.Equal(result.Assignments.Select(a => a.ManTarget), actors.Defenders.Select(d => d.CoverageReceiverIndex));
            Assert.Equal(result.Assignments.Select(a => a.Rush), actors.Defenders.Select(d => d.IsRusher));
        }
        Assert.Equal(calls, random.Calls);
    }

    [Fact]
    public void HiddenRoutesAndEligibilityCannotChangePreview()
    {
        var play = new PlayManager().SelectedPlay;
        var formation = new FormationFactory().CreateFormation(play, 30);
        var context = new DefensiveContext(30, 10, 1, 0, 0, SeasonStage.RegularSeason);
        var before = VisibleOffense.From(formation);
        foreach (var r in formation.Receivers) { r.IsBlocking = true; r.Eligible = false; r.Route = RouteType.Flat; }
        var after = VisibleOffense.From(formation);
        foreach (var call in DefensivePlaybook.All)
        {
            var a = DefensivePlayResolver.Resolve(call, before, context, DefensiveTeamAttributes.Default, new Random(17));
            var b = DefensivePlayResolver.Resolve(call, after, context, DefensiveTeamAttributes.Default, new Random(17));
            Assert.Equal(a.Assignments, b.Assignments);
        }
    }

    private sealed class CountingRandom(int seed) : Random(seed)
    {
        public int Calls { get; private set; }
        public override double NextDouble() { Calls++; return base.NextDouble(); }
        public override int Next(int maxValue) { Calls++; return base.Next(maxValue); }
    }
}
