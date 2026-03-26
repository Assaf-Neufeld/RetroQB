using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay.Controllers;
using RetroQB.Gameplay.Replay;
using RetroQB.Input;
using RetroQB.Rendering;

namespace RetroQB.Gameplay;

/// <summary>
/// Orchestrates the game session by coordinating specialized controllers.
/// Each controller handles a single responsibility following SOLID principles.
/// </summary>
public sealed class GameSession : IDisposable
{
    private const int WinningScore = 21;

    // Core managers
    private readonly GameStateManager _stateManager;
    private readonly PlayManager _playManager;
    private readonly IStatisticsTracker _statsTracker;
    private readonly InputManager _input;
    private readonly Random _sessionRng = new();

    // Controllers (Single Responsibility)
    private readonly PlaySetupController _playSetupController;
    private readonly PlayExecutionController _playExecutionController;
    private readonly BallController _ballController;
    private readonly TackleController _tackleController;
    private readonly OverlapResolver _overlapResolver;
    private readonly ReceiverPriorityManager _receiverPriorityManager;
    private readonly DrawingController _drawingController;
    private readonly MenuController _menuController;
    private readonly ReplayRecorder _replayRecorder;
    private readonly ReplayClipStore _replayClipStore;
    private readonly ReplayPlayer _replayPlayer;
    private readonly ReplayStateHandler _replayStateHandler;

    // Defensive AI
    private readonly DefensiveMemory _defensiveMemory = new();
    private readonly DefensiveCoordinator _defensiveCoordinator;

    // Team attributes
    private OffensiveTeamAttributes _offensiveTeam = OffensiveTeamAttributes.Default;
    private DefensiveTeamAttributes _defensiveTeam = DefensiveTeamAttributes.Default;

    // Season progression
    private SeasonStage _currentStage = SeasonStage.RegularSeason;
    public SeasonStage CurrentStage => _currentStage;
    private readonly SeasonSummary _seasonSummary = new();
    private readonly PlayerRecordStore _playerRecordStore = new();

    // Entity state (managed by PlayEntities)
    private readonly PlayEntities _entities = new();

    // Play state
    private string _lastPlayText = string.Empty;
    private string _driveOverText = string.Empty;
    private bool _qbPastLos;
    private float _playOverTimer;
    private float _playOverDuration = 1.25f;
    private bool _manualPlaySelection;
    private bool _autoPlaySelectionDone;
    private bool _isZoneCoverage;
    private CoverageScheme _coverageScheme;
    private List<string> _blitzers = new();
    private int _selectedTeamIndex;
    private float _crowdMomentum;
    private float _crowdSurge;
    private float _crowdCarryoverBuzz;
    private float _crowdCarryoverMomentum;
    private float _liveCrowdMomentum;
    private float _liveCrowdSurge;
    private string _homeCrowdChant = string.Empty;
    private float _homeCrowdChantTimer;
    private float _homeCrowdChantDuration;
    private Vector2 _homeCrowdChantWorldPosition = new(Constants.FieldWidth * 0.5f, Constants.EndZoneDepth + 50f);
    private string _playerName = string.Empty;
    private string _nameInput = string.Empty;
    private string _pendingPlayerName = string.Empty;
    private string _nameEntryMessage = string.Empty;
    private bool _isPostSeasonNameEntry;
    private LeaderboardSummary _leaderboardSummary = LeaderboardSummary.Empty;
    private int _driveSummaryScrollOffsetFromLatest;

    public GameSession() : this(
        new GameStateManager(),
        new PlayManager(),
        new InputManager(),
        new FieldRenderer(),
        new HudRenderer(),
        new FireworksEffect(),
        new Random(),
        new FormationFactory(),
        new DefenseFactory(),
        new StatisticsTracker(),
        new ThrowingMechanics(),
        new ReplayRecorder(),
        new ReplayClipStore(),
        new ReplayPlayer(),
        new ReplayStateHandler())
    {
    }

    public GameSession(
        GameStateManager stateManager,
        PlayManager playManager,
        InputManager input,
        FieldRenderer fieldRenderer,
        HudRenderer hudRenderer,
        FireworksEffect fireworks,
        Random rng,
        IFormationFactory formationFactory,
        IDefenseFactory defenseFactory,
        IStatisticsTracker statsTracker,
        IThrowingMechanics throwingMechanics,
        ReplayRecorder replayRecorder,
        ReplayClipStore replayClipStore,
        ReplayPlayer replayPlayer,
        ReplayStateHandler replayStateHandler)
    {
        _stateManager = stateManager;
        _playManager = playManager;
        _statsTracker = statsTracker;
        _input = input;
        _replayRecorder = replayRecorder;
        _replayClipStore = replayClipStore;
        _replayPlayer = replayPlayer;
        _replayStateHandler = replayStateHandler;

        _defensiveCoordinator = new DefensiveCoordinator(_defensiveMemory);

        // Initialize controllers
        _overlapResolver = new OverlapResolver();
        _receiverPriorityManager = new ReceiverPriorityManager();
        _menuController = new MenuController(input);
        
        _playSetupController = new PlaySetupController(formationFactory, defenseFactory, rng);
        _playExecutionController = new PlayExecutionController(input, new BlockingController());
        _ballController = new BallController(rng, throwingMechanics, statsTracker, _receiverPriorityManager);
        _tackleController = new TackleController(rng, _overlapResolver);
        _drawingController = new DrawingController(fieldRenderer, hudRenderer, fireworks, _receiverPriorityManager);

        InitializeDrive();
        _stateManager.SetState(GameState.MainMenu);
    }

    /// <summary>
    /// Sets the offensive team attributes for the current game.
    /// </summary>
    public void SetOffensiveTeam(OffensiveTeamAttributes team)
    {
        _offensiveTeam = team ?? OffensiveTeamAttributes.Default;
    }

