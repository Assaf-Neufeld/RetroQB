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
        { 1, "Mesh", "WR1:InShallow,WR2:Slant,WR3:InShallow,WR4:Go,TE1:Flat" },
        { 2, "Bunch Quick", "WR1:InShallow,WR2:Slant,WR3:Flat,WR4:DoubleMove,TE1:InShallow" },
        { 3, "Four Verts", "WR1:Go,WR2:Go,WR3:Go,WR4:Go,TE1:Go" },
        { 4, "Deep Ins", "WR1:Go,WR2:InDeep,WR3:PostDeep,TE1:OutShallow,RB1:Block" },
        { 5, "Flood", "WR1:Flat,WR2:OutShallow,WR3:OutDeep,WR4:Go,TE1:InShallow" },
        { 6, "Smash", "WR1:InShallow,WR2:OutDeep,WR3:Go,TE1:InShallow,RB1:Block" },
        { 7, "Slant Flat", "WR1:Slant,WR2:Slant,WR3:Flat,TE1:Go,RB1:Flat" },
        { 8, "PA Deep", "WR1:Go,WR2:PostDeep,WR3:InDeep,TE1:DoubleMove,RB1:Flat" },
        { 9, "Combo", "WR1:InShallow,WR2:Go,WR3:InDeep,TE1:InShallow,RB1:Flat" }
    };

    [Theory]
    [MemberData(nameof(PassCases))]
    public void NamedPassesKeepTheirEffectiveAssignments(int index, string name, string assignments)
    {
        var play = PlayResolver.Resolve(PlaybookBuilder.BuildPassPlays()[index]);
        var formation = new FormationFactory().CreateFormation(play, 40);
        RouteAssigner.AssignRoutes(formation.Receivers, play);
        Assert.Equal(name, play.Name);
        Assert.Equal(assignments, string.Join(",", formation.Receivers.Select(r =>
            $"{r.Slot}:{(r.IsBlocking ? "Block" : r.Route.ToString())}")));
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
