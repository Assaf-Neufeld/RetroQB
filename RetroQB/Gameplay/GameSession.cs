using System.Numerics;
using Raylib_cs;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay.Controllers;
using RetroQB.Gameplay.Stats;
using RetroQB.Input;
using RetroQB.Rendering;

namespace RetroQB.Gameplay;

/// <summary>
/// Orchestrates the game session by coordinating specialized controllers.
/// Each controller handles a single responsibility following SOLID principles.
/// </summary>
public sealed class GameSession
{
    private const int WinningScore = 21;

    // Core managers
    private readonly GameStateManager _stateManager;
    private readonly PlayManager _playManager;
    private readonly IStatisticsTracker _statsTracker;

    // Controllers (Single Responsibility)
    private readonly PlaySetupController _playSetupController;
    private readonly PlayExecutionController _playExecutionController;
    private readonly BallController _ballController;
    private readonly TackleController _tackleController;
    private readonly OverlapResolver _overlapResolver;
    private readonly ReceiverPriorityManager _receiverPriorityManager;
    private readonly DrawingController _drawingController;

    // Team attributes
    private OffensiveTeamAttributes _offensiveTeam = OffensiveTeamAttributes.Default;
    private DefensiveTeamAttributes _defensiveTeam = DefensiveTeamAttributes.Default;

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
        new CollisionResolver(),
        new ThrowingMechanics())
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
        ICollisionResolver collisionResolver,
        IThrowingMechanics throwingMechanics)
    {
        _stateManager = stateManager;
        _playManager = playManager;
        _statsTracker = statsTracker;

        // Initialize controllers
        _overlapResolver = new OverlapResolver();
        _receiverPriorityManager = new ReceiverPriorityManager();
        
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
        _drawingController.Fireworks.Clear();
    }

    private void InitializeGame()
    {
        _statsTracker.Reset();
        _playManager.StartNewGame();
        SetupEntities();
        ResetPlayState();
        _drawingController.Fireworks.Clear();
    }

    private void ResetPlayState()
    {
        _lastPlayText = string.Empty;
        _driveOverText = string.Empty;
        _playOverTimer = 0f;
        _playOverDuration = 1.25f;
        _manualPlaySelection = false;
        _autoPlaySelectionDone = false;
    }

    private void SetupEntities()
    {
        var result = _playSetupController.SetupPlay(
            _playManager.SelectedPlay,
            _playManager.LineOfScrimmage,
            _playManager.Distance,
            _offensiveTeam,
            _defensiveTeam);

        _entities.LoadFrom(result);
        _isZoneCoverage = result.IsZoneCoverage;
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

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
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
            case GameState.PlayActive:
                HandlePlayActive(dt);
                break;
            case GameState.PlayOver:
                HandlePlayOver();
                break;
            case GameState.DriveOver:
                HandleDriveOver();
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
        if (Raylib.IsKeyPressed(KeyboardKey.One)) _selectedTeamIndex = 0;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) _selectedTeamIndex = 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) _selectedTeamIndex = 2;

        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            var teams = OffensiveTeamPresets.All;
            if (_selectedTeamIndex < 0 || _selectedTeamIndex >= teams.Count)
            {
                _selectedTeamIndex = 0;
            }
            SetOffensiveTeam(teams[_selectedTeamIndex]);
            InitializeGame();
            _stateManager.SetState(GameState.PreSnap);
            _manualPlaySelection = false;
            _autoPlaySelectionDone = false;
        }
    }

    private void HandlePreSnap()
    {
        bool playChanged = false;
        
        // Check for pass play selection (1-9, 0)
        int? passSelection = GetPassPlaySelection();
        if (passSelection.HasValue)
        {
            playChanged = _playManager.SelectPassPlay(passSelection.Value, new Random());
            _manualPlaySelection = true;
        }

        // Check for run play selection (Q-P)
        int? runSelection = GetRunPlaySelection();
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

        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            _playManager.StartPlay();
            _playManager.StartPlayRecord(_isZoneCoverage, _blitzers);
            _stateManager.SetState(GameState.PlayActive);
        }
    }

    /// <summary>
    /// Returns pass play index (0-9) from number keys.
    /// Keys 1-9 map to indices 0-8, key 0 maps to index 9 (wildcard).
    /// </summary>
    private static int? GetPassPlaySelection()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.One)) return 0;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) return 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) return 2;
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) return 3;
        if (Raylib.IsKeyPressed(KeyboardKey.Five)) return 4;
        if (Raylib.IsKeyPressed(KeyboardKey.Six)) return 5;
        if (Raylib.IsKeyPressed(KeyboardKey.Seven)) return 6;
        if (Raylib.IsKeyPressed(KeyboardKey.Eight)) return 7;
        if (Raylib.IsKeyPressed(KeyboardKey.Nine)) return 8;
        if (Raylib.IsKeyPressed(KeyboardKey.Zero)) return 9;  // Wildcard
        return null;
    }

    /// <summary>
    /// Returns run play index (0-9) from Q-P keys.
    /// W=0, E=1, R=2, T=3, Y=4, U=5, I=6, O=7, P=8, Q=9 (wildcard)
    /// </summary>
    private static int? GetRunPlaySelection()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.W)) return 0;
        if (Raylib.IsKeyPressed(KeyboardKey.E)) return 1;
        if (Raylib.IsKeyPressed(KeyboardKey.R)) return 2;
        if (Raylib.IsKeyPressed(KeyboardKey.T)) return 3;
        if (Raylib.IsKeyPressed(KeyboardKey.Y)) return 4;
        if (Raylib.IsKeyPressed(KeyboardKey.U)) return 5;
        if (Raylib.IsKeyPressed(KeyboardKey.I)) return 6;
        if (Raylib.IsKeyPressed(KeyboardKey.O)) return 7;
        if (Raylib.IsKeyPressed(KeyboardKey.P)) return 8;
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) return 9;  // Wildcard
        return null;
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
            ClampToField);

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
            _qbPastLos);
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
        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            InitializeDrive();
            _stateManager.SetState(GameState.PreSnap);
        }
    }

    private void HandleGameOver()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            InitializeGame();
            _stateManager.SetState(GameState.PreSnap);
        }
    }

    private void EndPlay(bool tackle = false, bool incomplete = false, bool intercepted = false, bool touchdown = false)
    {
        if (_ballController.PassAttemptedThisPlay && intercepted)
        {
            _statsTracker.RecordInterception();
        }

        float gain = 0f;
        bool wasRun = false;
        string? catcherLabel = null;
        AI.RouteType? catcherRoute = null;

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
                _statsTracker.RecordQbRushYards((int)gain, touchdown);
                wasRun = true;
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

        _playManager.FinalizePlayRecord(outcome, gain, catcherLabel, catcherRoute, wasRun);

        if (touchdown)
        {
            _drawingController.Fireworks.Trigger();
            _playOverDuration = 2.6f;
        }
        else
        {
            _playOverDuration = 1.25f;
        }

        PlayResult result = _playManager.ResolvePlay(spot, incomplete, intercepted, touchdown);
        _lastPlayText = result.Message;
        _playOverTimer = 0f;

        if (_playManager.Score >= WinningScore || _playManager.AwayScore >= WinningScore)
        {
            _driveOverText = _playManager.Score >= WinningScore ? "HOME WINS!" : "AWAY WINS!";
            _stateManager.SetState(GameState.GameOver);
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
            _offensiveTeam,
            _selectedTeamIndex,
            _stateManager.IsPaused);
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
