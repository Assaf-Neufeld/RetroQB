namespace RetroQB.Gameplay;

public enum PlayType
{
    Pass,
    Run
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

/// <summary>
/// Manages play selection and coordinates game state.
/// Pass plays: 10 plays (1 as wildcard, 2-9 plus 0 as regular plays)
/// Run plays: 10 plays (Q as wildcard, W-P as regular plays)
/// </summary>
public sealed class PlayManager
{
    private const int WildcardIndex = 0;
    public const int PassPlayCount = 10;
    public const int RunPlayCount = 10;

    private readonly List<PlayDefinition> _passPlays;
    private readonly List<PlayDefinition> _runPlays;
    private readonly DriveState _driveState;

    private int _selectedPassIndex = 0;
    private int _selectedRunIndex = 0;

    public PlayType SelectedPlayType { get; private set; } = PlayType.Pass;
    public int SelectedReceiver { get; set; }
    
    public PlayDefinition SelectedPlay => SelectedPlayType == PlayType.Pass 
        ? _passPlays[_selectedPassIndex] 
        : _runPlays[_selectedRunIndex];
    
    public int SelectedPlayIndex => SelectedPlayType == PlayType.Pass 
        ? _selectedPassIndex 
        : _selectedRunIndex;

    public IReadOnlyList<PlayDefinition> PassPlays => _passPlays;
    public IReadOnlyList<PlayDefinition> RunPlays => _runPlays;

    // Drive state delegation
    public int Down => _driveState.Down;
    public float Distance => _driveState.Distance;
    public float LineOfScrimmage => _driveState.LineOfScrimmage;
    public float FirstDownLine => _driveState.FirstDownLine;
    public int Score => _driveState.Score;
    public int AwayScore => _driveState.AwayScore;
    public float DefenderSpeedMultiplier => _driveState.DifficultyMultiplier;
    public List<string> DriveHistory => _driveState.DriveHistory;
    public List<PlayRecord> PlayRecords => _driveState.PlayRecords;
    public int PlayNumber => _driveState.PlayNumber;

    /// <summary>
    /// Starts recording a new play with pre-snap information.
    /// </summary>
    public void StartPlayRecord(bool isZoneCoverage, List<string> blitzers)
    {
        _driveState.StartPlayRecord(SelectedPlay.Name, SelectedPlayType, isZoneCoverage, blitzers);
    }

    /// <summary>
    /// Finalizes the current play record with result information.
    /// </summary>
    public void FinalizePlayRecord(PlayOutcome outcome, float gain, string? catcherLabel, RouteType? catcherRoute, bool wasRun)
    {
        _driveState.FinalizePlayRecord(outcome, gain, catcherLabel, catcherRoute, wasRun);
    }

    public PlayManager()
    {
        _passPlays = PlaybookBuilder.BuildPassPlays();
        _runPlays = PlaybookBuilder.BuildRunPlays();
        _driveState = new DriveState();
    }

    public void StartNewDrive()
    {
        _driveState.Reset();
        SelectedPlayType = PlayType.Pass;
    }

    public void StartNewGame()
    {
        _driveState.ResetForNewGame();
        SelectedPlayType = PlayType.Pass;
    }

    public void StartPlay()
    {
        SelectedReceiver = 0;
    }

    /// <summary>
    /// Select a pass play by index (0-9, where 0 is wildcard).
    /// </summary>
    public bool SelectPassPlay(int index, Random rng)
    {
        if (index < 0 || index >= PassPlayCount)
        {
            return false;
        }

        _selectedPassIndex = index;
        SelectedPlayType = PlayType.Pass;
        
        if (index == WildcardIndex)
        {
            _passPlays[WildcardIndex] = PlaybookBuilder.CreatePassWildcardPlay(rng);
        }
        
        return true;
    }

    /// <summary>
    /// Select a run play by index (0-9, where 0 is wildcard).
    /// </summary>
    public bool SelectRunPlay(int index, Random rng)
    {
        if (index < 0 || index >= RunPlayCount)
        {
            return false;
        }

        _selectedRunIndex = index;
        SelectedPlayType = PlayType.Run;
        
        if (index == WildcardIndex)
        {
            _runPlays[WildcardIndex] = PlaybookBuilder.CreateRunWildcardPlay(rng);
        }
        
        return true;
    }

    public bool AutoSelectPlayBySituation(Random rng)
    {
        var candidates = PlaySuggestion.GetWeightedCandidates(Down, Distance);
        if (candidates.Count == 0)
        {
            return false;
        }

        PlayType pickedType = candidates[rng.Next(candidates.Count)];
        SelectedPlayType = pickedType;

        if (pickedType == PlayType.Pass)
        {
            _selectedPassIndex = rng.Next(PassPlayCount);
            if (_selectedPassIndex == WildcardIndex)
            {
                _passPlays[WildcardIndex] = PlaybookBuilder.CreatePassWildcardPlay(rng);
            }
        }
        else
        {
            _selectedRunIndex = rng.Next(RunPlayCount);
            if (_selectedRunIndex == WildcardIndex)
            {
                _runPlays[WildcardIndex] = PlaybookBuilder.CreateRunWildcardPlay(rng);
            }
        }

        return true;
    }

    public PlayType GetSuggestedPlayType()
    {
        return PlaySuggestion.GetSuggested(Down, Distance);
    }

    public string GetPlayLabel()
    {
        string typeName = SelectedPlayType == PlayType.Pass ? "Pass" : "Run";
        return $"{typeName}: {SelectedPlay.Name}";
    }

    public PlayResult ResolvePlay(float newBallY, bool incomplete, bool intercepted, bool touchdown)
    {
        PlayResult result;
        float gain = newBallY - LineOfScrimmage;

        if (touchdown)
        {
            result = _driveState.ResolveTouchdown(gain);
        }
        else if (intercepted)
        {
            result = _driveState.ResolveInterception();
        }
        else if (incomplete)
        {
            result = _driveState.ResolveIncomplete();
        }
        else
        {
            result = _driveState.ResolveTackle(newBallY);
        }

        return result;
    }

    public float GetYardLineDisplay(float worldY)
    {
        return FieldGeometry.GetYardLineDisplay(worldY);
    }
}
