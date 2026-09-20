using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Gameplay;
using RetroQB.Input;
using RetroQB.Stats;

namespace RetroQB.Tests;

public sealed class NormalTimedGameTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RetroQB-Release-" + Guid.NewGuid());
    private sealed class Input : IGameInput
    {
        public bool Enter, Space, Focused = true;
        public int? Team;
        public string Text = "";
        public Vector2 GetMovementDirection() => Vector2.Zero;
        public bool IsSprintHeld() => false;
        public bool IsEnterPressed() => Enter;
        public bool IsSpacePressed() => Space;
        public bool IsFocused() => Focused;
        public int? GetTeamSelection() => Team;
        public string ReadTextInput(int maxLength) { var text = Text; Text = ""; return text; }
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private (GameSession Game, Input Input, PlayerRecordStore Store) Start()
    {
        var input = new Input { Enter = true, Team = 2 };
        var records = new PlayerRecordStore(Path.Combine(_directory, "records.json"), MatchRuleset.TwoSidedTimed);
        var game = GameSession.CreateTimed(input, new Random(101), records);
        game.Update(0); Assert.NotNull(game.TimedSeason); Assert.True(game.TimedSeason.Pregame);
        input.Team = null; input.Text = "Release Player";
        game.Update(0); input.Enter = false;
        Assert.False(game.TimedSeason.Pregame); Assert.Equal("Release Player", game.TimedSeason.PlayerName);
        return (game, input, records);
    }

    [Fact]
    public void NormalTeamMenuStartsTimedSeasonAndTouchdownGivesCpuPossessionWithoutSyntheticPoints()
    {
        var (game, input, _) = Start(); using (game)
        {
            var s = game.FullMatch!;
            Assert.Equal(OffensiveTeamPresets.All[2].Name, s.Match.User.Definition.Name);
            Assert.Equal(MatchRuleset.TwoSidedTimed, s.Match.Ruleset); Assert.Equal(180, s.Clock.RemainingSeconds);
            input.Space = true; game.Update(0); input.Space = false;
            s.Drive.Actors.Qb.Position = new(25, 111); game.Update(.01f);
            Assert.Equal(7, s.Match.User.Score); Assert.Equal(0, s.Match.Opponent.Score);
            input.Space = true; game.Update(0); input.Space = false;
            Assert.True(s.HumanOnDefense); Assert.Equal(20, s.Match.Series.OwnYardLine);
            s.Match.User.Score = 21; game.Update(0); Assert.False(s.Timed.Finished);
        }
    }

    [Fact]
    public void NormalInputFreezesOnFocusLossAndBoundsCatchupOnReturn()
    {
        var (game, input, _) = Start(); using (game)
        {
            input.Space = true; game.Update(0); input.Space = false;
            double before = game.FullMatch!.Clock.RemainingSeconds;
            input.Focused = false; game.Update(120);
            Assert.Equal(before, game.FullMatch.Clock.RemainingSeconds);
            input.Focused = true; game.Update(120);
            Assert.InRange(before - game.FullMatch.Clock.RemainingSeconds, .099, .101);
        }
    }

    [Fact]
    public void SavedNormalSeasonReturnsToTeamSelectionWithoutDuplicateSave()
    {
        var (game, input, records) = Start(); using (game)
        {
            var s = game.FullMatch!; s.Match.Opponent.Score = 7;
            s.Clock.StartPeriod(4); s.Timed.Continue(); s.Timed.Advance(0, "fixture.final"); s.Timed.Advance(180);
            s.Drive.ResolveSpecial(PlayEndReason.Kneel, 19);
            input.Enter = true; game.Update(0);
            Assert.True(game.TimedSeason!.Complete); Assert.True(game.TimedSeason.Saved);
            game.Update(0); Assert.Null(game.TimedSeason); Assert.Null(game.FullMatch);
            Assert.Single(records.GetLeaderboard());
            game.Update(0); Assert.NotNull(game.TimedSeason); Assert.True(game.TimedSeason.Pregame);
            Assert.Single(records.GetLeaderboard());
        }
    }

    [Fact]
    public void CpuCoordinatorIncludesEveryVerifiedCatalogCall()
    {
        var calls = new PlayManager().Catalog.Plays;
        Assert.Equal(calls.Where(p => p.Family == PlayType.Pass).Select(p => p.Id).Order(), OffensiveCoordinator.Passes.Order());
        Assert.Equal(calls.Where(p => p.Family == PlayType.Run).Select(p => p.Id).Order(), OffensiveCoordinator.Runs.Order());
    }
}
