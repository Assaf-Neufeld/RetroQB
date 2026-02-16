using RetroQB.AI;

namespace RetroQB.Gameplay;

/// <summary>
/// Records detailed information about a single play for the drive summary.
/// </summary>
public sealed class PlayRecord
{
    // Pre-snap situation
    public int PlayNumber { get; init; }
    public int Down { get; init; }
    public float Distance { get; init; }
    public float YardLine { get; init; }
    
    // Offensive play info
    public string OffensivePlayName { get; init; } = string.Empty;
    public PlayType PlayFamily { get; init; }
    
    // Defensive scheme info
    public bool IsZoneCoverage { get; init; }
    public CoverageScheme CoverageScheme { get; init; }
    public List<string> Blitzers { get; init; } = new();
    
    // Play result
    public PlayOutcome Outcome { get; set; }
    public float Gain { get; set; }
    public string? CatcherLabel { get; set; }
    public RouteType? CatcherRoute { get; set; }
    public bool WasRun { get; set; }

    /// <summary>
    /// Gets a formatted string describing the pre-snap situation.
    /// Example: "OWN 35 | 2nd & 7"
    /// </summary>
    public string GetSituationText()
    {
        string downOrdinal = Down switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            4 => "4th",
            _ => $"{Down}th"
        };
        
        string distanceText = Distance >= 10 && YardLine + Distance >= 100 ? "Goal" : $"{Distance:F0}";
        string sideText = YardLine <= 50 ? "OWN" : "OPP";
        float displayYard = YardLine <= 50 ? YardLine : 100 - YardLine;
        
        return $"{sideText} {displayYard:F0} | {downOrdinal} & {distanceText}";
    }

    /// <summary>
    /// Gets a formatted string describing the play call.
    /// Example: "Quick: Slants vs Zone (LB blitz)"
    /// </summary>
    public string GetPlayCallText()
    {
        string familyName = PlayFamily switch
        {
            PlayType.Pass => "Pass",
            PlayType.Run => "Run",
            _ => "Play"
        };
        
        string coverageType = IsZoneCoverage ? "Zone" : "Man";
        string coverageShell = GetCoverageShellName(CoverageScheme);
        string blitzInfo = Blitzers.Count > 0 
            ? $" ({string.Join(", ", Blitzers)} blitz)" 
            : "";
        
        return $"{familyName}: {OffensivePlayName} vs {coverageType} - {coverageShell}{blitzInfo}";
    }

    /// <summary>
    /// Gets a formatted string describing the play result.
    /// Example: "+14 yd pass to WR2 on a Go route"
    /// </summary>
    public string GetResultText()
    {
        return Outcome switch
        {
            PlayOutcome.Touchdown when WasRun => $"TD! {Gain:F0} yd run",
            PlayOutcome.Touchdown when CatcherLabel != null && CatcherRoute != null => 
                $"TD! {Gain:F0} yd pass to {CatcherLabel} ({GetRouteName(CatcherRoute.Value)})",
            PlayOutcome.Touchdown => $"TD! {Gain:F0} yds",
            
            PlayOutcome.Interception => "INTERCEPTED!",
            PlayOutcome.Incomplete => "Incomplete",
            PlayOutcome.Turnover => "Turnover on downs",
            
            PlayOutcome.Tackle when WasRun => 
                $"{FormatGain(Gain)} yd run",
            PlayOutcome.Tackle when CatcherLabel != null && CatcherRoute != null => 
                $"{FormatGain(Gain)} yd pass to {CatcherLabel} ({GetRouteName(CatcherRoute.Value)})",
            PlayOutcome.Tackle => $"{FormatGain(Gain)} yds",
            
            _ => "..."
        };
    }

    private static string FormatGain(float gain)
    {
        return gain >= 0 ? $"+{gain:F0}" : $"{gain:F0}";
    }

    private static string GetRouteName(RouteType route)
    {
        return route switch
        {
            RouteType.Go => "Go",
            RouteType.Slant => "Slant",
            RouteType.OutShallow => "Out",
            RouteType.OutDeep => "Deep Out",
            RouteType.InShallow => "In",
            RouteType.InDeep => "Deep In",
            RouteType.PostShallow => "Post",
            RouteType.PostDeep => "Deep Post",
            RouteType.DoubleMove => "Dbl Move",
            RouteType.Flat => "Flat",
            _ => route.ToString()
        };
    }

    private static string GetCoverageShellName(CoverageScheme scheme)
    {
        return scheme switch
        {
            CoverageScheme.Cover0 => "Cover 0",
            CoverageScheme.Cover1 => "Cover 1",
            CoverageScheme.Cover2Zone => "Cover 2 Zone",
            CoverageScheme.Cover3Zone => "Cover 3 Zone",
            CoverageScheme.Cover4Zone => "Cover 4",
            CoverageScheme.Cover2Man => "Cover 2 Man",
            _ => scheme.ToString()
        };
    }
}
