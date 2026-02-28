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
public sealed class GameSession
{
    private const int WinningScore = Rules.WinningScore;
    private const float OpponentDriveIntroDuration = 1.0f;

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
    private readonly FieldGoalController _fieldGoalController;
    private readonly SimulatedDriveGenerator _simulatedDriveGenerator;
    private readonly SimulatedDriveController _simulatedDriveController;
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
    private bool _isTwoPointAttempt;
    private float _nextDriveStartYardLine = 20f;
    private float _pendingOpponentDriveWorldY = FieldGeometry.OpponentKickoffStartY;
    private bool _opponentDriveInitialized;
    private float _opponentDriveIntroTimer;
    private bool _pendingOpponentDriveAfterBanner;
    private float _pendingOpponentDriveAfterBannerStartY;
    private bool _isZoneCoverage;
    private CoverageScheme _coverageScheme;
    private List<string> _blitzers = new();
    private int _selectedTeamIndex;

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
        _fieldGoalController = new FieldGoalController();
        _simulatedDriveGenerator = new SimulatedDriveGenerator();
        _simulatedDriveController = new SimulatedDriveController();
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
        _playManager.StartNewDrive(_nextDriveStartYardLine);
        _nextDriveStartYardLine = 20f;
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
        _playOverTimer = 0f;
        _playOverDuration = 1.25f;
        _manualPlaySelection = false;
        _autoPlaySelectionDone = false;
        _isTwoPointAttempt = false;
        _opponentDriveInitialized = false;
        _opponentDriveIntroTimer = 0f;
        _pendingOpponentDriveWorldY = FieldGeometry.OpponentKickoffStartY;
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

        if (_input.IsEscapePressed())
        {
            _stateManager.TogglePause();
        }

        if (_stateManager.IsPaused)
        {
            return;
        }

        _drawingController.UpdateFireworks(dt);

        if (_stateManager.State == GameState.PlayOver)
        {
            _playOverTimer += dt;
        }

        switch (_stateManager.State)
        {
            case GameState.MainMenu:
                HandleMainMenu();
                break;
            case GameState.PreSnap:
                HandlePreSnap();
                break;
            case GameState.ExtraPoint:
                HandleExtraPoint();
                break;
            case GameState.FieldGoal:
                HandleFieldGoal(dt);
                break;
            case GameState.OpponentDrive:
                HandleOpponentDrive(dt);
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
        if (_stateManager.State == GameState.GameOver)
        {
            InitializeGame();
        }
        else
        {
            InitializeDrive();
        }
        _stateManager.SetState(GameState.PreSnap);
    }

    private void HandleMainMenu()
    {
        bool confirmed = _menuController.UpdateMainMenu();
        _selectedTeamIndex = _menuController.SelectedTeamIndex;

        if (confirmed)
        {
            var teams = OffensiveTeamPresets.All;
            if (_selectedTeamIndex < 0 || _selectedTeamIndex >= teams.Count)
            {
                _selectedTeamIndex = 0;
            }
            SetOffensiveTeam(teams[_selectedTeamIndex]);
            _currentStage = SeasonStage.RegularSeason;
            _statsTracker.Reset();
            _seasonSummary.Reset();
            InitializeGame();
            _stateManager.SetState(GameState.PreSnap);
            _manualPlaySelection = false;
            _autoPlaySelectionDone = false;
        }
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
            playChanged = _playManager.SelectPassPlay(passSelection.Value, _sessionRng);
            _manualPlaySelection = true;
        }

        // Check for run play selection (Q-P)
        int? runSelection = _input.GetRunPlaySelection();
        if (runSelection.HasValue)
        {
            playChanged = _playManager.SelectRunPlay(runSelection.Value, _sessionRng);
            _manualPlaySelection = true;
        }

        if (!passSelection.HasValue && !runSelection.HasValue && !_manualPlaySelection && !_autoPlaySelectionDone)
        {
            playChanged = _playManager.AutoSelectPlayBySituation(_sessionRng) || playChanged;
            _autoPlaySelectionDone = true;
        }

        if (playChanged)
        {
            SetupEntities();
        }

