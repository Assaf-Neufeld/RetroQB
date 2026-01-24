using System.Numerics;
using System.Linq;
using Raylib_cs;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay.Stats;
using RetroQB.Input;
using RetroQB.Rendering;

namespace RetroQB.Gameplay;

public sealed class GameSession
{
    // Injected dependencies
    private readonly GameStateManager _stateManager;
    private readonly PlayManager _playManager;
    private readonly InputManager _input;
    private readonly FieldRenderer _fieldRenderer;
    private readonly HudRenderer _hudRenderer;
    private readonly FireworksEffect _fireworks;
    private readonly Random _rng;
    private readonly IFormationFactory _formationFactory;
    private readonly IDefenseFactory _defenseFactory;
    private readonly IStatisticsTracker _statsTracker;
    private readonly ICollisionResolver _collisionResolver;
    private readonly IThrowingMechanics _throwingMechanics;

    // Entity state
    private readonly List<Receiver> _receivers = new();
    private readonly List<Defender> _defenders = new();
    private readonly List<Blocker> _blockers = new();
    private readonly Dictionary<int, int> _receiverPriorityByIndex = new();
    private readonly List<int> _priorityReceiverIndices = new();
    private Quarterback _qb = null!;
    private Ball _ball = null!;

    // Play state
    private string _lastPlayText = string.Empty;
    private string _driveOverText = string.Empty;
    private bool _qbPastLos;
    private float _playOverTimer;
    private float _playOverDuration = 1.25f;
    private bool _manualPlaySelection;
    private bool _autoPlaySelectionDone;
    private bool _passAttemptedThisPlay;
    private bool _passCompletedThisPlay;
    private Receiver? _passCatcher;
    private float _playStartLos;
    private bool _isZoneCoverage;
    private List<string> _blitzers = new();

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
        _input = input;
        _fieldRenderer = fieldRenderer;
        _hudRenderer = hudRenderer;
        _fireworks = fireworks;
        _rng = rng;
        _formationFactory = formationFactory;
        _defenseFactory = defenseFactory;
        _statsTracker = statsTracker;
        _collisionResolver = collisionResolver;
        _throwingMechanics = throwingMechanics;

