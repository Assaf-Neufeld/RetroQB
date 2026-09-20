using RetroQB.AI;
using RetroQB.Development;
using RetroQB.Gameplay;
using RetroQB.Data;
using RetroQB.Core;

namespace RetroQB.Tests;

public sealed class FullMatchTests
{
    [Fact]
    public void IncompleteBeyondBackBoundaryReturnsToLineOfScrimmage()
    {
        var s = New(); var p = s.Timed.Advance(0, "fixture")!;
        var contact = new RetroQB.Gameplay.Controllers.PlayContact(PlayEndReason.Incomplete, new(25, 122));
        var result = s.Timed.Resolve(contact.ToEvent(p, new(PassAttempt: true)));
        Assert.Equal(20, result.NextSeries!.OwnYardLine); Assert.Equal(0, result.Gain);
    }
    private static FullMatchSession New(DriveStart? start = null, bool cpu = false, double seconds = 180)
    {
        var u = TeamCatalog.Get("ballers"); var o = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        return new(new ScriptedInput(), 101, new(u, o, cpu ? o.Id : u.Id, start: start, quarterSeconds: seconds));
    }
    private static void End(FullMatchSession s, PlayEndReason reason, float spot)
    {
        Assert.NotNull(s.Timed.Advance(0, "fixture"));
        s.Drive.ResolveSpecial(reason, spot);
    }
    private static void Continue(FullMatchSession s) => s.Update(1.3f, new(Ready: true));

    [Fact]
    public void MATCH01_TouchdownStopAndPossessionChangesKeepTeamIdentity()
    {
        var s = New(new(99, 1, 1));
        Assert.True(s.Drive.Snap());
        s.Drive.Actors.Qb.Position = new(25, 111);
        s.Drive.Update(.01f);
        Assert.Equal(7, s.Match.User.Score); Assert.Equal(0, s.Match.Opponent.Score);
        Assert.False(s.HumanOnDefense); Continue(s);
        Assert.True(s.HumanOnDefense); Assert.Equal(new DriveStart(), s.Match.Series);
        Assert.True(s.Drive.Execution.Control.HumanOnDefense);
        for (int i = 0; i < 4; i++) { End(s, PlayEndReason.Incomplete, 20); Continue(s); }
        Assert.False(s.HumanOnDefense); Assert.False(s.Drive.Execution.Control.HumanOnDefense);
        Assert.Equal(80, s.Match.Series.OwnYardLine);
        Assert.Equal(7, s.Match.User.Score); Assert.Equal(5, s.Match.History.Count);
    }

    [Theory]
    [InlineData(30, 30)] [InlineData(59, 1)] [InlineData(60, 20)] [InlineData(90, 20)]
    public void KICK02_PuntUsesNetFortyAndTouchback(float start, float received)
    {
        var s = New(new(start, 4, 5)); Assert.True(s.SelectAction(MatchAction.Punt));
        s.Update(0, new(Ready: true)); s.Update(.8f); Continue(s);
        Assert.True(s.HumanOnDefense); Assert.Equal(received, s.Match.Series.OwnYardLine);
        Assert.Equal(0, s.Match.User.Score + s.Match.Opponent.Score);
    }

