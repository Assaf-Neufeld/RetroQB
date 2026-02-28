namespace RetroQB.Gameplay.Controllers;

public enum FieldGoalKickPhase
{
    SweepingAccuracy,
    Resolved
}

public readonly record struct FieldGoalKickResult(bool IsGood, string Message);

public sealed class FieldGoalController
{
    private const float BaseAccuracySweepSpeed = 2.0f;

    private FieldGoalKickResult? _pendingResult;
    private float _accuracyDirection = 1f;

    public bool IsActive { get; private set; }
    public FieldGoalKickPhase Phase { get; private set; } = FieldGoalKickPhase.SweepingAccuracy;
    public float Distance { get; private set; }
    public float AccuracyNormalized { get; private set; }

    /// <summary>
    /// Returns the maximum allowed error window for the current distance.
    /// Used by the renderer to draw the target zone.
    /// </summary>
    public float MaxAllowedError => Distance switch
    {
        <= 25f => 0.50f,
        <= 35f => 0.40f,
        <= 45f => 0.30f,
        <= 55f => 0.22f,
        _ => 0.16f
    };

    /// <summary>
    /// Returns a human-readable difficulty label for the current distance.
    /// </summary>
    public string DifficultyLabel => Distance switch
    {
        <= 25f => "Difficulty: Easy",
        <= 35f => "Difficulty: Medium",
        <= 45f => "Difficulty: Hard",
        <= 55f => "Difficulty: Very Hard",
        _ => "Difficulty: Extreme"
    };

    public void StartAttempt(float distance)
    {
        IsActive = true;
        Distance = distance;
        Phase = FieldGoalKickPhase.SweepingAccuracy;
        // Start sweep at a random edge so the player can't exploit an instant-kick at center
        AccuracyNormalized = 0.7f + (Random.Shared.NextSingle() * 0.3f);
        _accuracyDirection = Random.Shared.NextSingle() < 0.5f ? 1f : -1f;
        _pendingResult = null;
    }

    public void Update(float dt)
    {
        if (!IsActive)
        {
            return;
        }

        if (Phase == FieldGoalKickPhase.SweepingAccuracy)
        {
            float speedMultiplier = Distance switch
            {
                <= 25f => 1.0f,
                <= 35f => 1.1f,
                <= 45f => 1.25f,
                <= 55f => 1.45f,
                _ => 1.7f
            };

            float sweepSpeed = BaseAccuracySweepSpeed * speedMultiplier;
            AccuracyNormalized += _accuracyDirection * sweepSpeed * dt;
            if (AccuracyNormalized >= 1f)
            {
                AccuracyNormalized = 1f;
                _accuracyDirection = -1f;
            }
            else if (AccuracyNormalized <= -1f)
            {
                AccuracyNormalized = -1f;
                _accuracyDirection = 1f;
            }
        }
    }

    public void Confirm()
    {
        if (!IsActive)
        {
            return;
        }

        if (Phase == FieldGoalKickPhase.SweepingAccuracy)
        {
            ResolveKick();
        }
    }

    public bool TryConsumeResult(out FieldGoalKickResult result)
    {
        if (_pendingResult.HasValue)
        {
            result = _pendingResult.Value;
            _pendingResult = null;
            IsActive = false;
            return true;
        }

        result = default;
        return false;
    }

    private void ResolveKick()
    {
        bool isGood = MathF.Abs(AccuracyNormalized) <= MaxAllowedError;
        string message = isGood
            ? "GOOD!"
            : (AccuracyNormalized < 0f ? "WIDE LEFT" : "WIDE RIGHT");

        Phase = FieldGoalKickPhase.Resolved;
        _pendingResult = new FieldGoalKickResult(isGood, message);
    }
}
