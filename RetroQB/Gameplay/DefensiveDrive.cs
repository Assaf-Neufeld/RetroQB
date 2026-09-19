using System.Numerics;
using RetroQB.AI;
using RetroQB.Entities;
using RetroQB.Gameplay.Controllers;
using RetroQB.Input;

namespace RetroQB.Gameplay;

/// <summary>Development drive orchestration over the production controllers and match rules.</summary>
public sealed class DefensiveDrive
{
    private readonly Random _random;
    private readonly OffensiveCoordinator _coordinator;
    private readonly PlaySetupController _setup;
    private readonly ReceiverPriorityManager _priorities = new();
    private readonly OverlapResolver _overlap = new();
    private readonly TackleController _tackle;
    private readonly BallController _ball;
    private readonly QuarterbackAI _quarterback;
    private readonly BallCarrierAI _carrier = new();
    private readonly string? _fixedCall;
    private readonly bool _scriptedOffense;
    private float _furthestY;
    public MatchState Match { get; }
    public PlayManager Plays { get; } = new();
    public PlayExecutionController Execution { get; }
    public PlaySetupResult Actors { get; private set; } = null!;
    public bool Live => Match.ActivePlay != null;
    public bool Complete => Match.PendingPossession != null;
    public float LiveSeconds { get; private set; }
    public float SecondsWithoutProgress { get; private set; }
    public int SteeringChanges => _carrier.SteeringChanges;
    public int Throws { get; private set; }
    public Defender Linebacker => Execution.Control.ControlledDefender(Actors.Defenders)
        ?? throw new InvalidOperationException("No supported linebacker in the defensive personnel.");
    public PlayResolution? LastResult { get; private set; }
    public IEnumerable<Entity> Players => new Entity[] { Actors.Qb }.Concat(Actors.Receivers).Concat(Actors.Blockers).Concat(Actors.Defenders);

    public DefensiveDrive(IPlayerMovementInput input, int seed, DriveStart? start = null,
        string? fixedCall = null, bool scriptedOffense = false)
    {
        _random = new(seed); _coordinator = new(_random); _fixedCall = fixedCall; _scriptedOffense = scriptedOffense;
        var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        Match = new(user, cpu, cpu.Id, start: start);
        _quarterback = new(Match.Opponent.Tendencies.ReadIntervalSeconds);
        _setup = new(new FormationFactory(), new DefenseFactory(), new DefensiveCoordinator(Match.User.DefensiveMemory), _random);
        _ball = new(_random, new ThrowingMechanics(), new StatisticsTracker(), _priorities);
        _tackle = new(_random, _overlap);
        Execution = new(input, new BlockingController()) { Control = new(true) };
        Prepare();
    }

    private void Prepare()
    {
        Plays.StartNewDrive(Match.Series);
        Plays.SelectCatalogPlay(_fixedCall ?? _coordinator.Select(Match.Series, Match.Opponent.Tendencies), _random);
        var context = new DefensiveContext(Plays.LineOfScrimmage, Match.Series.Distance, Match.Series.Down,
            Match.Opponent.Score, Match.User.Score, SeasonStage.RegularSeason, Plays.FirstDownLine);
        Actors = _setup.SetupPlay(Plays.SelectedPlay, context,
            new(CoverageScheme.Cover3Zone, BlitzDecision.None), Match.Opponent.OffensiveAttributes, Match.User.DefensiveAttributes);
        Execution.Reset(); _quarterback.Reset(); _carrier.Reset(); _tackle.Reset();
        _ball.Reset(Plays.LineOfScrimmage); _priorities.AssignPriorities(Actors.Receivers);
        LiveSeconds = SecondsWithoutProgress = 0; _furthestY = Actors.Qb.Position.Y;
    }

    public bool Snap()
    {
        if (Live || Complete) return false;
        Plays.StartPlay(); Match.BeginPlay(Plays.SelectedPlay.Id); LastResult = null;
        return true;
    }

    public bool Continue()
    {
        if (Live || Complete || LastResult == null) return false;
        Prepare(); LastResult = null; return true;
    }

