using System.Numerics;
using RetroQB.Development;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class DefensivePlaySessionTests
{
    [Fact]
    public void TIME01_BrowsingCannotChangeCpuActorsCallOrDeadline()
    {
        var drive = new DefensiveDrive(new ScriptedInput(), 101);
        var session = new DefensivePlaySession(drive, 101);
        string offense = drive.Plays.SelectedPlay.Id;
        var qb = drive.Actors.Qb; var receiver = drive.Actors.Receivers[0];
        double deadline = session.SnapRemaining;
        for (int i = 0; i < 100 && !drive.Live; i++)
        {
            session.Update(.1f, new(Call: i % 10));
            Assert.Equal(offense, drive.Plays.SelectedPlay.Id);
            Assert.Same(qb, drive.Actors.Qb); Assert.Same(receiver, drive.Actors.Receivers[0]);
        }
        Assert.True(drive.Live);
        Assert.InRange(deadline, 5, 8);
        Assert.Equal(0, session.SnapRemaining);
        Assert.Equal(1, drive.Match.Opponent.GetCallCount(offense));
        Assert.Equal(1, drive.Match.User.GetCallCount(drive.SelectedDefense.Definition.Id));
        Assert.False(drive.SelectDefense("def.cover3"));
    }

    [Fact]
    public void TIME01_ReadyOnlyShortensAndPausePreservesExactCadence()
    {
        var session = new DefensivePlaySession(new(new ScriptedInput(), 17), 17);
        session.Update(.1f, new(Ready: true));
        double remaining = session.SnapRemaining;
        session.Update(0, new(Ready: true)); Assert.Equal(remaining, session.SnapRemaining);
        session.Update(0, new(Pause: true));
        var paused = session.Clock.Snapshot(); session.Update(100);
        Assert.Equal(paused, session.Clock.Snapshot()); Assert.Equal(remaining, session.SnapRemaining);
        session.Update(0, new(Pause: true)); session.Update(.3f);
        Assert.True(session.Drive.Live);
    }

    [Fact]
    public void TIME02_DelayRebuildsSameOffenseWithoutLosingDownOrRunningClock()
    {
        var drive = new DefensiveDrive(new ScriptedInput(), 101, new(4));
        string call = drive.Plays.SelectedPlay.Id;
        drive.AdvancePreSnap(20);
        Assert.Equal(new DriveStart(2, 1, 12), drive.Match.Series);
        Assert.Equal(call, drive.Plays.SelectedPlay.Id);
        drive.AdvancePreSnap(20);
        Assert.Equal(new DriveStart(1, 1, 13), drive.Match.Series);
        Assert.Equal(180, drive.Timed.Clock.RemainingSeconds);
        Assert.Equal(20, drive.Timed.Clock.PlaySeconds);
    }

    [Fact]
    public void TIME03_TimeoutAndFocusLossCannotResetOtherTimers()
    {
        var session = new DefensivePlaySession(new(new ScriptedInput(), 101), 101);
        session.Update(.5f); double deadline = session.SnapRemaining;
        session.Update(0, new(Timeout: true)); session.Update(0, new(Timeout: true));
        Assert.Equal(2, session.Clock.Timeouts(session.Drive.Match.User.Definition.Id));
        Assert.Equal(deadline, session.SnapRemaining);
        session.Update(0, new(Focused: false)); var clock = session.Clock.Snapshot();
        session.Update(100, new(Focused: false, Call: 8));
        Assert.Equal(clock, session.Clock.Snapshot());
        Assert.Equal(deadline, session.SnapRemaining);
        session.Update(0, new(Focused: true));
        Assert.Equal(180, session.Clock.RemainingSeconds);
        Assert.Equal(ClockStopReason.Timeout, session.Clock.StopReason);
    }

    [Fact]
    public void TIME04_ResultAutoContinuesAndReplayNeverAdvancesMatch()
    {
        var input = new ScriptedInput();
        var drive = new DefensiveDrive(input, 101, fixedCall: "run.hb-dive");
        var session = new DefensivePlaySession(drive, 101);
        session.Update(0, new(Ready: true));
        for (int i = 0; i < 1800 && drive.LastResult == null; i++)
        {
            var delta = (drive.Actors.Ball.Holder?.Position ?? drive.Actors.Ball.Position) - drive.Linebacker.Position;
            input.Frame = new(Movement: delta.LengthSquared() > .01 ? Vector2.Normalize(delta) : Vector2.Zero);
            session.Update(1f / 60);
        }
        Assert.NotNull(drive.LastResult); Assert.False(drive.Complete); Assert.NotNull(drive.LastReplay);
        session.Update(0, new(Replay: true));
        var clock = session.Clock.Snapshot(); var series = drive.Match.Series; var actors = drive.Players.Select(a => a.Position).ToArray();
        session.Update(.1f);
        Assert.Equal(clock, session.Clock.Snapshot()); Assert.Equal(series, drive.Match.Series);
        Assert.Equal(actors, drive.Players.Select(a => a.Position));
        session.Update(0, new(Replay: true));
        session.Update(1.3f);
        Assert.Null(drive.LastResult); Assert.Equal(ClockPhase.PreSnap, session.Clock.Phase);
        var replayCall = drive.LastReplayDefense;
        int replayIndex = drive.LastReplayControlledIndex;
        session.Update(0, new(Call: 9));
        Assert.Same(replayCall, drive.LastReplayDefense);
        Assert.Equal(replayIndex, drive.LastReplayControlledIndex);
        Assert.NotEqual(drive.SelectedDefense, drive.LastReplayDefense);
        var time = session.Clock.RemainingSeconds;
        session.Update(.1f);
        Assert.True(session.Clock.RemainingSeconds < time);
    }

    [Fact]
    public void PeriodExpiryWinsOverReadyInputAndRepeatedDelaysStopRunningTime()
    {
        var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: "pass.mesh", quarterSeconds: 21);
        drive.Snap();
        drive.Actors.Qb.Position = new(25, 34); drive.Linebacker.Position = new(25, 35.8f);
        drive.Update(.02f);
        Assert.NotNull(drive.LastResult); Assert.True(drive.Continue());
        drive.AdvancePreSnap(20);
        double remaining = drive.Timed.Clock.RemainingSeconds;
        Assert.InRange(remaining, .9, 1);
        drive.AdvancePreSnap(20);
        Assert.Equal(remaining, drive.Timed.Clock.RemainingSeconds);
        Assert.Equal(ClockStopReason.DelayOfGame, drive.Timed.Clock.StopReason);
    }

    [Fact]
    public void ExpiryTickCannotSnapInNewQuarter()
    {
        var drive = new DefensiveDrive(new ScriptedInput(), 101, fixedCall: "pass.mesh", quarterSeconds: .2);
        drive.Snap();
        drive.Actors.Qb.Position = new(25, 34); drive.Linebacker.Position = new(25, 35.8f);
        drive.Update(.02f); Assert.True(drive.Continue());
        var session = new DefensivePlaySession(drive, 101);
        session.Update(.4f, new(Ready: true));
        Assert.Equal(2, session.Clock.Quarter);
        Assert.False(drive.Live);
        Assert.Equal(.2, session.Clock.RemainingSeconds);
    }
}