    /// <summary>
    /// Sets the defensive team attributes for the current game.
    /// </summary>
    public void SetDefensiveTeam(DefensiveTeamAttributes team)
    {
        _defensiveTeam = team ?? DefensiveTeamAttributes.Default;
    }

    /// <summary>
    /// Gets the current offensive team attributes.
    /// </summary>
    public OffensiveTeamAttributes OffensiveTeam => _offensiveTeam;

    /// <summary>
    /// Gets the current defensive team attributes.
    /// </summary>
    public DefensiveTeamAttributes DefensiveTeam => _defensiveTeam;

    private void InitializeDrive()
    {
        _playManager.StartNewDrive();
        SetupEntities();
        ResetPlayState();
        _replayRecorder.Reset();
        _drawingController.Fireworks.Clear();
    }

    private void InitializeGame()
    {
        _playManager.StartNewGame();
        _defensiveMemory.Reset();
        SetDefensiveTeamForStage(_currentStage);
        SetupEntities();
        ResetPlayState();
        _replayRecorder.Reset();
        _drawingController.Fireworks.Clear();
    }

    /// <summary>
    /// Selects and applies a defensive team appropriate to the current season stage.
    /// Higher stages get harder defenses with boosted attributes.
    /// </summary>
    private void SetDefensiveTeamForStage(SeasonStage stage)
    {
        DefensiveTeamAttributes baseDefense = stage switch
        {
            SeasonStage.RegularSeason => DefensiveTeamPresets.ScarletGuard,
            SeasonStage.Playoff => DefensiveTeamPresets.CrimsonRush,
            SeasonStage.SuperBowl => DefensiveTeamPresets.BloodlineBastion,
            _ => DefensiveTeamPresets.ScarletGuard
        };

        // Apply stage difficulty multiplier to create a scaled-up version.
        // Pass-rush / DE speed uses a softer curve so the QB isn't instantly sacked.
        float stageMult = stage.GetDifficultyMultiplier();
        float rushMult  = stage.GetPassRushMultiplier();
        var scaledDefense = new DefensiveTeamAttributes
        {
            Name = baseDefense.Name,
            Description = baseDefense.Description,
            PrimaryColor = baseDefense.PrimaryColor,
            SecondaryColor = baseDefense.SecondaryColor,
            Roster = baseDefense.Roster,
            OverallRating = baseDefense.OverallRating * stageMult,
            SpeedMultiplier = baseDefense.SpeedMultiplier * stageMult,
            InterceptionAbility = baseDefense.InterceptionAbility * MathF.Sqrt(stageMult),
            TackleAbility = baseDefense.TackleAbility * stageMult,
            CoverageTightness = baseDefense.CoverageTightness * MathF.Sqrt(stageMult),
            PassRushAbility = baseDefense.PassRushAbility * rushMult,
            BlitzFrequency = baseDefense.BlitzFrequency * rushMult,
            BlitzSlotMultipliers = baseDefense.BlitzSlotMultipliers,
            DlSpeed = (baseDefense.DlSpeed > 0 ? baseDefense.DlSpeed : Constants.DlSpeed) * rushMult,
            DeSpeed = (baseDefense.DeSpeed > 0 ? baseDefense.DeSpeed : Constants.DeSpeed) * rushMult,
            LbSpeed = (baseDefense.LbSpeed > 0 ? baseDefense.LbSpeed : Constants.LbSpeed) * stageMult,
            DbSpeed = (baseDefense.DbSpeed > 0 ? baseDefense.DbSpeed : Constants.DbSpeed) * stageMult
        };

        SetDefensiveTeam(scaledDefense);
    }

    /// <summary>
    /// Advances to the next season stage and starts a fresh game for that stage.
    /// </summary>
    private void AdvanceToNextStage()
    {
        var nextStage = _currentStage.GetNextStage();
        if (nextStage.HasValue)
        {
            _currentStage = nextStage.Value;
            InitializeGame();
            _stateManager.SetState(GameState.PreSnap);
        }
    }

    private void ResetPlayState()
    {
        _lastPlayText = string.Empty;
        _driveOverText = string.Empty;
        _driveSummaryScrollOffsetFromLatest = 0;
        _playOverTimer = 0f;
        _playOverDuration = 1.25f;
        _manualPlaySelection = false;
        _autoPlaySelectionDone = false;
        _crowdMomentum = 0f;
        _crowdSurge = 0f;
        _crowdCarryoverBuzz = 0f;
        _crowdCarryoverMomentum = 0f;
        _liveCrowdMomentum = 0f;
        _liveCrowdSurge = 0f;
        _homeCrowdChant = string.Empty;
        _homeCrowdChantTimer = 0f;
        _homeCrowdChantDuration = 0f;
        _homeCrowdChantWorldPosition = new Vector2(Constants.FieldWidth * 0.5f, Constants.EndZoneDepth + 50f);
    }

    private void SetupEntities()
    {
        var context = new DefensiveContext(
            _playManager.LineOfScrimmage,
            _playManager.Distance,
            _playManager.Down,
            _playManager.Score,
            _playManager.AwayScore,
            _currentStage);

        // Single decision point: coordinator handles situation + stage + memory
        var call = _defensiveCoordinator.Decide(context, _defensiveTeam, _sessionRng);

        var result = _playSetupController.SetupPlay(
            _playManager.SelectedPlay,
            context,
            call,
            _offensiveTeam,
            _defensiveTeam);

        _entities.LoadFrom(result);
        _isZoneCoverage = result.IsZoneCoverage;
        _coverageScheme = result.CoverageScheme;
        _blitzers = result.Blitzers;

        // Reset controllers for new play
        _ballController.Reset(_playManager.LineOfScrimmage);
        _tackleController.Reset();
        _receiverPriorityManager.AssignPriorities(_entities.Receivers);
        _playManager.SelectedReceiver = _receiverPriorityManager.GetFirstReceiverIndex();
    }

