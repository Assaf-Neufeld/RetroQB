using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay.Stats;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles ball state management, throws, catches, and interceptions.
/// </summary>
public sealed class BallController
{
    private readonly Random _rng;
    private readonly IThrowingMechanics _throwingMechanics;
    private readonly IStatisticsTracker _statsTracker;
    private readonly ReceiverPriorityManager _priorityManager;

    private bool _passAttemptedThisPlay;
    private bool _passCompletedThisPlay;
    private Receiver? _passCatcher;
    private float _playStartLos;

    public bool PassAttemptedThisPlay => _passAttemptedThisPlay;
    public bool PassCompletedThisPlay => _passCompletedThisPlay;
    public Receiver? PassCatcher => _passCatcher;
    public float PlayStartLos => _playStartLos;

    public BallController(
        Random rng,
        IThrowingMechanics throwingMechanics,
        IStatisticsTracker statsTracker,
        ReceiverPriorityManager priorityManager)
    {
        _rng = rng;
        _throwingMechanics = throwingMechanics;
        _statsTracker = statsTracker;
        _priorityManager = priorityManager;
    }

    /// <summary>
    /// Resets ball state for a new play.
    /// </summary>
    public void Reset(float lineOfScrimmage)
    {
        _passAttemptedThisPlay = false;
        _passCompletedThisPlay = false;
        _passCatcher = null;
        _playStartLos = lineOfScrimmage;
    }

    /// <summary>
    /// Updates ball position and state. Returns a result indicating if play should end.
    /// </summary>
    public BallUpdateResult Update(
        Ball ball,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Defender> defenders,
        OffensiveTeamAttributes offensiveTeam,
        DefensiveTeamAttributes defensiveTeam,
        float dt)
    {
        if (ball.State == BallState.HeldByQB)
        {
            ball.SetHeld(qb, BallState.HeldByQB);
            return BallUpdateResult.Continue;
        }

        if (ball.State == BallState.HeldByReceiver)
        {
            ball.Update(dt);
            return BallUpdateResult.Continue;
        }

        if (ball.State == BallState.InAir)
        {
            ball.Update(dt);

            // Check if ball is out of bounds or exceeded max air time
            if (ball.AirTime > Constants.BallMaxAirTime || !Rules.IsInBounds(ball.Position))
            {
                return BallUpdateResult.Incomplete;
            }

            // Check if ball exceeded max travel distance
            if (ball.MaxTravelDistance > 0f && ball.GetTravelDistance() > ball.MaxTravelDistance)
            {
                return BallUpdateResult.Incomplete;
            }

            // Check if ball is too high to catch
            float ballHeight = ball.GetArcHeight();
            if (ballHeight > Constants.PassCatchMaxHeight)
            {
                return BallUpdateResult.Continue;
            }

            // Check for catches
            return TryCompleteCatch(ball, receivers, defenders, offensiveTeam, defensiveTeam);
        }

        return BallUpdateResult.Continue;
    }

    private BallUpdateResult TryCompleteCatch(
        Ball ball,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Defender> defenders,
        OffensiveTeamAttributes offensiveTeam,
        DefensiveTeamAttributes defensiveTeam)
    {
        foreach (var receiver in receivers)
        {
            if (!receiver.Eligible) continue;

            float catchRadius = Constants.CatchRadius * offensiveTeam.GetReceiverCatchRadiusMultiplier(receiver.Slot);
            float receiverDist = Vector2.Distance(receiver.Position, ball.Position);

            if (receiverDist <= catchRadius)
            {
                // Find closest defender to the ball
                Defender? closestDefender = null;
                float closestDefenderDist = float.MaxValue;
                foreach (var d in defenders)
                {
                    float dist = Vector2.Distance(d.Position, ball.Position);
                    if (dist < closestDefenderDist)
                    {
                        closestDefenderDist = dist;
                        closestDefender = d;
                    }
                }

                // Check for interception
                float interceptRadius = closestDefender != null
                    ? defensiveTeam.GetEffectiveInterceptRadius(closestDefender.PositionRole)
                    : 0f;

                if (closestDefender != null && closestDefenderDist <= interceptRadius && closestDefenderDist < receiverDist)
                {
                    return BallUpdateResult.Intercepted;
                }

                // Check for contested catch drop
                if (closestDefenderDist <= Constants.ContestedCatchRadius)
                {
                    float dropRate = 1.0f - offensiveTeam.GetReceiverCatchingAbility(receiver.Slot);
                    if (_rng.NextDouble() < dropRate)
                    {
                        return BallUpdateResult.Incomplete;
                    }
                }

                // Catch successful
                receiver.HasBall = true;
                ball.SetHeld(receiver, BallState.HeldByReceiver);
                if (_passAttemptedThisPlay && !_passCompletedThisPlay)
                {
                    _passCompletedThisPlay = true;
                    _passCatcher = receiver;
                    _statsTracker.RecordCompletion(receiver.Slot);
                }
                return BallUpdateResult.Continue;
            }
        }

        return BallUpdateResult.Continue;
    }