    public void Update(float dt)
    {
        if (!float.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        if (!Live) return;
        LiveSeconds += dt;
        bool past = Actors.Qb.Position.Y > Plays.LineOfScrimmage + .1f;
        OffensiveIntent intent;
        if (Actors.Ball.State == BallState.HeldByReceiver && Actors.Ball.Holder is Receiver receiver)
        {
            Vector2? entry = Plays.SelectedPlay.Family == PlayType.Run
                ? new Vector2(Constants.FieldWidth / 2 + Plays.SelectedPlay.RunningBackSide * 6, Plays.LineOfScrimmage + 4) : null;
            intent = new(_carrier.Decide(receiver.Position, Actors.Defenders.Select(d => d.Position).ToArray(),
                Actors.Blockers.Select(b => b.Position).ToArray(), entry, dt), true);
        }
        else if (Plays.SelectedPlay.Family == PlayType.Run)
            intent = new(Execution.Backfield.Phase == BackfieldPhase.Aborted ? Vector2.UnitY : Vector2.Zero);
        else if (_scriptedOffense)
            intent = new(Vector2.Zero, ReceiverIndex: LiveSeconds >= 1 ? _priorities.GetFirstReceiverIndex() : null);
        else
        {
            var reads = _priorities.GetPriorityIndices().Select(i => Actors.Receivers[i])
                .Select(r => new ReceiverRead(r.Index, r.Position, r.Velocity, r.Eligible, r.IsBlocking,
                    Plays.SelectedPlay.Id == "pass.four-verts" ? 10 : -10)).ToArray();
            // The authored fake must start before any throw is considered.
            bool ready = Execution.Backfield.AllowsThrow
                && (Plays.SelectedPlay.Backfield.Action != BackfieldAction.PlayAction || LiveSeconds > dt);
            intent = _quarterback.Decide(new(Actors.Qb.Position, Plays.LineOfScrimmage, ready, reads,
                Actors.Defenders.Select(d => d.Position).ToArray()), dt);
        }
        Execution.CpuIntent = intent;
        Execution.UpdatePlay(Actors.Qb, Actors.Ball, Actors.Receivers, Actors.Defenders, Actors.Blockers,
            Plays, past, Actors.UsesZoneResponsibilities, Actors.IsUnderneathManCoverage, Clamp, dt);
        _overlap.ResolveOverlaps(Actors.Qb, Actors.Ball, Actors.Receivers, Actors.Blockers, Actors.Defenders,
            Plays.LineOfScrimmage, Clamp, Execution.Backfield);
        _ball.Update(Actors.Ball, Actors.Qb, Actors.Receivers, Actors.Defenders,
            Match.Opponent.OffensiveAttributes, Match.User.DefensiveAttributes, Plays.SelectedReceiver, dt);
        if (_ball.LastTerminal is { } flight) { Finish(flight); return; }
        _tackle.CheckTackleOrScore(Actors.Ball, Actors.Qb, Actors.Defenders, Match.Opponent.OffensiveAttributes, Clamp, Plays.LineOfScrimmage);
        if (_tackle.LastTerminal is { } contact) { Finish(contact); return; }
        if (intent.ReceiverIndex is { } target && _ball.TryThrow(target, Actors.Ball, Actors.Qb, Actors.Receivers,
            Actors.Defenders, Plays, Match.Opponent.OffensiveAttributes, Execution.Backfield.AllowsThrow)) Throws++;
        if (intent.ThrowAway && _ball.TryThrowAway(Actors.Ball, Actors.Qb, Plays,
            Match.Opponent.OffensiveAttributes, Execution.Backfield.AllowsThrow)) Throws++;
        foreach (var actor in Players) actor.Animation.Update(dt, actor.Velocity);
        float y = Actors.Ball.Holder?.Position.Y ?? Actors.Ball.Position.Y;
        if (y > _furthestY + .25f) { _furthestY = y; SecondsWithoutProgress = 0; }
        else SecondsWithoutProgress += dt;
    }

    private void Finish(PlayContact contact)
    {
        ReceiverSlot? target = _ball.PassCatcher?.Slot ?? _ball.ThrowTargetSlot;
        var stats = new OffensivePlayStats(_ball.PassAttemptedThisPlay, _ball.PassCompletedThisPlay, target,
            _ball.PassAttemptedThisPlay || contact.Reason == PlayEndReason.Sack ? RushingRole.None
                : Actors.Ball.Holder is Receiver ? RushingRole.RunningBack : RushingRole.Quarterback);
        LastResult = Match.Resolve(contact.ToEvent(Match.ActivePlay!, stats, Actors.CoverageScheme));
    }

    private void Clamp(Entity actor)
    {
        bool carrier = Actors.Ball.State == BallState.HeldByQB ? actor == Actors.Qb : actor == Actors.Ball.Holder;
        actor.Position = new(carrier ? actor.Position.X : Math.Clamp(actor.Position.X, .5f, Constants.FieldWidth - .5f),
            Math.Clamp(actor.Position.Y, .5f, Constants.FieldLength - .5f));
    }
}