        if (!_isTwoPointAttempt && _input.IsFieldGoalPressed())
        {
            if (_playManager.IsFieldGoalRange())
            {
                _fieldGoalController.StartAttempt(_playManager.GetFieldGoalDistance());
                _stateManager.SetState(GameState.FieldGoal);
            }
            else
            {
                _lastPlayText = "FIELD GOAL OUT OF RANGE";
            }
            return;
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

    private void HandleExtraPoint()
    {
        if (_input.IsEnterPressed())
        {
            _lastPlayText = _playManager.ResolveExtraPoint();
            _driveOverText = "TOUCHDOWN DRIVE COMPLETE (+7)";

            if (CheckWinConditionAndSetState())
            {
                return;
            }

            _stateManager.SetState(GameState.DriveOver);
            return;
        }

        if (_input.IsSpacePressed())
        {
            _isTwoPointAttempt = true;
            _playManager.SetupTwoPointAttempt();
            SetupEntities();
            _lastPlayText = "2PT ATTEMPT";
            _manualPlaySelection = false;
            _autoPlaySelectionDone = false;
            _stateManager.SetState(GameState.PreSnap);
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

    private void HandleFieldGoal(float dt)
    {
        _fieldGoalController.Update(dt);

        if (_input.IsSpacePressed())
        {
            _fieldGoalController.Confirm();
        }

        if (!_fieldGoalController.TryConsumeResult(out FieldGoalKickResult kickResult))
        {
            return;
        }

        float kickDistance = _playManager.GetFieldGoalDistance();
        if (kickResult.IsGood)
        {
            _lastPlayText = _playManager.ResolveFieldGoalMade();
            _driveOverText = $"FIELD GOAL GOOD! ({kickDistance:F0} YDS) +3";

            _drawingController.Fireworks.Trigger();
            _drawingController.ScreenEffects.TriggerShake(4f, 0.12f);
            _drawingController.ScreenEffects.TriggerFlash(new Raylib_cs.Color(255, 255, 200, 255), 45, 0.12f);

            if (CheckWinConditionAndSetState())
            {
                return;
            }

            _pendingOpponentDriveAfterBanner = true;
            _pendingOpponentDriveAfterBannerStartY = FieldGeometry.OpponentKickoffStartY;
            _stateManager.SetState(GameState.DriveOver);
            return;
        }
        else
        {
            _lastPlayText = $"FIELD GOAL {kickResult.Message}";
            _driveOverText = $"FIELD GOAL {kickResult.Message}";
            float kickSpot = MathF.Max(FieldGeometry.EndZoneDepth + 20f, _playManager.LineOfScrimmage - 7f);
            _pendingOpponentDriveAfterBanner = true;
            _pendingOpponentDriveAfterBannerStartY = kickSpot;
            _stateManager.SetState(GameState.DriveOver);
            return;
        }
    }

    private void HandleOpponentDrive(float dt)
    {
        if (!_opponentDriveInitialized)
        {
            _opponentDriveIntroTimer += dt;
            _lastPlayText = "OPPONENT POSSESSION";

            bool skipIntro = _input.IsSpacePressed();
            if (!skipIntro && _opponentDriveIntroTimer < OpponentDriveIntroDuration)
            {
                return;
            }

            SimulatedDriveResult generated = _simulatedDriveGenerator.Generate(_pendingOpponentDriveWorldY, _currentStage, _sessionRng);
            _simulatedDriveController.Start(generated);
            _opponentDriveInitialized = true;
            _opponentDriveIntroTimer = 0f;
        }

        if (_input.IsSpacePressed())
        {
            _simulatedDriveController.SkipToEnd();
        }

        _simulatedDriveController.Update(dt);
        _lastPlayText = _simulatedDriveController.CurrentPlayText;

        if (!_simulatedDriveController.IsComplete || _simulatedDriveController.ActiveDrive == null)
        {
            return;
        }

        SimulatedDriveResult result = _simulatedDriveController.ActiveDrive;
        _playManager.AddOpponentScore(result.PointsScored);
        _driveOverText = _simulatedDriveController.ResultBanner;
        _nextDriveStartYardLine = FieldGeometry.GetYardLineDisplay(result.PlayerNextStartWorldY);

        if (CheckWinConditionAndSetState())
        {
            return;
        }

        _stateManager.SetState(GameState.DriveOver);
        _opponentDriveInitialized = false;
    }

    private void HandleDriveOver()
    {
        if (TryEnterReplayFromState(GameState.DriveOver))
        {
            return;
        }

        if (_menuController.IsConfirmPressed())
        {
            if (_pendingOpponentDriveAfterBanner)
            {
                _pendingOpponentDriveAfterBanner = false;
                StartOpponentDrive(_pendingOpponentDriveAfterBannerStartY);
            }
            else
            {
                InitializeDrive();
                _stateManager.SetState(GameState.PreSnap);
            }
        }
    }

    private void HandleStageComplete()
    {
        if (TryEnterReplayFromState(GameState.StageComplete))
        {
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

        if (_menuController.IsConfirmPressed())
        {
            ResetPlayState();
            _drawingController.Fireworks.Clear();
            _currentStage = SeasonStage.RegularSeason;
            _seasonSummary.Reset();
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
            _defensiveMemory.RecordOutcome(lastRecord);
        }

        if (_isTwoPointAttempt)
        {
            bool conversionGood = touchdown;
            _lastPlayText = _playManager.ResolveTwoPointConversion(conversionGood);
            _playOverTimer = 0f;
            _isTwoPointAttempt = false;

            if (CheckWinConditionAndSetState())
            {
                return;
            }

            StartOpponentDrive(FieldGeometry.OpponentKickoffStartY);
            return;
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
        PlayResult result = _playManager.ResolvePlay(spot, incomplete, intercepted, touchdown, tackleMessageOverride);
        _lastPlayText = result.Message;
        _playOverTimer = 0f;

        if (result.Outcome == PlayOutcome.Touchdown)
        {
            _stateManager.SetState(GameState.ExtraPoint);
            return;
        }

        if (CheckWinConditionAndSetState())
        {
            return;
        }

        if (result.Outcome is PlayOutcome.Interception or PlayOutcome.Turnover)
        {
            _driveOverText = result.Message;
            _pendingOpponentDriveAfterBanner = true;

            if (result.Outcome == PlayOutcome.Interception)
            {
                float interceptionSpot = _entities.Ball.Holder?.Position.Y ?? _playManager.LineOfScrimmage;
                _pendingOpponentDriveAfterBannerStartY = interceptionSpot;
            }
            else
            {
                _pendingOpponentDriveAfterBannerStartY = _playManager.LineOfScrimmage;
            }

            _stateManager.SetState(GameState.DriveOver);
        }
        else
        {
            _stateManager.SetState(GameState.PlayOver);
        }
    }

    private bool CheckWinConditionAndSetState()
    {
        if (_playManager.Score < WinningScore && _playManager.AwayScore < WinningScore)
        {
            return false;
        }

        var snap = _statsTracker.BuildSnapshot();
        _seasonSummary.RecordGame(_currentStage, _playManager.Score, _playManager.AwayScore, snap.Qb);

        if (_playManager.Score >= WinningScore)
        {
            var nextStage = _currentStage.GetNextStage();
            if (nextStage.HasValue)
            {
                _driveOverText = $"{_currentStage.GetDisplayName()} WON!";
                _stateManager.SetState(GameState.StageComplete);
            }
            else
            {
                _driveOverText = "CHAMPION!";
                _stateManager.SetState(GameState.GameOver);
            }
        }
        else
        {
            _driveOverText = $"ELIMINATED IN {_currentStage.GetDisplayName()}!";
            _stateManager.SetState(GameState.GameOver);
        }

        return true;
    }

    private void StartOpponentDrive(float opponentStartWorldY)
    {
        _pendingOpponentDriveWorldY = opponentStartWorldY;
        _opponentDriveInitialized = false;
        _opponentDriveIntroTimer = 0f;
        _stateManager.SetState(GameState.OpponentDrive);
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
                _offensiveTeam,
                _defensiveTeam,
                _selectedTeamIndex,
                _stateManager.IsPaused,
                _currentStage,
                _seasonSummary,
                replayAvailable);
            return;
        }

        _drawingController.Draw(
            _playManager,
            _entities.Qb,
            _entities.Ball,
            _entities.Receivers,
            _entities.Blockers,
            _entities.Defenders,
            _fieldGoalController,
            _simulatedDriveController,
            _stateManager.State == GameState.OpponentDrive && !_opponentDriveInitialized,
            _stateManager.State,
            _lastPlayText,
            _driveOverText,
            _offensiveTeam,
            _defensiveTeam,
            _selectedTeamIndex,
            _stateManager.IsPaused,
            _currentStage,
            _seasonSummary,
            replayAvailable);
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
}