    /// <summary>
    /// Handles keyboard input for throwing to receivers.
    /// </summary>
    public void HandleThrowInput(
        Ball ball,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Defender> defenders,
        PlayManager playManager,
        OffensiveTeamAttributes offensiveTeam,
        bool qbPastLos)
    {
        if (ball.State != BallState.HeldByQB || qbPastLos)
        {
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.One)) TryThrowToPriority(1, ball, qb, receivers, defenders, playManager, offensiveTeam);
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) TryThrowToPriority(2, ball, qb, receivers, defenders, playManager, offensiveTeam);
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) TryThrowToPriority(3, ball, qb, receivers, defenders, playManager, offensiveTeam);
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) TryThrowToPriority(4, ball, qb, receivers, defenders, playManager, offensiveTeam);
        if (Raylib.IsKeyPressed(KeyboardKey.Five)) TryThrowToPriority(5, ball, qb, receivers, defenders, playManager, offensiveTeam);
    }

    private void TryThrowToPriority(
        int priority,
        Ball ball,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Defender> defenders,
        PlayManager playManager,
        OffensiveTeamAttributes offensiveTeam)
    {
        if (!_priorityManager.TryGetReceiverIndexForPriority(priority, out int receiverIndex)) return;
        playManager.SelectedReceiver = receiverIndex;
        ExecuteThrow(ball, qb, receivers, defenders, playManager, offensiveTeam, receiverIndex);
    }

    private void ExecuteThrow(
        Ball ball,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Defender> defenders,
        PlayManager playManager,
        OffensiveTeamAttributes offensiveTeam,
        int receiverIndex)
    {
        if (!_priorityManager.TryGetReceiverIndexForPriority(1, out int fallbackIndex))
        {
            return;
        }

        if (!_priorityManager.HasPriority(receiverIndex))
        {
            receiverIndex = fallbackIndex;
            playManager.SelectedReceiver = receiverIndex;
        }

        if (receiverIndex < 0 || receiverIndex >= receivers.Count) return;
        var receiver = receivers[receiverIndex];
        if (!receiver.Eligible) return;

        if (!_passAttemptedThisPlay)
        {
            _passAttemptedThisPlay = true;
            _statsTracker.RecordPassAttempt();
        }

        float pressure = GetQbPressureFactor(qb, defenders);
        Vector2 throwVelocity = _throwingMechanics.CalculateThrowVelocity(
            qb.Position,
            qb.Velocity,
            receiver.Position,
            receiver.Velocity,
            Constants.BallMaxSpeed,
            pressure,
            offensiveTeam,
            _rng);

        Vector2 toReceiver = receiver.Position - qb.Position;
        float leadTime = _throwingMechanics.CalculateInterceptTime(toReceiver, receiver.Velocity, Constants.BallMaxSpeed);
        leadTime = Math.Clamp(leadTime, 0f, Constants.BallMaxAirTime);
        Vector2 leadTarget = receiver.Position + receiver.Velocity * leadTime;
        float intendedDistance = Vector2.Distance(qb.Position, leadTarget);

        float overthrowAllowance = GetOverthrowAllowance(intendedDistance);
        float maxTravelDistance = intendedDistance + overthrowAllowance;
        float arcApexHeight = GetPassArcApex(intendedDistance);

        ball.SetInAir(qb.Position, throwVelocity, intendedDistance, maxTravelDistance, arcApexHeight);
    }

    private static float GetOverthrowAllowance(float intendedDistance)
    {
        float allowance = intendedDistance * Constants.PassOverthrowFactor;
        return Math.Clamp(allowance, Constants.PassOverthrowMin, Constants.PassOverthrowMax);
    }

    private static float GetPassArcApex(float intendedDistance)
    {
        float t = 0f;
        float range = Constants.PassArcLongDistance - Constants.PassArcShortDistance;
        if (range > 0.01f)
        {
            t = Math.Clamp((intendedDistance - Constants.PassArcShortDistance) / range, 0f, 1f);
        }

        return Constants.PassArcMinHeight + (Constants.PassArcMaxHeight - Constants.PassArcMinHeight) * t;
    }

    private static float GetQbPressureFactor(Quarterback qb, IReadOnlyList<Defender> defenders)
    {
        float closest = float.MaxValue;
        foreach (var defender in defenders)
        {
            if (!defender.IsRusher) continue;
            float dist = Vector2.Distance(defender.Position, qb.Position);
            if (dist < closest)
            {
                closest = dist;
            }
        }

        if (closest == float.MaxValue || closest >= Constants.ThrowPressureMaxDistance)
        {
            return 0f;
        }

        if (closest <= Constants.ThrowPressureMinDistance)
        {
            return 1f;
        }

        float t = 1f - (closest - Constants.ThrowPressureMinDistance) / (Constants.ThrowPressureMaxDistance - Constants.ThrowPressureMinDistance);
        return Math.Clamp(t, 0f, 1f);
    }
}

public enum BallUpdateResult
{
    Continue,
    Incomplete,
    Intercepted
}
