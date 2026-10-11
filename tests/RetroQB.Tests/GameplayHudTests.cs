using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Replay;
using RetroQB.Rendering;

namespace RetroQB.Tests;

public sealed class GameplayHudTests
{
    private static FullMatchSession Session(bool defense = false, bool kickoff = false)
    {
        var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.Get("scarlet-guard");
        return new(new ScriptedInput(), 101, new(user, cpu, defense ? cpu.Id : user.Id,
            quarterSeconds: 120, regulationPeriods: 2, specialTeams: kickoff));
    }

    [Fact]
    public void DefenseHasBothDeadlinesAndLiveOrResultStatesHaveNeither()
    {
        var session = Session(defense: true);
        var before = session.Clock.Snapshot();
        var timers = MatchHudPresentation.Timers(session);
        Assert.True(timers.PlayClock);
        Assert.Equal("CPU SNAP", timers.CpuLabel);
        Assert.InRange(timers.CpuSeconds, 6, 8);
        Assert.Equal(before, session.Clock.Snapshot()); // Presentation never advances time.
        session.Update(0, new(Ready: true));
        Assert.True(session.Drive.Live);
        Assert.False(MatchHudPresentation.Timers(session).PlayClock);
        Assert.Null(MatchHudPresentation.Timers(session).CpuLabel);
        session.Drive.ResolveSpecial(PlayEndReason.Incomplete, 20);
        Assert.False(MatchHudPresentation.Timers(session).PlayClock);
        Assert.Null(MatchHudPresentation.Timers(session).CpuLabel);
        Assert.DoesNotContain("switch defense", FullMatchRenderer.SidebarControls(session, true));
    }

    [Fact]
    public void OffenseHasOnlyPlayClockAndPauseRetainsFrozenValues()
    {
        var session = Session();
        session.Update(3);
        Assert.True(MatchHudPresentation.Timers(session).PlayClock);
        Assert.Null(MatchHudPresentation.Timers(session).CpuLabel);
        session.Update(0, new(Pause: true));
        var before = session.Clock.Snapshot();
        session.Update(5);
        Assert.Equal(before, session.Clock.Snapshot());
        Assert.Equal("PAUSED", MatchHudPresentation.Status(session));
    }

    [Fact]
    public void CpuKickoffShowsCadenceWithoutScrimmageDeadline()
    {
        var cpuKick = Session(kickoff: true);
        var timers = MatchHudPresentation.Timers(cpuKick);
        Assert.False(timers.PlayClock);
        Assert.Equal("CPU KICK", timers.CpuLabel);
        Assert.Equal(6, timers.CpuSeconds);
        Assert.StartsWith("KICKOFF", MatchHudPresentation.Status(cpuKick));
        cpuKick.Update(0, new(Ready: true));
        Assert.Null(MatchHudPresentation.Timers(cpuKick).CpuLabel);
        var humanKick = Session(defense: true, kickoff: true);
        Assert.False(MatchHudPresentation.Timers(humanKick).PlayClock);
        Assert.Null(MatchHudPresentation.Timers(humanKick).CpuLabel);
    }

    [Fact]
    public void ReplayHidesCurrentCadenceAndPlaySelectionControls()
    {
        var session = Session(defense: true);
        session.Replay.Load(new(1, PlayOutcome.Incomplete, 1, 60, [new ReplayFrame { ElapsedSeconds = 0 }]));
        Assert.NotNull(session.Replay.CurrentFrame);
        Assert.False(MatchHudPresentation.Selecting(session));
        Assert.False(MatchHudPresentation.Timers(session).PlayClock);
        Assert.Null(MatchHudPresentation.Timers(session).CpuLabel);
        Assert.StartsWith("REPLAY", MatchHudPresentation.Status(session));
        Assert.DoesNotContain("switch defense", FullMatchRenderer.SidebarControls(session, true));
    }

    [Theory]
    [InlineData(1000, 700, 240)]
    [InlineData(1280, 720, 290)]
    [InlineData(1440, 900, 320)]
    [InlineData(1920, 1080, 320)]
    [InlineData(2518, 1349, 320)]
    public void PanelsReserveMatchingColumnsAndLeaveRoomForField(int width, int height, int expected)
    {
        var layout = new HudLayout(width, height);
        Assert.Equal(expected, layout.PanelWidth);
        Assert.Equal(layout.Left.Width, layout.Right.Width);
        Assert.True(layout.Right.X - (layout.Left.X + layout.Left.Width) >= 500);
        Assert.True(layout.Body >= 14);
        Assert.True(layout.Hint >= 12);
        Assert.Equal(height - 10, layout.Left.Y + layout.Left.Height);
    }
}
