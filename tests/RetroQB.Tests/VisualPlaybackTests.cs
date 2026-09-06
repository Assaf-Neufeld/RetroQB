using System.Numerics;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Gameplay.Replay;
using RetroQB.Rendering;

namespace RetroQB.Tests;

public sealed class VisualPlaybackTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyContactTacklesTriggerTheFallPose(bool outOfBounds)
    {
        var qb = new Quarterback(new Vector2(outOfBounds ? -1 : 25, 40));
        var ball = new Ball(qb.Position);
        ball.SetHeld(qb, BallState.HeldByQB);
        var defender = new Defender(qb.Position + Vector2.UnitX, DefensivePosition.DB, DefenderSlot.CB1);
        var controller = new TackleController(new Random(1), new OverlapResolver());
        var result = controller.CheckTackleOrScore(ball, qb, [defender], OffensiveTeamAttributes.Default, _ => { });
        Assert.Equal(TackleCheckResult.Tackle, result);
        Assert.Equal(outOfBounds ? PlayerPose.Normal : PlayerPose.Tackled, qb.Animation.Frame.Pose);
        Assert.Equal(outOfBounds ? PlayerPose.Normal : PlayerPose.Tackled, defender.Animation.Frame.Pose);
        Assert.Same(qb, ball.Holder);
    }

    [Fact]
    public void ThrowFollowThroughDoesNotChangePhysicsAndFacingPersistsWhenStopped()
    {
        var qb = new Quarterback(new Vector2(25, 40)) { Velocity = new Vector2(2, 0) };
        qb.Animation.Trigger(PlayerPose.Throwing, -Vector2.UnitX);
        qb.Animation.Update(0.15f, qb.Velocity);
        Assert.Equal(-Vector2.UnitX, qb.Animation.Frame.Facing);
        qb.Animation.Update(0.4f, Vector2.Zero);
        Assert.Equal(PlayerPose.Normal, qb.Animation.Frame.Pose);
        Assert.Equal(-Vector2.UnitX, qb.Animation.Frame.Facing);
        Assert.Equal(new Vector2(25, 40), qb.Position);
        Assert.Equal(new Vector2(2, 0), qb.Velocity);
    }

    [Fact]
    public void ReplayPreservesOriginalDownAndPoseAfterLivePlayerChanges()
    {
        var qb = new Quarterback(new Vector2(25, 40));
        var ball = new Ball(qb.Position);
        var recorder = new ReplayRecorder();
        qb.Animation.Trigger(PlayerPose.Throwing, Vector2.UnitY);
        qb.Animation.Update(0.1f, Vector2.Zero);
        var recorded = qb.Animation.Frame;
        recorder.Begin(1);
        recorder.Capture(qb, ball, [], [], [], 40, 50, 0.1f, 3);
        qb.Animation.Update(1f, Vector2.Zero);
        var clip = recorder.FinalizeClip(PlayOutcome.Incomplete)!;
        Assert.Single(clip.Frames);
        Assert.Equal(3, clip.Frames[0].Down);
        Assert.Equal(recorded, clip.Frames[0].Quarterback.Visual);
    }

    [Fact]
    public void ContactReplaySettlesWithoutMovingTheSpotOrMutatingLiveState()
    {
        var qb = new Quarterback(new Vector2(25, 40));
        var ball = new Ball(qb.Position);
        ball.SetHeld(qb, BallState.HeldByQB);
        qb.Animation.Trigger(PlayerPose.Tackled, Vector2.UnitX);
        var recorder = new ReplayRecorder();
        recorder.Begin(1);
        recorder.Capture(qb, ball, [], [], [], 38, 48, 0.1f, 2);
        var clip = recorder.FinalizeClip(PlayOutcome.Tackle)!;
        Assert.True(clip.Frames[^1].Quarterback.Visual.ActionTime >= 0.3f);
        Assert.All(clip.Frames, frame =>
        {
            Assert.Equal(qb.Position, frame.Quarterback.Position);
            Assert.Equal(BallState.HeldByQB, frame.Ball.State);
            Assert.Equal(2, frame.Down);
            Assert.Equal(48f, frame.FirstDownLine);
        });
        Assert.Equal(0f, qb.Animation.Frame.ActionTime);
    }

    [Fact]
    public void CrowdWaveReachesNearSectionsFirstAndThenSubsides()
    {
        var state = new CrowdBackdropState(0.5f, 0.3f, 0.4f, "", 0, 0, 0,
            ReactionAge: 0.2f, ReactionStrength: 1f, ReactionFieldY: 0.125f);
        Assert.True(CrowdReaction.GetSectionPulse(state, 0.1f, false) > 0f);
        Assert.Equal(0f, CrowdReaction.GetSectionPulse(state, 0.9f, false));
        Assert.True(CrowdReaction.GetSectionPulse(state with { ReactionAge = 1.3f }, 0.9f, false) > 0f);
        Assert.Equal(0f, CrowdReaction.GetSectionPulse(state with { ReactionAge = 3f }, 0.9f, false));
    }
}
