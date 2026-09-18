using RetroQB.AI;

namespace RetroQB.Gameplay;

public enum PlayType
{
    Pass,
    Run,
    FieldGoal
}

public enum PlayOutcome
{
    Ongoing,
    Tackle,
    Incomplete,
    PassDefended,
    Touchdown,
    Interception,
    Turnover,
    Safety,
    FieldGoalGood,
    FieldGoalMissed
}

/// <summary>Coordinates the drive and selects resolved catalog calls through a separate call sheet.</summary>
public sealed class PlayManager
{
    private const int RecentCallLimit = 4;
    public const int PassPlayCount = PlayCallSheet.PlaysPerFamily;
    public const int RunPlayCount = PlayCallSheet.PlaysPerFamily;
    private readonly DriveState _driveState = new();
    private readonly Dictionary<string, ResolvedPlay> _resolvedCalls;
    private readonly Dictionary<string, int> _callCounts = new(StringComparer.Ordinal);
    private readonly Queue<string> _recentPassCalls = new();
    private readonly Queue<string> _recentRunCalls = new();
    private string _selectedPassId = string.Empty;
    private string _selectedRunId = string.Empty;

    private (PlaySituation Situation, int Play, int Score, int Away)? _sheetSituation;
    public string SituationLabel => SituationalCallSheet.Label(GetPlaySituation());
    public bool EnsureSituationCallSheet()
    {
        var key = (GetPlaySituation(), PlayNumber, Score, AwayScore);
        if (_sheetSituation == key) return false;
        SetCallSheet(SituationalCallSheet.Build(Catalog, key.Item1, PlayNumber * 7919 + Score * 31 + AwayScore, GetCallCount));
        _sheetSituation = key;
        return true;
    }

    public PlayCatalog Catalog { get; }
    public PlayCallSheet CallSheet { get; private set; } = null!;
    public PlayType SelectedPlayType { get; private set; } = PlayType.Pass;
    public int SelectedReceiver { get; set; }
    public ResolvedPlay SelectedPlay => _resolvedCalls[SelectedPlayType == PlayType.Pass ? _selectedPassId : _selectedRunId];
    public int SelectedPlayIndex => (SelectedPlayType == PlayType.Pass ? CallSheet.PassIds : CallSheet.RunIds)
        .ToList().IndexOf(SelectedPlay.Id);
    public IReadOnlyList<ResolvedPlay> PassPlays { get; private set; } = Array.Empty<ResolvedPlay>();
    public IReadOnlyList<ResolvedPlay> RunPlays { get; private set; } = Array.Empty<ResolvedPlay>();

    public PlayManager(PlayCatalog? catalog = null)
    {
        Catalog = catalog ?? PlaybookBuilder.BuildCatalog();
        _resolvedCalls = Catalog.Plays.ToDictionary(p => p.Id, p => PlayResolver.Resolve(p), StringComparer.Ordinal);
        SetCallSheet(PlayCallSheet.Default(Catalog));
    }

    /// <summary>Replace only between snaps. Selection and usage follow IDs when hotkeys move.</summary>
    public void SetCallSheet(PlayCallSheet callSheet)
    {
        ArgumentNullException.ThrowIfNull(callSheet);
        if (!ReferenceEquals(Catalog, callSheet.Catalog))
            throw new ArgumentException("Call sheet must reference this manager's catalog.");
        CallSheet = callSheet;
        if (!callSheet.PassIds.Contains(_selectedPassId)) _selectedPassId = callSheet.PassIds[0];
        if (!callSheet.RunIds.Contains(_selectedRunId)) _selectedRunId = callSheet.RunIds[0];
        RefreshAvailableCalls();
    }

    private void RefreshAvailableCalls()
    {
        PassPlays = Array.AsReadOnly(CallSheet.PassIds.Select(id => _resolvedCalls[id]).ToArray());
        RunPlays = Array.AsReadOnly(CallSheet.RunIds.Select(id => _resolvedCalls[id]).ToArray());
    }

    public void StartNewDrive()
    {
        _driveState.Reset();
        _sheetSituation = null;
        SelectedPlayType = PlayType.Pass;
        ClearCallRecency();
    }

    public void StartNewGame()
    {
        _driveState.ResetForNewGame();
        _sheetSituation = null;
        SelectedPlayType = PlayType.Pass;
        _callCounts.Clear();
        ClearCallRecency();
    }

    public void StartPlay()
    {
        SelectedReceiver = 0;
        // Count actual snaps, including manual calls, rather than pre-snap browsing.
        string id = SelectedPlay.Id;
        _callCounts[id] = GetCallCount(id) + 1;
        var recent = SelectedPlayType == PlayType.Pass ? _recentPassCalls : _recentRunCalls;
        recent.Enqueue(id);
        while (recent.Count > RecentCallLimit) recent.Dequeue();
    }

    public int GetCallCount(string playId) => _callCounts.GetValueOrDefault(playId);

    private void ClearCallRecency()
    {
        _recentPassCalls.Clear();
        _recentRunCalls.Clear();
    }

    public bool SelectPassPlay(int index, Random rng) => SelectPlay(PlayType.Pass, index, rng);
    public bool SelectRunPlay(int index, Random rng) => SelectPlay(PlayType.Run, index, rng);

