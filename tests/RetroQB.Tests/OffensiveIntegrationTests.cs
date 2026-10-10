using System.Numerics;
using Raylib_cs;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Gameplay;
using RetroQB.Input;
using RetroQB.Stats;

namespace RetroQB.Tests;

public sealed class OffensiveIntegrationTests
{
    private static string Settings() => Path.Combine(Path.GetTempPath(), "RetroQB-Favorites-" + Guid.NewGuid(), "favorites.json");
    private static FullMatchSession Session(bool cpu = false, double playSeconds = 20)
    {
        var user = TeamCatalog.Get("ballers"); var opponent = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        return new(new ScriptedInput(), 101, new(user, opponent, cpu ? opponent.Id : user.Id, playSeconds: playSeconds),
            favorites: new(PlaybookBuilder.BuildCatalog(), Settings()));
    }

    public static IEnumerable<object[]> Calls() => PlaybookBuilder.BuildCatalog().Plays.Select(p => new object[] { p.Id });
    [Theory] [MemberData(nameof(Calls))]
    public void EveryCallCanBePreviewedAndSelectedWithoutUsageOrRerolls(string id)
    {
        var s = Session(); var active = s.Drive.Plays.SelectedPlay;
        s.Update(0, new(Browser: new(Toggle: true)));
        int index = s.Browser.Results.ToList().FindIndex(p => p.Id == id);
        s.Update(0, new(Browser: new(Move: index - s.Browser.Cursor)));
        var preview = s.Browser.Preview!;
        Assert.Equal(id, preview.Id); Assert.Same(active, s.Drive.Plays.SelectedPlay);
        Assert.Empty(s.Match.History); Assert.Empty(s.Match.User.OffenseStrategy.Recent);
        s.Update(0, new(Browser: new(Select: true), Ready: true, Call: 2, Run: 3));
        Assert.False(s.Drive.Live); Assert.False(s.Browser.IsOpen);
        Assert.Same(preview, s.Drive.Plays.SelectedPlay);
        Assert.Contains(id, s.Drive.Plays.CallSheet.PassIds.Concat(s.Drive.Plays.CallSheet.RunIds));
        Assert.Equal(10, s.Drive.Plays.PassPlays.Count); Assert.Equal(10, s.Drive.Plays.RunPlays.Count);
        Assert.Equal(0, s.Match.User.GetCallCount(id));
        s.Update(0, new(Ready: true));
        Assert.True(s.Drive.Live); Assert.Equal(1, s.Match.User.GetCallCount(id));
        Assert.Equal(id, Assert.Single(s.Match.User.OffenseStrategy.Recent));
    }

    [Fact]
    public void BrowserOwnsConflictingKeysAndClockButFocusLossStillSuspendsTime()
    {
        var s = Session(); var active = s.Drive.Plays.SelectedPlay;
        s.Update(.5f, new(Browser: new(Toggle: true), Ready: true));
        s.Update(.5f, new(Browser: new(Category: true, Favorites: true), Call: 1, Run: 2,
            Ready: true, Kick: true, Punt: true, Kneel: true, Timeout: true, Replay: true, Statistics: true));
        Assert.Same(active, s.Drive.Plays.SelectedPlay); Assert.False(s.Drive.Live);
        Assert.Equal(19, s.Clock.PlaySeconds, 5); Assert.Equal(3, s.Clock.Timeouts(s.Match.User.Definition.Id));
        Assert.False(s.ShowStatistics); Assert.False(s.Replay.IsPlaying);
        Assert.Empty(s.Browser.Results);
        s.Update(1, new(Focused: false)); Assert.Equal(19, s.Clock.PlaySeconds, 5);
        s.Update(.5f, new(Browser: new(Close: true), Pause: true));
        Assert.False(s.Browser.IsOpen); Assert.Equal(ClockSuspension.None, s.Clock.Suspension);
        Assert.Equal(18.5, s.Clock.PlaySeconds, 5);
        s.Update(0, new(Pause: true)); Assert.Equal(ClockSuspension.Pause, s.Clock.Suspension);
        s.Update(1); Assert.Equal(18.5, s.Clock.PlaySeconds, 5);
    }

