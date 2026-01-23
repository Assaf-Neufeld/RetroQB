using System.Numerics;
using System.Linq;
using Raylib_cs;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Input;
using RetroQB.Rendering;

namespace RetroQB.Gameplay;

public sealed class GameSession
{
    private readonly GameStateManager _stateManager = new();
    private readonly PlayManager _playManager = new();
    private readonly InputManager _input = new();
    private readonly FieldRenderer _fieldRenderer = new();
    private readonly HudRenderer _hudRenderer = new();
    private readonly Random _rng = new();

    private readonly List<Receiver> _receivers = new();
    private readonly List<Defender> _defenders = new();
    private readonly List<Blocker> _blockers = new();
    private readonly Dictionary<int, int> _receiverPriorityByIndex = new();
    private readonly List<int> _priorityReceiverIndices = new();
    private Quarterback _qb = null!;
    private Ball _ball = null!;
    private string _lastPlayText = string.Empty;
    private bool _qbPastLos;
    private float _playOverTimer;
    private bool _manualPlaySelection;
    private bool _autoPlaySelectionDone;

    public GameSession()
    {
        InitializeDrive();
        _stateManager.SetState(GameState.MainMenu);
    }

    private void InitializeDrive()
    {
        _playManager.StartNewDrive();
        SetupEntities();
        _lastPlayText = string.Empty;
        _playOverTimer = 0f;
        _manualPlaySelection = false;
        _autoPlaySelectionDone = false;
    }

    private void SetupEntities()
    {
        _receivers.Clear();
        _defenders.Clear();
        _blockers.Clear();
        _receiverPriorityByIndex.Clear();
        _priorityReceiverIndices.Clear();

        float los = _playManager.LineOfScrimmage;
        _qb = new Quarterback(new Vector2(Constants.FieldWidth / 2f, los - 1.6f));
        _ball = new Ball(_qb.Position);
        _ball.SetHeld(_qb, BallState.HeldByQB);

        var formation = _playManager.SelectedPlay.Formation;
        AddFormation(formation, los);

        SpawnDefenders(los);
        ReceiverAI.AssignRoutes(_receivers, _playManager.SelectedPlay, _rng);
        AssignReceiverPriorities();
    }

    private static readonly float[] BaseLineX = { 0.42f, 0.46f, 0.50f, 0.54f, 0.58f };

