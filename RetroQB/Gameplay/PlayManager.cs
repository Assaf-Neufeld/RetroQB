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

/// <summary>
/// Manages play selection and coordinates game state.
/// Delegates drive tracking to DriveState and playbook to PlaybookBuilder.
/// </summary>
public sealed class PlayManager
{
    private const int WildcardIndex = 0;

    private readonly Dictionary<PlayType, List<PlayDefinition>> _playbook;
    private readonly Dictionary<PlayType, int> _selectedPlayIndexByFamily;
    private readonly List<PlayOption> _playOptions;
    private readonly DriveState _driveState;

    public PlayType SelectedPlayFamily { get; private set; } = PlayType.QuickPass;
    public int SelectedReceiver { get; set; }
    
    public PlayDefinition SelectedPlay => 
        _playbook[SelectedPlayFamily][_selectedPlayIndexByFamily[SelectedPlayFamily]];
    
    public int SelectedPlayIndex => _selectedPlayIndexByFamily[SelectedPlayFamily];
    public IReadOnlyList<PlayOption> PlayOptions => _playOptions;

    // Drive state delegation
    public int Down => _driveState.Down;
    public float Distance => _driveState.Distance;
    public float LineOfScrimmage => _driveState.LineOfScrimmage;
    public float FirstDownLine => _driveState.FirstDownLine;
    public int Score => _driveState.Score;
    public float DefenderSpeedMultiplier => _driveState.DifficultyMultiplier;
    public List<string> DriveHistory => _driveState.DriveHistory;
    public List<PlayRecord> PlayRecords => _driveState.PlayRecords;
    public int PlayNumber => _driveState.PlayNumber;

    /// <summary>
    /// Starts recording a new play with pre-snap information.
    /// </summary>
    public void StartPlayRecord(bool isZoneCoverage, List<string> blitzers)
    {
        _driveState.StartPlayRecord(SelectedPlay.Name, SelectedPlayFamily, isZoneCoverage, blitzers);
    }

    /// <summary>
    /// Finalizes the current play record with result information.
    /// </summary>
    public void FinalizePlayRecord(PlayOutcome outcome, float gain, string? catcherLabel, AI.RouteType? catcherRoute, bool wasRun)
    {
        _driveState.FinalizePlayRecord(outcome, gain, catcherLabel, catcherRoute, wasRun);
    }

    public PlayManager()
    {
        _playbook = PlaybookBuilder.Build();
        _driveState = new DriveState();
        _selectedPlayIndexByFamily = InitializePlayIndices();
        _playOptions = BuildPlayOptions();
    }

    public void StartNewDrive()
    {
        _driveState.Reset();
        SelectedPlayFamily = PlayType.QuickPass;
    }

    public void StartPlay()
    {
        SelectedReceiver = 0;
    }

    public void SelectPlayFamily(PlayType family, Random rng)
    {
        if (SelectedPlayFamily == family)
        {
            CyclePlayInFamily(family);
        }
        else
        {
            SelectedPlayFamily = family;
        }

        TryRegenerateWildcard(rng);
    }

    public bool SelectPlayByGlobalIndex(int globalIndex, Random rng)
    {
        if (globalIndex < 0 || globalIndex >= _playOptions.Count)
        {
            return false;
        }

        var option = _playOptions[globalIndex];
        SelectedPlayFamily = option.Family;
        _selectedPlayIndexByFamily[option.Family] = option.Index;
        
        TryRegenerateWildcard(rng);
        return true;
    }

    public bool AutoSelectPlayBySituation(Random rng)
    {
        var candidates = PlaySuggestion.GetWeightedCandidates(Down, Distance);
        if (candidates.Count == 0)
        {
            return false;
        }

        PlayType pickedFamily = candidates[rng.Next(candidates.Count)];
        SelectedPlayFamily = pickedFamily;

        if (_playbook.TryGetValue(pickedFamily, out var plays) && plays.Count > 0)
        {
            _selectedPlayIndexByFamily[pickedFamily] = rng.Next(plays.Count);
            TryRegenerateWildcard(rng);
            return true;
        }

        return false;
    }

    public PlayType GetSuggestedPlayFamily()
    {
        return PlaySuggestion.GetSuggested(Down, Distance);
    }

    public string GetPlayLabel(PlayType family)
    {
        if (!_playbook.TryGetValue(family, out var plays) || plays.Count == 0)
        {
            return family.ToString();
        }

        int index = _selectedPlayIndexByFamily[family];
        string familyName = GetFamilyDisplayName(family);
        return $"{familyName}: {plays[index].Name}";
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

    private Dictionary<PlayType, int> InitializePlayIndices()
    {
        return new Dictionary<PlayType, int>
        {
            [PlayType.QuickPass] = 0,
            [PlayType.LongPass] = 0,
            [PlayType.QbRunFocus] = 0
        };
    }

    private List<PlayOption> BuildPlayOptions()
    {
        var list = new List<PlayOption>();
        PlayType[] order = { PlayType.QuickPass, PlayType.LongPass, PlayType.QbRunFocus };

        foreach (var family in order)
        {
            if (!_playbook.TryGetValue(family, out var plays))
            {
                continue;
            }

            for (int i = 0; i < plays.Count; i++)
            {
                list.Add(new PlayOption(family, i, plays[i].Name));
            }
        }

        return list;
    }

    private void CyclePlayInFamily(PlayType family)
    {
        int count = _playbook[family].Count;
        _selectedPlayIndexByFamily[family] = (_selectedPlayIndexByFamily[family] + 1) % count;
    }

    private void TryRegenerateWildcard(Random rng)
    {
        if (SelectedPlayFamily != PlayType.QuickPass)
        {
            return;
        }

        if (_selectedPlayIndexByFamily[PlayType.QuickPass] != WildcardIndex)
        {
            return;
        }

        if (_playbook.TryGetValue(PlayType.QuickPass, out var plays) && plays.Count > 0)
        {
            plays[WildcardIndex] = PlaybookBuilder.CreateWildcardPlay(rng);
        }
    }

    private static string GetFamilyDisplayName(PlayType family)
    {
        return family switch
        {
            PlayType.QuickPass => "Quick",
            PlayType.LongPass => "Long",
            PlayType.QbRunFocus => "Run",
            _ => "Play"
        };
    }

    public readonly record struct PlayOption(PlayType Family, int Index, string Name);
}
