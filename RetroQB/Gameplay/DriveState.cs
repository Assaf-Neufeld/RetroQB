using RetroQB.AI;

namespace RetroQB.Gameplay;

/// <summary>
/// Tracks the current state of an offensive drive (down, distance, field position).
/// Separated from play selection logic for single responsibility.
/// </summary>
public sealed class DriveState
{
    private const float DefaultStartingYardLine = 20f;
    private const float DefaultDistance = 10f;
    private const float TwoPointDistance = 2f;
    private const float FieldGoalMaxDistance = 45f;
    private const int MaxDowns = 4;
    private const int TouchdownPoints = 6;

    public int Down { get; private set; } = 1;
    public float Distance { get; private set; } = DefaultDistance;
    public float LineOfScrimmage { get; private set; }
    public float FirstDownLine { get; private set; }
    public int Score { get; private set; }
    public int AwayScore { get; private set; }
    public int PlayNumber { get; private set; } = 1;
    public List<string> DriveHistory { get; } = new();
    public List<PlayRecord> PlayRecords { get; } = new();
    public PlayRecord? CurrentPlayRecord { get; private set; }
    
    /// <summary>
    /// Multiplier that increases as the player scores, making defenders faster.
    /// </summary>
    public float DifficultyMultiplier { get; private set; } = 1.0f;

    public DriveState()
    {
        Reset();
    }

    public void ResetForNewGame()
    {
        Score = 0;
        AwayScore = 0;
        DifficultyMultiplier = 1.0f;
        Reset();
    }

    public void Reset(float startingYardLine = DefaultStartingYardLine)
    {
        Down = 1;
        Distance = DefaultDistance;
        float clampedStart = MathF.Max(5f, MathF.Min(95f, startingYardLine));
        LineOfScrimmage = FieldGeometry.EndZoneDepth + clampedStart;
        FirstDownLine = LineOfScrimmage + Distance;
        DriveHistory.Clear();
        PlayRecords.Clear();
        CurrentPlayRecord = null;
        PlayNumber = 1;
    }

    /// <summary>
    /// Creates a new PlayRecord for the current play with pre-snap information.
    /// </summary>
    public void StartPlayRecord(string playName, PlayType playFamily, bool isZoneCoverage, CoverageScheme coverageScheme, List<string> blitzers)
    {
        float yardLine = FieldGeometry.GetYardLineDisplay(LineOfScrimmage);
        CurrentPlayRecord = new PlayRecord
        {
            PlayNumber = PlayNumber,
            Down = Down,
            Distance = Distance,
            YardLine = yardLine,
            OffensivePlayName = playName,
            PlayFamily = playFamily,
            IsZoneCoverage = isZoneCoverage,
            CoverageScheme = coverageScheme,
            Blitzers = new List<string>(blitzers)
        };
    }

    /// <summary>
    /// Finalizes the current PlayRecord with result information.
    /// </summary>
    public void FinalizePlayRecord(
        PlayOutcome outcome,
        float gain,
        string? catcherLabel,
        RouteType? catcherRoute,
        bool wasRun,
        bool isSack = false,
        int sackYardsLost = 0)
    {
        if (CurrentPlayRecord != null)
        {
            CurrentPlayRecord.Outcome = outcome;
            CurrentPlayRecord.Gain = gain;
            CurrentPlayRecord.CatcherLabel = catcherLabel;
            CurrentPlayRecord.CatcherRoute = catcherRoute;
            CurrentPlayRecord.WasRun = wasRun;
            CurrentPlayRecord.IsSack = isSack;
            CurrentPlayRecord.SackYardsLost = sackYardsLost;
            PlayRecords.Add(CurrentPlayRecord);
            CurrentPlayRecord = null;
        }
    }

    public PlayResult ResolveTouchdown(float gain = 0f)
    {
        Score += TouchdownPoints;
        DifficultyMultiplier += 0.03f;
        var result = new PlayResult(PlayOutcome.Touchdown, gain, "TOUCHDOWN! +6");
        RecordPlay(result);
        return result;
    }

    public string ResolveExtraPoint()
    {
        Score += 1;
        return "EXTRA POINT GOOD! +1";
    }

    public string ResolveTwoPointConversion(bool success)
    {
        if (success)
        {
            Score += 2;
            return "2PT GOOD! +2";
        }

        return "2PT NO GOOD";
    }

    public string ResolveFieldGoalMade()
    {
        Score += 3;
        return "FIELD GOAL GOOD! +3";
    }

    public bool IsFieldGoalRange()
    {
        return (FieldGeometry.OpponentGoalLine - LineOfScrimmage) <= FieldGoalMaxDistance;
    }

    public float GetFieldGoalDistance()
    {
        float lineToGoal = FieldGeometry.OpponentGoalLine - LineOfScrimmage;
        return MathF.Max(0f, lineToGoal + 7f);
    }

    public void SetupTwoPointAttempt()
    {
        Down = 1;
        Distance = TwoPointDistance;
        LineOfScrimmage = FieldGeometry.OpponentGoalLine - TwoPointDistance;
        FirstDownLine = FieldGeometry.OpponentGoalLine;
    }

    public PlayResult ResolveInterception()
    {
        var result = new PlayResult(PlayOutcome.Interception, 0f, "INTERCEPTION!");
        RecordPlay(result);
        return result;
    }

    public void AddOpponentScore(int points)
    {
        AwayScore += Math.Max(0, points);
    }

    public PlayResult ResolveIncomplete()
    {
        Down++;
        var result = CheckTurnoverOnDowns() ?? new PlayResult(PlayOutcome.Incomplete, 0f, "Incomplete");
        RecordPlay(result);
        return result;
    }

    public PlayResult ResolveTackle(float newBallY, string? tackleMessageOverride = null)
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
        
        string defaultMessage = gain >= 0f ? $"+{gain:F0} yds" : $"{gain:F0} yds";
        var result = CheckTurnoverOnDowns() ?? new PlayResult(PlayOutcome.Tackle, gain, tackleMessageOverride ?? defaultMessage);
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