    public void Update(float dt)
    {
        Constants.UpdateFieldRect();

        if (CanRestartCurrentSession() && _input.IsRestartPressed())
        {
            HandleRestart();
            return;
        }

        if (_input.IsEscapePressed())
        {
            _stateManager.TogglePause();
        }

        if (_stateManager.IsPaused)
        {
            return;
        }

        _drawingController.UpdateFireworks(dt);
        UpdateCrowdEnergy(dt);
        UpdateDriveSummaryScroll();

        if (_stateManager.State == GameState.PlayOver)
        {
            _playOverTimer += dt;
        }

        switch (_stateManager.State)
        {
            case GameState.MainMenu:
                HandleMainMenu();
                break;
            case GameState.PlayerNameEntry:
                HandlePlayerNameEntry();
                break;
            case GameState.NameConflict:
                HandleNameConflict();
                break;
            case GameState.PreSnap:
                HandlePreSnap();
                break;
            case GameState.PlayActive:
                HandlePlayActive(dt);
                break;
            case GameState.Replay:
                HandleReplay(dt);
                break;
            case GameState.PlayOver:
                HandlePlayOver();
                break;
            case GameState.DriveOver:
                HandleDriveOver();
                break;
            case GameState.StageComplete:
                HandleStageComplete();
                break;
            case GameState.GameOver:
                HandleGameOver();
                break;
        }
    }

    private void HandleRestart()
    {
        _currentStage = SeasonStage.RegularSeason;
        _statsTracker.Reset();
        _seasonSummary.Reset();
        _isPostSeasonNameEntry = false;
        _nameInput = string.Empty;
        _pendingPlayerName = string.Empty;
        _nameEntryMessage = string.Empty;
        _leaderboardSummary = LeaderboardSummary.Empty;
        _driveOverText = string.Empty;
        _lastPlayText = string.Empty;
        _driveSummaryScrollOffsetFromLatest = 0;
        _stateManager.ClearPause();
        _replayPlayer.Unload();
        _replayClipStore.Clear();
        _drawingController.Fireworks.Clear();
        InitializeGame();
        _stateManager.SetState(GameState.PreSnap);
    }

    private void HandleMainMenu()
    {
        _leaderboardSummary = BuildMenuLeaderboardSummary();
        bool confirmed = _menuController.UpdateMainMenu();
        _selectedTeamIndex = _menuController.SelectedTeamIndex;

        if (confirmed)
        {
            var teams = OffensiveTeamPresets.All;
            if (_selectedTeamIndex < 0 || _selectedTeamIndex >= teams.Count)
            {
                _selectedTeamIndex = 0;
            }

            _menuController.CloseLeaderboard();
            SetOffensiveTeam(teams[_selectedTeamIndex]);
            StartSeasonFromMenu();
        }
    }

    private void HandlePlayerNameEntry()
    {
        string typedText = _input.ReadTextInput(InputManager.MaxPlayerNameLength);
        if (!string.IsNullOrEmpty(typedText) && _nameInput.Length < InputManager.MaxPlayerNameLength)
        {
            int available = InputManager.MaxPlayerNameLength - _nameInput.Length;
            if (typedText.Length > available)
            {
                typedText = typedText[..available];
            }

            _nameInput += typedText;
        }

        if (_input.IsBackspacePressed() && _nameInput.Length > 0)
        {
            _nameInput = _nameInput[..^1];
        }

        if (!_input.IsEnterPressed())
        {
            return;
        }

        string normalizedName = PlayerRecordStore.NormalizeName(_nameInput);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            _nameEntryMessage = "Enter a player name first.";
            return;
        }

        if (_isPostSeasonNameEntry)
        {
            _playerName = normalizedName;
            _nameInput = normalizedName;
            _pendingPlayerName = string.Empty;
            _nameEntryMessage = string.Empty;
            _isPostSeasonNameEntry = false;
            _leaderboardSummary = SaveSeasonLeaderboard();
            if (_leaderboardSummary.IsOnPodium)
            {
                _drawingController.Fireworks.Trigger(_leaderboardSummary.IsFirstPlace ? 4.2f : 3.2f);
            }

            ResetPlayState();
            _drawingController.Fireworks.Clear();
            _currentStage = SeasonStage.RegularSeason;
            _seasonSummary.Reset();
            _stateManager.SetState(GameState.MainMenu);
            _menuController.OpenLeaderboard();
            return;
        }

