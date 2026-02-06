namespace RetroQB.Core;

/// <summary>
/// Represents the three progression stages the player must win to become champion.
/// Each stage features increasingly difficult defensive opponents.
/// </summary>
public enum SeasonStage
{
    /// <summary>Regular season — baseline difficulty.</summary>
    RegularSeason,

    /// <summary>Playoff round — tougher defense, faster pass rush.</summary>
    Playoff,

    /// <summary>Super Bowl — elite defense, highest difficulty.</summary>
    SuperBowl
}

public static class SeasonStageExtensions
{
    /// <summary>Returns a display-friendly name for the stage.</summary>
    public static string GetDisplayName(this SeasonStage stage) => stage switch
    {
        SeasonStage.RegularSeason => "REGULAR SEASON",
        SeasonStage.Playoff => "PLAYOFF",
        SeasonStage.SuperBowl => "SUPER BOWL",
        _ => "UNKNOWN"
    };

    /// <summary>Returns a short label for the scoreboard.</summary>
    public static string GetShortLabel(this SeasonStage stage) => stage switch
    {
        SeasonStage.RegularSeason => "REG",
        SeasonStage.Playoff => "PLAYOFF",
        SeasonStage.SuperBowl => "SB",
        _ => "?"
    };

    /// <summary>Returns the next stage, or null if this is the final stage.</summary>
    public static SeasonStage? GetNextStage(this SeasonStage stage) => stage switch
    {
        SeasonStage.RegularSeason => SeasonStage.Playoff,
        SeasonStage.Playoff => SeasonStage.SuperBowl,
        _ => null
    };

    /// <summary>Returns the stage number (1-based) for display.</summary>
    public static int GetStageNumber(this SeasonStage stage) => stage switch
    {
        SeasonStage.RegularSeason => 1,
        SeasonStage.Playoff => 2,
        SeasonStage.SuperBowl => 3,
        _ => 0
    };

    /// <summary>
    /// Returns a base difficulty multiplier that scales defensive attributes for this stage.
    /// </summary>
    public static float GetDifficultyMultiplier(this SeasonStage stage) => stage switch
    {
        SeasonStage.RegularSeason => 1.0f,
        SeasonStage.Playoff => 1.06f,
        SeasonStage.SuperBowl => 1.13f,
        _ => 1.0f
    };

    /// <summary>
    /// Returns a softer multiplier for pass-rush / DE speed so the QB isn't
    /// instantly sacked in later stages.  Scales at roughly half the rate of
    /// the general difficulty multiplier.
    /// </summary>
    public static float GetPassRushMultiplier(this SeasonStage stage) => stage switch
    {
        SeasonStage.RegularSeason => 1.0f,
        SeasonStage.Playoff => 1.03f,
        SeasonStage.SuperBowl => 1.06f,
        _ => 1.0f
    };
}
