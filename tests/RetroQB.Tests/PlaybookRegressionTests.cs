using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Input;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class PlaybookRegressionTests
{
    public static TheoryData<int, string, string> PassCases => new()
    {
        { 1, "Mesh", "pass.mesh" }, { 2, "Bunch Quick", "pass.bunch-quick" },
        { 3, "Four Verts", "pass.four-verts" }, { 4, "Deep Ins", "pass.deep-ins" },
        { 5, "Flood", "pass.flood" }, { 6, "Smash", "pass.smash" },
        { 7, "Slant Flat", "pass.slant-flat" }, { 8, "PA Deep", "pass.pa-deep" },
        { 9, "Combo", "pass.combo" }
    };

    [Theory]
    [MemberData(nameof(PassCases))]
    public void NamedPassesKeepTheirIdentityPersonnelAndTargetingOrder(int index, string name, string id)
    {
        var play = PlayResolver.Resolve(PlaybookBuilder.BuildPassPlays()[index]);
        var formation = new FormationFactory().CreateFormation(play, 40);
        RouteAssigner.AssignRoutes(formation.Receivers, play);
        Assert.Equal(name, play.Name);
        Assert.Equal(id, play.Id);
        Assert.All(formation.Receivers.Where(r => r.Eligible), r => Assert.NotNull(r.RouteDefinition));
        Assert.Equal(11, 1 + formation.Receivers.Count + formation.Blockers.Count);
        Assert.Equal(38.4f, formation.Qb.Position.Y, 3);
        var priorities = new ReceiverPriorityManager();
        priorities.AssignPriorities(formation.Receivers);
        Assert.Equal(formation.Receivers.Count(r => r.Eligible), priorities.GetPriorityIndices().Count);
    }

    [Fact]
    public void NamedRunsKeepTheirPersonnelAndHandoff()
    {
        var manager = new PlayManager();
        var execution = new PlayExecutionController(new InputManager(), new BlockingController());
        for (int index = 1; index < 10; index++)
        {
            manager.SelectRunPlay(index, new Random(42));
            var play = manager.SelectedPlay;
            var formation = new FormationFactory().CreateFormation(play, 40);
            RouteAssigner.AssignRoutes(formation.Receivers, play);
            Assert.Equal(11, 1 + formation.Receivers.Count + formation.Blockers.Count);
            Assert.All(formation.Receivers.Where(r => r.IsTightEnd), r => Assert.True(r.IsBlocking));
            var rb = Assert.Single(formation.Receivers, r => r.IsRunningBack);
            rb.Position = formation.Qb.Position;
            execution.Backfield.Update(play, formation.Ball, formation.Qb, formation.Receivers, play.Backfield.MinimumDelay + 0.01f);
            execution.TryHandoffToRunningBack(manager, formation.Ball, formation.Qb, formation.Receivers);
            Assert.Same(rb, formation.Ball.Holder);
            Assert.Equal(BallState.HeldByReceiver, formation.Ball.State);
        }
    }

    [Fact]
    public void ExistingHotkeyOrderIsPreserved()
    {
        var manager = new PlayManager();
        Assert.Equal(new[] { "Wildcard", "Mesh", "Bunch Quick", "Four Verts", "Deep Ins", "Flood", "Smash", "Slant Flat", "PA Deep", "Combo" }, manager.PassPlays.Select(p => p.Name));
        Assert.Equal(new[] { "Wildcard", "HB Dive", "Power Right", "Power Left", "Counter Right", "Counter Left", "Sweep Right", "Sweep Left", "Stretch Right", "Draw" }, manager.RunPlays.Select(p => p.Name));
        Assert.False(manager.SelectPassPlay(10, new Random(42)));
        Assert.False(manager.SelectRunPlay(-1, new Random(42)));
    }
}