        InitializeDrive();
        _stateManager.SetState(GameState.MainMenu);
    }

    private void InitializeDrive()
    {
        _playManager.StartNewDrive();
        SetupEntities();
        _lastPlayText = string.Empty;
        _driveOverText = string.Empty;
        _playOverTimer = 0f;
        _playOverDuration = 1.25f;
        _manualPlaySelection = false;
        _autoPlaySelectionDone = false;
        _fireworks.Clear();
    }

    private void SetupEntities()
    {
        _receivers.Clear();
        _defenders.Clear();
        _blockers.Clear();
        _receiverPriorityByIndex.Clear();
        _priorityReceiverIndices.Clear();
        _passAttemptedThisPlay = false;
        _passCompletedThisPlay = false;
        _passCatcher = null;
        _playStartLos = _playManager.LineOfScrimmage;

        // Use FormationFactory to create offensive formation
        var formationResult = _formationFactory.CreateFormation(_playManager.SelectedPlay, _playManager.LineOfScrimmage);
        _qb = formationResult.Qb;
        _ball = formationResult.Ball;
        _receivers.AddRange(formationResult.Receivers);
        _blockers.AddRange(formationResult.Blockers);

        // Use DefenseFactory to create defense
        var defenseResult = _defenseFactory.CreateDefense(_playManager.LineOfScrimmage, _playManager.Distance, _receivers, _rng);
        _defenders.AddRange(defenseResult.Defenders);
        _isZoneCoverage = defenseResult.IsZoneCoverage;
        _blitzers = defenseResult.Blitzers;

        ReceiverAI.AssignRoutes(_receivers, _playManager.SelectedPlay, _rng);
        AssignReceiverPriorities();
    }

    public void Update(float dt)
    {
        // Update field rect for window resizing
        Constants.UpdateFieldRect();
        
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            _stateManager.TogglePause();
        }

        if (_stateManager.IsPaused)
        {
            return;
        }

        _fireworks.Update(dt);

        if (_stateManager.State == GameState.PlayOver)
        {
            _playOverTimer += dt;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            InitializeDrive();
            _stateManager.SetState(GameState.PreSnap);
            return;
        }

        switch (_stateManager.State)
        {
            case GameState.MainMenu:
                if (Raylib.IsKeyPressed(KeyboardKey.Enter))
                {
                    _stateManager.SetState(GameState.PreSnap);
                    _manualPlaySelection = false;
                    _autoPlaySelectionDone = false;
                }
                break;
            case GameState.PreSnap:
                HandlePreSnap();
                break;
            case GameState.PlayActive:
                HandlePlayActive(dt);
                break;
            case GameState.PlayOver:
                if (_playOverTimer >= _playOverDuration)
                {
                    _stateManager.SetState(GameState.PreSnap);
                    SetupEntities();
                    _lastPlayText = string.Empty;
                    _manualPlaySelection = false;
                    _autoPlaySelectionDone = false;
                }
                break;
            case GameState.DriveOver:
                if (Raylib.IsKeyPressed(KeyboardKey.Enter))
                {
                    InitializeDrive();
                    _stateManager.SetState(GameState.PreSnap);
                }
                break;
        }
    }

    private void HandlePreSnap()
    {
        bool playChanged = false;
        int? selection = null;

        if (Raylib.IsKeyPressed(KeyboardKey.One)) selection = 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) selection = 2;
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) selection = 3;
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) selection = 4;
        if (Raylib.IsKeyPressed(KeyboardKey.Five)) selection = 5;
        if (Raylib.IsKeyPressed(KeyboardKey.Six)) selection = 6;
        if (Raylib.IsKeyPressed(KeyboardKey.Seven)) selection = 7;
        if (Raylib.IsKeyPressed(KeyboardKey.Eight)) selection = 8;
        if (Raylib.IsKeyPressed(KeyboardKey.Nine)) selection = 9;
        if (Raylib.IsKeyPressed(KeyboardKey.Zero)) selection = 10;

        if (selection.HasValue)
        {
            playChanged = _playManager.SelectPlayByGlobalIndex(selection.Value - 1, _rng);
            _manualPlaySelection = true;
        }

        if (!selection.HasValue && !_manualPlaySelection && !_autoPlaySelectionDone)
        {
            playChanged = _playManager.AutoSelectPlayBySituation(_rng) || playChanged;
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

    private void HandlePlayActive(float dt)
    {
        _qbPastLos = _qb.Position.Y > _playManager.LineOfScrimmage + 0.1f && _ball.State == BallState.HeldByQB;

        TryHandoffToRunningBack();

        Vector2 inputDir = _input.GetMovementDirection();
        bool sprint = _input.IsSprintHeld();
        Receiver? controlledReceiver = _ball.State == BallState.HeldByReceiver ? _ball.Holder as Receiver : null;

        if (_ball.State == BallState.HeldByQB)
        {
            _qb.ApplyInput(inputDir, sprint, false, dt);
        }
        else
        {
            _qb.ApplyInput(Vector2.Zero, false, false, dt);
        }
        _qb.Update(dt);
        ClampToField(_qb);

        foreach (var receiver in _receivers)
        {
            if (receiver.IsBlocking)
            {
                UpdateBlockingReceiver(receiver, dt);
                receiver.Update(dt);
                ClampToField(receiver);
                continue;
            }

            if (IsRunPlayActivePreHandoff() && receiver.IsRunningBack)
            {
                Vector2 toQb = _qb.Position - receiver.Position;
                float dist = toQb.Length();
                if (dist > 0.01f)
                {
                    toQb /= dist;
                }

                float settleRange = 4.5f;
                float approachSpeed = receiver.Speed * 0.55f;
                if (dist <= settleRange)
                {
                    receiver.Velocity = toQb * approachSpeed;
                }
                else
                {
                    ReceiverAI.UpdateRoute(receiver, dt);
                }

                receiver.Update(dt);
                ClampToField(receiver);
                continue;
            }

            if (receiver == controlledReceiver)
            {
                float carrierSpeed = sprint ? controlledReceiver.Speed * 1.15f : controlledReceiver.Speed;
                if (IsRunPlayActiveWithRunningBack() && controlledReceiver.IsRunningBack)
                {
                    carrierSpeed *= 1.08f;
                }
                receiver.Velocity = inputDir * carrierSpeed;
                if (IsRunPlayActiveWithRunningBack() && controlledReceiver.IsRunningBack && inputDir.LengthSquared() > 0.001f)
                {
                    Vector2 currentDir = receiver.Velocity.LengthSquared() > 0.001f
                        ? Vector2.Normalize(receiver.Velocity)
                        : inputDir;
                    float turnDot = Vector2.Dot(currentDir, inputDir);
                    if (turnDot < 0.45f)
                    {
                        receiver.Velocity += inputDir * (carrierSpeed * 0.35f);
                    }
                }
                receiver.Update(dt);
                ClampToField(receiver);
                continue;
            }

            ReceiverAI.UpdateRoute(receiver, dt);
            if (_qbPastLos)
            {
                Defender? target = GetClosestDefender(receiver.Position, Constants.BlockEngageRadius, preferRushers: true);
                if (target != null)
                {
                    Vector2 toTarget = target.Position - receiver.Position;
                    if (toTarget.LengthSquared() > 0.001f)
                    {
                        toTarget = Vector2.Normalize(toTarget);
                    }
                    receiver.Velocity = toTarget * (receiver.Speed * 0.9f);
                }
            }
            else if (_ball.State == BallState.InAir && receiver.Eligible)
            {
                Vector2 routeVelocity = receiver.Velocity;
                Vector2 toBall = _ball.Position - receiver.Position;
                float distToBall = toBall.Length();
                if (distToBall > 0.001f)
                {
                    toBall /= distToBall;
                }

                bool ballAhead = _ball.Position.Y >= receiver.Position.Y - 0.75f;
                bool allowComeback = distToBall <= Constants.CatchRadius + 0.75f;

                if (ballAhead || allowComeback)
                {
                    Vector2 baseDir = routeVelocity.LengthSquared() > 0.001f
                        ? Vector2.Normalize(routeVelocity)
                        : toBall;

                    float adjustWeight = Math.Clamp(1f - (distToBall / 12f), 0.15f, 0.6f);
                    Vector2 blendedDir = baseDir * (1f - adjustWeight) + toBall * adjustWeight;
                    if (blendedDir.LengthSquared() > 0.001f)
                    {
                        blendedDir = Vector2.Normalize(blendedDir);
                    }
                    receiver.Velocity = blendedDir * receiver.Speed;
                }
            }
            receiver.Update(dt);
            ClampToField(receiver);
        }

        foreach (var defender in _defenders)
        {
            bool useZone = _isZoneCoverage;
            float runDefenseAdjust = IsRunPlayActiveWithRunningBack() ? 0.9f : 1f;
            float speedMultiplier = _playManager.DefenderSpeedMultiplier * runDefenseAdjust;
            DefenderAI.UpdateDefender(defender, _qb, _receivers, _ball, speedMultiplier, dt, _qbPastLos, useZone, _playManager.LineOfScrimmage);
            ClampToField(defender);
        }

        bool runBlockingBoost = IsRunPlayActiveWithRunningBack();
        OffensiveLinemanAI.UpdateBlockers(
            _blockers,
            _defenders,
            _playManager.SelectedPlayFamily,
            _playManager.LineOfScrimmage,
            _playManager.SelectedPlay.RunningBackSide,
            dt,
            runBlockingBoost,
            ClampToField);
        ResolvePlayerOverlaps();

        HandleBall(dt);
        CheckTackleOrScore();
    }

    private void HandleBall(float dt)
    {
        if (_ball.State == BallState.HeldByQB)
        {
            _ball.SetHeld(_qb, BallState.HeldByQB);
        }
        else if (_ball.State == BallState.HeldByReceiver)
        {
            _ball.Update(dt);
        }
        else if (_ball.State == BallState.InAir)
        {
            _ball.Update(dt);

            if (_ball.AirTime > Constants.BallMaxAirTime || !Rules.IsInBounds(_ball.Position))
            {
                EndPlay(incomplete: true);
                return;
            }

            foreach (var receiver in _receivers)
            {
                if (!receiver.Eligible) continue;
                float receiverDist = Vector2.Distance(receiver.Position, _ball.Position);
                if (receiverDist <= Constants.CatchRadius)
                {
                    // Find closest defender to the ball
                    float closestDefenderDist = float.MaxValue;
                    foreach (var d in _defenders)
                    {
                        float dist = Vector2.Distance(d.Position, _ball.Position);
                        if (dist < closestDefenderDist) closestDefenderDist = dist;
                    }
                    
                    // Interception only if defender is closer than receiver AND within intercept radius
                    if (closestDefenderDist <= Constants.InterceptRadius && closestDefenderDist < receiverDist)
                    {
                        EndPlay(intercepted: true);
                        return;
                    }
                    
                    // Contested catch - if defender is close but not closer, 70% catch rate
                    if (closestDefenderDist <= Constants.ContestedCatchRadius)
                    {
                        if (_rng.NextDouble() < 0.3) // 30% drop rate on contested
                        {
                            EndPlay(incomplete: true);
                            return;
                        }
                    }

                    receiver.HasBall = true;
                    _ball.SetHeld(receiver, BallState.HeldByReceiver);
                    if (_passAttemptedThisPlay && !_passCompletedThisPlay)
                    {
                        _passCompletedThisPlay = true;
                        _passCatcher = receiver;
                        _statsTracker.RecordCompletion(receiver.Index);
                    }
                    return;
                }
            }
        }

        if (_ball.State == BallState.HeldByQB && !_qbPastLos)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.One)) TryThrowToPriority(1);
            if (Raylib.IsKeyPressed(KeyboardKey.Two)) TryThrowToPriority(2);
            if (Raylib.IsKeyPressed(KeyboardKey.Three)) TryThrowToPriority(3);
            if (Raylib.IsKeyPressed(KeyboardKey.Four)) TryThrowToPriority(4);
            if (Raylib.IsKeyPressed(KeyboardKey.Five)) TryThrowToPriority(5);
        }
    }

    private void CheckTackleOrScore()
    {
        Entity? carrier = _ball.State switch
        {
            BallState.HeldByQB => _qb,
            BallState.HeldByReceiver => _ball.Holder,
            _ => null
        };

        if (carrier == null) return;

        if (IsSidelineOutOfBounds(carrier.Position))
        {
            EndPlay(tackle: true);
            return;
        }

        if (Rules.IsTouchdown(carrier.Position))
        {
            EndPlay(touchdown: true);
            return;
        }

        foreach (var defender in _defenders)
        {
            if (Vector2.Distance(defender.Position, carrier.Position) <= defender.Radius + carrier.Radius)
            {
                EndPlay(tackle: true);
                return;
            }
        }
    }

    private void EndPlay(bool tackle = false, bool incomplete = false, bool intercepted = false, bool touchdown = false)
    {
        if (_passAttemptedThisPlay && intercepted)
        {
            _statsTracker.RecordInterception();
        }

        float gain = 0f;
        bool wasRun = false;
        string? catcherLabel = null;
        AI.RouteType? catcherRoute = null;

        if (!incomplete && !intercepted && _ball.Holder != null)
        {
            gain = MathF.Round(_ball.Holder.Position.Y - _playStartLos);
            if (_passCompletedThisPlay && _passCatcher != null)
            {
                _statsTracker.RecordPassYards(_passCatcher.Index, (int)gain, touchdown);
                
                // Capture catcher info for play record
                catcherLabel = GetCatcherLabel(_passCatcher);
                catcherRoute = _passCatcher.Route;
            }

            if (_ball.Holder is Receiver ballCarrier && ballCarrier.IsRunningBack)
            {
                _statsTracker.RecordRushYards((int)gain, touchdown);
                wasRun = true;
            }
            else if (_ball.Holder is Quarterback)
            {
                _statsTracker.RecordQbRushYards((int)gain, touchdown);
                wasRun = true;
            }
        }

        float spot = _playManager.LineOfScrimmage;
        if (tackle && _ball.Holder != null)
        {
            spot = _ball.Holder.Position.Y;
        }
        else if (touchdown)
        {
            spot = Constants.EndZoneDepth + 100f;
        }

        // Determine play outcome for record
        PlayOutcome outcome = touchdown ? PlayOutcome.Touchdown :
                              intercepted ? PlayOutcome.Interception :
                              incomplete ? PlayOutcome.Incomplete :
                              PlayOutcome.Tackle;
        
        // Finalize play record before resolving (which may reset drive)
        _playManager.FinalizePlayRecord(outcome, gain, catcherLabel, catcherRoute, wasRun);

        if (touchdown)
        {
            _fireworks.Trigger();
            _playOverDuration = 2.6f;
        }
        else
        {
            _playOverDuration = 1.25f;
        }

        PlayResult result = _playManager.ResolvePlay(spot, incomplete, intercepted, touchdown);
        _lastPlayText = result.Message;
        _playOverTimer = 0f;

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
        // Get position-based label (WR1, WR2, TE, RB)
        if (catcher.IsRunningBack) return "RB";
        if (catcher.IsTightEnd) return "TE";
        
        // Count WRs by their left-to-right order
        var wrs = _receivers
            .Where(r => !r.IsRunningBack && !r.IsTightEnd && r.Eligible)
            .OrderBy(r => r.Position.X)
            .ToList();
        
        int wrIndex = wrs.FindIndex(r => r.Index == catcher.Index);
        return wrIndex >= 0 ? $"WR{wrIndex + 1}" : "WR";
    }

    public void Draw()
    {
        _fieldRenderer.DrawField(_playManager.LineOfScrimmage, _playManager.FirstDownLine);

        _fireworks.Draw();

        if (_stateManager.State == GameState.PreSnap)
        {
            DrawRouteOverlay();
        }

        foreach (var receiver in _receivers)
        {
            receiver.Draw();
        }

        DrawReceiverPriorityLabels();

        foreach (var blocker in _blockers)
        {
            blocker.Draw();
        }

        foreach (var defender in _defenders)
        {
            defender.Draw();
        }

        _qb.Draw();
        _ball.Draw();

        // Draw scoreboard and side panel HUD
        string targetLabel = GetSelectedReceiverPriorityLabel();
        _hudRenderer.SetStatsSnapshot(BuildStatsSnapshot());
        _hudRenderer.DrawScoreboard(_playManager, _lastPlayText, _stateManager.State);
        _hudRenderer.DrawSidePanel(_playManager, _lastPlayText, targetLabel, _stateManager.State);

        if (_stateManager.State == GameState.DriveOver)
        {
            DrawDriveOverBanner(_driveOverText, "PRESS ENTER FOR NEXT DRIVE");
        }

        if (_stateManager.State == GameState.MainMenu)
        {
            _hudRenderer.DrawMainMenu();
        }

        if (_stateManager.IsPaused)
        {
            _hudRenderer.DrawPause();
        }
    }

    private void ClampToField(Entity entity)
    {
        bool isCarrier = _ball.State switch
        {
            BallState.HeldByQB => entity == _qb,
            BallState.HeldByReceiver => entity == _ball.Holder,
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

    private static bool IsSidelineOutOfBounds(Vector2 position)
    {
        return position.X < 0 || position.X > Constants.FieldWidth;
    }

    private static void DrawDriveOverBanner(string titleText, string subText)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        int bannerWidth = Math.Min(760, screenW - 120);
        int bannerHeight = 130;
        int x = (screenW - bannerWidth) / 2;
        int y = (screenH - bannerHeight) / 2;

        Raylib.DrawRectangle(x, y, bannerWidth, bannerHeight, new Color(10, 10, 14, 235));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, bannerWidth, bannerHeight), 3, Palette.Gold);
        Raylib.DrawRectangle(x + 6, y + 6, bannerWidth - 12, bannerHeight - 12, new Color(20, 20, 28, 235));

        string title = string.IsNullOrWhiteSpace(titleText) ? "DRIVE OVER" : titleText.ToUpperInvariant();
        int titleSize = 40;
        int titleWidth = Raylib.MeasureText(title, titleSize);
        int titleX = x + (bannerWidth - titleWidth) / 2;
        int titleY = y + 18;
        Raylib.DrawText(title, titleX, titleY, titleSize, Palette.Gold);

        string sub = string.IsNullOrWhiteSpace(subText) ? "PRESS ENTER" : subText.ToUpperInvariant();
        int subSize = 18;
        int subWidth = Raylib.MeasureText(sub, subSize);
        Raylib.DrawText(sub, x + (bannerWidth - subWidth) / 2, y + bannerHeight - subSize - 14, subSize, Palette.White);
    }

    private void ResolvePlayerOverlaps()
    {
        // Determine who is carrying the ball - they should NOT be pushed away from defenders
        Entity? ballCarrier = _ball.State switch
        {
            BallState.HeldByQB => _qb,
            BallState.HeldByReceiver => _ball.Holder,
            _ => null
        };

        var entities = new List<Entity>(_receivers.Count + _defenders.Count + _blockers.Count + 1)
        {
            _qb
        };
        entities.AddRange(_receivers);
        entities.AddRange(_blockers);
        entities.AddRange(_defenders);

        for (int i = 0; i < entities.Count; i++)
        {
            for (int j = i + 1; j < entities.Count; j++)
            {
                Entity a = entities[i];
                Entity b = entities[j];

                // Skip overlap resolution between ball carrier and defenders (allows tackles)
                bool aIsDefender = a is Defender;
                bool bIsDefender = b is Defender;
                if (ballCarrier != null)
                {
                    if ((a == ballCarrier && bIsDefender) || (b == ballCarrier && aIsDefender))
                    {
                        continue;
                    }
                }

                // Don't let a blocking RB shove the QB around in the pocket
                if ((a == _qb && b is Receiver rbB && rbB.IsRunningBack && rbB.IsBlocking) ||
                    (b == _qb && a is Receiver rbA && rbA.IsRunningBack && rbA.IsBlocking))
                {
                    continue;
                }

                Vector2 delta = b.Position - a.Position;
                float minDist = a.Radius + b.Radius + 0.05f;
                float distSq = delta.LengthSquared();
                if (distSq <= 0.0001f) continue;

                float dist = MathF.Sqrt(distSq);
                if (dist < minDist)
                {
                    Vector2 pushDir = delta / dist;
                    float push = (minDist - dist) * 0.5f;
                    a.Position -= pushDir * push;
                    b.Position += pushDir * push;
                    ClampToField(a);
                    ClampToField(b);
                }
            }
        }
    }

    private void TryHandoffToRunningBack()
    {
        if (_playManager.SelectedPlayFamily != PlayType.QbRunFocus)
        {
            return;
        }

        if (_ball.State != BallState.HeldByQB)
        {
            return;
        }

        Receiver? runningBack = _receivers.FirstOrDefault(r => r.IsRunningBack);
        if (runningBack == null)
        {
            return;
        }

        float handoffRange = 3.2f;
        float distance = Vector2.Distance(_qb.Position, runningBack.Position);
        if (distance > handoffRange)
        {
            return;
        }

        runningBack.HasBall = true;
        _ball.SetHeld(runningBack, BallState.HeldByReceiver);
    }

    private bool IsRunPlayActivePreHandoff()
    {
        if (_playManager.SelectedPlayFamily != PlayType.QbRunFocus)
        {
            return false;
        }

        return _ball.State == BallState.HeldByQB;
    }

    private bool IsRunPlayActiveWithRunningBack()
    {
        if (_playManager.SelectedPlayFamily != PlayType.QbRunFocus)
        {
            return false;
        }

        if (_ball.State != BallState.HeldByReceiver)
        {
            return false;
        }

        return _ball.Holder is Receiver receiver && receiver.IsRunningBack;
    }

    private void UpdateBlockingReceiver(Receiver receiver, float dt)
    {
        if (receiver.IsRunningBack && receiver.IsBlocking)
        {
            int side = receiver.RouteSide == 0 ? (receiver.Position.X <= _qb.Position.X ? -1 : 1) : receiver.RouteSide;
            Vector2 pocketSpot = _qb.Position + new Vector2(1.7f * side, -0.4f);

            Defender? rbTarget = GetClosestDefender(_qb.Position, Constants.BlockEngageRadius + 6.0f, preferRushers: true);
            if (rbTarget != null)
            {
                float blockMultiplier = GetReceiverBlockStrength(receiver) * GetDefenderBlockDifficulty(rbTarget);
                Vector2 toTarget = rbTarget.Position - receiver.Position;
                if (toTarget.LengthSquared() > 0.001f)
                {
                    toTarget = Vector2.Normalize(toTarget);
                }
                receiver.Velocity = toTarget * (receiver.Speed * 0.9f);

                float contactRange = receiver.Radius + rbTarget.Radius + 0.8f;
                float distance = Vector2.Distance(receiver.Position, rbTarget.Position);
                if (distance <= contactRange)
                {
                    Vector2 pushDir = rbTarget.Position - receiver.Position;
                    if (pushDir.LengthSquared() > 0.001f)
                    {
                        pushDir = Vector2.Normalize(pushDir);
                    }
                    float overlap = contactRange - distance;
                    float holdStrength = (Constants.BlockHoldStrength * 1.1f) * blockMultiplier;
                    float overlapBoost = 6f * blockMultiplier;
                    rbTarget.Position += pushDir * (holdStrength + overlap * overlapBoost) * dt;
                    rbTarget.Velocity *= GetDefenderSlowdown(blockMultiplier, 0.12f);
                    receiver.Velocity *= 0.25f;
                }
            }
            else
            {
                Vector2 toPocket = pocketSpot - receiver.Position;
                if (toPocket.LengthSquared() > 0.001f)
                {
                    toPocket = Vector2.Normalize(toPocket);
                }
                receiver.Velocity = toPocket * (receiver.Speed * 0.6f);

                if (Vector2.DistanceSquared(receiver.Position, pocketSpot) <= 0.8f * 0.8f)
                {
                    receiver.Velocity = Vector2.Zero;
                }
            }

            return;
        }

        Defender? target = GetClosestDefender(receiver.Position, Constants.BlockEngageRadius, preferRushers: true);
        if (target != null)
        {
            bool runBlockingBoost = IsRunPlayActiveWithRunningBack();
            int runSide = Math.Sign(_playManager.SelectedPlay.RunningBackSide);
            float blockMultiplier = GetReceiverBlockStrength(receiver) * GetDefenderBlockDifficulty(target);
            Vector2 toTarget = target.Position - receiver.Position;
            if (toTarget.LengthSquared() > 0.001f)
            {
                toTarget = Vector2.Normalize(toTarget);
            }
            receiver.Velocity = toTarget * receiver.Speed;
            if (runBlockingBoost && runSide != 0)
            {
                Vector2 driveDir = new Vector2(runSide * 0.7f, 1f);
                if (driveDir.LengthSquared() > 0.001f)
                {
                    driveDir = Vector2.Normalize(driveDir);
                }
                receiver.Velocity += driveDir * (receiver.Speed * 0.3f);
            }

            float contactRange = receiver.Radius + target.Radius + (runBlockingBoost ? 0.9f : 0.6f);
            float distance = Vector2.Distance(receiver.Position, target.Position);
            if (distance <= contactRange)
            {
                Vector2 pushDir = target.Position - receiver.Position;
                if (pushDir.LengthSquared() > 0.001f)
                {
                    pushDir = Vector2.Normalize(pushDir);
                }
                float overlap = contactRange - distance;
                float holdStrength = runBlockingBoost ? Constants.BlockHoldStrength * 1.4f : Constants.BlockHoldStrength;
                float overlapBoost = runBlockingBoost ? 8f : 6f;
                holdStrength *= blockMultiplier;
                overlapBoost *= blockMultiplier;
                target.Position += pushDir * (holdStrength + overlap * overlapBoost) * dt;
                if (runBlockingBoost && runSide != 0)
                {
                    Vector2 driveDir = new Vector2(runSide * 0.7f, 1f);
                    if (driveDir.LengthSquared() > 0.001f)
                    {
                        driveDir = Vector2.Normalize(driveDir);
                    }
                    target.Position += driveDir * 0.8f * dt;
                }
                float baseSlow = runBlockingBoost ? 0.08f : 0.15f;
                target.Velocity *= GetDefenderSlowdown(blockMultiplier, baseSlow);
                receiver.Velocity *= 0.25f;
            }
        }
        else
        {
            if (IsRunPlayActiveWithRunningBack())
            {
                int runSide = Math.Sign(_playManager.SelectedPlay.RunningBackSide);
                receiver.Velocity = new Vector2(runSide * receiver.Speed * 0.18f, receiver.Speed * 0.4f);
            }
            else
            {
                receiver.Velocity = new Vector2(0f, receiver.Speed * 0.4f);
            }
        }
    }

    private static float GetReceiverBlockStrength(Receiver receiver)
    {
        if (receiver.IsTightEnd)
        {
            return 1.1f;
        }

        if (receiver.IsRunningBack)
        {
            return 1.0f;
        }

        return 0.75f;
    }

    private static float GetDefenderBlockDifficulty(Defender defender)
    {
        return defender.PositionRole switch
        {
            DefensivePosition.DL => 0.75f,
            DefensivePosition.LB => 0.95f,
            _ => 1.15f
        };
    }

    private static float GetDefenderSlowdown(float blockMultiplier, float baseSlow)
    {
        float bonus = Math.Clamp(blockMultiplier - 1f, -0.6f, 0.6f);
        float adjusted = baseSlow - bonus * 0.06f;
        return Math.Clamp(adjusted, 0.05f, 0.22f);
    }

    private void AssignReceiverPriorities()
    {
        _receiverPriorityByIndex.Clear();
        _priorityReceiverIndices.Clear();

        var ordered = _receivers
            .Where(r => r.Eligible)
            .OrderBy(r => r.Position.X)
            .ToList();

        int count = Math.Min(5, ordered.Count);
        for (int i = 0; i < count; i++)
        {
            int receiverIndex = ordered[i].Index;
            _receiverPriorityByIndex[receiverIndex] = i + 1;
            _priorityReceiverIndices.Add(receiverIndex);
        }

        _playManager.SelectedReceiver = count > 0 ? _priorityReceiverIndices[0] : 0;
    }

    private bool TryGetReceiverIndexForPriority(int priority, out int receiverIndex)
    {
        receiverIndex = -1;
        if (priority <= 0) return false;
        int listIndex = priority - 1;
        if (listIndex < 0 || listIndex >= _priorityReceiverIndices.Count) return false;
        receiverIndex = _priorityReceiverIndices[listIndex];
        return receiverIndex >= 0 && receiverIndex < _receivers.Count;
    }

    private string GetReceiverPriorityLabel(int receiverIndex)
    {
        return _receiverPriorityByIndex.TryGetValue(receiverIndex, out int priority)
            ? priority.ToString()
            : "-";
    }

    private string GetSelectedReceiverPriorityLabel()
    {
        if (_playManager.SelectedReceiver < 0 || _playManager.SelectedReceiver >= _receivers.Count)
        {
            return "-";
        }

        return GetReceiverPriorityLabel(_playManager.SelectedReceiver);
    }

    private GameStatsSnapshot BuildStatsSnapshot()
    {
        return _statsTracker.BuildSnapshot(priority =>
        {
            return TryGetReceiverIndexForPriority(priority, out _);
        });
    }

    private void TryThrowToPriority(int priority)
    {
        if (!TryGetReceiverIndexForPriority(priority, out int receiverIndex)) return;
        _playManager.SelectedReceiver = receiverIndex;
        TryThrowToSelected();
    }

    private void TryThrowToSelected()
    {
        if (!TryGetReceiverIndexForPriority(1, out int fallbackIndex))
        {
            return;
        }

        int receiverIndex = _playManager.SelectedReceiver;
        if (!_receiverPriorityByIndex.ContainsKey(receiverIndex))
        {
            receiverIndex = fallbackIndex;
            _playManager.SelectedReceiver = receiverIndex;
        }

        if (receiverIndex < 0 || receiverIndex >= _receivers.Count) return;
        var receiver = _receivers[receiverIndex];
        if (!receiver.Eligible) return;

        if (!_passAttemptedThisPlay)
        {
            _passAttemptedThisPlay = true;
            _statsTracker.RecordPassAttempt();
        }

        float pressure = GetQbPressureFactor();
        Vector2 throwVelocity = _throwingMechanics.CalculateThrowVelocity(
            _qb.Position,
            _qb.Velocity,
            receiver.Position,
            receiver.Velocity,
            Constants.BallMaxSpeed,
            pressure,
            _rng);

        _ball.SetInAir(_qb.Position, throwVelocity);
    }

    private void DrawRouteOverlay()
    {
        foreach (var receiver in _receivers)
        {
            if (!receiver.Eligible) continue;

            var points = ReceiverAI.GetRouteWaypoints(receiver);
            if (points.Count < 2) continue;

            Color routeColor = GetRouteColor(receiver.Index);
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = Constants.WorldToScreen(points[i]);
                Vector2 b = Constants.WorldToScreen(points[i + 1]);
                Raylib.DrawLineEx(a, b, 2.0f, routeColor);
            }

        }

        OffensiveLinemanAI.DrawRoutes(
            _blockers,
            _playManager.SelectedPlayFamily,
            _playManager.LineOfScrimmage,
            _playManager.SelectedPlay.RunningBackSide);
    }

    private void DrawReceiverPriorityLabels()
    {
        if (_receivers.Count == 0) return;

        foreach (var receiver in _receivers)
        {
            if (!receiver.Eligible) continue;

            string priorityLabel = GetReceiverPriorityLabel(receiver.Index);
            if (priorityLabel == "-") continue;

            Vector2 labelPos = Constants.WorldToScreen(receiver.Position);
            int fontSize = 20;
            Color shadow = new Color(10, 10, 12, 220);
            int textWidth = Raylib.MeasureText(priorityLabel, fontSize);
            int drawX = (int)labelPos.X - textWidth / 2;
            int drawY = (int)labelPos.Y - 20;
            Raylib.DrawText(priorityLabel, drawX + 1, drawY + 1, fontSize, shadow);
            Raylib.DrawText(priorityLabel, drawX, drawY, fontSize, Palette.Lime);
        }
    }

    private static Color GetRouteColor(int receiverIndex)
    {
        return Palette.RouteReceiving;
    }

    private Defender? GetClosestDefender(Vector2 position, float maxDistance, bool preferRushers)
    {
        Defender? closest = null;
        float bestDistSq = maxDistance * maxDistance;

        IEnumerable<Defender> candidates = _defenders;
        if (preferRushers)
        {
            var rushers = _defenders.Where(d => d.IsRusher).ToList();
            if (rushers.Count > 0)
            {
                candidates = rushers;
            }
        }

        foreach (var defender in candidates)
        {
            float distSq = Vector2.DistanceSquared(position, defender.Position);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                closest = defender;
            }
        }

        return closest;
    }

    private float GetQbPressureFactor()
    {
        float closest = float.MaxValue;
        foreach (var defender in _defenders)
        {
            if (!defender.IsRusher) continue;
            float dist = Vector2.Distance(defender.Position, _qb.Position);
            if (dist < closest)
            {
                closest = dist;
            }
        }

        if (closest == float.MaxValue || closest >= Constants.ThrowPressureMaxDistance)
        {
            return 0f;
        }

        if (closest <= Constants.ThrowPressureMinDistance)
        {
            return 1f;
        }

        float t = 1f - (closest - Constants.ThrowPressureMinDistance) / (Constants.ThrowPressureMaxDistance - Constants.ThrowPressureMinDistance);
        return Math.Clamp(t, 0f, 1f);
    }
}
