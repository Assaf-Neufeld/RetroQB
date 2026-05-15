using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Routes;

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
    private bool _passDefenseAttemptedThisThrow;
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
        _passDefenseAttemptedThisThrow = false;
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
        int selectedReceiverIndex,
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
            return TryCompleteCatch(ball, receivers, defenders, offensiveTeam, defensiveTeam, selectedReceiverIndex);
        }

        return BallUpdateResult.Continue;
    }

    private BallUpdateResult TryDefendPass(
        Ball ball,
        IReadOnlyList<Defender> defenders,
        DefensiveTeamAttributes defensiveTeam)
    {
        if (_passDefenseAttemptedThisThrow || defenders.Count == 0)
        {
            return BallUpdateResult.Continue;
        }

        float flightProgress = ball.GetFlightProgress();
        float depthFactor = GetPassDepthFactor(ball.IntendedDistance);
        float minProgress = Lerp(0.42f, 0.62f, depthFactor);
        if (flightProgress < minProgress)
        {
            return BallUpdateResult.Continue;
        }

        Vector2 landing = ball.GetPredictedLanding();
        float landingRadius = Lerp(Constants.PassDefendShortLandingRadius, Constants.PassDefendLongLandingRadius, depthFactor);

        Defender? bestDefender = null;
        float bestScore = 0f;
        foreach (var defender in defenders)
        {
            float distToBall = Vector2.Distance(defender.Position, ball.Position);
            if (distToBall > Constants.PassDefendBallRadius)
            {
                continue;
            }

            float distToLanding = Vector2.Distance(defender.Position, landing);
            if (distToLanding > landingRadius)
            {
                continue;
            }

            float pathDistance = DistanceToSegment(defender.Position, ball.ThrowStart, landing);
            if (pathDistance > Constants.PassDefendPathRadius)
            {
                continue;
            }

            float ballScore = 1f - Math.Clamp(distToBall / Constants.PassDefendBallRadius, 0f, 1f);
            float landingScore = 1f - Math.Clamp(distToLanding / landingRadius, 0f, 1f);
            float pathScore = 1f - Math.Clamp(pathDistance / Constants.PassDefendPathRadius, 0f, 1f);
            float score = (ballScore * 0.45f) + (landingScore * 0.35f) + (pathScore * 0.2f);
            if (score > bestScore)
            {
                bestScore = score;
                bestDefender = defender;
            }
        }

        if (bestDefender == null)
        {
            return BallUpdateResult.Continue;
        }

        _passDefenseAttemptedThisThrow = true;
        float chance = GetPassDefendedChance(ball, bestDefender, defensiveTeam, depthFactor, bestScore);
        return _rng.NextDouble() < chance ? BallUpdateResult.PassDefended : BallUpdateResult.Continue;
    }

    private static float GetPassDefendedChance(
        Ball ball,
        Defender defender,
        DefensiveTeamAttributes defensiveTeam,
        float depthFactor,
        float proximityScore)
    {
        float baseChance = Lerp(0.58f, 0.22f, depthFactor);
        float defenderSkill = defensiveTeam.InterceptionAbility
            * defensiveTeam.GetPositionInterceptionMultiplier(defender.PositionRole)
            * defender.InterceptionMultiplier;
        float skillMultiplier = Math.Clamp(defenderSkill, 0.45f, 1.65f);
        float heightFactor = 1f - Math.Clamp(ball.GetArcHeight() / Constants.PassCatchMaxHeight, 0f, 1f);
        float lowBallBoost = Lerp(1.2f, 0.75f, depthFactor) * (0.65f + heightFactor * 0.35f);
        float chance = baseChance * skillMultiplier * lowBallBoost * Math.Clamp(proximityScore, 0.35f, 1f);

        return Math.Clamp(chance, 0.04f, 0.68f);
    }

    private static float GetPassDepthFactor(float intendedDistance)
    {
        float range = Constants.PassArcLongDistance - Constants.PassArcShortDistance;
        if (range <= 0.01f)
        {
            return 0f;
        }

        return Math.Clamp((intendedDistance - Constants.PassArcShortDistance) / range, 0f, 1f);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.001f)
        {
            return Vector2.Distance(point, start);
        }

        float t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        Vector2 projection = start + segment * t;
        return Vector2.Distance(point, projection);
    }

    private static float Lerp(float a, float b, float t)
        => a + (b - a) * Math.Clamp(t, 0f, 1f);

    private BallUpdateResult TryCompleteCatch(
        Ball ball,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Defender> defenders,
        OffensiveTeamAttributes offensiveTeam,
        DefensiveTeamAttributes defensiveTeam,
        int selectedReceiverIndex)
    {
        foreach (var receiver in GetCatchCandidateOrder(receivers, selectedReceiverIndex))
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
                    ? defensiveTeam.GetEffectiveInterceptRadius(closestDefender.PositionRole) * closestDefender.InterceptionMultiplier
                    : 0f;

                if (closestDefender != null && closestDefenderDist <= interceptRadius && closestDefenderDist < receiverDist)
                {
                    return BallUpdateResult.Intercepted;
                }

                BallUpdateResult defendedResult = TryDefendPass(ball, defenders, defensiveTeam);
                if (defendedResult != BallUpdateResult.Continue)
                {
                    return defendedResult;
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

    private static IEnumerable<Receiver> GetCatchCandidateOrder(IReadOnlyList<Receiver> receivers, int selectedReceiverIndex)
    {
        Receiver? selectedReceiver = receivers.FirstOrDefault(r => r.Index == selectedReceiverIndex);
        if (selectedReceiver != null)
        {
            yield return selectedReceiver;
        }

        foreach (var receiver in receivers)
        {
            if (receiver != selectedReceiver)
            {
                yield return receiver;
            }
        }
    }

    /// <summary>
    /// Handles throw input for a given target priority (1-based).
    /// Pass null if no throw key was pressed.
    /// </summary>
    public void HandleThrowInput(
        Ball ball,
        Quarterback qb,
        IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Defender> defenders,
        PlayManager playManager,
        OffensiveTeamAttributes offensiveTeam,
        bool qbPastLos,
        int? throwTarget)
    {
        if (ball.State != BallState.HeldByQB || qbPastLos)
        {
            return;
        }

        if (throwTarget.HasValue)
        {
            TryThrowToPriority(throwTarget.Value + 1, ball, qb, receivers, defenders, playManager, offensiveTeam);
        }
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
            _statsTracker.RecordTarget(receiver.Slot);
        }

        float pressure = GetQbPressureFactor(qb, defenders);
        Vector2 targetVelocityForThrow = GetTargetVelocityForThrow(qb, receiver);
        float qbArmStrength = offensiveTeam.GetQbArmStrengthMultiplier();
        float throwSpeed = Math.Clamp(
            Constants.BallMaxSpeed * qbArmStrength,
            Constants.BallMinSpeed,
            Constants.BallMaxSpeed * Constants.QbArmStrengthMax);

        Vector2 leadTarget = ResolveLeadTarget(qb.Position, receiver, targetVelocityForThrow, throwSpeed);

        Vector2 throwVelocity = _throwingMechanics.CalculateThrowVelocity(
            qb.Position,
            qb.Velocity,
            leadTarget,
            throwSpeed,
            pressure,
            offensiveTeam,
            _rng);
        float intendedDistance = Vector2.Distance(qb.Position, leadTarget);

        float overthrowAllowance = GetOverthrowAllowance(intendedDistance);
        float qbMaxThrowDistance = offensiveTeam.GetQbMaxThrowDistance();
        float maxTravelDistance = MathF.Min(intendedDistance + overthrowAllowance, qbMaxThrowDistance);
        float arcApexHeight = GetPassArcApex(intendedDistance);

        _passDefenseAttemptedThisThrow = false;
        ball.SetInAir(qb.Position, throwVelocity, intendedDistance, maxTravelDistance, arcApexHeight);
    }

    private static Vector2 GetTargetVelocityForThrow(Quarterback qb, Receiver receiver)
    {
        Vector2 targetVelocity = receiver.Velocity;

        // Quick-game throws were often over-led because we treated target velocity
        // the same at all depths. Reduce lead on short throws and fade back to
        // full lead for intermediate/deep passes.
        float distance = Vector2.Distance(qb.Position, receiver.Position);
        const float fullDampingDistance = 5f;
        const float noDampingDistance = 17f;
        float shortThrowFactor = 1f - Math.Clamp((distance - fullDampingDistance) / (noDampingDistance - fullDampingDistance), 0f, 1f);
        float leadMultiplier = 1f - (0.5f * shortThrowFactor);
        targetVelocity *= leadMultiplier;

        // Short RB checkdowns (flat/hitch-like timing throws) were being over-led.
        // Damp lead based on distance so the throw stays catchable in the quick game.
        if (receiver.IsRunningBack && (receiver.Route == RouteType.Flat || receiver.Route == RouteType.OutShallow))
        {
            const float rbFullDampingDistance = 4f;
            const float rbNoDampingDistance = 16f;

            float rbShortThrowFactor = 1f - Math.Clamp((distance - rbFullDampingDistance) / (rbNoDampingDistance - rbFullDampingDistance), 0f, 1f);
            float dampMultiplier = 1f - (0.32f * rbShortThrowFactor);
            targetVelocity *= dampMultiplier;
        }

        return targetVelocity;
    }

    private Vector2 ResolveLeadTarget(Vector2 qbPosition, Receiver receiver, Vector2 targetVelocityForThrow, float throwSpeed)
    {
        Vector2 toReceiver = receiver.Position - qbPosition;
        float leadTime = _throwingMechanics.CalculateInterceptTime(toReceiver, targetVelocityForThrow, throwSpeed);
        leadTime = Math.Clamp(leadTime, 0f, Constants.BallMaxAirTime);

        Vector2 leadTarget = receiver.Position + targetVelocityForThrow * leadTime;
        return ClampLeadTargetToField(leadTarget);
    }

    private static Vector2 ClampLeadTargetToField(Vector2 leadTarget)
    {
        const float sidelinePadding = 0.65f;
        const float verticalPadding = 0.5f;

        return new Vector2(
            Math.Clamp(leadTarget.X, sidelinePadding, Constants.FieldWidth - sidelinePadding),
            Math.Clamp(leadTarget.Y, verticalPadding, Constants.FieldLength - verticalPadding));
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
    PassDefended,
    Intercepted
}
