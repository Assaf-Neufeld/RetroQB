using RetroQB.AI;

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
    private const int RecentAutoSelectionLimit = 4;
    public const int PassPlayCount = 10;
    public const int RunPlayCount = 10;

    private readonly List<PlayDefinition> _passPlays;
    private readonly List<PlayDefinition> _runPlays;
    private readonly DriveState _driveState;
    private readonly int[] _autoPassSelectionCounts;
    private readonly int[] _autoRunSelectionCounts;
    private readonly Queue<int> _recentAutoPassSelections;
    private readonly Queue<int> _recentAutoRunSelections;

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
    public void StartPlayRecord(bool isUnderneathManCoverage, CoverageScheme coverageScheme, List<string> blitzers)
    {
        _driveState.StartPlayRecord(SelectedPlay.Name, SelectedPlayType, isUnderneathManCoverage, coverageScheme, blitzers);
    }

    /// <summary>
    /// Finalizes the current play record with result information.
    /// </summary>
    public void FinalizePlayRecord(
        PlayOutcome outcome,
        float gain,
        string? catcherLabel,
        RouteType? catcherRoute,
        bool wasRun,
        string? ballCarrierLabel = null,
        bool isSack = false,
        int sackYardsLost = 0)
    {
        _driveState.FinalizePlayRecord(outcome, gain, catcherLabel, catcherRoute, wasRun, ballCarrierLabel, isSack, sackYardsLost);
    }

    public PlayManager()
    {
        _passPlays = PlaybookBuilder.BuildPassPlays();
        _runPlays = PlaybookBuilder.BuildRunPlays();
        _driveState = new DriveState();
        _autoPassSelectionCounts = new int[PassPlayCount];
        _autoRunSelectionCounts = new int[RunPlayCount];
        _recentAutoPassSelections = new Queue<int>(RecentAutoSelectionLimit);
        _recentAutoRunSelections = new Queue<int>(RecentAutoSelectionLimit);
    }

    public void StartNewDrive()
    {
        _driveState.Reset();
        SelectedPlayType = PlayType.Pass;
        ClearAutoSelectionRecency();
    }

    public void StartNewGame()
    {
        _driveState.ResetForNewGame();
        SelectedPlayType = PlayType.Pass;
        ResetAutoSelectionTracking();
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
        float passFamilyWeight = PlaySuggestion.GetFamilyWeight(PlayType.Pass, Down, Distance);
        float runFamilyWeight = PlaySuggestion.GetFamilyWeight(PlayType.Run, Down, Distance);

        var candidates = new List<(PlayType Type, int Index, float Weight)>(PassPlayCount + RunPlayCount);
        AddAutoPlayCandidates(candidates, PlayType.Pass, _passPlays, _autoPassSelectionCounts, _recentAutoPassSelections, passFamilyWeight);
        AddAutoPlayCandidates(candidates, PlayType.Run, _runPlays, _autoRunSelectionCounts, _recentAutoRunSelections, runFamilyWeight);

        if (candidates.Count == 0)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += candidates[i].Weight;
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float pick = (float)rng.NextDouble() * totalWeight;
        for (int i = 0; i < candidates.Count; i++)
        {
            pick -= candidates[i].Weight;
            if (pick > 0f)
            {
                continue;
            }

            ApplyAutoSelection(candidates[i].Type, candidates[i].Index, rng);
            return true;
        }

        var fallback = candidates[^1];
        ApplyAutoSelection(fallback.Type, fallback.Index, rng);
        return true;
    }

    private void AddAutoPlayCandidates(
        List<(PlayType Type, int Index, float Weight)> candidates,
        PlayType family,
        IReadOnlyList<PlayDefinition> plays,
        int[] selectionCounts,
        Queue<int> recentSelections,
        float familyWeight)
    {
        for (int index = 0; index < plays.Count; index++)
        {
            float situationalWeight = PlaySuggestion.GetPlayWeight(plays[index], Down, Distance);
            float diversityWeight = GetAutoSelectionDiversityWeight(index, selectionCounts, recentSelections);
            float wildcardWeight = index == WildcardIndex ? 0.85f : 1f;
            float totalWeight = familyWeight * situationalWeight * diversityWeight * wildcardWeight;

            if (totalWeight > 0f)
            {
                candidates.Add((family, index, totalWeight));
            }
        }
    }

    private float GetAutoSelectionDiversityWeight(int index, int[] selectionCounts, Queue<int> recentSelections)
    {
        int totalSelections = 0;
        for (int i = 0; i < selectionCounts.Length; i++)
        {
            totalSelections += selectionCounts[i];
        }

        float weight = 1f;
        if (totalSelections > 0)
        {
            float averageSelections = (float)totalSelections / selectionCounts.Length;
            float usageGap = averageSelections - selectionCounts[index];
            weight *= Math.Clamp(1f + (usageGap * 0.22f), 0.65f, 1.7f);
        }

        int recentOffset = GetRecentSelectionOffset(index, recentSelections);
        if (recentOffset == 0)
        {
            weight *= 0.18f;
        }
        else if (recentOffset == 1)
        {
            weight *= 0.35f;
        }
        else if (recentOffset >= 2)
        {
            weight *= 0.6f;
        }

        return MathF.Max(weight, 0.05f);
    }

    private static int GetRecentSelectionOffset(int index, Queue<int> recentSelections)
    {
        int[] recent = recentSelections.ToArray();
        int offset = 0;
        for (int i = recent.Length - 1; i >= 0; i--)
        {
            if (recent[i] == index)
            {
                return offset;
            }

            offset++;
        }

        return -1;
    }

    private void ApplyAutoSelection(PlayType family, int index, Random rng)
    {
        SelectedPlayType = family;

        if (family == PlayType.Pass)
        {
            _selectedPassIndex = index;
            if (index == WildcardIndex)
            {
                _passPlays[WildcardIndex] = PlaybookBuilder.CreatePassWildcardPlay(rng);
            }

            _autoPassSelectionCounts[index]++;
            RecordRecentAutoSelection(_recentAutoPassSelections, index);
            return;
        }

        _selectedRunIndex = index;
        if (index == WildcardIndex)
        {
            _runPlays[WildcardIndex] = PlaybookBuilder.CreateRunWildcardPlay(rng);
        }

        _autoRunSelectionCounts[index]++;
        RecordRecentAutoSelection(_recentAutoRunSelections, index);
    }

    private static void RecordRecentAutoSelection(Queue<int> recentSelections, int index)
    {
        recentSelections.Enqueue(index);
        while (recentSelections.Count > RecentAutoSelectionLimit)
        {
            recentSelections.Dequeue();
        }
    }

    private void ResetAutoSelectionTracking()
    {
        Array.Clear(_autoPassSelectionCounts);
        Array.Clear(_autoRunSelectionCounts);
        ClearAutoSelectionRecency();
    }

    private void ClearAutoSelectionRecency()
    {
        _recentAutoPassSelections.Clear();
        _recentAutoRunSelections.Clear();
    }

    public PlayType GetSuggestedPlayType()
    {
        return GetSuggestedPlaySelection().Type;
    }

    public string GetSuggestedPlayLabel()
    {
        var suggested = GetSuggestedPlaySelection();
        PlayDefinition play = suggested.Type == PlayType.Pass
            ? _passPlays[suggested.Index]
            : _runPlays[suggested.Index];

        string typeName = suggested.Type == PlayType.Pass ? "Pass" : "Run";
        return $"{typeName}: {play.Name}";
    }

    private (PlayType Type, int Index) GetSuggestedPlaySelection()
    {
        return PlaySuggestion.GetSuggestedPlay(Down, Distance, _passPlays, _runPlays);
    }

    public string GetPlayLabel()
    {
        string typeName = SelectedPlayType == PlayType.Pass ? "Pass" : "Run";
        return $"{typeName}: {SelectedPlay.Name}";
    }

    public PlayResult ResolvePlay(float newBallY, bool incomplete, bool intercepted, bool touchdown, string? tackleMessageOverride = null)
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
            result = _driveState.ResolveTackle(newBallY, tackleMessageOverride);
        }

        return result;
    }

    public float GetYardLineDisplay(float worldY)
    {
        return FieldGeometry.GetYardLineDisplay(worldY);
    }
}
