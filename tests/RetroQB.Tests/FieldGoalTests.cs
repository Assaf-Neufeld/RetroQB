using System.Reflection;
using RetroQB.Core;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class FieldGoalTests
{
    [Theory]
    [InlineData(30, 47)]
    [InlineData(43, 60)]
    [InlineData(80, 97)]
    public void DistanceIncludesSnapAndEndZone(float yardsToGoal, float expected)
    {
        Assert.Equal(expected, FieldGoalAttempt.DistanceFrom(FieldGeometry.OpponentGoalLine - yardsToGoal));
    }

    [Fact]
    public void OutOfRangeCannotSnap()
    {
        var kick = new FieldGoalAttempt(FieldGeometry.OpponentGoalLine - 44);
        kick.PressSpace();
        kick.Update(10);
        Assert.Equal(KickPhase.Setup, kick.Phase);
    }

    [Theory]
    [InlineData(25, 0.5f, 0.5f, true, "FIELD GOAL GOOD! +3")]
    [InlineData(47, 0.1f, 0.5f, false, "SHORT")]
    [InlineData(47, 0.5f, 0.9f, false, "WIDE RIGHT")]
    [InlineData(47, 0.5f, 0.1f, false, "WIDE LEFT")]
    [InlineData(60, 0.5f, 0.5f, true, "FIELD GOAL GOOD! +3")]
    public void PowerAndTimingDetermineOutcome(float distance, float power, float accuracy, bool good, string result)
    {
        var kick = CompleteKick(distance, power, accuracy);
        Assert.Equal(good, kick.IsGood);
        Assert.Equal(result, kick.Result);
        Assert.Equal(KickPhase.Result, kick.Phase);
    }

    [Fact]
    public void UnattendedMeterEventuallyMissesInsteadOfWaitingAtPerfectTiming()
    {
        var kick = new FieldGoalAttempt(FieldGeometry.OpponentGoalLine - 30);
        kick.PressSpace();
        kick.Update(FieldGoalAttempt.SnapDuration);
        kick.PressSpace();
        for (int i = 0; i < 600; i++) kick.Update(1f / 60);
        Assert.Equal(KickPhase.Result, kick.Phase);
        Assert.False(kick.IsGood);
        Assert.Equal("SHORT", kick.Result);
    }

    [Fact]
    public void LongerKicksNarrowBothCenteredWindows()
    {
        var easy = CompleteKick(25, 0.5f);
        var hard = CompleteKick(55, 0.5f);
        Assert.Equal(0.5f, FieldGoalAttempt.PowerTarget);
        Assert.Equal(0.5f, FieldGoalAttempt.AccuracyTarget);
        Assert.True(easy.PowerHalfWidth > hard.PowerHalfWidth);
        Assert.True(easy.AccuracyHalfWidth > hard.AccuracyHalfWidth);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(30)]
    [InlineData(47)]
    [InlineData(60)]
    public void BothGreenEdgesSucceedAndOutsideEitherEdgeMisses(float distance)
    {
        var centered = CompleteKick(distance, 0.5f);
        foreach (int side in new[] { -1, 1 })
        {
            var greenPower = CompleteKick(distance, 0.5f + side * centered.PowerHalfWidth);
            Assert.True(greenPower.IsGood);
            var redPower = CompleteKick(distance, 0.5f + side * (centered.PowerHalfWidth + 0.01f));
            Assert.Equal("SHORT", redPower.Result);
            var greenAccuracy = CompleteKick(distance, 0.5f, 0.5f + side * centered.AccuracyHalfWidth);
            Assert.True(greenAccuracy.IsGood);
            var redAccuracy = CompleteKick(distance, 0.5f, 0.5f + side * (centered.AccuracyHalfWidth + 0.01f));
            Assert.Equal(side > 0 ? "WIDE RIGHT" : "WIDE LEFT", redAccuracy.Result);
        }
    }

    [Fact]
    public void PerfectCenterCanMakeEverySupportedDistance()
    {
        for (int distance = 17; distance <= FieldGoalAttempt.MaxDistance; distance++)
            Assert.True(CompleteKick(distance, 0.5f).IsGood, $"Centered kick failed at {distance} yards.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KickScoresOnceAndRecordsSpecialTeamsResult(bool good)
    {
        var drive = new DriveState();
        var kick = CompleteKick(47, 0.5f, good ? 0.5f : 0.9f);
        var result = drive.ResolveFieldGoal(kick);
        Assert.Equal(good ? 3 : 0, drive.Score);
        Assert.Equal(good ? 3 : 7, drive.AwayScore);
        Assert.Contains($"AWAY +{(good ? 3 : 7)}", result.Message);
        Assert.Single(drive.PlayRecords);
        Assert.Equal(PlayType.FieldGoal, drive.PlayRecords[0].PlayFamily);
        Assert.Contains("47-yard", drive.PlayRecords[0].GetPlayCallText());
        Assert.Equal(kick.ScoringResult, drive.PlayRecords[0].GetResultText());
        Assert.Throws<InvalidOperationException>(() => drive.ResolveFieldGoal(kick));
        Assert.Equal(good ? 3 : 0, drive.Score);
        Assert.Equal(good ? 3 : 7, drive.AwayScore);
        drive.Reset();
        Assert.Empty(drive.PlayRecords);
        Assert.Equal(good ? 3 : 0, drive.Score);
        Assert.Equal(good ? 3 : 7, drive.AwayScore);
    }

    [Fact]
    public void UnfinishedKickCannotScore()
    {
        var drive = new DriveState();
        Assert.Throws<InvalidOperationException>(() => drive.ResolveFieldGoal(new FieldGoalAttempt(80)));
        Assert.Equal(0, drive.Score);
        Assert.Equal(0, drive.AwayScore);
    }

    [Theory]
    [InlineData(0, 0, true, GameState.DriveOver, 3, 3)]
    [InlineData(0, 0, false, GameState.DriveOver, 0, 7)]
    [InlineData(18, 0, true, GameState.StageComplete, 21, 21)]
    [InlineData(18, 0, false, GameState.PlayerNameEntry, 18, 25)]
    [InlineData(0, 2, false, GameState.PlayerNameEntry, 0, 21)]
    [InlineData(6, 2, true, GameState.PlayerNameEntry, 9, 23)]
    public void SessionFinishesDriveOrGameOnce(int startingScore, int opponentTouchdowns, bool good, GameState expected, int finalScore, int finalAwayScore)
    {
        using var session = new GameSession();
        var manager = Get<PlayManager>(session, "_playManager");
        var state = Get<GameStateManager>(session, "_stateManager");
        for (int i = 0; i < startingScore / 3; i++) manager.ResolveFieldGoal(CompleteKick(47, 0.5f));
        for (int i = 0; i < opponentTouchdowns; i++) manager.ResolvePlay(80, false, false, true, false);
        Set(session, "_fieldGoal", CompleteKick(47, 0.5f, good ? 0.5f : 0.9f));
        state.SetState(GameState.FieldGoal);
        Invoke(session, "FinishFieldGoal");
        Invoke(session, "FinishFieldGoal");
        Assert.Equal(expected, state.State);
        Assert.Equal(finalScore, manager.Score);
        Assert.Equal(finalAwayScore, manager.AwayScore);
    }

    [Fact]
    public void RestartClearsPendingKickAndPause()
    {
        using var session = new GameSession();
        Invoke(session, "StartSeasonFromMenu");
        Set(session, "_fieldGoal", CompleteKick(47, 0.5f));
        var state = Get<GameStateManager>(session, "_stateManager");
        state.SetState(GameState.FieldGoal);
        state.TogglePause();
        Invoke(session, "HandleRestart");
        Assert.Null(Get<FieldGoalAttempt?>(session, "_fieldGoal"));
        Assert.False(state.IsPaused);
        Assert.Equal(GameState.Pregame, state.State);
    }

    [Fact]
    public void TouchdownsStillAwardSeven()
    {
        var drive = new DriveState();
        drive.ResolveTouchdown();
        Assert.Equal(7, drive.Score);
    }

    private static FieldGoalAttempt CompleteKick(float distance, float power, float accuracy = 0.5f)
    {
        var kick = new FieldGoalAttempt(FieldGeometry.OpponentGoalLine - distance + 17);
        kick.PressSpace();
        kick.Update(FieldGoalAttempt.SnapDuration);
        kick.PressSpace();
        kick.Update(power / 0.65f);
        if (kick.Phase == KickPhase.Power) kick.PressSpace();
        kick.Update((1 - accuracy) / (0.55f + kick.Power * 0.25f));
        kick.PressSpace();
        kick.Update(FieldGoalAttempt.FlightDuration);
        return kick;
    }

    [Fact]
    public void SpaceSnapsBeforeMeterAppearsAndReadyWaitsForFreshPress()
    {
        var kick = new FieldGoalAttempt(80);
        Assert.Equal(kick.SnapStart, kick.SnapBallPosition);
        Assert.False(kick.ShowMeter);
        kick.PressSpace();
        Assert.Equal(KickPhase.Snap, kick.Phase);
        kick.Update(FieldGoalAttempt.SnapDuration / 2);
        Assert.InRange(kick.SnapBallPosition.Y, kick.HolderSpot.Y + 1, kick.SnapStart.Y - 1);
        Assert.False(kick.ShowMeter);
        // Mashing Space during the snap cannot start or skip the timing meter.
        kick.PressSpace();
        Assert.Equal(KickPhase.Snap, kick.Phase);
        kick.Update(10);
        Assert.Equal(KickPhase.Ready, kick.Phase);
        Assert.Equal(kick.HolderSpot, kick.SnapBallPosition);
        Assert.True(kick.ShowMeter);
        kick.Update(10);
        Assert.Equal(0, kick.Marker);
        Assert.Equal(KickPhase.Ready, kick.Phase);
        kick.PressSpace();
        kick.Update(0.25f);
        Assert.Equal(KickPhase.Power, kick.Phase);
        Assert.True(kick.Marker > 0);
        Assert.Equal(kick.HolderSpot, kick.SnapBallPosition);
        Assert.Equal(1, kick.SnapProgress);
    }

    [Fact]
    public void TimingFinishesBeforeFlightAndMeterReturnsForResult()
    {
        var kick = new FieldGoalAttempt(80);
        kick.PressSpace();
        kick.Update(FieldGoalAttempt.SnapDuration);
        kick.PressSpace();
        kick.Update(1);
        kick.PressSpace();
        Assert.True(kick.TimingActive);
        kick.PressSpace();
        Assert.Equal(KickPhase.Flight, kick.Phase);
        Assert.False(kick.TimingActive);
        Assert.False(kick.ShowMeter);
        kick.Update(FieldGoalAttempt.FlightDuration);
        Assert.True(kick.ShowMeter);
    }

    private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(obj)!;
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(obj, value);
    private static void Invoke(object obj, string method) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(obj, null);
}
