using RetroQB.Core;

namespace RetroQB.Gameplay;

public enum PlayType
{
    QuickPass,
    LongPass,
    QbRunFocus
}

public enum PlayOutcome
{
    Ongoing,
    Tackle,
    Incomplete,
    Touchdown,
    Interception,
    Turnover
}

public sealed class PlayManager
{
    public int Down { get; private set; } = 1;
    public float Distance { get; private set; } = 10f;
    public float LineOfScrimmage { get; private set; } = Constants.EndZoneDepth + 20f;
    public float FirstDownLine { get; private set; } = Constants.EndZoneDepth + 30f;
    public int Score { get; private set; }
    public int SelectedReceiver { get; set; }
    public PlayType SelectedPlayType { get; set; } = PlayType.QuickPass;
    public float DefenderSpeedMultiplier { get; private set; } = 1.0f;
    
    // Drive history - stores play results for the current drive
    public List<string> DriveHistory { get; } = new();
    public int PlayNumber { get; private set; } = 1;

    public void StartNewDrive()
    {
        Down = 1;
        Distance = 10f;
        LineOfScrimmage = Constants.EndZoneDepth + 20f;
        FirstDownLine = LineOfScrimmage + Distance;
        DriveHistory.Clear();
        PlayNumber = 1;
    }

    public void StartPlay()
    {
        SelectedReceiver = 0;
    }

    public string ResolvePlay(float newBallY, bool incomplete, bool intercepted, bool touchdown)
    {
        string result;
        int playNum = PlayNumber;
        PlayNumber++;
        
        if (touchdown)
        {
            Score += 7;
            result = "TOUCHDOWN! +7";
            DriveHistory.Add($"#{playNum}: {result}");
            StartNewDrive();
            DefenderSpeedMultiplier += 0.03f;
            return result;
        }

        if (intercepted)
        {
            result = "INTERCEPTION!";
            DriveHistory.Add($"#{playNum}: {result}");
            StartNewDrive();
            return result;
        }

        if (incomplete)
        {
            Down++;
            result = HandleDowns(0f, true);
            DriveHistory.Add($"#{playNum}: {result}");
            return result;
        }

        float gain = newBallY - LineOfScrimmage;
        LineOfScrimmage = newBallY;
        if (LineOfScrimmage > Constants.EndZoneDepth + 100f)
        {
            LineOfScrimmage = Constants.EndZoneDepth + 100f;
        }

        if (gain >= Distance)
        {
            Down = 1;
            Distance = 10f;
            FirstDownLine = MathF.Min(LineOfScrimmage + Distance, Constants.EndZoneDepth + 100f);
            DefenderSpeedMultiplier += 0.02f;
            result = $"+{gain:F0} yds, 1ST DOWN!";
            DriveHistory.Add($"#{playNum}: {result}");
            return result;
        }

        Down++;
        Distance -= gain;
        FirstDownLine = MathF.Min(LineOfScrimmage + Distance, Constants.EndZoneDepth + 100f);
        result = HandleDowns(gain, false);
        DriveHistory.Add($"#{playNum}: {result}");
        return result;
    }

    private string HandleDowns(float gain, bool incomplete)
    {
        if (Down > 4)
        {
            string tod = "TURNOVER ON DOWNS";
            StartNewDrive();
            return tod;
        }

        if (incomplete)
        {
            return "Incomplete";
        }

        return $"+{gain:F0} yds";
    }

    public float GetYardLineDisplay(float worldY)
    {
        float yardLine = worldY - Constants.EndZoneDepth;
        yardLine = MathF.Max(0f, MathF.Min(100f, yardLine));
        return yardLine;
    }
}
