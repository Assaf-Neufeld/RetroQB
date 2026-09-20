using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class PlaytestFeedbackTests
{
    [Fact]
    public void CpuCallsMorePassesInNormalAndLongYardageSituationsButKeepsShortYardageRuns()
    {
        int Passes(DriveStart series)
        {
            var calls = new OffensiveCoordinator(new Random(417));
            return Enumerable.Range(0, 1000).Count(_ => OffensiveCoordinator.Passes.Contains(calls.Select(series, new(.5f))));
        }
        Assert.InRange(Passes(new(20)), 600, 700);
        Assert.InRange(Passes(new(20, 3, 10)), 800, 900);
        Assert.InRange(Passes(new(20, 3, 1)), 400, 500);
    }

    [Fact]
    public void CpuWaitsForPositiveRouteDepthAndKeepsPassingWindowWhenPressured()
    {
        var ai = new QuarterbackAI();
        var read = new ReceiverRead(0, new(40, 27), Vector2.UnitY, true, false, 2);
        var view = new QuarterbackObservation(new(25, 25), 30, true, [read], [new(27, 25)]);
        var early = ai.Decide(view, .3f);
        Assert.Null(early.ReceiverIndex);
        Assert.True(early.Movement.Y <= 0); // No automatic sprint over LOS.
        Assert.Equal(0, ai.Decide(view with { Reads = [read with { Position = new(40, 36) }] }, .3f).ReceiverIndex);
    }

    [Fact]
    public void DeepReadCanClearUnderneathCoverageButCannotIgnoreDefenderAtCatchPoint()
    {
        var read = new ReceiverRead(0, new(25, 55), Vector2.Zero, true, false);
        Assert.True(QuarterbackAI.IsOpen(new(25, 25), read, [new(25, 38)]));
        Assert.False(QuarterbackAI.IsOpen(new(25, 25), read, [new(25, 54)]));
    }

    [Fact]
    public void DefensiveUpInputMovesUpScreenWhileSimulationRemainsOffenseRelative()
    {
        var input = new ScriptedInput { Frame = new(Movement: Vector2.UnitY) };
        var drive = new DefensiveDrive(input, 101, fixedCall: "run.hb-dive");
        drive.Execution.ScreenRelativeDefensiveInput = true;
        drive.Linebacker.Position = new(5, 70);
        drive.Snap(); drive.Update(1f / 60);
        Assert.True(drive.Linebacker.Velocity.Y < 0);
        Assert.Equal(Vector2.UnitY, Constants.OrientDirection(drive.Linebacker.Velocity, true) / drive.Linebacker.Velocity.Length());
        Assert.Equal(new Vector2(1, -1), Constants.OrientDirection(new(1, 1), true));
        Assert.Equal(new Vector2(1, 1), Constants.OrientDirection(new(1, 1), false));
    }

    [Fact]
    public void FailedCpuDriveKeepsSummaryAndClockUntilAcknowledgedThenStartsUserPossession()
    {
        var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        var s = new FullMatchSession(new ScriptedInput(), 101, new(user, cpu, cpu.Id));
        for (int down = 1; down <= 4; down++)
        {
            Assert.True(s.Drive.Snap());
            s.Drive.ResolveSpecial(PlayEndReason.Incomplete, 20);
            if (down < 4) s.Update(1.3f);
        }
        var summary = CompletedDriveSummary.From(s)!;
        Assert.Equal("TURNOVER ON DOWNS", summary.Outcome);
        Assert.Equal(cpu.Name, summary.TeamName);
        Assert.Equal(4, summary.Plays); Assert.Equal(0, summary.Yards); Assert.Equal(0, summary.Points);
        Assert.Contains(user.Name, summary.Next);
        double time = s.Clock.RemainingSeconds;
        s.Update(10);
        Assert.Equal(summary, CompletedDriveSummary.From(s));
        Assert.Equal(cpu.Id, s.Match.PossessionId); Assert.Equal(time, s.Clock.RemainingSeconds);
        s.Update(0, new(Ready: true));
        Assert.Null(CompletedDriveSummary.From(s)); Assert.Equal(user.Id, s.Match.PossessionId);
        Assert.Equal(80, s.Match.Series.OwnYardLine); Assert.Equal(4, s.Match.History.Count);
    }
}
