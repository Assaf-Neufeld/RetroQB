using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class MatchClockTests
{
    [Fact]
    public void CLOCK01_CountdownClampsAndEmitsOneExpiryWhilePlayFinishes()
    {
        var clock = new MatchClock("u", "o", 1);
        Assert.True(clock.TrySnap());
        Assert.False(clock.Advance(.5).PeriodExpired);
        Assert.Equal(.5, clock.RemainingSeconds);
        Assert.True(clock.Advance(2).PeriodExpired);
        Assert.Equal(0, clock.RemainingSeconds);
        Assert.Equal(ClockPhase.LivePlay, clock.Phase);
        Assert.True(clock.PendingPeriodEnd);
        Assert.False(clock.Advance(2).PeriodExpired);
        Assert.Equal(1, clock.ExpiryCount);
        clock.EndPlay(false);
        Assert.False(clock.ReadyForNextPlay());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CLOCK03_ResultPresentationFreezesThenUsesCorrectRestart(bool stops)
    {
        var clock = new MatchClock("u", "o", 60);
        clock.Advance(3); // Pre-snap at a stopped clock.
        Assert.Equal(60, clock.RemainingSeconds);
        clock.TrySnap(); clock.Advance(2); clock.EndPlay(stops);
        clock.Advance(10);
        Assert.Equal(58, clock.RemainingSeconds);
        Assert.True(clock.ReadyForNextPlay());
        Assert.False(clock.ReadyForNextPlay());
        clock.Advance(3);
        Assert.Equal(stops ? 58 : 55, clock.RemainingSeconds);
        Assert.Equal(17, clock.PlaySeconds);
    }

    [Fact]
    public void CLOCK06_TimeoutBudgetAndNestedSuspensionRestoreWithoutTimeLoss()
    {
        var clock = new MatchClock("u", "o");
        for (int i = 0; i < 3; i++)
        {
            clock.TrySnap();
            Assert.False(clock.TryTimeout("u"));
            clock.EndPlay(false);
            Assert.True(clock.TryTimeout("u"));
            Assert.False(clock.TryTimeout("u"));
            clock.ReadyForNextPlay();
        }
        Assert.Equal(0, clock.Timeouts("u"));
        clock.TrySnap(); clock.EndPlay(false); clock.ReadyForNextPlay();
        Assert.False(clock.TryTimeout("u"));
        clock.Advance(2);
        var original = clock.Snapshot();
        clock.Suspend(ClockSuspension.Pause); clock.Suspend(ClockSuspension.Replay);
        clock.Advance(100); clock.Resume(ClockSuspension.Pause); clock.Advance(100);
        Assert.False(clock.TrySnap());
        clock.Resume(ClockSuspension.Replay);
        Assert.Equal(original, clock.Snapshot());
        clock.Advance(1);
        Assert.Equal(original.RemainingSeconds - 1, clock.RemainingSeconds);
    }

    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void FramePartitionKeepsEquivalentTimeAndSingleExpiry(int hz)
    {
        var clock = new MatchClock("u", "o", 1);
        clock.TrySnap();
        int expiries = 0;
        for (int i = 0; i < hz + 1; i++) if (clock.Advance(1d / hz).PeriodExpired) expiries++;
        Assert.Equal(0, clock.RemainingSeconds);
        Assert.Equal(1, expiries);
        Assert.Equal(1, clock.ExpiryCount);
    }

    [Fact]
    public void ClockRejectsInvalidTimeBeforeMutation()
    {
        var clock = new MatchClock("u", "o");
        var snapshot = clock.Snapshot();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.PositiveInfinity));
        Assert.Equal(snapshot, clock.Snapshot());
    }
}
