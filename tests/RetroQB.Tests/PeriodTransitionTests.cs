using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class PeriodTransitionTests
{
    [Theory]
    [InlineData(PlayEndReason.Tackle, 24, true)]
    [InlineData(PlayEndReason.Tackle, 35, true)]
    [InlineData(PlayEndReason.Incomplete, 50, false)]
    [InlineData(PlayEndReason.PassDefended, 50, false)]
    [InlineData(PlayEndReason.OutOfBounds, 24, false)]
    [InlineData(PlayEndReason.Touchdown, 100, false)]
    [InlineData(PlayEndReason.Interception, 70, false)]
    public void CLOCK03_ResultControlsNextPresnapClock(PlayEndReason reason, float spot, bool running)
    {
        foreach (bool cpu in new[] { false, true })
        {
            var game = Create(cpu);
            End(game, reason, spot);
            game.Advance(100);
            Assert.Equal(10, game.Clock.RemainingSeconds);
            game.Continue(); game.Advance(1);
            Assert.Equal(running ? 9 : 10, game.Clock.RemainingSeconds);
        }
    }

    [Fact]
    public void LargeStepStopsAtDelayBeforeLaterQuarterExpiry()
    {
        var game = Create(seconds: 30);
        End(game, PlayEndReason.Tackle, 24); game.Continue();
        game.Advance(40);
        Assert.Equal(1, game.Clock.Quarter);
        Assert.Equal(10, game.Clock.RemainingSeconds);
        Assert.Equal(new DriveStart(19, 2, 11), game.Match.Series);
    }

    [Fact]
    public void InvalidTimeCannotConsumeTimeout()
    {
        var game = Create(); var before = game.Clock.Snapshot();
        Assert.Throws<ArgumentOutOfRangeException>(() => game.Advance(double.NaN, timeoutTeamId: game.Match.PossessionId));
        Assert.Equal(before, game.Clock.Snapshot());
    }

    internal static TimedMatch Create(bool opponent = false, DriveStart? start = null, double seconds = 10)
    {
        var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        return new(user, cpu, opponent ? cpu.Id : user.Id, start: start, quarterSeconds: seconds);
    }

    internal static PlayResolution End(TimedMatch game, PlayEndReason reason, float spot, bool expire = false)
    {
        var play = game.Advance(0, "test.call")!;
        Assert.NotNull(play);
        if (expire) game.Advance(game.Clock.RemainingSeconds + 1);
        return game.Resolve(new(play.Id, play.OffenseId, reason, spot));
    }

    internal static void ReachOvertime(TimedMatch game)
    {
        for (int i = 0; i < 4; i++)
        {
            End(game, PlayEndReason.Incomplete, game.Match.Series.OwnYardLine, true);
            game.Continue();
        }
        Assert.True(game.Clock.IsOvertime);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CLOCK04_QuarterOneAndThreePreserveSeriesAndIdentity(bool cpu)
    {
        var game = Create(cpu);
        string first = game.Match.PossessionId;
        End(game, PlayEndReason.Tackle, 24, true);
        Assert.Equal(2, game.Clock.Quarter);
        Assert.Equal(first, game.Match.PossessionId);
        Assert.Equal(new DriveStart(24, 2, 6), game.Match.Series);
        game.Continue();
        End(game, PlayEndReason.Incomplete, 24, true);
        game.Continue();
        string second = game.Match.PossessionId;
        End(game, PlayEndReason.Tackle, 25, true);
        Assert.Equal(4, game.Clock.Quarter);
        Assert.Equal(second, game.Match.PossessionId);
        Assert.Equal(new DriveStart(25, 2, 5), game.Match.Series);
    }

    [Theory]
    [InlineData(false, PlayEndReason.Touchdown, 100)]
    [InlineData(true, PlayEndReason.Touchdown, 100)]
    [InlineData(false, PlayEndReason.Interception, 70)]
    [InlineData(true, PlayEndReason.FieldGoalMissed, 63)]
    public void CLOCK04_HalftimeOverridesPendingPossessionAfterScoring(bool cpu, PlayEndReason reason, float spot)
    {
        var game = Create(cpu);
        End(game, PlayEndReason.Touchdown, 100, true);
        game.Continue();
        string receiver = game.Match.SecondHalfReceiverId;
        Assert.Equal(receiver, game.Match.PossessionId);
        Assert.True(game.Clock.TryTimeout(receiver));
        var result = End(game, reason, spot, true);
        Assert.Equal(3, game.Clock.Quarter);
        Assert.Equal(receiver, game.Match.PossessionId);
        Assert.Equal(new DriveStart(), game.Match.Series);
        Assert.Null(game.Match.PendingPossession);
        Assert.Equal(3, game.Clock.Timeouts(receiver));
        int plays = game.Match.History.Count;
        game.Resolve(result.Event);
        Assert.Equal(3, game.Clock.Quarter);
        Assert.Equal(plays, game.Match.History.Count);
    }

    [Theory]
    [InlineData(PlayEndReason.Touchdown, 100)]
    [InlineData(PlayEndReason.FieldGoalGood, 63)]
    [InlineData(PlayEndReason.Safety, -1)]
    public void CLOCK02_FinalLivePlayAtZeroCanDetermineWinner(PlayEndReason reason, float spot)
    {
        var game = Create();
        for (int i = 0; i < 3; i++) { End(game, PlayEndReason.Incomplete, 20, true); game.Continue(); }
        string offense = game.Match.PossessionId;
        var play = game.Advance(0, "final")!;
        game.Advance(20);
        Assert.Equal(ClockPhase.LivePlay, game.Clock.Phase);
        Assert.False(game.Finished);
        game.Resolve(new(play.Id, play.OffenseId, reason, spot));
        Assert.True(game.Finished);
        Assert.Equal(reason == PlayEndReason.Safety ? game.Match.Other(offense).Definition.Id : offense, game.WinnerId);
        Assert.Null(game.Match.PendingPossession);
        Assert.False(game.Continue());
        Assert.Null(game.Advance(1, "too-late"));
    }

    [Fact]
    public void CLOCK05_TwentyOnePointsDoesNotFinishAnEarlyQuarter()
    {
        var game = Create(seconds: 180);
        for (int i = 0; i < 5; i++) { End(game, PlayEndReason.Touchdown, 100); game.Continue(); }
        Assert.Equal(21, game.Match.User.Score);
        Assert.Equal(14, game.Match.Opponent.Score);
        Assert.False(game.Finished);
        Assert.Equal(1, game.Clock.Quarter);
    }

    [Fact]
    public void ExpiryBeforeSnapRejectsSnapRatherThanStartingInNextQuarter()
    {
        var game = Create(seconds: 1);
        End(game, PlayEndReason.Tackle, 21); game.Continue();
        Assert.Null(game.Advance(1, "too-late"));
        Assert.Equal(2, game.Clock.Quarter);
        Assert.Null(game.Match.ActivePlay);
        Assert.Equal(ClockPhase.PeriodBreak, game.Clock.Phase);
    }

    [Fact]
    public void TimeoutInputPrecedesClockButCannotReviveAnExpiredPeriod()
    {
        var game = Create(seconds: 1);
        End(game, PlayEndReason.Tackle, 21); game.Continue();
        var play = game.Advance(1, "saved-snap", game.Match.PossessionId);
        Assert.NotNull(play);
        Assert.Equal(1, game.Clock.RemainingSeconds);
        game.Advance(1);
        Assert.False(game.Clock.TryTimeout(game.Match.PossessionId));
    }

    [Fact]
    public void DelayPenaltyUsesHalfDistanceAndStopsGameClock()
    {
        var game = Create(start: new(4), seconds: 180);
        game.Advance(20);
        Assert.Equal(new DriveStart(2, 1, 12), game.Match.Series);
        Assert.Equal(180, game.Clock.RemainingSeconds);
        game.Advance(20);
        Assert.Equal(new DriveStart(1, 1, 13), game.Match.Series);
        Assert.Equal(180, game.Clock.RemainingSeconds);
    }

    [Fact]
    public void CLOCK07_OvertimePairsAlternateAndCompareOnlyAfterBothAttempts()
    {
        var game = Create(); ReachOvertime(game);
        string opener = game.OvertimeOpenerId!;
        Assert.Equal(1, game.Clock.Timeouts(opener));
        Assert.Equal(new DriveStart(75), game.Match.Series);
        End(game, PlayEndReason.Touchdown, 100); game.Continue();
        Assert.False(game.Finished); Assert.Equal(2, game.OvertimeAttempt);
        End(game, PlayEndReason.Touchdown, 100); game.Continue();
        Assert.Equal(2, game.OvertimePair); Assert.Equal(1, game.OvertimeAttempt);
        Assert.NotEqual(opener, game.OvertimeOpenerId);
        string nextOpener = game.OvertimeOpenerId!;
        End(game, PlayEndReason.FieldGoalGood, 68); game.Continue();
        var final = End(game, PlayEndReason.Interception, 90);
        Assert.True(game.Finished);
        Assert.Equal(nextOpener, game.WinnerId);
        game.Resolve(final.Event);
        Assert.Equal(2, game.OvertimePair);
    }

    [Fact]
    public void OvertimeHasNoGameClockAndInterceptionsStartNextFixedAttempt()
    {
        var game = Create(true); ReachOvertime(game);
        var play = game.Advance(0, "overtime")!;
        game.Advance(1000);
        Assert.False(game.Clock.PendingPeriodEnd);
        Assert.Throws<ArgumentException>(() => game.Resolve(new(play.Id, play.OffenseId, PlayEndReason.Punt, 75)));
        game.Resolve(new(play.Id, play.OffenseId, PlayEndReason.Interception, 82));
        Assert.Equal(new DriveStart(75), game.Match.Series);
        Assert.Equal(2, game.OvertimeAttempt);
    }

    [Fact]
    public void RestartClearsScoresClockTimeoutsOvertimeAndPendingState()
    {
        var game = Create(true); ReachOvertime(game);
        string opening = game.Match.OpeningReceiverId;
        End(game, PlayEndReason.Touchdown, 100); game.Continue();
        game.Clock.TryTimeout(game.Match.PossessionId);
        game.Clock.Suspend(ClockSuspension.Replay);
        game.Restart();
        Assert.Equal(opening, game.Match.PossessionId);
        Assert.Equal(1, game.Clock.Quarter); Assert.Equal(10, game.Clock.RemainingSeconds);
        Assert.Equal(3, game.Clock.Timeouts(opening));
        Assert.Equal(ClockSuspension.None, game.Clock.Suspension);
        Assert.False(game.Clock.IsOvertime); Assert.Equal(0, game.OvertimePair);
        Assert.Empty(game.Match.History); Assert.Equal(0, game.Match.User.Score + game.Match.Opponent.Score);
        Assert.Null(game.WinnerId); Assert.Null(game.Match.PendingPossession);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OvertimeTimeoutsResetPerPairAndPlayClockRemainsActive(bool cpu)
    {
        var game = Create(cpu); ReachOvertime(game);
        string opener = game.Match.PossessionId;
        Assert.True(game.Clock.TryTimeout(opener));
        game.Advance(20);
        Assert.Equal(70, game.Match.Series.OwnYardLine);
        Assert.Equal(0, game.Clock.RemainingSeconds);
        End(game, PlayEndReason.Interception, 90); game.Continue();
        Assert.Equal(0, game.Clock.Timeouts(opener));
        Assert.False(game.Clock.TryTimeout(opener));
        Assert.True(game.Clock.TryTimeout(game.Match.PossessionId));
        End(game, PlayEndReason.Interception, 90); game.Continue();
        Assert.Equal(2, game.OvertimePair);
        Assert.Equal(1, game.Clock.Timeouts(opener));
        Assert.Equal(1, game.Clock.Timeouts(game.Match.Other(opener).Definition.Id));
    }
}


