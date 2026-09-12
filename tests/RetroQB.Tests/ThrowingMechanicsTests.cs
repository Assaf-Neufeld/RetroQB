using System.Numerics;
using RetroQB.Data;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class ThrowingMechanicsTests
{
    [Fact]
    public void FootworkRewardsStationaryForwardAndAlignedThrows()
    {
        float stationary = Error(Vector2.Zero, Vector2.UnitY);
        float forward = Error(new Vector2(0, 8), Vector2.UnitY);
        float sideways = Error(new Vector2(8, 0), Vector2.UnitY);
        float backward = Error(new Vector2(0, -8), Vector2.UnitY);
        Assert.True(stationary < forward);
        Assert.True(forward < sideways);
        Assert.True(sideways < backward);
        Assert.True(forward < stationary * 1.5f);
        Assert.True(backward > stationary * 5f);
        Assert.True(Error(new Vector2(8, 0), Vector2.UnitX) < sideways);
        Assert.True(Error(new Vector2(8, 0), -Vector2.UnitX) > sideways);
        Assert.True(Error(new Vector2(0, 8), Vector2.UnitX) < sideways);
    }

    [Fact]
    public void BackpedalingPenaltyScalesWithSpeedAndRecoversWhenStopped()
    {
        Assert.True(Error(new Vector2(0, -8), Vector2.UnitY) > Error(new Vector2(0, -4), Vector2.UnitY));
        Assert.True(Error(new Vector2(0, -4), Vector2.UnitY) > Error(new Vector2(0, -1), Vector2.UnitY));
        Assert.Equal(Error(Vector2.Zero, Vector2.UnitY), Error(new Vector2(0, -0.1f), Vector2.UnitY));
    }

    [Fact]
    public void PressureStillReducesAccuracyWithGoodFootwork()
    {
        Assert.True(Error(Vector2.Zero, Vector2.UnitY, 0.8f) > Error(Vector2.Zero, Vector2.UnitY));
    }

    private static float Error(Vector2 velocity, Vector2 direction, float pressure = 0)
    {
        var thrown = new ThrowingMechanics().CalculateThrowVelocity(
            Vector2.Zero, velocity, direction * 20, 32, pressure,
            OffensiveTeamAttributes.Default, new FixedRandom());
        return MathF.Abs(MathF.Atan2(direction.X * thrown.Y - direction.Y * thrown.X,
            Vector2.Dot(direction, thrown)));
    }

    private sealed class FixedRandom : Random
    {
        public override double NextDouble() => 1;
    }
}