        StartSeasonForPlayer(normalizedName);
    }

    private void HandleNameConflict()
    {
        int? choice = _input.GetNameConflictChoice();
        if (!choice.HasValue)
        {
            return;
        }

        if (choice.Value == 1)
        {
            StartSeasonForPlayer(_pendingPlayerName);
            return;
        }

        _nameInput = _pendingPlayerName;
        _pendingPlayerName = string.Empty;
        _nameEntryMessage = "That name already exists. Change it or press 1 next time.";
        _stateManager.SetState(GameState.PlayerNameEntry);
    }

    private void HandlePreSnap()
    {
        if (TryEnterReplayFromState(GameState.PreSnap))
        {
            return;
        }

        bool playChanged = false;
        
        // Check for pass play selection (1-9, 0)
        int? passSelection = _input.GetPassPlaySelection();
        if (passSelection.HasValue)
        {
            playChanged = _playManager.SelectPassPlay(passSelection.Value, new Random());
            _manualPlaySelection = true;
        }

        // Check for run play selection (Q-P)
        int? runSelection = _input.GetRunPlaySelection();
        if (runSelection.HasValue)
        {
            playChanged = _playManager.SelectRunPlay(runSelection.Value, new Random());
            _manualPlaySelection = true;
        }

        if (!passSelection.HasValue && !runSelection.HasValue && !_manualPlaySelection && !_autoPlaySelectionDone)
        {
            playChanged = _playManager.AutoSelectPlayBySituation(new Random()) || playChanged;
            _autoPlaySelectionDone = true;
        }

        if (playChanged)
        {
            SetupEntities();
        }

        if (_input.IsSpacePressed())
        {
            _replayClipStore.Clear();
            _replayPlayer.Unload();
            _playManager.StartPlay();
            _playManager.StartPlayRecord(_isZoneCoverage, _coverageScheme, _blitzers);
            _replayRecorder.Begin(_playManager.PlayNumber);
            _stateManager.SetState(GameState.PlayActive);
        }
    }

    private void HandlePlayActive(float dt)
    {
        _qbPastLos = _entities.Qb.Position.Y > _playManager.LineOfScrimmage + 0.1f && 
                     _entities.Ball.State == BallState.HeldByQB;

        // Try handoff on run plays
        _playExecutionController.TryHandoffToRunningBack(
            _playManager, _entities.Ball, _entities.Qb, _entities.Receivers);

        // Update all entities
        _playExecutionController.UpdatePlay(
            _entities.Qb,
            _entities.Ball,
            _entities.Receivers,
            _entities.Defenders,
            _entities.Blockers,
            _playManager,
            _qbPastLos,
            _isZoneCoverage,
            ClampToField,
            dt);

        // Resolve overlaps
        _overlapResolver.ResolveOverlaps(
            _entities.Qb,
            _entities.Ball,
            _entities.Receivers,
            _entities.Blockers,
            _entities.Defenders,
            _playManager.LineOfScrimmage,
            ClampToField);

        _replayRecorder.Capture(
            _entities.Qb,
            _entities.Ball,
            _entities.Receivers,
            _entities.Blockers,
            _entities.Defenders,
            _playManager.LineOfScrimmage,
            _playManager.FirstDownLine,
            dt);

        // Handle ball state
        HandleBall(dt);

        // Check for tackle or score
        CheckTackleOrScore();
    }

    private void HandleBall(float dt)
    {
        var result = _ballController.Update(
            _entities.Ball,
            _entities.Qb,
            _entities.Receivers,
            _entities.Defenders,
            _offensiveTeam,
            _defensiveTeam,
            dt);

        switch (result)
        {
            case BallUpdateResult.Incomplete:
                EndPlay(incomplete: true);
                return;
            case BallUpdateResult.Intercepted:
                EndPlay(intercepted: true);
                return;
        }

        // Handle throw input
        _ballController.HandleThrowInput(
            _entities.Ball,
            _entities.Qb,
            _entities.Receivers,
            _entities.Defenders,
            _playManager,
            _offensiveTeam,
            _qbPastLos,
            _input.GetThrowTarget());
    }

    private void CheckTackleOrScore()
    {
        var result = _tackleController.CheckTackleOrScore(
            _entities.Ball,
            _entities.Qb,
            _entities.Defenders,
            _offensiveTeam,
            ClampToField);

        switch (result)
        {
            case TackleCheckResult.Tackle:
                EndPlay(tackle: true);
                break;
            case TackleCheckResult.Touchdown:
                EndPlay(touchdown: true);
                break;
        }
    }

    private void HandlePlayOver()
    {
        if (TryEnterReplayFromState(GameState.PlayOver))
        {
            return;
        }

        if (_playOverTimer >= _playOverDuration)
        {
            _stateManager.SetState(GameState.PreSnap);
            SetupEntities();
            _lastPlayText = string.Empty;
            _manualPlaySelection = false;
            _autoPlaySelectionDone = false;
        }
    }

    private void HandleDriveOver()
    {
        if (TryEnterReplayFromState(GameState.DriveOver))
        {
            return;
        }

        if (_menuController.IsConfirmPressed())
        {
            InitializeDrive();
            _stateManager.SetState(GameState.PreSnap);
        }
    }

    private void HandleStageComplete()
    {
        if (TryEnterReplayFromState(GameState.StageComplete))
        {
            return;
        }

        if (_menuController.IsRestartPressed())
        {
            HandleRestart();
            return;
        }

        if (_menuController.IsConfirmPressed())
        {
            AdvanceToNextStage();
        }
    }

    private void HandleGameOver()
    {
        if (TryEnterReplayFromState(GameState.GameOver))
        {
            return;
        }

        if (_menuController.IsRestartPressed())
        {
            HandleRestart();
            return;
        }

        if (_menuController.IsConfirmPressed())
        {
            ResetPlayState();
            _drawingController.Fireworks.Clear();
            _isPostSeasonNameEntry = false;
            _nameInput = string.Empty;
            _pendingPlayerName = string.Empty;
            _nameEntryMessage = string.Empty;
            _currentStage = SeasonStage.RegularSeason;
            _seasonSummary.Reset();
            _leaderboardSummary = LeaderboardSummary.Empty;
            _stateManager.SetState(GameState.MainMenu);
        }
    }

    private void HandleReplay(float dt)
    {
        _replayStateHandler.Update(dt, _input, _replayPlayer, _stateManager);
    }

    private void EndPlay(bool tackle = false, bool incomplete = false, bool intercepted = false, bool touchdown = false)
    {
        if (_ballController.PassAttemptedThisPlay && intercepted)
        {
            _statsTracker.RecordInterception();
        }

        float gain = 0f;
        bool wasRun = false;
        bool isSack = false;
        int sackYardsLost = 0;
        string? catcherLabel = null;
        RouteType? catcherRoute = null;

        if (!incomplete && !intercepted && _entities.Ball.Holder != null)
        {
            gain = MathF.Round(_entities.Ball.Holder.Position.Y - _ballController.PlayStartLos);
            if (_ballController.PassCompletedThisPlay && _ballController.PassCatcher != null)
            {
                _statsTracker.RecordPassYards(_ballController.PassCatcher.Slot, (int)gain, touchdown);
                catcherLabel = GetCatcherLabel(_ballController.PassCatcher);
                catcherRoute = _ballController.PassCatcher.Route;
            }

            if (_entities.Ball.Holder is Receiver ballCarrier && ballCarrier.IsRunningBack)
            {
                _statsTracker.RecordRushYards((int)gain, touchdown);
                wasRun = true;
            }
            else if (_entities.Ball.Holder is Quarterback)
            {
                if (tackle && gain < 0f)
                {
                    isSack = true;
                    sackYardsLost = (int)MathF.Abs(gain);
                    _statsTracker.RecordSack(sackYardsLost);
                }
                else
                {
                    _statsTracker.RecordQbRushYards((int)gain, touchdown);
                    wasRun = true;
                }
            }
        }

        float spot = _playManager.LineOfScrimmage;
        if (tackle && _entities.Ball.Holder != null)
        {
            spot = _entities.Ball.Holder.Position.Y;
        }
        else if (touchdown)
        {
            spot = Constants.EndZoneDepth + 100f;
        }

        PlayOutcome outcome = touchdown ? PlayOutcome.Touchdown :
                              intercepted ? PlayOutcome.Interception :
                              incomplete ? PlayOutcome.Incomplete :
                              PlayOutcome.Tackle;

        ReplayClip? clip = _replayRecorder.FinalizeClip(outcome);
        _replayClipStore.Store(clip);

        _playManager.FinalizePlayRecord(outcome, gain, catcherLabel, catcherRoute, wasRun, isSack, sackYardsLost);

        // Feed the result to defensive memory so it learns for future plays
        var lastRecord = _playManager.PlayRecords.LastOrDefault();
        if (lastRecord != null)
        {
            _seasonSummary.RecordPlay(lastRecord);
            _defensiveMemory.RecordOutcome(lastRecord);
        }

        if (touchdown)
        {
            _drawingController.Fireworks.Trigger();
            _drawingController.ScreenEffects.TriggerShake(5f, 0.15f);
            _drawingController.ScreenEffects.TriggerFlash(new Raylib_cs.Color(255, 255, 255, 255), 60, 0.15f);
            _playOverDuration = 2.6f;
        }
        else if (intercepted)
        {
            _drawingController.ScreenEffects.TriggerShake(6f, 0.18f);
            _drawingController.ScreenEffects.TriggerFlash(new Raylib_cs.Color(220, 70, 60, 255), 45, 0.12f);
            _playOverDuration = 1.25f;
        }
        else if (tackle)
        {
            bool isQbHit = _entities.Ball.State == BallState.HeldByQB || _entities.Ball.Holder is Quarterback;
            bool isBigPlay = gain >= 20f;

            if (isQbHit)
            {
                _drawingController.ScreenEffects.TriggerShake(5f, 0.15f);
            }
            else if (isBigPlay)
            {
                _drawingController.ScreenEffects.TriggerShake(4f, 0.12f);
            }

            _playOverDuration = 1.25f;
        }
        else
        {
            _playOverDuration = 1.25f;
        }

        string? tackleMessageOverride = isSack ? $"SACK! -{sackYardsLost} yds" : null;
        Vector2 playEndPosition = GetPlayEndPosition();
        PlayResult result = _playManager.ResolvePlay(spot, incomplete, intercepted, touchdown, tackleMessageOverride);
        _lastPlayText = result.Message;
        _playOverTimer = 0f;
        ApplyCrowdReaction(result, gain, isSack, playEndPosition);

        if (_playManager.Score >= WinningScore || _playManager.AwayScore >= WinningScore)
        {
            // Record the game result for the season summary
            var snap = _statsTracker.BuildSnapshot();
            _seasonSummary.RecordGame(_currentStage, _playManager.Score, _playManager.AwayScore, snap);

            if (_playManager.Score >= WinningScore)
            {
                // Player won this stage
                var nextStage = _currentStage.GetNextStage();
                if (nextStage.HasValue)
                {
                    _driveOverText = $"{_currentStage.GetDisplayName()} WON!";
                    _stateManager.SetState(GameState.StageComplete);
                }
                else
                {
                    _driveOverText = "CHAMPION!";
                    BeginPlayerNameEntry(postSeasonSaveMode: true);
                }
            }
            else
            {
                _driveOverText = $"ELIMINATED IN {_currentStage.GetDisplayName()}!";
                BeginPlayerNameEntry(postSeasonSaveMode: true);
            }
            return;
        }

        if (result.Outcome is PlayOutcome.Touchdown or PlayOutcome.Interception or PlayOutcome.Turnover)
        {
            _driveOverText = result.Message;
            _stateManager.SetState(GameState.DriveOver);
        }
        else
        {
            _stateManager.SetState(GameState.PlayOver);
        }
    }

    private string GetCatcherLabel(Receiver catcher)
    {
        return catcher.Slot.GetLabel();
    }

    public void Draw()
    {
        _drawingController.SetStatsSnapshot(BuildStatsSnapshot());
        bool replayAvailable = _replayClipStore.HasClip;

        if (_stateManager.State == GameState.Replay && _replayPlayer.CurrentFrame is ReplayFrame replayFrame)
        {
            _drawingController.DrawReplay(
                _playManager,
                replayFrame,
                _lastPlayText,
                _driveOverText,
                _driveSummaryScrollOffsetFromLatest,
                _offensiveTeam,
                _defensiveTeam,
                _selectedTeamIndex,
                _playerName,
                _nameInput,
                _pendingPlayerName,
                _nameEntryMessage,
                _leaderboardSummary,
                _stateManager.IsPaused,
                _currentStage,
                _seasonSummary,
                replayAvailable,
                BuildCrowdBackdropState());
            return;
        }

        _drawingController.Draw(
            _playManager,
            _entities.Qb,
            _entities.Ball,
            _entities.Receivers,
            _entities.Blockers,
            _entities.Defenders,
            _stateManager.State,
            _lastPlayText,
            _driveOverText,
            _driveSummaryScrollOffsetFromLatest,
            _offensiveTeam,
            _defensiveTeam,
            _selectedTeamIndex,
            _playerName,
            _nameInput,
            _pendingPlayerName,
            _nameEntryMessage,
            _isPostSeasonNameEntry,
            _leaderboardSummary,
            _menuController.ShowLeaderboard,
            _stateManager.IsPaused,
            _currentStage,
            _seasonSummary,
            replayAvailable,
            BuildCrowdBackdropState());
    }

    public void Dispose()
    {
    }

    private void UpdateDriveSummaryScroll()
    {
        if (_stateManager.State is GameState.MainMenu or GameState.PlayerNameEntry or GameState.NameConflict)
        {
            return;
        }

        int maxOffset = Math.Max(0, _playManager.PlayRecords.Count - 1);
        _driveSummaryScrollOffsetFromLatest = Math.Clamp(_driveSummaryScrollOffsetFromLatest, 0, maxOffset);

        int scrollDelta = 0;
        float wheelMove = _input.GetMouseWheelMove();
        if (wheelMove > 0.1f)
        {
            scrollDelta++;
        }
        else if (wheelMove < -0.1f)
        {
            scrollDelta--;
        }

        if (_input.IsDriveSummaryScrollOlderPressed())
        {
            scrollDelta++;
        }

        if (_input.IsDriveSummaryScrollNewerPressed())
        {
            scrollDelta--;
        }

        if (scrollDelta == 0)
        {
            return;
        }

        _driveSummaryScrollOffsetFromLatest = Math.Clamp(_driveSummaryScrollOffsetFromLatest + scrollDelta, 0, maxOffset);
    }

    private void UpdateCrowdEnergy(float dt)
    {
        UpdateLiveCrowdState();

        float situationalBuzzFloor = GetSituationalCrowdFloor();
        float buzzDecay = _crowdCarryoverBuzz > situationalBuzzFloor ? dt * 0.05f : dt * 0.18f;
        _crowdCarryoverBuzz = MoveTowards(_crowdCarryoverBuzz, situationalBuzzFloor, buzzDecay);
        _crowdCarryoverMomentum = MoveTowards(_crowdCarryoverMomentum, 0f, dt * 0.07f);
        _crowdMomentum = MoveTowards(_crowdMomentum, _crowdCarryoverMomentum, dt * 0.16f);
        _crowdSurge = MoveTowards(_crowdSurge, _crowdCarryoverBuzz, dt * 0.12f);

        if (_homeCrowdChantTimer > 0f)
        {
            _homeCrowdChantTimer = MathF.Max(0f, _homeCrowdChantTimer - dt);
            if (_homeCrowdChantTimer <= 0f)
            {
                _homeCrowdChant = string.Empty;
                _homeCrowdChantDuration = 0f;
            }
        }
    }

    private void ApplyCrowdReaction(PlayResult result, float gain, bool isSack, Vector2 playEndPosition)
    {
        float offenseSwing = result.Outcome switch
        {
            PlayOutcome.Touchdown => 1.0f,
            PlayOutcome.Interception => -1.0f,
            PlayOutcome.Turnover => -0.85f,
            PlayOutcome.Incomplete => -0.28f,
            _ => isSack ? -0.78f : Math.Clamp(gain / 22f, -0.7f, 0.8f)
        };

        float highlightLevel = result.Outcome switch
        {
            PlayOutcome.Touchdown => 1.0f,
            PlayOutcome.Interception => 0.92f,
            PlayOutcome.Turnover => 0.85f,
            _ => Math.Clamp(MathF.Abs(gain) / 20f, 0.15f, isSack ? 0.82f : 0.7f)
        };

        float endZoneBoost = GetEndZoneExcitement(playEndPosition.Y);
        float carryoverStrength = Math.Clamp(
            (highlightLevel * (offenseSwing >= 0f ? 0.85f : 0.58f)) +
            (endZoneBoost * (offenseSwing >= 0f ? 0.6f : 0.25f)),
            0f,
            1f);

        _crowdMomentum = Math.Clamp((_crowdMomentum * 0.35f) + offenseSwing, -1f, 1f);
        _crowdCarryoverMomentum = Math.Clamp((_crowdCarryoverMomentum * 0.45f) + (offenseSwing * (0.55f + (endZoneBoost * 0.35f))), -1f, 1f);
        _crowdCarryoverBuzz = Math.Clamp(MathF.Max(_crowdCarryoverBuzz, carryoverStrength), 0f, 1f);
        _crowdSurge = Math.Clamp(MathF.Max(_crowdSurge, highlightLevel + (endZoneBoost * 0.18f)), 0f, 1f);
        TriggerHomeCrowdChant(result, gain, isSack, highlightLevel, offenseSwing, playEndPosition);
    }

    private CrowdBackdropState BuildCrowdBackdropState()
    {
        float stageEnergy = _currentStage switch
        {
            SeasonStage.RegularSeason => 0.18f,
            SeasonStage.Playoff => 0.36f,
            SeasonStage.SuperBowl => 0.58f,
            _ => 0.18f
        };

        float scoreEnergy = Math.Clamp((_playManager.Score + _playManager.AwayScore) / 28f, 0f, 0.24f);
        float playProgressEnergy = Math.Clamp((_playManager.PlayNumber - 1) / 14f, 0f, 0.12f);
        float closeGameEnergy = Math.Abs(_playManager.Score - _playManager.AwayScore) switch
        {
            0 => 0.12f,
            <= 7 => 0.08f,
            _ => 0f
        };

        float effectiveSurge = Math.Clamp(MathF.Max(_crowdSurge, _liveCrowdSurge) + (_crowdCarryoverBuzz * 0.16f), 0f, 1f);
        float effectiveMomentum = Math.Clamp(_crowdMomentum + _liveCrowdMomentum, -1f, 1f);

        float overall = Math.Clamp(stageEnergy + scoreEnergy + playProgressEnergy + closeGameEnergy + (_crowdCarryoverBuzz * 0.12f) + (effectiveSurge * 0.32f), 0f, 1f);
        float homeEnergy = Math.Clamp(0.18f + (overall * 0.45f) + MathF.Max(0f, effectiveMomentum) * 0.6f - MathF.Max(0f, -effectiveMomentum) * 0.2f, 0f, 1f);
        float awayEnergy = Math.Clamp(0.18f + (overall * 0.45f) + MathF.Max(0f, -effectiveMomentum) * 0.6f - MathF.Max(0f, effectiveMomentum) * 0.2f, 0f, 1f);

        float chantStrength = 0f;
        if (_homeCrowdChantTimer > 0f && _homeCrowdChantDuration > 0f)
        {
            chantStrength = Math.Clamp(_homeCrowdChantTimer / _homeCrowdChantDuration, 0f, 1f);
        }

        float chantFieldX = Math.Clamp(_homeCrowdChantWorldPosition.X / Constants.FieldWidth, 0f, 1f);
        float chantFieldY = Math.Clamp((_homeCrowdChantWorldPosition.Y - Constants.EndZoneDepth) / 100f, 0f, 1f);

        return new CrowdBackdropState(homeEnergy, awayEnergy, overall, _homeCrowdChant, chantStrength, chantFieldX, chantFieldY);
    }

    private void TriggerHomeCrowdChant(PlayResult result, float gain, bool isSack, float highlightLevel, float offenseSwing, Vector2 playEndPosition)
    {
        string chant = SelectHomeCrowdChant(result, gain, isSack, offenseSwing);
        if (string.IsNullOrWhiteSpace(chant))
        {
            return;
        }

        _homeCrowdChant = chant;
        _homeCrowdChantDuration = 2.2f + (highlightLevel * 1.2f);
        _homeCrowdChantTimer = _homeCrowdChantDuration;
        _homeCrowdChantWorldPosition = playEndPosition;
    }

    private void UpdateLiveCrowdState()
    {
        _liveCrowdMomentum = 0f;
        _liveCrowdSurge = 0f;

        if (_stateManager.State != GameState.PlayActive)
        {
            return;
        }

        float playLos = _ballController.PlayStartLos > 0f ? _ballController.PlayStartLos : _playManager.LineOfScrimmage;
        float endZonePulse = 0f;

        switch (_entities.Ball.State)
        {
            case BallState.HeldByReceiver when _entities.Ball.Holder != null:
            {
                float liveGain = _entities.Ball.Holder.Position.Y - playLos;
                endZonePulse = GetEndZoneExcitement(_entities.Ball.Holder.Position.Y);
                _liveCrowdMomentum = Math.Clamp(liveGain / 18f, 0f, 0.95f);
                _liveCrowdSurge = Math.Clamp((liveGain / 24f) + (endZonePulse * 0.35f), 0f, 1f);
                break;
            }

            case BallState.HeldByQB:
            {
                float scrambleGain = _entities.Qb.Position.Y - playLos;
                if (_qbPastLos || scrambleGain >= 6f)
                {
                    endZonePulse = GetEndZoneExcitement(_entities.Qb.Position.Y);
                    _liveCrowdMomentum = Math.Clamp(scrambleGain / 22f, 0f, 0.72f);
                    _liveCrowdSurge = Math.Clamp((scrambleGain / 28f) + (endZonePulse * 0.22f), 0f, 0.82f);
                }
                break;
            }

            case BallState.InAir when _ballController.PassAttemptedThisPlay:
            {
                float airGain = _entities.Ball.Position.Y - playLos;
                if (airGain >= 10f)
                {
                    float flightProgress = _entities.Ball.GetFlightProgress();
                    endZonePulse = GetEndZoneExcitement(_entities.Ball.Position.Y);
                    _liveCrowdMomentum = Math.Clamp(airGain / 30f, 0f, 0.8f);
                    _liveCrowdSurge = Math.Clamp((airGain / 36f) + (flightProgress * 0.28f) + (endZonePulse * 0.2f), 0f, 0.92f);
                }
                break;
            }
        }

        if (_liveCrowdSurge > 0f)
        {
            _liveCrowdSurge = Math.Clamp(_liveCrowdSurge + (endZonePulse * 0.1f), 0f, 1f);
        }
    }

    private float GetSituationalCrowdFloor()
    {
        float redZoneBuzz = Math.Clamp((_playManager.LineOfScrimmage - (FieldGeometry.OpponentGoalLine - 20f)) / 20f, 0f, 1f) * 0.34f;
        float goalToGoBuzz = Math.Clamp((_playManager.LineOfScrimmage - (FieldGeometry.OpponentGoalLine - 8f)) / 8f, 0f, 1f) * 0.24f;
        float scorePressureBuzz = (_playManager.Score >= 14 || _playManager.AwayScore >= 14) ? 0.05f : 0f;
        return Math.Clamp(redZoneBuzz + goalToGoBuzz + scorePressureBuzz, 0f, 0.6f);
    }

    private static float GetEndZoneExcitement(float worldY)
    {
        return Math.Clamp((worldY - (FieldGeometry.OpponentGoalLine - 20f)) / 20f, 0f, 1f);
    }

    private string SelectHomeCrowdChant(PlayResult result, float gain, bool isSack, float offenseSwing)
    {
        int variant = (_playManager.PlayNumber + _playManager.Score + _playManager.AwayScore) % 3;

        if (result.Outcome == PlayOutcome.Touchdown)
        {
            return variant switch
            {
                0 => "LET'S GO!",
                1 => "YEAH!",
                _ => "TOUCHDOWN!"
            };
        }

        if (result.Outcome == PlayOutcome.Interception || result.Outcome == PlayOutcome.Turnover)
        {
            return variant switch
            {
                0 => "BOOO!",
                1 => "UGH!",
                _ => "NOOO!"
            };
        }

        if (isSack)
        {
            return variant switch
            {
                0 => "BOOO!",
                1 => "WAKE UP!",
                _ => "COME ON!"
            };
        }

        if (gain >= 18f)
        {
            return variant switch
            {
                0 => "YAY!",
                1 => "LET'S GO!",
                _ => "BIG PLAY!"
            };
        }

        if (gain >= 8f || offenseSwing > 0.25f)
        {
            return variant switch
            {
                0 => "YEAH!",
                1 => "NICE!",
                _ => "LET'S GO!"
            };
        }

        if (result.Outcome == PlayOutcome.Incomplete)
        {
            return variant switch
            {
                0 => "AWW!",
                1 => "OOH!",
                _ => "COME ON!"
            };
        }

        if (gain <= -4f)
        {
            return variant switch
            {
                0 => "BOO!",
                1 => "UGH!",
                _ => "CMON!"
            };
        }

        return string.Empty;
    }

    private Vector2 GetPlayEndPosition()
    {
        Vector2 position = _entities.Ball.Holder?.Position ?? _entities.Ball.Position;

        return new Vector2(
            Math.Clamp(position.X, 0f, Constants.FieldWidth),
            Math.Clamp(position.Y, Constants.EndZoneDepth, Constants.EndZoneDepth + 100f));
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + (MathF.Sign(target - current) * maxDelta);
    }

    private bool TryEnterReplayFromState(GameState state)
    {
        if (!_input.IsReplayPressed())
        {
            return false;
        }

        return _replayStateHandler.TryEnterReplay(state, _replayClipStore, _replayPlayer, _stateManager);
    }

    private void ClampToField(Entity entity)
    {
        bool isCarrier = _entities.Ball.State switch
        {
            BallState.HeldByQB => entity == _entities.Qb,
            BallState.HeldByReceiver => entity == _entities.Ball.Holder,
            _ => false
        };

        float x = entity.Position.X;
        if (!isCarrier)
        {
            x = MathF.Max(0.5f, MathF.Min(Constants.FieldWidth - 0.5f, x));
        }

        float y = MathF.Max(0.5f, MathF.Min(Constants.FieldLength - 0.5f, entity.Position.Y));
        entity.Position = new Vector2(x, y);
    }

    private GameStatsSnapshot BuildStatsSnapshot()
    {
        return _statsTracker.BuildSnapshot();
    }

    private bool CanRestartCurrentSession()
    {
        return _stateManager.State is not GameState.MainMenu and not GameState.PlayerNameEntry and not GameState.NameConflict;
    }

    private void BeginPlayerNameEntry(bool postSeasonSaveMode)
    {
        _isPostSeasonNameEntry = postSeasonSaveMode;
        _nameInput = postSeasonSaveMode
            ? string.Empty
            : string.IsNullOrWhiteSpace(_playerName)
                ? string.Empty
                : _playerName;
        _pendingPlayerName = string.Empty;
        _nameEntryMessage = postSeasonSaveMode ? "Enter your name to save this season score." : string.Empty;
        _leaderboardSummary = BuildMenuLeaderboardSummary();
        _stateManager.SetState(GameState.PlayerNameEntry);
    }

    private LeaderboardSummary BuildMenuLeaderboardSummary()
    {
        return _playerRecordStore.BuildSummary(_playerName, 0f);
    }

    private void StartSeasonForPlayer(string playerName)
    {
        _isPostSeasonNameEntry = false;
        _playerName = playerName;
        _nameInput = playerName;
        _pendingPlayerName = string.Empty;
        _nameEntryMessage = string.Empty;
        _currentStage = SeasonStage.RegularSeason;
        _statsTracker.Reset();
        _seasonSummary.Reset();
        _leaderboardSummary = _playerRecordStore.BuildSummary(playerName, 0f);
        InitializeGame();
        _stateManager.SetState(GameState.PreSnap);
        _manualPlaySelection = false;
        _autoPlaySelectionDone = false;
    }

    private void StartSeasonFromMenu()
    {
        _isPostSeasonNameEntry = false;
        _playerName = string.Empty;
        _nameInput = string.Empty;
        _pendingPlayerName = string.Empty;
        _nameEntryMessage = string.Empty;
        _currentStage = SeasonStage.RegularSeason;
        _statsTracker.Reset();
        _seasonSummary.Reset();
        _leaderboardSummary = LeaderboardSummary.Empty;
        InitializeGame();
        _stateManager.SetState(GameState.PreSnap);
        _manualPlaySelection = false;
        _autoPlaySelectionDone = false;
    }

    private LeaderboardSummary SaveSeasonLeaderboard()
    {
        if (string.IsNullOrWhiteSpace(_playerName))
        {
            return LeaderboardSummary.Empty;
        }

        float dominanceScore = _seasonSummary.ComputeDominanceScore();
        string scoreHistory = _seasonSummary.BuildThreeStageScoreHistory();
        string scoreDetails = _seasonSummary.BuildDominanceScoreDetails();
        return _playerRecordStore.SaveSeasonResult(_playerName, _offensiveTeam.Name, scoreHistory, scoreDetails, dominanceScore);
    }
}
