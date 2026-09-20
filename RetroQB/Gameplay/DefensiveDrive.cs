using System.Numerics;
using RetroQB.AI;
using RetroQB.Entities;
using RetroQB.Gameplay.Controllers;
using RetroQB.Input;
using RetroQB.Gameplay.Replay;

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
    private readonly bool _fullMatch;
    private readonly IGameInput? _gameInput;
    public bool HumanOnDefense => Match.PossessionId != Match.User.Definition.Id;
    public string PreparedOffenseId { get; private set; } = "";
    public string ReceiverLabel(int index) => _priorities.GetPriorityLabel(index);
    private float _furthestY;
    private readonly int _defenseSeed;
    private readonly Dictionary<string, ResolvedDefensivePlay> _defensiveCalls = new();
    public ResolvedDefensivePlay SelectedDefense { get; private set; } = null!;
    public TimedMatch Timed { get; }
    public MatchState Match => Timed.Match;
    private readonly ReplayRecorder _recorder = new();
    private ReplayMatchContext? _replayContext;
    public ReplayClip? LastReplay { get; private set; }
    public ResolvedDefensivePlay? LastReplayDefense { get; private set; }
    public int LastReplayControlledIndex { get; private set; }
    public PlayManager Plays { get; } = new();
    public PlayExecutionController Execution { get; }
    public PlaySetupResult Actors { get; private set; } = null!;
    public bool Live => Match.ActivePlay != null;
    public bool Complete => Timed.Finished || !_fullMatch && (Match.PendingPossession != null || !HumanOnDefense);
    public float LiveSeconds { get; private set; }
    public float SecondsWithoutProgress { get; private set; }
    public int SteeringChanges => _carrier.SteeringChanges;
    public int Throws { get; private set; }
    public Defender Linebacker => Execution.Control.ControlledDefender(Actors.Defenders)
        ?? throw new InvalidOperationException("No supported linebacker in the defensive personnel.");
    public PlayResolution? LastResult { get; private set; }
    public IEnumerable<Entity> Players => new Entity[] { Actors.Qb }.Concat(Actors.Receivers).Concat(Actors.Blockers).Concat(Actors.Defenders);

    public DefensiveDrive(IPlayerMovementInput input, int seed, DriveStart? start = null,
        string? fixedCall = null, bool scriptedOffense = false, double quarterSeconds = 180, TimedMatch? match = null)
    {
        _random = new(seed); _coordinator = new(_random); _fixedCall = fixedCall; _scriptedOffense = scriptedOffense;
        _defenseSeed = seed;
        var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        _fullMatch = match != null; _gameInput = input as IGameInput;
        Timed = match ?? new(user, cpu, cpu.Id, start: start, quarterSeconds: quarterSeconds);
        _quarterback = new(Match.Opponent.Tendencies.ReadIntervalSeconds);
        _setup = new(new FormationFactory(), new DefenseFactory(), new DefensiveCoordinator(Match.User.DefensiveMemory), _random);
        _ball = new(_random, new ThrowingMechanics(), new StatisticsTracker(), _priorities);
        _tackle = new(_random, _overlap);
        Execution = new(input, new BlockingController()) { Control = new(true) };
        Prepare();
    }

    private void Prepare(bool retainOffensiveCall = false)
    {
        string? retained = retainOffensiveCall ? Plays.SelectedPlay.Id : null;
        Plays.StartNewDrive(Match.Series);
        Plays.SelectCatalogPlay(retained ?? _fixedCall ?? _coordinator.Select(Match.Series, Match.Offense.Tendencies), _random);
        PrepareActors();
    }

    private void PrepareActors()
    {
        PreparedOffenseId = Match.PossessionId;
        Execution.Control = new(HumanOnDefense);
        var context = new DefensiveContext(Plays.LineOfScrimmage, Match.Series.Distance, Match.Series.Down,
            Match.Offense.Score, Match.Defense.Score, Match.Stage, Plays.FirstDownLine);
        var formation = new FormationFactory().CreateFormation(Plays.SelectedPlay, context.LineOfScrimmage, Match.Offense.OffensiveAttributes);
        var visible = VisibleOffense.From(formation);
        _defensiveCalls.Clear();
        for (int i = 0; i < DefensivePlaybook.All.Count; i++)
        {
            var definition = DefensivePlaybook.All[i];
            _defensiveCalls.Add(definition.Id, DefensivePlayResolver.Resolve(definition, visible, context,
                Match.Defense.DefensiveAttributes, new Random(unchecked(_defenseSeed + Match.History.Count * 7919 + i * 101))));
        }
        SelectedDefense = _defensiveCalls[HumanOnDefense ? SelectedDefense?.Definition.Id ?? "def.cover3"
            : DefensivePlaybook.All[new Random(unchecked(_defenseSeed + Match.History.Count * 7919)).Next(DefensivePlaybook.All.Count)].Id];
        Actors = _setup.SetupPlay(Plays.SelectedPlay, context.LineOfScrimmage, SelectedDefense,
            Match.Offense.OffensiveAttributes, Match.Defense.DefensiveAttributes);
        Execution.Reset(); _quarterback.Reset(); _carrier.Reset(); _tackle.Reset();
        _ball.Reset(Plays.LineOfScrimmage); _priorities.AssignPriorities(Actors.Receivers);
        LiveSeconds = SecondsWithoutProgress = 0; _furthestY = Actors.Qb.Position.Y;
    }

    public bool SelectDefense(string id)
    {
        if (Live || Complete || LastResult != null || !HumanOnDefense) return false;
        SelectedDefense = _defensiveCalls[id];
        // Keep the offensive actors, offensive call and all clocks intact while browsing.
        var defenders = SelectedDefense.CreateDefense(Match.Defense.DefensiveAttributes);
        Actors = new(Actors.Qb, Actors.Ball, Actors.Receivers, Actors.Blockers, defenders.Defenders,
            defenders.UsesZoneResponsibilities, defenders.IsUnderneathManCoverage, defenders.Blitzers, defenders.Scheme);
        return true;
    }

    public bool SelectOffense(int index, bool run = false, bool flip = false)
    {
        if (HumanOnDefense || Live || Complete || LastResult != null) return false;
        bool selected = flip || (run ? Plays.SelectRunPlay(index, _random) : Plays.SelectPassPlay(index, _random));
        if (!selected) return false;
        if (flip) Plays.FlipSelectedPlay();
        PrepareActors();
        return true;
    }

    public void Restart()
    {
        Timed.Restart(); LastResult = null; LastReplay = null; LastReplayDefense = null; Throws = 0;
        SelectedDefense = null!; Prepare();
    }

    public PlayResolution ResolveSpecial(PlayEndReason reason, float spot)
    {
        LastResult = Timed.Resolve(new(Match.ActivePlay!.Id, Match.ActivePlay.OffenseId, reason, spot,
            reason == PlayEndReason.Kneel ? new(Rush: RushingRole.Quarterback) : null));
        return LastResult;
    }

    public bool Snap()
    {
        if (Live || Complete) return false;
        if (Timed.Advance(0, Plays.SelectedPlay.Id) == null) return false;
        Plays.StartPlay(); LastResult = null;
        Match.Defense.RecordCall(SelectedDefense.Definition.Id);
        _recorder.Begin(Match.History.Count + 1);
        _replayContext = new(Match.PossessionId, Match.Defense.Definition.Id, Match.Stage, Timed.Clock.Snapshot(),
            Match.User.Score, Match.Opponent.Score, Match.User.Definition.Id, Match.Opponent.Definition.Id,
            HumanOnDefense ? Linebacker.Slot : null, HumanOnDefense ? Actors.Defenders.IndexOf(Linebacker) : -1, SelectedDefense, Plays.SelectedPlay.Name);
        CaptureReplay(0);
        return true;
    }

    public bool Continue()
    {
        if (Live || Complete || LastResult == null && Timed.Clock.Phase != ClockPhase.PeriodBreak) return false;
        if (!Timed.Continue()) return false;
        Prepare(); LastResult = null; return true;
    }

    public void AdvancePreSnap(double dt)
    {
        if (Live || Complete || LastResult != null) return;
        var before = Match.Series;
        string possession = Match.PossessionId;
        Timed.Advance(dt);
        if (Complete) return;
        if (!_fullMatch && Timed.Clock.Phase == ClockPhase.PeriodBreak) Timed.Continue();
        if (before != Match.Series || possession != Match.PossessionId) Prepare(retainOffensiveCall: possession == Match.PossessionId);
    }

    public void Update(float dt)
    {
        if (!float.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        if (!Live || Timed.Clock.Suspension != ClockSuspension.None) return;
        Timed.Advance(dt);
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
            Match.Offense.OffensiveAttributes, Match.Defense.DefensiveAttributes, Plays.SelectedReceiver, dt);
        if (_ball.LastTerminal is { } flight) { Finish(flight); return; }
        _tackle.CheckTackleOrScore(Actors.Ball, Actors.Qb, Actors.Defenders, Match.Offense.OffensiveAttributes, Clamp, Plays.LineOfScrimmage);
        if (_tackle.LastTerminal is { } contact) { Finish(contact); return; }
        if (!HumanOnDefense && Execution.Backfield.AllowsThrow)
            _ball.HandleThrowInput(Actors.Ball, Actors.Qb, Actors.Receivers, Actors.Defenders, Plays,
                Match.Offense.OffensiveAttributes, past, _gameInput?.GetThrowTarget());
        if (HumanOnDefense && intent.ReceiverIndex is { } target && _ball.TryThrow(target, Actors.Ball, Actors.Qb, Actors.Receivers,
            Actors.Defenders, Plays, Match.Offense.OffensiveAttributes, Execution.Backfield.AllowsThrow)) Throws++;
        if (HumanOnDefense && intent.ThrowAway && _ball.TryThrowAway(Actors.Ball, Actors.Qb, Plays,
            Match.Offense.OffensiveAttributes, Execution.Backfield.AllowsThrow)) Throws++;
        foreach (var actor in Players) actor.Animation.Update(dt, actor.Velocity);
        CaptureReplay(dt);
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
        CaptureReplay(0);
        LastReplayDefense = SelectedDefense;
        LastReplayControlledIndex = HumanOnDefense ? Actors.Defenders.IndexOf(Linebacker) : -1;
        LastResult = Timed.Resolve(contact.ToEvent(Match.ActivePlay!, stats, Actors.CoverageScheme) with
            { ControlledDefender = HumanOnDefense ? Linebacker.Slot : null });
        LastReplay = _recorder.FinalizeClip(contact.Reason switch
        {
            PlayEndReason.Touchdown => PlayOutcome.Touchdown, PlayEndReason.Interception => PlayOutcome.Interception,
            PlayEndReason.Incomplete => PlayOutcome.Incomplete, PlayEndReason.PassDefended => PlayOutcome.PassDefended,
            PlayEndReason.Safety => PlayOutcome.Safety, _ => PlayOutcome.Tackle
        });
        if (LastReplay != null) LastReplay.MatchContext = _replayContext;
    }

    private void CaptureReplay(float dt) => _recorder.Capture(Actors.Qb, Actors.Ball, Actors.Receivers,
        Actors.Blockers, Actors.Defenders, Plays.LineOfScrimmage, Plays.FirstDownLine, dt, Plays.Down);

    private void Clamp(Entity actor)
    {
        bool carrier = Actors.Ball.State == BallState.HeldByQB ? actor == Actors.Qb : actor == Actors.Ball.Holder;
        actor.Position = new(carrier ? actor.Position.X : Math.Clamp(actor.Position.X, .5f, Constants.FieldWidth - .5f),
            Math.Clamp(actor.Position.Y, .5f, Constants.FieldLength - .5f));
    }
}
