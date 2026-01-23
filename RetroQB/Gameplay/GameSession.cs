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
    private Quarterback _qb = null!;
    private Ball _ball = null!;
    private string _lastPlayText = string.Empty;
    private bool _qbPastLos;

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
    }

    private void SetupEntities()
    {
        _receivers.Clear();
        _defenders.Clear();
        _blockers.Clear();

        float los = _playManager.LineOfScrimmage;
        _qb = new Quarterback(new Vector2(Constants.FieldWidth / 2f, los - 1.0f));
        _ball = new Ball(_qb.Position);
        _ball.SetHeld(_qb, BallState.HeldByQB);

        var formation = _playManager.SelectedPlay.Formation;
        AddFormation(formation, los);

        SpawnDefenders(los);
        ReceiverAI.AssignRoutes(_receivers, _playManager.SelectedPlay, _rng);
        SelectFirstEligibleReceiver();
    }

    private static readonly float[] BaseLineX = { 0.42f, 0.47f, 0.50f, 0.53f, 0.58f };

    private void AddFormation(FormationType formation, float los)
    {
        // All receivers must be on or behind the LOS (los - offset means behind)
        switch (formation)
        {
            case FormationType.SpreadFour:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.12f, los - 0.3f)); // X WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.30f, los - 1.0f)); // Slot L
                AddReceiver(new Vector2(Constants.FieldWidth * 0.70f, los - 1.0f)); // Slot R
                AddReceiver(new Vector2(Constants.FieldWidth * 0.88f, los - 0.3f)); // Z WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 3.8f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Twins:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.18f, los - 0.3f)); // X WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.82f, los - 0.3f)); // Z WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.62f, los - 0.05f), isTightEnd: true); // TE inline
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 3.6f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: false);
                break;
            case FormationType.Heavy:
                AddReceiver(new Vector2(Constants.FieldWidth * 0.20f, los - 0.3f)); // X WR
                AddReceiver(new Vector2(Constants.FieldWidth * 0.64f, los - 0.05f), isTightEnd: true); // TE inline
                AddReceiver(new Vector2(Constants.FieldWidth * 0.52f, los - 3.9f), isRunningBack: true); // RB
                AddBaseLine(los, addExtra: true);
                break;
            default:
                // SinglebackTrips
                AddReceiver(new Vector2(Constants.FieldWidth * 0.15f, los - 0.3f)); // X WR (left)
                AddReceiver(new Vector2(Constants.FieldWidth * 0.32f, los - 1.0f)); // Slot (left)
                AddReceiver(new Vector2(Constants.FieldWidth * 0.85f, los - 0.3f)); // Z WR (right)
                AddReceiver(new Vector2(Constants.FieldWidth * 0.66f, los - 0.05f), isTightEnd: true); // TE (right)
                AddReceiver(new Vector2(Constants.FieldWidth * 0.50f, los - 3.5f), isRunningBack: true); // RB
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
                }
                break;
            case GameState.PreSnap:
                HandlePreSnap();
                break;
            case GameState.PlayActive:
                HandlePlayActive(dt);
                break;
            case GameState.PlayOver:
                if (Raylib.IsKeyPressed(KeyboardKey.Enter))
                {
                    _stateManager.SetState(GameState.PreSnap);
                    SetupEntities();
                    _lastPlayText = string.Empty;
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

        if (_ball.State == BallState.HeldByQB && !_qbPastLos)
        {
            AutoSelectReceiver();
        }

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

            if (receiver == controlledReceiver)
            {
                float carrierSpeed = sprint ? controlledReceiver.Speed * 1.15f : controlledReceiver.Speed;
                receiver.Velocity = inputDir * carrierSpeed;
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
            DefenderAI.UpdateDefender(defender, _qb, _receivers, _ball, _playManager.DefenderSpeedMultiplier, dt, _qbPastLos, useZone, _playManager.LineOfScrimmage);
            ClampToField(defender);
        }

        UpdateBlockers(dt);
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

        if (_ball.State == BallState.HeldByQB && !_qbPastLos && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            EnsureSelectedReceiverEligible();
            if (_playManager.SelectedReceiver >= 0 && _playManager.SelectedReceiver < _receivers.Count)
            {
                var receiver = _receivers[_playManager.SelectedReceiver];
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
                float inaccuracyDeg = Lerp(Constants.ThrowBaseInaccuracyDeg, Constants.ThrowMaxInaccuracyDeg, pressure);
                float inaccuracyRad = inaccuracyDeg * (MathF.PI / 180f);
                float angle = ((float)_rng.NextDouble() * 2f - 1f) * inaccuracyRad;
                dir = Rotate(dir, angle);

                _ball.SetInAir(_qb.Position, dir * speed);
            }
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

        DrawSelectedReceiverHighlight();
        
        // Draw side panel with all HUD info
        string targetLabel = "WR";
        if (_playManager.SelectedReceiver >= 0 && _playManager.SelectedReceiver < _receivers.Count)
        {
            targetLabel = _receivers[_playManager.SelectedReceiver].Glyph;
        }
        _hudRenderer.DrawSidePanel(_playManager, _lastPlayText, _playManager.SelectedReceiver, targetLabel, _stateManager.State);

        if (_stateManager.State == GameState.MainMenu)
        {
            _hudRenderer.DrawMainMenu();
        }

        if (_stateManager.IsPaused)
        {
            _hudRenderer.DrawPause();
        }
    }

    private void DrawSelectedReceiverHighlight()
    {
        if (_playManager.SelectedReceiver < 0 || _playManager.SelectedReceiver >= _receivers.Count) return;
        var receiver = _receivers[_playManager.SelectedReceiver];
        if (!receiver.Eligible) return;
        Vector2 screen = Constants.WorldToScreen(receiver.Position);
        Raylib.DrawCircleLines((int)screen.X, (int)screen.Y, 12, Palette.Yellow);
    }

    private static void ClampToField(Entity entity)
    {
        float x = MathF.Max(0.5f, MathF.Min(Constants.FieldWidth - 0.5f, entity.Position.X));
        float y = MathF.Max(0.5f, MathF.Min(Constants.FieldLength - 0.5f, entity.Position.Y));
        entity.Position = new Vector2(x, y);
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

    private void UpdateBlockers(float dt)
    {
        foreach (var blocker in _blockers)
        {
            Defender? target = GetClosestDefender(blocker.Position, Constants.BlockEngageRadius, preferRushers: true);
            if (target != null)
            {
                Vector2 toTarget = target.Position - blocker.Position;
                if (toTarget.LengthSquared() > 0.001f)
                {
                    toTarget = Vector2.Normalize(toTarget);
                }
                blocker.Velocity = toTarget * blocker.Speed;

                float contactRange = blocker.Radius + target.Radius + 0.6f;
                float distance = Vector2.Distance(blocker.Position, target.Position);
                if (distance <= contactRange)
                {
                    Vector2 pushDir = target.Position - blocker.Position;
                    if (pushDir.LengthSquared() > 0.001f)
                    {
                        pushDir = Vector2.Normalize(pushDir);
                    }
                    float overlap = contactRange - distance;
                    target.Position += pushDir * (Constants.BlockHoldStrength + overlap * 6f) * dt;
                    target.Velocity *= 0.15f;
                    blocker.Velocity *= 0.25f;
                }
            }
            else
            {
                blocker.Velocity = new Vector2(0f, blocker.Speed * 0.4f);
            }

            blocker.Update(dt);
            ClampToField(blocker);
        }
    }

    private void UpdateBlockingReceiver(Receiver receiver, float dt)
    {
        Defender? target = GetClosestDefender(receiver.Position, Constants.BlockEngageRadius, preferRushers: true);
        if (target != null)
        {
            Vector2 toTarget = target.Position - receiver.Position;
            if (toTarget.LengthSquared() > 0.001f)
            {
                toTarget = Vector2.Normalize(toTarget);
            }
            receiver.Velocity = toTarget * receiver.Speed;

            float contactRange = receiver.Radius + target.Radius + 0.6f;
            float distance = Vector2.Distance(receiver.Position, target.Position);
            if (distance <= contactRange)
            {
                Vector2 pushDir = target.Position - receiver.Position;
                if (pushDir.LengthSquared() > 0.001f)
                {
                    pushDir = Vector2.Normalize(pushDir);
                }
                float overlap = contactRange - distance;
                target.Position += pushDir * (Constants.BlockHoldStrength + overlap * 6f) * dt;
                target.Velocity *= 0.15f;
                receiver.Velocity *= 0.25f;
            }
        }
        else
        {
            receiver.Velocity = new Vector2(0f, receiver.Speed * 0.4f);
        }
    }

    private void SelectFirstEligibleReceiver()
    {
        for (int i = 0; i < _receivers.Count; i++)
        {
            if (_receivers[i].Eligible)
            {
                _playManager.SelectedReceiver = i;
                return;
            }
        }

        _playManager.SelectedReceiver = 0;
    }

    private void SelectNextEligibleReceiver()
    {
        if (_receivers.Count == 0) return;

        int start = _playManager.SelectedReceiver;
        for (int i = 1; i <= _receivers.Count; i++)
        {
            int index = (start + i) % _receivers.Count;
            if (_receivers[index].Eligible)
            {
                _playManager.SelectedReceiver = index;
                return;
            }
        }
    }

    private void EnsureSelectedReceiverEligible()
    {
        if (_playManager.SelectedReceiver >= 0 && _playManager.SelectedReceiver < _receivers.Count)
        {
            if (_receivers[_playManager.SelectedReceiver].Eligible) return;
        }

        SelectNextEligibleReceiver();
    }

    private void AutoSelectReceiver()
    {
        if (_receivers.Count == 0) return;

        const float maxTargetRange = 28f;
        const float openWeight = 1.35f;
        const float distanceWeight = 0.35f;

        int bestIndex = -1;
        float bestScore = float.MinValue;
        float bestOpen = float.MinValue;
        float bestDist = float.MaxValue;

        foreach (var receiver in _receivers)
        {
            if (!receiver.Eligible) continue;

            float distToQb = Vector2.Distance(receiver.Position, _qb.Position);
            if (distToQb > maxTargetRange) continue;

            float openDist = GetNearestDefenderDistance(receiver.Position);
            float score = openDist * openWeight - distToQb * distanceWeight;

            bool isBetter = score > bestScore;
            if (MathF.Abs(score - bestScore) < 0.001f)
            {
                if (openDist > bestOpen + 0.01f)
                {
                    isBetter = true;
                }
                else if (MathF.Abs(openDist - bestOpen) < 0.01f && distToQb < bestDist)
                {
                    isBetter = true;
                }
            }

            if (isBetter)
            {
                bestScore = score;
                bestOpen = openDist;
                bestDist = distToQb;
                bestIndex = receiver.Index;
            }
        }

        if (bestIndex >= 0 && bestIndex < _receivers.Count)
        {
            _playManager.SelectedReceiver = bestIndex;
        }
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

            Vector2 labelPos = Constants.WorldToScreen(points[0] + new Vector2(0.6f, 0.4f));
            Raylib.DrawText(ReceiverAI.GetRouteLabel(receiver.Route), (int)labelPos.X, (int)labelPos.Y, 12, routeColor);
        }
    }

    private static Color GetRouteColor(int receiverIndex)
    {
        return receiverIndex switch
        {
            0 => Palette.Gold,
            1 => Palette.Lime,
            2 => Palette.Blue,
            3 => Palette.Orange,
            _ => Palette.Yellow
        };
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
