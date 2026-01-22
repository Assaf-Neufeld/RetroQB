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

    private bool _aiming;
    private float _aimPower;
    private Vector2 _aimDirection = Vector2.UnitY;
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
        _qb = new Quarterback(new Vector2(Constants.FieldWidth / 2f, los - 2f));
        _ball = new Ball(_qb.Position);
        _ball.SetHeld(_qb, BallState.HeldByQB);

        _receivers.Add(new Receiver(0, new Vector2(Constants.FieldWidth * 0.25f, los + 2f)));
        _receivers.Add(new Receiver(1, new Vector2(Constants.FieldWidth * 0.5f, los + 2f)));
        _receivers.Add(new Receiver(2, new Vector2(Constants.FieldWidth * 0.75f, los + 2f)));

        _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.35f, los + 1f)));
        _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.5f, los + 1f)));
        _blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.65f, los + 1f)));

        SpawnDefenders(los);
        ReceiverAI.AssignRoutes(_receivers, _playManager.SelectedPlayType, _rng);
    }

    private void SpawnDefenders(float los)
    {
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.4f, los + 6f)) { IsRusher = true });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.6f, los + 6f)) { IsRusher = true });

        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.2f, los + 10f)) { CoverageReceiverIndex = 0 });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.5f, los + 12f)) { CoverageReceiverIndex = 1 });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.8f, los + 10f)) { CoverageReceiverIndex = 2 });

        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.35f, los + 14f)) { CoverageReceiverIndex = 0 });
        _defenders.Add(new Defender(new Vector2(Constants.FieldWidth * 0.65f, los + 14f)) { CoverageReceiverIndex = 2 });
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

        if (_input.IsAimPressed())
        {
            _aiming = true;
            _aimPower = 0f;
        }

        if (_aiming)
        {
            _aimPower = MathF.Min(1f, _aimPower + dt / Constants.AimChargeSeconds);
            Vector2 mouse = Raylib.GetMousePosition();
            Vector2 qbScreen = Constants.WorldToScreen(_qb.Position);
            _aimDirection = mouse - qbScreen;
            if (_aimDirection.LengthSquared() > 0.001f)
            {
                _aimDirection = Vector2.Normalize(_aimDirection);
            }
        }

        _qb.ApplyInput(inputDir, sprint, _aiming, dt);
        _qb.Update(dt);
        ClampToField(_qb);

        foreach (var receiver in _receivers)
        {
            ReceiverAI.UpdateRoute(receiver, dt);
            receiver.Update(dt);
            ClampToField(receiver);
        }

        foreach (var blocker in _blockers)
        {
            blocker.Velocity = new Vector2(0f, 2.2f);
            blocker.Update(dt);
            ClampToField(blocker);
        }

        foreach (var defender in _defenders)
        {
            DefenderAI.UpdateDefender(defender, _qb, _receivers, _ball, _playManager.DefenderSpeedMultiplier, dt);
            ClampToField(defender);
        }

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

        if (_aiming && _input.IsAimReleased() && _ball.State == BallState.HeldByQB)
        {
            float speed = Constants.BallMinSpeed + (Constants.BallMaxSpeed - Constants.BallMinSpeed) * _aimPower;
            Vector2 velocity = _aimDirection * speed;
            _ball.SetInAir(_qb.Position, velocity);
            _aiming = false;
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
        _aiming = false;
        _aimPower = 0f;
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

        if (_aiming)
        {
            Vector2 qbScreen = Constants.WorldToScreen(_qb.Position);
            Vector2 aimEnd = qbScreen + _aimDirection * 80f;
            Raylib.DrawLine((int)qbScreen.X, (int)qbScreen.Y, (int)aimEnd.X, (int)aimEnd.Y, Palette.Yellow);
            DrawSelectedReceiverHighlight();
        }

        float aimPower = _aiming ? _aimPower : -1f;
        _hudRenderer.DrawHud(_playManager, _lastPlayText, _playManager.SelectedReceiver, aimPower);

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
}