    private bool SelectPlay(PlayType family, int index, Random rng)
    {
        var ids = family == PlayType.Pass ? CallSheet.PassIds : CallSheet.RunIds;
        if (index < 0 || index >= ids.Count) return false;
        string id = ids[index];
        return ActivatePlay(Catalog[id].IsWildcard ? GenerateWildcard(_resolvedCalls[id], rng) : _resolvedCalls[id]);
    }

    private static ResolvedPlay GenerateWildcard(ResolvedPlay play, Random rng)
    {
        var generated = play.Family == PlayType.Pass
            ? PlaybookBuilder.CreatePassWildcardPlay(rng) : PlaybookBuilder.CreateRunWildcardPlay(rng);
        if (generated.Id != play.Id)
            throw new InvalidOperationException("No generator is registered for this wildcard ID.");
        return PlayResolver.Resolve(generated);
    }

    private bool ActivatePlay(ResolvedPlay play)
    {
        if (!ReferenceEquals(_resolvedCalls[play.Id], play))
        {
            _resolvedCalls[play.Id] = play;
            RefreshAvailableCalls();
        }
        SelectedPlayType = play.Family;
        if (play.Family == PlayType.Pass) _selectedPassId = play.Id;
        else _selectedRunId = play.Id;
        return true;
    }

    public void FlipSelectedPlay()
    {
        _resolvedCalls[SelectedPlay.Id] = SelectedPlay.Flip();
        RefreshAvailableCalls();
    }

    public bool AutoSelectPlayBySituation(Random rng)
    {
        var situation = GetPlaySituation();
        var candidates = new List<(ResolvedPlay Play, float Weight)>();
        AddCandidates(PassPlays, PlayType.Pass);
        AddCandidates(RunPlays, PlayType.Run);
        float total = candidates.Sum(c => c.Weight);
        if (total <= 0f) return false;
        float pick = (float)rng.NextDouble() * total;
        foreach (var candidate in candidates)
        {
            pick -= candidate.Weight;
            if (pick <= 0f) return ActivatePlay(candidate.Play);
        }
        var last = candidates[^1];
        return ActivatePlay(last.Play);

        void AddCandidates(IReadOnlyList<ResolvedPlay> plays, PlayType family)
        {
            float familyWeight = PlaySuggestion.GetFamilyWeight(family, situation);
            for (int i = 0; i < plays.Count; i++)
            {
                // Score the exact generated assignments that will execute if selected.
                var play = plays[i].IsWildcard ? GenerateWildcard(plays[i], rng) : plays[i];
                float weight = familyWeight * PlaySuggestion.GetPlayWeight(play, situation)
                    * GetDiversityWeight(play) * (play.IsWildcard ? 0.85f : 1f);
                if (weight > 0f) candidates.Add((play, weight));
            }
        }
    }

    private float GetDiversityWeight(ResolvedPlay play)
    {
        var familyIds = Catalog.Plays.Where(p => p.Family == play.Family).Select(p => p.Id).ToArray();
        float average = familyIds.Average(id => (float)GetCallCount(id));
        float weight = Math.Clamp(1f + ((average - GetCallCount(play.Id)) * 0.22f), 0.65f, 1.7f);
        var recent = (play.Family == PlayType.Pass ? _recentPassCalls : _recentRunCalls).Reverse().ToArray();
        int offset = Array.IndexOf(recent, play.Id);
        weight *= offset switch { 0 => 0.18f, 1 => 0.35f, >= 2 => 0.6f, _ => 1f };
        return MathF.Max(weight, 0.05f);
    }

    public PlayType GetSuggestedPlayType() => GetSuggestedPlaySelection().Type;

    public string GetSuggestedPlayLabel()
    {
        var suggested = GetSuggestedPlaySelection();
        var play = suggested.Type == PlayType.Pass ? PassPlays[suggested.Index] : RunPlays[suggested.Index];
        return $"{suggested.Type}: {play.Name}";
    }

    private (PlayType Type, int Index) GetSuggestedPlaySelection() =>
        PlaySuggestion.GetSuggestedPlay(GetPlaySituation(), PassPlays, RunPlays);

    private PlaySituation GetPlaySituation() => new(Down, Distance, LineOfScrimmage, FirstDownLine);

    public string GetPlayLabel() => $"{SelectedPlayType}: {SelectedPlay.Definition.Info.Formation} / {SelectedPlay.Name}{(SelectedPlay.IsFlipped ? " (flipped)" : "")}";

    // Drive state delegation
    public PlayResult ResolveFieldGoal(FieldGoalAttempt kick) => _driveState.ResolveFieldGoal(kick);

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
        _driveState.StartPlayRecord($"{SelectedPlay.Definition.Info.Formation} / {SelectedPlay.Name}", SelectedPlayType, isUnderneathManCoverage, coverageScheme, blitzers, SelectedPlay.Id, SelectedPlay.IsFlipped);
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

    public PlayResult ResolvePlay(float newBallY, bool incomplete, bool passDefended, bool intercepted, bool touchdown, string? tackleMessageOverride = null)
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
        else if (passDefended)
        {
            result = _driveState.ResolvePassDefended();
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