    private void AddFormation(FormationType formation, float los)
    {
        // All receivers must be on or behind the LOS (los - offset means behind)
        switch (formation)
        {
            case FormationType.ShotgunTrips:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.62f, los - 1.0f)); // R1
                AddReceiver(new Vector2(Constants.FieldWidth * 0.78f, los - 0.6f)); // R2
                AddReceiver(new Vector2(Constants.FieldWidth * 0.90f, los - 0.3f)); // R3
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 4.6f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Bunch:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.64f, los - 0.5f)); // R1
                AddReceiver(new Vector2(Constants.FieldWidth * 0.70f, los - 1.1f)); // R2
                AddReceiver(new Vector2(Constants.FieldWidth * 0.76f, los - 0.2f)); // R3
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 4.8f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Spread:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.18f, los - 0.3f)); // R1
                AddReceiver(new Vector2(Constants.FieldWidth * 0.34f, los - 1.0f)); // R2
                AddReceiver(new Vector2(Constants.FieldWidth * 0.82f, los - 0.3f)); // R3
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 5.2f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Slot:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.16f, los - 0.3f)); // R1
                AddReceiver(new Vector2(Constants.FieldWidth * 0.66f, los - 1.0f)); // R2
                AddReceiver(new Vector2(Constants.FieldWidth * 0.86f, los - 0.3f)); // R3
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 5.0f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Shotgun:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.18f, los - 0.3f)); // R1
                AddReceiver(new Vector2(Constants.FieldWidth * 0.34f, los - 1.0f)); // R2
                AddReceiver(new Vector2(Constants.FieldWidth * 0.82f, los - 0.3f)); // R3
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 4.4f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Pistol:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.18f, los - 0.3f)); // R1
                AddReceiver(new Vector2(Constants.FieldWidth * 0.82f, los - 0.3f)); // R2
                AddReceiver(new Vector2(Constants.FieldWidth * 0.62f, los - 0.05f), isTightEnd: true); // R3 (TE)
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 3.8f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Trips:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.12f, los - 0.3f)); // R1
                AddReceiver(new Vector2(Constants.FieldWidth * 0.28f, los - 1.0f)); // R2
                AddReceiver(new Vector2(Constants.FieldWidth * 0.34f, los - 0.4f)); // R3
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 5.0f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.SpreadFour:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.10f, los - 0.3f)); // X WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.28f, los - 1.0f)); // Slot L
                AddReceiver(new Vector2(Constants.FieldWidth * 0.72f, los - 1.0f)); // Slot R
                AddReceiver(new Vector2(Constants.FieldWidth * 0.90f, los - 0.3f)); // Z WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 5.4f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Twins:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.14f, los - 0.3f)); // X WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.86f, los - 0.3f)); // Z WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.66f, los - 0.05f), isTightEnd: true); // TE inline
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 5.2f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Heavy:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.16f, los - 0.3f)); // X WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.68f, los - 0.05f), isTightEnd: true); // TE inline
                AddReceiver(new Vector2(Constants.FieldWidth * 0.52f, los - 5.4f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: true);
                break;
            default:
                // SinglebackTrips
                AddReceiver(new Vector2(Constants.FieldWidth * 0.12f, los - 0.3f)); // X WR (left)
                AddReceiver(new Vector2(Constants.FieldWidth * 0.30f, los - 1.0f)); // Slot (left)
                AddReceiver(new Vector2(Constants.FieldWidth * 0.88f, los - 0.3f)); // Z WR (right)
                AddReceiver(new Vector2(Constants.FieldWidth * 0.70f, los - 0.05f), isTightEnd: true); // TE (right)
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 5.2f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
        }
    }

    private void AddReceiver(Vector2 position, bool isRunningBack = false, bool isTightEnd = false)
    {
        _receivers.Add(new Receiver(_receivers.Count, position, isRunningBack, isTightEnd));
    }

    private void AddBaseLine(float los, bool addExtra)
    {
        foreach (float x in BaseLineX)
        {
            _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * x, los - 0.1f)));
        }

        if (addExtra)
        {
            _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.62f, los - 0.1f)));
        }
    }

    private void SpawnDefenders(float los)
    {
        ResolveCoverageIndices(out int left, out int leftSlot, out int middle, out int rightSlot, out int right);

        // Defensive formation: 4-3 with 4 DB
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, los + 2.8f), DefensivePosition.DL) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -5.0f });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.46f, los + 2.8f), DefensivePosition.DL) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = -2.0f });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.54f, los + 2.8f), DefensivePosition.DL) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 2.0f });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, los + 2.8f), DefensivePosition.DL) { IsRusher = true, ZoneRole = CoverageRole.None, RushLaneOffsetX = 5.0f });

        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.38f, los + 6.2f), DefensivePosition.LB) { CoverageReceiverIndex = leftSlot, ZoneRole = CoverageRole.HookLeft }); // LB
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.50f, los + 6.2f), DefensivePosition.LB) { CoverageReceiverIndex = middle, ZoneRole = CoverageRole.HookMiddle }); // MLB
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.62f, los + 6.2f), DefensivePosition.LB) { CoverageReceiverIndex = rightSlot, ZoneRole = CoverageRole.HookRight }); // LB

        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.18f, los + 10.5f), DefensivePosition.DB) { CoverageReceiverIndex = left, ZoneRole = CoverageRole.FlatLeft }); // CB (flat)
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.82f, los + 10.5f), DefensivePosition.DB) { CoverageReceiverIndex = right, ZoneRole = CoverageRole.FlatRight }); // CB (flat)
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, los + 13.5f), DefensivePosition.DB) { CoverageReceiverIndex = leftSlot, ZoneRole = CoverageRole.DeepLeft }); // S (deep half)
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, los + 13.5f), DefensivePosition.DB) { CoverageReceiverIndex = rightSlot, ZoneRole = CoverageRole.DeepRight }); // S (deep half)
    }

    private void ResolveCoverageIndices(out int left, out int leftSlot, out int middle, out int rightSlot, out int right)
    {
        if (_receivers.Count == 0)
        {
            left = leftSlot = middle = rightSlot = right = -1;
            return;
        }

        var ordered = _receivers
            .Select((receiver, index) => new { receiver.Position.X, index })
            .OrderBy(item => item.X)
            .Select(item => item.index)
            .ToList();

        left = ordered[0];
        right = ordered[^1];
        middle = ordered[ordered.Count / 2];
        leftSlot = ordered.Count > 2 ? ordered[1] : left;
        rightSlot = ordered.Count > 3 ? ordered[^2] : right;
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
                if (_playOverTimer >= 1.25f)
                {
                    _stateManager.SetState(GameState.PreSnap);
                    SetupEntities();
                    _lastPlayText = string.Empty;
                    _manualPlaySelection = false;
                    _autoPlaySelectionDone = false;
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
            playChanged = _playManager.SelectPlayByGlobalIndex(selection.Value - 1);
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
            SetupEntities();
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
            bool useZone = _playManager.Distance > Constants.ManCoverageDistanceThreshold;
            float runDefenseAdjust = IsRunPlayActiveWithRunningBack() ? 0.9f : 1f;
            float speedMultiplier = _playManager.DefenderSpeedMultiplier * runDefenseAdjust;
            DefenderAI.UpdateDefender(defender, _qb, _receivers, _ball, speedMultiplier, dt, _qbPastLos, useZone, _playManager.LineOfScrimmage);
            ClampToField(defender);
        }

        bool runBlockingBoost = IsRunPlayActiveWithRunningBack();
        UpdateBlockers(dt, runBlockingBoost);
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
        float spot = _playManager.LineOfScrimmage;
        if (tackle && _ball.Holder != null)
        {
            spot = _ball.Holder.Position.Y;
        }
        else if (touchdown)
        {
            spot = Constants.EndZoneDepth + 100f;
        }

        _lastPlayText = _playManager.ResolvePlay(spot, incomplete, intercepted, touchdown);
        _playOverTimer = 0f;
        _stateManager.SetState(GameState.PlayOver);
    }

    public void Draw()
    {
        _fieldRenderer.DrawField(_playManager.LineOfScrimmage, _playManager.FirstDownLine);

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

        // Draw side panel with all HUD info
        string targetLabel = GetSelectedReceiverPriorityLabel();
        _hudRenderer.DrawSidePanel(_playManager, _lastPlayText, targetLabel, _stateManager.State);

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

    private void UpdateBlockers(float dt, bool runBlockingBoost)
    {
        bool isRunPlay = _playManager.SelectedPlayFamily == PlayType.QbRunFocus;
        float los = _playManager.LineOfScrimmage;
        int runSide = Math.Sign(_playManager.SelectedPlay.RunningBackSide);
        float lateralPush = isRunPlay && runSide != 0 ? runSide * 2.2f : 0f;
        float targetY = isRunPlay ? los + 1.6f : los - 1.4f;

        foreach (var blocker in _blockers)
        {
            Vector2 targetAnchor = new Vector2(
                Math.Clamp(blocker.HomeX + lateralPush, 1.5f, Constants.FieldWidth - 1.5f),
                targetY);

            Defender? target = GetClosestDefender(blocker.Position, Constants.BlockEngageRadius, preferRushers: true);
            float anchorDistSq = Vector2.DistanceSquared(blocker.Position, targetAnchor);
            bool closeToAnchor = anchorDistSq <= 0.75f * 0.75f;

            if (target != null && (closeToAnchor || Vector2.DistanceSquared(blocker.Position, target.Position) <= Constants.BlockEngageRadius * Constants.BlockEngageRadius))
            {
                Vector2 toTarget = target.Position - blocker.Position;
                if (toTarget.LengthSquared() > 0.001f)
                {
                    toTarget = Vector2.Normalize(toTarget);
                }
                Vector2 baseVelocity = toTarget * blocker.Speed;
                if (runBlockingBoost)
                {
                    baseVelocity += new Vector2(0f, blocker.Speed * 0.45f);
                }
                blocker.Velocity = baseVelocity;

                float contactRange = blocker.Radius + target.Radius + (runBlockingBoost ? 1.0f : 0.6f);
                float distance = Vector2.Distance(blocker.Position, target.Position);
                if (distance <= contactRange)
                {
                    Vector2 pushDir = target.Position - blocker.Position;
                    if (pushDir.LengthSquared() > 0.001f)
                    {
                        pushDir = Vector2.Normalize(pushDir);
                    }
                    float overlap = contactRange - distance;
                    float holdStrength = runBlockingBoost ? Constants.BlockHoldStrength * 1.6f : Constants.BlockHoldStrength;
                    float overlapBoost = runBlockingBoost ? 9f : 6f;
                    target.Position += pushDir * (holdStrength + overlap * overlapBoost) * dt;
                    target.Velocity *= runBlockingBoost ? 0.05f : 0.15f;
                    blocker.Velocity *= 0.25f;
                }
            }
            else
            {
                Vector2 toAnchor = targetAnchor - blocker.Position;
                if (toAnchor.LengthSquared() > 0.001f)
                {
                    toAnchor = Vector2.Normalize(toAnchor);
                }

                float anchorSpeed = isRunPlay ? blocker.Speed * 0.85f : blocker.Speed * 0.7f;
                blocker.Velocity = toAnchor * anchorSpeed;

                if (closeToAnchor)
                {
                    float settleSpeed = isRunPlay ? blocker.Speed * 0.45f : blocker.Speed * 0.1f;
                    blocker.Velocity = new Vector2(0f, settleSpeed * (isRunPlay ? 1f : -1f));
                }
            }

            blocker.Update(dt);
            ClampToField(blocker);
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

            Defender? rbTarget = GetClosestDefender(_qb.Position, Constants.BlockEngageRadius + 1.8f, preferRushers: true);
            if (rbTarget != null && Vector2.Distance(rbTarget.Position, _qb.Position) <= 4.8f)
            {
                Vector2 toTarget = rbTarget.Position - receiver.Position;
                if (toTarget.LengthSquared() > 0.001f)
                {
                    toTarget = Vector2.Normalize(toTarget);
                }
                receiver.Velocity = toTarget * receiver.Speed;

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
                    rbTarget.Position += pushDir * (Constants.BlockHoldStrength * 1.1f + overlap * 6f) * dt;
                    rbTarget.Velocity *= 0.12f;
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
            Vector2 toTarget = target.Position - receiver.Position;
            if (toTarget.LengthSquared() > 0.001f)
            {
                toTarget = Vector2.Normalize(toTarget);
            }
            receiver.Velocity = toTarget * receiver.Speed;

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
                target.Position += pushDir * (holdStrength + overlap * overlapBoost) * dt;
                target.Velocity *= runBlockingBoost ? 0.08f : 0.15f;
                receiver.Velocity *= 0.25f;
            }
        }
        else
        {
            receiver.Velocity = new Vector2(0f, receiver.Speed * 0.4f);
        }
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

        float speed = Constants.BallMaxSpeed;
        Vector2 toReceiver = receiver.Position - _qb.Position;
        float leadTime = CalculateInterceptTime(toReceiver, receiver.Velocity, speed);
        leadTime = Math.Clamp(leadTime, 0f, Constants.BallMaxAirTime);
        Vector2 leadTarget = receiver.Position + receiver.Velocity * leadTime;

        Vector2 dir = leadTarget - _qb.Position;
        if (dir.LengthSquared() > 0.001f)
        {
            dir = Vector2.Normalize(dir);
        }

        float pressure = GetQbPressureFactor();
        float movementPenalty = GetMovementInaccuracyPenalty(dir);
        float combinedFactor = Math.Clamp(pressure + movementPenalty, 0f, 1f);
        float inaccuracyDeg = Lerp(Constants.ThrowBaseInaccuracyDeg, Constants.ThrowMaxInaccuracyDeg, combinedFactor);
        float inaccuracyRad = inaccuracyDeg * (MathF.PI / 180f);
        float angle = ((float)_rng.NextDouble() * 2f - 1f) * inaccuracyRad;
        dir = Rotate(dir, angle);

        _ball.SetInAir(_qb.Position, dir * speed);
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

        DrawBlockerRoutes();
    }

    private void DrawBlockerRoutes()
    {
        if (_blockers.Count == 0) return;

        bool isRunPlay = _playManager.SelectedPlayFamily == PlayType.QbRunFocus;
        float los = _playManager.LineOfScrimmage;
        int runSide = Math.Sign(_playManager.SelectedPlay.RunningBackSide);
        float lateralPush = isRunPlay && runSide != 0 ? runSide * 2.2f : 0f;
        float targetY = isRunPlay ? los + 1.6f : los - 1.4f;

        foreach (var blocker in _blockers)
        {
            Vector2 start = blocker.Position;
            Vector2 end = new Vector2(
                Math.Clamp(blocker.HomeX + lateralPush, 1.5f, Constants.FieldWidth - 1.5f),
                targetY);

            Vector2 a = Constants.WorldToScreen(start);
            Vector2 b = Constants.WorldToScreen(end);
            Raylib.DrawLineEx(a, b, 2.0f, Palette.RouteBlocking);

            Vector2 dir = end - start;
            if (dir.LengthSquared() > 0.001f)
            {
                dir = Vector2.Normalize(dir);
                Vector2 perp = new Vector2(-dir.Y, dir.X);
                Vector2 orthoStart = end - perp * 0.8f;
                Vector2 orthoEnd = end + perp * 0.8f;
                Vector2 oA = Constants.WorldToScreen(orthoStart);
                Vector2 oB = Constants.WorldToScreen(orthoEnd);
                Raylib.DrawLineEx(oA, oB, 2.0f, Palette.RouteBlocking);
            }
        }
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

    private float GetNearestDefenderDistance(Vector2 position)
    {
        float best = float.MaxValue;
        foreach (var defender in _defenders)
        {
            float dist = Vector2.Distance(defender.Position, position);
            if (dist < best)
            {
                best = dist;
            }
        }

        return best;
    }

    private float GetMovementInaccuracyPenalty(Vector2 throwDir)
    {
        float speed = _qb.Velocity.Length();
        if (speed < 0.2f) return 0f;

        Vector2 moveDir = _qb.Velocity / speed;
        float dot = Vector2.Dot(moveDir, throwDir);
        dot = Math.Clamp(dot, -1f, 1f);

        float penalty = (1f - dot) * 0.5f;
        return Math.Clamp(penalty, 0f, 1f);
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

    private static float CalculateInterceptTime(Vector2 toTarget, Vector2 targetVelocity, float projectileSpeed)
    {
        float a = Vector2.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
        float b = 2f * Vector2.Dot(targetVelocity, toTarget);
        float c = Vector2.Dot(toTarget, toTarget);

        if (MathF.Abs(a) < 0.0001f)
        {
            if (MathF.Abs(b) < 0.0001f)
            {
                return 0f;
            }
            float time = -c / b;
            return time > 0f ? time : 0f;
        }

        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
        {
            return 0f;
        }

        float sqrt = MathF.Sqrt(discriminant);
        float t1 = (-b - sqrt) / (2f * a);
        float t2 = (-b + sqrt) / (2f * a);
        float t = (t1 > 0f && t2 > 0f) ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
        return t > 0f ? t : 0f;
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
