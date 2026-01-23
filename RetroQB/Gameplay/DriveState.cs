namespace RetroQB.Gameplay;

/// <summary>
/// Tracks the current state of an offensive drive (down, distance, field position).
/// Separated from play selection logic for single responsibility.
/// </summary>
public sealed class DriveState
{
    private const float DefaultStartingYardLine = 20f;
    private const float DefaultDistance = 10f;
    private const int MaxDowns = 4;
    private const int TouchdownPoints = 7;

    public int Down { get; private set; } = 1;
    public float Distance { get; private set; } = DefaultDistance;
    public float LineOfScrimmage { get; private set; }
    public float FirstDownLine { get; private set; }
    public int Score { get; private set; }
    public int PlayNumber { get; private set; } = 1;
    public List<string> DriveHistory { get; } = new();
    
    /// <summary>
    /// Multiplier that increases as the player scores, making defenders faster.
    /// </summary>
    public float DifficultyMultiplier { get; private set; } = 1.0f;

    public DriveState()
    {
        Reset();
    }

    public void Reset()
    {
        Down = 1;
        Distance = DefaultDistance;
        LineOfScrimmage = FieldGeometry.EndZoneDepth + DefaultStartingYardLine;
        FirstDownLine = LineOfScrimmage + Distance;
        DriveHistory.Clear();
        PlayNumber = 1;
    }

    public PlayResult ResolveTouchdown()
    {
        Score += TouchdownPoints;
        DifficultyMultiplier += 0.03f;
        var result = new PlayResult(PlayOutcome.Touchdown, 0f, "TOUCHDOWN! +7");
        RecordPlay(result);
        Reset();
        return result;
    }

    public PlayResult ResolveInterception()
    {
        var result = new PlayResult(PlayOutcome.Interception, 0f, "INTERCEPTION!");
        RecordPlay(result);
        Reset();
        return result;
    }

    public PlayResult ResolveIncomplete()
    {
        Down++;
        var result = CheckTurnoverOnDowns() ?? new PlayResult(PlayOutcome.Incomplete, 0f, "Incomplete");
        RecordPlay(result);
        return result;
    }

    public PlayResult ResolveTackle(float newBallY)
    {
        float gain = newBallY - LineOfScrimmage;
        LineOfScrimmage = MathF.Min(newBallY, FieldGeometry.OpponentGoalLine);

        if (gain >= Distance)
        {
            return ResolveFirstDown(gain);
        }

        Down++;
        Distance -= gain;
        FirstDownLine = MathF.Min(LineOfScrimmage + Distance, FieldGeometry.OpponentGoalLine);
        
        var result = CheckTurnoverOnDowns() ?? new PlayResult(PlayOutcome.Tackle, gain, $"+{gain:F0} yds");
        RecordPlay(result);
        return result;
    }

    private PlayResult ResolveFirstDown(float gain)
    {
        Down = 1;
        Distance = DefaultDistance;
        FirstDownLine = MathF.Min(LineOfScrimmage + Distance, FieldGeometry.OpponentGoalLine);
        DifficultyMultiplier += 0.02f;
        var result = new PlayResult(PlayOutcome.Tackle, gain, $"+{gain:F0} yds, 1ST DOWN!");
        RecordPlay(result);
        return result;
    }

    private PlayResult? CheckTurnoverOnDowns()
    {
        if (Down > MaxDowns)
        {
            var result = new PlayResult(PlayOutcome.Turnover, 0f, "TURNOVER ON DOWNS");
            Reset();
            return result;
        }
        return null;
    }

    private void RecordPlay(PlayResult result)
    {
        DriveHistory.Add($"#{PlayNumber}: {result.Message}");
        PlayNumber++;
    }
}

/// <summary>
/// Result of a single play, containing outcome and display text.
/// </summary>
public readonly record struct PlayResult(PlayOutcome Outcome, float Gain, string Message);