    [Fact]
    public void KICK02_PuntRejectedBeforeFourthAndInOvertime()
    {
        var s = New(); Assert.False(s.SelectAction(MatchAction.Punt));
        s.Match.StartPossession(s.Match.User.Definition.Id, new(75, 4, 5)); s.Clock.StartOvertime(true); s.Timed.Continue();
        Assert.False(s.SelectAction(MatchAction.Punt));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void KICK01_UserMeterScoresOnlyKickerAndMissUsesKickSpot(bool good)
    {
        var s = New(new(70, 4, 5)); Assert.True(s.SelectAction(MatchAction.FieldGoal));
        s.Update(0, new(Ready: true)); s.Update(.45f);
        Assert.Equal(KickPhase.Ready, s.Kick!.Phase);
        s.Update(0, new(Ready: true));
        s.Update((good ? .5f : .9f) / .65f); s.Update(0, new(Ready: true));
        s.Update(.5f / (.55f + s.Kick.Power * .25f)); s.Update(0, new(Ready: true)); s.Update(1.6f);
        Assert.Equal(good ? 3 : 0, s.Match.User.Score); Assert.Equal(0, s.Match.Opponent.Score);
        Continue(s); Assert.Equal(good ? 20 : 37, s.Match.Series.OwnYardLine);
        Assert.Single(s.Match.History);
    }

    [Fact]
    public void KICK03_CpuKickIsSeededAndNeverNeedsMeterInput()
    {
        var a = New(new(70, 4, 5), true); var b = New(new(70, 4, 5), true);
        foreach (var s in new[] { a, b })
        {
            Assert.Equal(MatchAction.FieldGoal, s.Action);
            for (int i = 0; i < 700 && s.Drive.LastResult == null; i++) s.Update(1f / 60);
            Assert.NotNull(s.Drive.LastResult); Assert.Equal(0, s.Match.User.Score);
            Assert.True(s.Match.Opponent.Score is 0 or 3);
        }
        Assert.Equal(a.Drive.LastResult, b.Drive.LastResult);
        Assert.True(MatchStrategy.KickProbability(25) > MatchStrategy.KickProbability(55));
    }

    [Theory]
    [InlineData(PlayEndReason.Touchdown, 100)] [InlineData(PlayEndReason.Interception, 40)]
    [InlineData(PlayEndReason.FieldGoalMissed, 60)]
    public void MATCH02_FinalHalfResultResolvesOnceThenHalftimeOverridesPossession(PlayEndReason reason, float spot)
    {
        var s = New(seconds: 1); s.Clock.StartPeriod(2); s.Timed.Continue();
        var p = s.Timed.Advance(0, "fixture")!; s.Timed.Advance(2);
        var result = s.Drive.ResolveSpecial(reason, spot);
        Assert.Equal(3, s.Clock.Quarter); Assert.Equal(s.Match.SecondHalfReceiverId, s.Match.PossessionId);
        Assert.Equal(20, s.Match.Series.OwnYardLine);
        Assert.Equal(result, s.Timed.Resolve(result.Event)); Assert.Single(s.Match.History);
        Continue(s); Assert.True(s.HumanOnDefense); Assert.Equal(20, s.Match.Series.OwnYardLine);
    }

    [Theory]
    [InlineData(PlayEndReason.Touchdown, 100)] [InlineData(PlayEndReason.FieldGoalGood, 60)]
    public void MATCH03_ZeroDoesNotFinishLivePlayAndLastScoreCanWin(PlayEndReason reason, float spot)
    {
        var s = New(seconds: 1); s.Match.Opponent.Score = 2; s.Clock.StartPeriod(4); s.Timed.Continue();
        s.Timed.Advance(0, "fixture"); s.Timed.Advance(5);
        Assert.False(s.Timed.Finished); s.Drive.ResolveSpecial(reason, spot);
        Assert.True(s.Timed.Finished); Assert.Equal(s.Match.User.Definition.Id, s.Timed.WinnerId);
    }

    [Fact]
    public void MATCH03_TwentyOneDoesNotEndRegulation()
    {
        var s = New(); s.Match.User.Score = 21; End(s, PlayEndReason.Kneel, 19);
        Assert.False(s.Timed.Finished);
    }

    [Theory]
    [InlineData(20, 19, 0)] [InlineData(.5f, 20, 2)]
    public void MATCH04_KneelIsLiveConsumesDownAndYardOrSafety(float start, float expected, int defensePoints)
    {
        var s = New(new(start)); Assert.True(s.SelectAction(MatchAction.Kneel));
        s.Update(0, new(Ready: true)); Assert.True(s.Drive.Live); s.Update(.8f);
        Assert.Equal(PlayEndReason.Kneel, s.Drive.LastResult!.Event.Reason);
        Assert.Equal(defensePoints, s.Match.Opponent.Score); Continue(s);
        Assert.Equal(expected, s.Match.Series.OwnYardLine);
        if (defensePoints == 0) { Assert.Equal(2, s.Match.Series.Down); Assert.Equal(ClockStopReason.Running, s.Clock.StopReason); }
    }

    [Theory]
    [InlineData(-3, MatchAction.FieldGoal)] [InlineData(-4, MatchAction.Scrimmage)]
    public void LATE01_CpuTiesWithKickButNeedsTouchdownDownFour(int margin, MatchAction action)
        => Assert.Equal(action, MatchStrategy.Choose(new(4, 25, false, margin, 4, 70, 5, 0, 20, false)));

    [Fact]
    public void LATE02_TrailingCpuDefenseUsesOneTimeoutPerInboundsEvent()
    {
        var s = New(seconds: 30); s.Match.User.Score = 7; s.Clock.StartPeriod(4); s.Timed.Continue();
        End(s, PlayEndReason.Kneel, 19);
        for (int i = 0; i < 30; i++) s.Update(.01f);
        Assert.Equal(2, s.Clock.Timeouts(s.Match.Opponent.Definition.Id));
        Assert.Equal(ClockStopReason.Timeout, s.Clock.StopReason);
    }

    [Theory]
    [InlineData(40, 0, 1, true)] [InlineData(60, 0, 1, false)]
    [InlineData(40, 3, 1, false)] [InlineData(10, 0, 4, false)]
    public void LATE03_KneelOnlyWhenRemainingDownsAndTimeoutsGuaranteeExhaustion(double seconds, int timeouts, int down, bool safe)
        => Assert.Equal(safe, MatchStrategy.CanKneelOut(new(4, seconds, false, 7, down, 20, 10, timeouts, 20, false)));

    [Fact]
    public void OT02_TiedPairReversesOpenerThenDecisivePairFinishesOnce()
    {
        var s = new FullMatchSession(new ScriptedInput(), 101, FullMatchScenarioLauncher.Create("overtime-pairs"));
        string first = s.Match.PossessionId;
        End(s, PlayEndReason.FieldGoalGood, 68); Continue(s);
        Assert.Equal(2, s.Timed.OvertimeAttempt); Assert.NotEqual(first, s.Match.PossessionId);
        End(s, PlayEndReason.FieldGoalGood, 68); Continue(s);
        Assert.Equal(2, s.Timed.OvertimePair); Assert.NotEqual(first, s.Match.PossessionId);
        Assert.Equal(75, s.Match.Series.OwnYardLine); Assert.Equal(1, s.Clock.Timeouts(first));
        End(s, PlayEndReason.Interception, 80); Continue(s);
        Assert.False(s.Timed.Finished); Assert.Equal(first, s.Match.PossessionId);
        End(s, PlayEndReason.FieldGoalGood, 68);
        Assert.True(s.Timed.Finished); Assert.Equal(first, s.Timed.WinnerId);
        var end = s.Drive.LastResult!; s.Timed.Resolve(end.Event); Assert.Equal(6, s.Match.Team(first).Score);
    }

    [Theory]
    [InlineData("timed-short")] [InlineData("late-protect-lead")] [InlineData("overtime-pairs")]
    public void RestartClearsMatchPresentationAndPreservesOpeningReceiver(string scenario)
    {
        var s = new FullMatchSession(new ScriptedInput(), 101, FullMatchScenarioLauncher.Create(scenario));
        string opener = s.Match.OpeningReceiverId; s.Clock.TryTimeout(opener);
        s.Update(0, new(Pause: true)); s.Restart();
        Assert.Equal(opener, s.Match.PossessionId); Assert.Equal(1, s.Clock.Quarter);
        Assert.Equal(ClockSuspension.None, s.Clock.Suspension); Assert.False(s.Clock.IsOvertime);
        Assert.Equal(0, s.Match.User.Score + s.Match.Opponent.Score); Assert.Empty(s.Match.History);
        Assert.Null(s.Drive.LastReplay); Assert.Null(s.Drive.LastResult); Assert.Null(s.Kick);
        Assert.Equal(3, s.Clock.Timeouts(opener)); Assert.Equal(new DriveStart(), s.Match.Series);
    }
}