    [Fact]
    public void DelayPenaltyRebasesPreviewWithoutChangingCallFlipOrSnapCount()
    {
        var s = Session(playSeconds: 1); s.Drive.SelectOffenseById("pass.wildcard", false);
        var active = s.Drive.Plays.SelectedPlay;
        s.Update(0, new(Browser: new(Toggle: true)));
        s.Update(0, new(Browser: new(Flip: true)));
        float before = s.Browser.Actors!.Qb.Position.Y;
        s.Update(1.1f);
        Assert.Equal(before - 5, s.Browser.Actors!.Qb.Position.Y, 4);
        Assert.Same(active, s.Drive.Plays.SelectedPlay); Assert.True(s.Browser.Preview!.IsFlipped);
        Assert.Empty(s.Match.User.OffenseStrategy.Recent); Assert.False(s.Drive.Live);
    }

    [Fact]
    public void RunningPeriodClockClosesBrowserWithoutReinterpretingNavigationAsSnap()
    {
        var user = TeamCatalog.Get("ballers"); var opponent = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        var s = new FullMatchSession(new ScriptedInput(), 101, new(user, opponent, user.Id, quarterSeconds: .1),
            favorites: new(PlaybookBuilder.BuildCatalog(), Settings()));
        s.Drive.Snap(); s.Drive.ResolveSpecial(PlayEndReason.Tackle, 21);
        s.Update(1.3f, new(Ready: true)); Assert.Equal(ClockStopReason.Running, s.Clock.StopReason);
        s.Update(.11f, new(Browser: new(Toggle: true), Ready: true));
        Assert.False(s.Browser.IsOpen); Assert.False(s.Drive.Live);
        Assert.Single(s.Match.History); Assert.Equal(2, s.Clock.Quarter);
    }

    [Fact]
    public void FiltersPagingAndPreviewFlipPreserveActiveExecution()
    {
        var s = Session(); s.Browser.Open(); var active = s.Drive.Actors.Qb;
        s.Browser.Update(new(Jump: -1)); s.Browser.Update(new(Page: 1));
        Assert.Equal(12, s.Browser.Cursor);
        s.Browser.Update(new(Jump: 1)); Assert.Equal(107, s.Browser.Cursor);
        s.Browser.Update(new(Family: true)); Assert.All(s.Browser.Results, p => Assert.Equal(PlayType.Pass, p.Family));
        s.Browser.Update(new(Formation: true)); Assert.All(s.Browser.Results, p => Assert.Equal(s.Browser.Formation, p.Info.Formation));
        s.Browser.Update(new(Category: true)); Assert.All(s.Browser.Results, p => Assert.Equal(s.Browser.Category, p.Info.Category));
        s.Browser.Update(new(Concept: true)); Assert.All(s.Browser.Results, p => Assert.Equal(s.Browser.Concept, p.Info.Concept));
        Assert.Same(active, s.Drive.Actors.Qb); Assert.Empty(s.Match.History);
        s.Browser.Update(new(Reset: true)); Assert.Equal(108, s.Browser.Results.Count);
    }

    [Fact]
    public void FavoritesPersistThroughRestartAndReloadWithoutReplacingQuickSheet()
    {
        var s = Session(); var sheet = s.Drive.Plays.CallSheet;
        s.Browser.Open(); string id = s.Browser.Preview!.Id;
        s.Browser.Update(new(Star: true)); Assert.Same(sheet, s.Drive.Plays.CallSheet);
        s.Browser.Update(new(Favorites: true)); Assert.Equal(id, Assert.Single(s.Browser.Results).Id);
        s.Restart(); Assert.False(s.Browser.IsOpen); Assert.True(s.Browser.Favorites.Contains(id));
        string path = Settings(); var favorites = new PlaybookFavorites(s.Drive.Plays.Catalog, path);
        favorites.Toggle(id); Assert.True(new PlaybookFavorites(s.Drive.Plays.Catalog, path).Contains(id));
        favorites.Toggle(id); Assert.Empty(new PlaybookFavorites(s.Drive.Plays.Catalog, path).Ids);
    }

