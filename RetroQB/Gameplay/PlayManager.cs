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

    public void StartNewDrive()
    {
        Down = 1;
        Distance = 10f;
        LineOfScrimmage = Constants.EndZoneDepth + 20f;
        FirstDownLine = LineOfScrimmage + Distance;
    }

    public void StartPlay()
    {
        SelectedReceiver = 0;
    }

    public string ResolvePlay(float newBallY, bool incomplete, bool intercepted, bool touchdown)
    {
        if (touchdown)
        {
            Score += 7;
            StartNewDrive();
            DefenderSpeedMultiplier += 0.03f;
            return "Touchdown! +7";
        }

        if (intercepted)
        {
            StartNewDrive();
            return "Intercepted! Drive resets.";
        }

        if (incomplete)
        {
            Down++;
            return HandleDowns(0f, true);
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
            return "First down!";
        }

        Down++;
        Distance -= gain;
        FirstDownLine = MathF.Min(LineOfScrimmage + Distance, Constants.EndZoneDepth + 100f);
        return HandleDowns(gain, false);
    }

    private string HandleDowns(float gain, bool incomplete)
    {
        if (Down > 4)
        {
            StartNewDrive();
            return "Turnover on downs! Drive resets.";
        }

        if (incomplete)
        {
            return "Incomplete pass.";
        }

        return $"Gain {gain:F1} yards.";
    }

    public float GetYardLineDisplay(float worldY)
    {
        float yardLine = worldY - Constants.EndZoneDepth;
        yardLine = MathF.Max(0f, MathF.Min(100f, yardLine));
        return yardLine;
    }
}
