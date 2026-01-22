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

        // Offensive formation: Singleback with TE, 3WR (2 wide + slot), RB
        _receivers.Add(new Receiver(0, new Vector2(Constants.FieldWidth * 0.20f, los + 1.6f))); // X WR (left)
        _receivers.Add(new Receiver(1, new Vector2(Constants.FieldWidth * 0.38f, los + 1.2f))); // Slot (left)
        _receivers.Add(new Receiver(2, new Vector2(Constants.FieldWidth * 0.80f, los + 1.6f))); // Z WR (right)
        _receivers.Add(new Receiver(3, new Vector2(Constants.FieldWidth * 0.66f, los - 0.1f))); // TE (right)

        // Offensive line: LT, LG, C, RG, RT
        _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.42f, los - 0.1f)));
        _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.47f, los - 0.1f)));
        _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.50f, los - 0.1f)));
        _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.53f, los - 0.1f)));
        _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.58f, los - 0.1f)));

        // Running back
        _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.5f, los - 3.5f)));

        SpawnDefenders(los);
        ReceiverAI.AssignRoutes(_receivers, _playManager.SelectedPlayType, _rng);
    }

    private void SpawnDefenders(float los)
    {
        // Defensive formation: 4-3 with 4 DB
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, los + 2.8f)) { IsRusher = true });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.46f, los + 2.8f)) { IsRusher = true });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.54f, los + 2.8f)) { IsRusher = true });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, los + 2.8f)) { IsRusher = true });

        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.38f, los + 6.2f)) { CoverageReceiverIndex = 1 }); // LB
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.50f, los + 6.2f)) { CoverageReceiverIndex = 3 }); // MLB (TE)
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.62f, los + 6.2f)) { CoverageReceiverIndex = 2 }); // LB

        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.20f, los + 10.5f)) { CoverageReceiverIndex = 0 }); // CB
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.80f, los + 10.5f)) { CoverageReceiverIndex = 2 }); // CB
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.40f, los + 12.5f)) { CoverageReceiverIndex = 1 }); // S
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.60f, los + 12.5f)) { CoverageReceiverIndex = 3 }); // S
    }

    public void Update(float dt)
    {
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
        if (Raylib.IsKeyPressed(KeyboardKey.One)) _playManager.SelectedPlayType = PlayType.QuickPass;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) _playManager.SelectedPlayType = PlayType.LongPass;
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) _playManager.SelectedPlayType = PlayType.QbRunFocus;

        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            _playManager.StartPlay();
            SetupEntities();
            _stateManager.SetState(GameState.PlayActive);
        }
    }

    private void HandlePlayActive(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            _playManager.SelectedReceiver = (_playManager.SelectedReceiver + 1) % _receivers.Count;
        }

        Vector2 inputDir = _input.GetMovementDirection();
        bool sprint = _input.IsSprintHeld();

        _qb.ApplyInput(inputDir, sprint, false, dt);
        _qb.Update(dt);
        ClampToField(_qb);

        foreach (var receiver in _receivers)
        {
            ReceiverAI.UpdateRoute(receiver, dt);
            receiver.Update(dt);
            ClampToField(receiver);
        }

        foreach (var defender in _defenders)
        {
            DefenderAI.UpdateDefender(defender, _qb, _receivers, _ball, _playManager.DefenderSpeedMultiplier, dt);
            ClampToField(defender);
        }

        UpdateBlockers(dt);

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
                if (Vector2.Distance(receiver.Position, _ball.Position) <= Constants.CatchRadius)
                {
                    bool intercepted = _defenders.Any(d => Vector2.Distance(d.Position, _ball.Position) <= Constants.InterceptRadius);
                    if (intercepted)
                    {
                        EndPlay(intercepted: true);
                        return;
                    }

                    receiver.HasBall = true;
                    _ball.SetHeld(receiver, BallState.HeldByReceiver);
                    return;
                }
            }
        }

        if (_ball.State == BallState.HeldByQB && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            if (_playManager.SelectedReceiver >= 0 && _playManager.SelectedReceiver < _receivers.Count)
            {
                Vector2 target = _receivers[_playManager.SelectedReceiver].Position;
                Vector2 dir = target - _qb.Position;
                if (dir.LengthSquared() > 0.001f)
                {
                    dir = Vector2.Normalize(dir);
                }
                float speed = Constants.BallMaxSpeed;
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
        _hudRenderer.DrawHud(_playManager, _lastPlayText, _playManager.SelectedReceiver);

        if (_stateManager.State == GameState.MainMenu)
        {
            _hudRenderer.DrawMainMenu();
        }
        else if (_stateManager.State == GameState.PreSnap)
        {
            _hudRenderer.DrawPreSnap(_playManager.SelectedPlayType);
        }
        else if (_stateManager.State == GameState.PlayOver)
        {
            _hudRenderer.DrawPlayOver();
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
        Vector2 screen = Constants.WorldToScreen(receiver.Position);
        Raylib.DrawCircleLines((int)screen.X, (int)screen.Y, 12, Palette.Yellow);
    }

    private static void ClampToField(Entity entity)
    {
        float x = MathF.Max(0.5f, MathF.Min(Constants.FieldWidth - 0.5f, entity.Position.X));
        float y = MathF.Max(0.5f, MathF.Min(Constants.FieldLength - 0.5f, entity.Position.Y));
        entity.Position = new Vector2(x, y);
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
                blocker.Velocity = toTarget * Constants.BlockerSpeed;

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
                blocker.Velocity = new Vector2(0f, Constants.BlockerSpeed * 0.4f);
            }

            blocker.Update(dt);
            ClampToField(blocker);
        }
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
}
