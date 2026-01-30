namespace RetroQB.Gameplay;

/// <summary>
/// Suggests plays based on down and distance situation.
/// Encapsulates play-calling AI logic separate from selection mechanics.
/// </summary>
public static class PlaySuggestion
{
    // Situational thresholds
    private const float LongYardageThreshold = 9f;
    private const float ThirdDownLongThreshold = 6f;
    private const float ShortYardageThreshold = 3f;
    private const int ThirdDown = 3;

    /// <summary>
    /// Gets the single best suggested play type for the situation.
    /// </summary>
    public static PlayType GetSuggested(int down, float distance)
    {
        if (IsShortYardageSituation(distance))
        {
            return PlayType.Run;
        }

        return PlayType.Pass;
    }

    /// <summary>
    /// Gets weighted list of play types for random selection.
    /// Types appear multiple times based on situational preference.
    /// </summary>
    public static List<PlayType> GetWeightedCandidates(int down, float distance)
    {
        if (IsLongYardageSituation(down, distance))
        {
            return new List<PlayType>
            {
                PlayType.Pass,
                PlayType.Pass,
                PlayType.Pass
            };
        }

        if (IsShortYardageSituation(distance))
        {
            return new List<PlayType>
            {
                PlayType.Run,
                PlayType.Run,
                PlayType.Pass
            };
        }

        return new List<PlayType>
        {
            PlayType.Pass,
            PlayType.Pass,
            PlayType.Run,
            PlayType.Run
        };
    }

    private static bool IsLongYardageSituation(int down, float distance)
    {
        return distance >= LongYardageThreshold || 
               (down >= ThirdDown && distance >= ThirdDownLongThreshold);
    }

    private static bool IsShortYardageSituation(float distance)
    {
        return distance <= ShortYardageThreshold;
    }
}