    [Theory] [InlineData("{")] [InlineData("null")] [InlineData("{\"Version\":2,\"Favorites\":[]}")]
    public void CorruptOrUnknownVersionFavoritesRecoverGracefully(string json)
    {
        string path = Settings(); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, json);
        var favorites = new PlaybookFavorites(PlaybookBuilder.BuildCatalog(), path);
        Assert.Empty(favorites.Ids); Assert.NotEmpty(favorites.Status);
        Assert.True(favorites.Toggle("pass.mesh")); Assert.True(new PlaybookFavorites(PlaybookBuilder.BuildCatalog(), path).Contains("pass.mesh"));
    }

    [Fact]
    public void MissingFavoriteIdsAreIgnoredAndSaveFailureKeepsSessionChoice()
    {
        string path = Settings(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"Version\":1,\"Favorites\":[\"pass.mesh\",\"gone\",null,\"pass.mesh\"]}");
        var favorites = new PlaybookFavorites(PlaybookBuilder.BuildCatalog(), path);
        Assert.Equal("pass.mesh", Assert.Single(favorites.Ids)); Assert.False(favorites.Toggle("gone"));
        var failed = new PlaybookFavorites(PlaybookBuilder.BuildCatalog(), Path.Combine(path, "unwritable.json"));
        Assert.True(failed.Toggle("pass.mesh")); Assert.True(failed.Contains("pass.mesh")); Assert.Contains("could not save", failed.Status);
    }

    [Fact]
    public void InputMappingFeedsBrowserEdgesSeparatelyFromExistingQuickKeys()
    {
        var keys = new HashSet<KeyboardKey> { KeyboardKey.L, KeyboardKey.Down, KeyboardKey.RightBracket, KeyboardKey.Enter, KeyboardKey.C, KeyboardKey.G };
        var input = new InputManager(keys.Contains); var browser = input.GetBrowserInput();
        Assert.True(browser.Toggle); Assert.Equal(1, browser.Move); Assert.True(browser.Formation);
        Assert.True(browser.Select); Assert.True(browser.Category); Assert.True(browser.Star);
        Assert.True(input.IsTimeoutPressed()); // Session arbitration owns the conflict.
    }

    [Fact]
    public void RecentHistoryIsBoundedSeparateFromTotalsAndCompletedObservationsAreIdempotent()
    {
        var m = new PlayManager(); m.SelectCatalogPlay("pass.mesh", new(1));
        for (int i = 0; i < 20; i++) m.StartPlay();
        Assert.Equal(20, m.GetCallCount("pass.mesh")); Assert.Equal(8, m.RecommendationMemory.Recent.Count);
        for (int i = 0; i < 8; i++) { m.SelectCatalogPlay("pass.flood", new(1)); m.StartPlay(); }
        Assert.DoesNotContain("pass.mesh", m.RecommendationMemory.Recent);
        var memory = m.RecommendationMemory; memory.ObserveCompleted(1, 1, 0); memory.ObserveCompleted(1, 0, 1);
        Assert.Equal(1, memory.ObservedPlays); Assert.Equal(1, memory.Pressure);
        memory.ObserveCompleted(2, 0, 1); Assert.Equal(.75f, memory.Pressure); Assert.Equal(.25f, memory.TightCoverage);
    }

    [Fact]
    public void SeededCpuChoicesReactToClockRosterPressureAndRecentCalls()
    {
        var a = new OffensiveCoordinator(new(77)); var b = new OffensiveCoordinator(new(77));
        var context = new OffensiveCallContext(new(2, 7, 60, 67)); var roster = TeamCatalog.Get("crimson-rush").Offense;
        var memory = new OffensiveStrategyMemory();
        for (int i = 0; i < 100; i++) { string call = a.Select(context, new(.6f), roster, memory); Assert.Equal(call, b.Select(context, new(.6f), roster, memory)); memory.RecordCall(call); }
        int Passes(OffensiveCallContext c) { var cpu = new OffensiveCoordinator(new(19)); return Enumerable.Range(0, 1000).Count(_ => OffensiveCoordinator.Passes.Contains(cpu.Select(c, new(.6f), roster))); }
        Assert.True(Passes(context with { LateGame = true, SecondsRemaining = 30, ScoreMargin = -7, Timeouts = 0 })
            > Passes(context with { LateGame = true, SecondsRemaining = 30, ScoreMargin = 7 }));
        var screen = PlayResolver.Resolve(PlaybookBuilder.BuildCatalog()["pass.gun-doubles.rb-screen"]);
        float ordinary = OffensiveCallScoring.Weight(screen, context, roster);
        var pressure = new OffensiveStrategyMemory(); pressure.ObserveCompleted(1, 1, 0);
        Assert.True(OffensiveCallScoring.Weight(screen, context, roster, pressure) > ordinary);
        pressure.RecordCall(screen.Id); Assert.True(OffensiveCallScoring.Weight(screen, context, roster, pressure) < ordinary);
        var otherRoster = TeamCatalog.Get("bloodline-bastion").Offense;
        Assert.NotEqual(OffensiveCallScoring.Weight(screen, context, roster), OffensiveCallScoring.Weight(screen, context, otherRoster));
    }

    [Fact]
    public void MovingLaneObservationRejectsClosingDefenderButRecognizesRetreat()
    {
        var read = new ReceiverRead(0, new(25, 40), Vector2.Zero, true, false);
        Assert.False(QuarterbackAI.IsOpen(new(25, 25), read, [new DefenderRead(new(29, 40), new(-8, 0))], 25));
        Assert.True(QuarterbackAI.IsOpen(new(25, 25), read, [new DefenderRead(new(26, 40), new(8, 0))], 25));
    }

    [Fact]
    public void HotReadsRespectAuthoredGateAndBlockedContactDoesNotForceEscape()
    {
        var read = new ReceiverRead(0, new(40, 29), Vector2.Zero, true, false, -10, Developed: false);
        var view = new QuarterbackObservation(new(25, 25), 30, true, [read], [new(27, 25)], [new(new(27, 25), Vector2.Zero, true)]);
        var qb = new QuarterbackAI(); var early = qb.Decide(view, .3f);
        Assert.Null(early.ReceiverIndex); Assert.False(early.Sprint);
        Assert.Equal(0, qb.Decide(view with { Reads = [read with { Developed = true }] }, .3f).ReceiverIndex);
        qb.Reset(); Assert.True(qb.Decide(view with { Position = new(40, 25), Movement = [new(new(42, 25), Vector2.Zero)] }, 4.1f).ThrowAway);
    }

    [Fact]
    public void LegacyBrowserSelectionSurvivesNextFrameAndEscapeClosesWithoutPause()
    {
        var input = new ScriptedInput(); using var game = new GameSession(input, new(101), new PlayerRecordStore(Settings()));
        game.StartScenario(new(50), "pass.mesh", TeamCatalog.Get("ballers").Offense, TeamCatalog.ForStage(SeasonStage.RegularSeason).Defense);
        input.Frame = new(Browser: new(Toggle: true), Space: true); game.Update(0);
        Assert.True(game.LegacyBrowser.IsOpen); Assert.Equal(GameState.PreSnap, game.CaptureSnapshot().State);
        input.Frame = new(Browser: new(Jump: 1, Flip: true)); game.Update(0);
        string id = game.LegacyBrowser.Preview!.Id;
        input.Frame = new(Browser: new(Select: true), Space: true, Enter: true); game.Update(0);
        Assert.Equal(id, game.CaptureSnapshot().PlayId); Assert.Equal(GameState.PreSnap, game.CaptureSnapshot().State);
        input.Frame = new(); game.Update(0); Assert.Equal(id, game.CaptureSnapshot().PlayId);
        input.Frame = new(Browser: new(Toggle: true)); game.Update(0);
        input.Frame = new(Browser: new(Close: true), Escape: true); game.Update(0);
        Assert.False(game.LegacyBrowser.IsOpen); Assert.False(game.CaptureSnapshot().Paused);
        input.Frame = new(Space: true); game.Update(0); Assert.Equal(GameState.PlayActive, game.CaptureSnapshot().State);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void FilteringOrFavoritingTheSamePreviewPreservesItsFlipAndResolvedIdentity(int operation)
    {
        var s = Session(); s.Browser.Open();
        s.Browser.Favorites.Toggle(s.Browser.Preview!.Id);
        s.Browser.Update(new(Flip: true)); var preview = s.Browser.Preview!;
        var active = s.Drive.Plays.SelectedPlay;
        s.Browser.Update(operation switch { 0 => new(Family: true), 1 => new(Favorites: true),
            2 => new(Reset: true), _ => new(Star: true) });
        Assert.Same(preview, s.Browser.Preview); Assert.True(s.Browser.Preview!.IsFlipped != active.IsFlipped);
        Assert.Same(active, s.Drive.Plays.SelectedPlay);
    }

    [Fact]
    public void RestartClearsBothUsageOwnersAndStrategyWhileRetainingFavorites()
    {
        var s = Session(); string id = s.Drive.Plays.SelectedPlay.Id;
        s.Browser.Favorites.Toggle(id); s.Drive.Snap();
        Assert.Equal(1, s.Drive.Plays.GetCallCount(id)); Assert.Equal(1, s.Match.User.GetCallCount(id));
        s.Match.User.OffenseStrategy.ObserveCompleted(1, 1, 1); s.Restart();
        Assert.Equal(0, s.Drive.Plays.GetCallCount(id)); Assert.Equal(0, s.Match.User.GetCallCount(id));
        Assert.Empty(s.Drive.Plays.RecommendationMemory.Recent); Assert.Equal(0, s.Match.User.OffenseStrategy.ObservedPlays);
        Assert.Empty(s.Match.History); Assert.False(s.Drive.Live); Assert.True(s.Browser.Favorites.Contains(id));
    }

    [Fact]
    public void ReplayEdgesCannotAlsoSnapAndPausedPlaybackCannotBeSkipped()
    {
        var user = TeamCatalog.Get("ballers"); var opponent = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        var s = new FullMatchSession(new ScriptedInput(), 101, new(user, opponent, user.Id, start: new(99, 1, 1)),
            favorites: new(PlaybookBuilder.BuildCatalog(), Settings()));
        s.Drive.Snap(); s.Drive.Actors.Qb.Position = new(25, 111); s.Drive.Update(.01f);
        var clip = s.Drive.LastReplay!; Assert.NotNull(clip); s.Update(0, new(Ready: true));
        var clock = s.Clock.Snapshot();
        s.Update(0, new(Replay: true, Ready: true)); Assert.Same(clip, s.Replay.Clip); Assert.False(s.Drive.Live);
        s.Update(0, new(Pause: true)); s.Update(1, new(Ready: true)); Assert.Same(clip, s.Replay.Clip);
        s.Update(0, new(Pause: true)); s.Update(0, new(Replay: true, Ready: true));
        Assert.False(s.Replay.IsPlaying); Assert.False(s.Drive.Live); Assert.Single(s.Match.History);
        Assert.Equal(clock, s.Clock.Snapshot());
    }

    [Fact]
    public void UntimedOvertimeDoesNotApplyZeroClockUrgencyToCallsOrRecommendations()
    {
        var context = new OffensiveCallContext(new(2, 7, 85, 92), ScoreMargin: -7, Timeouts: 0);
        var overtime = context with { LateGame = true, Overtime = true, SecondsRemaining = 0 };
        var roster = TeamCatalog.Get("crimson-rush").Offense;
        var ordinary = new OffensiveCoordinator(new(417)); var ot = new OffensiveCoordinator(new(417));
        foreach (var definition in PlaybookBuilder.BuildCatalog().Plays)
        {
            var play = PlayResolver.Resolve(definition);
            Assert.Equal(OffensiveCallScoring.Weight(play, context, roster), OffensiveCallScoring.Weight(play, overtime, roster));
        }
        for (int i = 0; i < 100; i++)
            Assert.Equal(ordinary.Select(context, new(.6f), roster), ot.Select(overtime, new(.6f), roster));
    }
}
