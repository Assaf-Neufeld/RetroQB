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
    public PlayContact? LastTerminal { get; private set; }
    public ReceiverSlot? ThrowTargetSlot { get; private set; }

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
        LastTerminal = null;
        ThrowTargetSlot = null;
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
        Ball ball, Quarterback qb, IReadOnlyList<Receiver> receivers, IReadOnlyList<Defender> defenders,
        OffensiveTeamAttributes offensiveTeam, DefensiveTeamAttributes defensiveTeam, int selectedReceiverIndex, float dt)
    {
        LastTerminal = null;
        var result = UpdateCore(ball, qb, receivers, defenders, offensiveTeam, defensiveTeam, selectedReceiverIndex, dt);
        if (result == BallUpdateResult.Incomplete)
            LastTerminal = new(PlayEndReason.Incomplete, ball.Position);
        return result;
    }

    private BallUpdateResult UpdateCore(
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

            // Resolve defensive contact even when no receiver can catch the pass.
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
            float distToLanding = Vector2.Distance(defender.Position, landing);
            float pathDistance = DistanceToSegment(defender.Position, ball.ThrowStart, landing);

            // Being near the predicted landing point is not contact with the ball.
            if (distToBall > Constants.PassDefendBallRadius)
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
        if (_rng.NextDouble() >= chance) return BallUpdateResult.Continue;
        LastTerminal = new(PlayEndReason.PassDefended, ball.Position, bestDefender.Slot);
        return BallUpdateResult.PassDefended;
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
        Receiver? receiver = GetCatchCandidateOrder(receivers, selectedReceiverIndex)
            .FirstOrDefault(candidate => candidate.Eligible &&
                Vector2.Distance(candidate.Position, ball.Position) <=
                Constants.CatchRadius * offensiveTeam.GetReceiverCatchRadiusMultiplier(candidate.Slot));
        float receiverDist = receiver == null
            ? float.MaxValue
            : Vector2.Distance(receiver.Position, ball.Position);
        float closestDefenderDist = float.MaxValue;
        float bestInterceptionChance = 0f;
        Defender? interceptingDefender = null;

        foreach (var defender in defenders)
        {
            float distance = Vector2.Distance(defender.Position, ball.Position);
            closestDefenderDist = MathF.Min(closestDefenderDist, distance);
            float interceptRadius = defensiveTeam.GetEffectiveInterceptRadius(defender.PositionRole)
                * defender.InterceptionMultiplier;

            if (distance <= interceptRadius && distance < receiverDist)
            {
                float proximity = 1f - Math.Clamp(distance / MathF.Max(interceptRadius, 0.001f), 0f, 1f);
                float skill = defensiveTeam.InterceptionAbility
                    * defensiveTeam.GetPositionInterceptionMultiplier(defender.PositionRole)
                    * defender.InterceptionMultiplier;
                float chance = Math.Clamp((0.20f + 0.50f * proximity) * Math.Clamp(skill, 0.5f, 1.3f), 0.10f, 0.85f);
                if (chance > bestInterceptionChance)
                {
                    bestInterceptionChance = chance;
                    interceptingDefender = defender;
                }
            }
        }

        if (bestInterceptionChance > 0f)
        {
            // Resolve possession once for this contact. An unsecured ball is a
            // breakup and ends the play, preventing repeated interception rolls.
            bool secured = _rng.NextDouble() < bestInterceptionChance;
            LastTerminal = new(secured ? PlayEndReason.Interception : PlayEndReason.PassDefended,
                ball.Position, interceptingDefender!.Slot);
            return secured ? BallUpdateResult.Intercepted : BallUpdateResult.PassDefended;
        }

        BallUpdateResult defendedResult = TryDefendPass(ball, defenders, defensiveTeam);
        if (defendedResult != BallUpdateResult.Continue)
        {
            return defendedResult;
        }

        if (receiver == null)
        {
            return BallUpdateResult.Continue;
        }

        if (closestDefenderDist <= Constants.ContestedCatchRadius)
        {
            float dropRate = 1.0f - offensiveTeam.GetReceiverCatchingAbility(receiver.Slot);
            if (_rng.NextDouble() < dropRate)
            {
                return BallUpdateResult.Incomplete;
            }
        }

        receiver.HasBall = true;
        receiver.Animation.Trigger(PlayerPose.Catching, ball.Position - receiver.Position);
        ball.SetHeld(receiver, BallState.HeldByReceiver);
        if (_passAttemptedThisPlay && !_passCompletedThisPlay)
        {
            _passCompletedThisPlay = true;
            _passCatcher = receiver;
            _statsTracker.RecordCompletion(receiver.Slot);
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
        TryThrow(receiverIndex, ball, qb, receivers, defenders, playManager, offensiveTeam, true);
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
            ThrowTargetSlot = receiver.Slot;
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
        qb.Animation.Trigger(PlayerPose.Throwing, throwVelocity);
    }

    /// <summary>Shared physical throw command for human and CPU callers; no completion shortcuts.</summary>
    public bool TryThrow(int receiverIndex, Ball ball, Quarterback qb, IReadOnlyList<Receiver> receivers,
        IReadOnlyList<Defender> defenders, PlayManager play, OffensiveTeamAttributes offense, bool allowsThrow)
    {
        if (!allowsThrow || ball.State != BallState.HeldByQB || qb.Position.Y > play.LineOfScrimmage + .1f
            || receiverIndex < 0 || receiverIndex >= receivers.Count || !receivers[receiverIndex].Eligible
            || receivers[receiverIndex].IsBlocking || !_priorityManager.HasPriority(receiverIndex)) return false;
        play.SelectedReceiver = receiverIndex;
        ExecuteThrow(ball, qb, receivers, defenders, play, offense, receiverIndex);
        return ball.State == BallState.InAir;
    }

    public bool TryThrowAway(Ball ball, Quarterback qb, PlayManager play, OffensiveTeamAttributes offense, bool allowsThrow)
    {
        if (!allowsThrow || ball.State != BallState.HeldByQB || qb.Position.Y > play.LineOfScrimmage
            || MathF.Abs(qb.Position.X - Constants.FieldWidth / 2) <= 9) return false;
        var target = new Vector2(qb.Position.X < Constants.FieldWidth / 2 ? -4 : Constants.FieldWidth + 4,
            play.LineOfScrimmage + 5);
        var velocity = _throwingMechanics.CalculateThrowVelocity(qb.Position, qb.Velocity, target,
            Constants.BallMaxSpeed, 0, offense, _rng);
        float distance = Vector2.Distance(qb.Position, target);
        if (!_passAttemptedThisPlay) { _passAttemptedThisPlay = true; _statsTracker.RecordPassAttempt(); }
        _passDefenseAttemptedThisThrow = false;
        ball.SetInAir(qb.Position, velocity, distance, distance + 4, GetPassArcApex(distance));
        qb.Animation.Trigger(PlayerPose.Throwing, velocity);
        return true;
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
