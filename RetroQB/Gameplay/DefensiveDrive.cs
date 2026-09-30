using System.Numerics;
using RetroQB.AI;
using RetroQB.Entities;
using RetroQB.Gameplay.Controllers;
using RetroQB.Input;
using RetroQB.Gameplay.Replay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Gameplay;

/// <summary>Drive orchestration over the production controllers and match rules.</summary>
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
    public DefensiveEngineMode Engine { get; }
    public ResolvedDefensivePlan? CompiledPlan { get; private set; }
    public string? SelectionRejection { get; private set; }
    private readonly Dictionary<string, ResolvedDefensivePlan> _compiledCalls = new();
    private DefensePackage? _requestedPackage;
    private DefensiveFront _selectedFront;
    private readonly Dictionary<string, string> _unavailableCalls = new();
    public DefensiveCallSelection? CpuSelection { get; private set; }
    private DefensiveOutcomeTracker? _defenseOutcome;
    public string PersonnelSelection => _requestedPackage?.ToString() ?? "Auto";
    public bool SupportsDefense(string id) => Engine == DefensiveEngineMode.Legacy || _compiledCalls.ContainsKey(id);
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
        ?? (Execution.Control.AutomateDefense ? new ControlContext(true).ControlledDefender(Actors.Defenders) : null)
        ?? throw new InvalidOperationException("No supported linebacker in the defensive personnel.");
    public PlayResolution? LastResult { get; private set; }
    public IEnumerable<Entity> Players => new Entity[] { Actors.Qb }.Concat(Actors.Receivers).Concat(Actors.Blockers).Concat(Actors.Defenders);

    public DefensiveDrive(IPlayerMovementInput input, int seed, DriveStart? start = null,
        string? fixedCall = null, bool scriptedOffense = false, double quarterSeconds = 180, TimedMatch? match = null,
        DefensiveEngineMode engine = DefensiveEngineMode.Legacy)
    {
#if !DEBUG
        if (engine is not (DefensiveEngineMode.Legacy or DefensiveEngineMode.Production))
            throw new NotSupportedException("Partial defense engines are available only in Debug builds.");
#endif
        Engine = engine;
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
        if (!HumanOnDefense && _fixedCall == null && !retainOffensiveCall)
            Plays.SetCallSheet(SituationalCallSheet.Build(Plays.Catalog,
                new(Match.Series.Down, Match.Series.Distance, Plays.LineOfScrimmage, Plays.FirstDownLine),
                unchecked(_defenseSeed + Match.History.Count * 7919), Match.Offense.GetCallCount));
        Plays.SelectCatalogPlay(retained ?? _fixedCall ?? (HumanOnDefense
            ? _coordinator.Select(Match.Series, Match.Offense.Tendencies) : Plays.CallSheet.PassIds[0]), _random);
        PrepareActors();
    }

    private void PrepareActors()
    {
        PreparedOffenseId = Match.PossessionId;
        Execution.Control = new(HumanOnDefense);
        var context = new DefensiveContext(Plays.LineOfScrimmage, Match.Series.Distance, Match.Series.Down,
            Match.Offense.Score, Match.Defense.Score, Match.Stage, Plays.FirstDownLine);
        var formation = new FormationFactory().CreateFormation(Plays.SelectedPlay, context.LineOfScrimmage, Match.Offense.OffensiveAttributes);
        if (Engine != DefensiveEngineMode.Legacy)
        {
            _compiledCalls.Clear();
            _unavailableCalls.Clear();
            var snapshot = OffensiveAlignmentSnapshot.From(formation, context.LineOfScrimmage);
            foreach (var call in DefensiveEngineCalls.For(Engine))
            try
            {
                _compiledCalls.Add(call.Id, DefensivePlanCompiler.Compile(snapshot, call, HumanOnDefense ? _requestedPackage : null,
                    resolutionSeed: unchecked(_defenseSeed + Match.History.Count * 7919),
                    adjustments: (Engine is DefensiveEngineMode.ManDevelopment or DefensiveEngineMode.ZoneDevelopment or DefensiveEngineMode.MatchDevelopment or DefensiveEngineMode.StrategyDevelopment) && call.Zone == SpotZoneFamily.None && call.Match == MatchFamily.None ? new(switchPairs: true) : null,
                    front: HumanOnDefense ? _selectedFront : DefensiveFront.Even));
            }
            catch (ArgumentException error) { _unavailableCalls[call.Id] = error.Message; }
            string chosen = HumanOnDefense && CompiledPlan != null && _compiledCalls.ContainsKey(CompiledPlan.RequestedCall.Id)
                ? CompiledPlan.RequestedCall.Id : "def.cover1";
            var selected = _compiledCalls[chosen];
            CpuSelection = null;
            if (Engine == DefensiveEngineMode.StrategyDevelopment && !HumanOnDefense)
            {
                // A dedicated per-play draw: browsing previews never consumes/re-rolls the stream.
                int choiceSeed = unchecked(_defenseSeed ^ (int)(Match.History.Count * 7919) ^ 0x4d37);
                foreach (char ch in Match.Defense.Definition.Id) choiceSeed = unchecked(choiceSeed * 31 + ch);
                var situation = new DefensiveCallContext(Match.Series.Down, Match.Series.Distance, Match.Series.OwnYardLine,
                    Match.Defense.Score - Match.Offense.Score, Timed.Clock.RemainingSeconds,
                    Timed.Clock.IsOvertime || Timed.Clock.Quarter == Timed.Clock.RegulationPeriods,
                    Timed.Clock.Timeouts(Match.Offense.Definition.Id), Timed.Clock.Timeouts(Match.Defense.Definition.Id));
                CpuSelection = DefensivePlaycaller.Select(snapshot, situation, Match.Defense.DefensiveAttributes,
                    Match.Defense.DefenseStrategy, selected.ResolutionSeed, new Random(choiceSeed).NextDouble());
                selected = CpuSelection.Plan;
            }
            ApplyCompiledPlan(selected, retainOffense: false);
            ResetActors(); return;
        }
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
        ResetActors();
    }

    private void ResetActors()
    {
        Execution.Reset(); _quarterback.Reset(); _carrier.Reset(); _tackle.Reset();
        _ball.Reset(Plays.LineOfScrimmage); _priorities.AssignPriorities(Actors.Receivers);
        LiveSeconds = SecondsWithoutProgress = 0; _furthestY = Actors.Qb.Position.Y;
    }

    public IReadOnlyList<DefensivePlayDefinition> Calls => DefensivePlaybook.ForEngine(Engine);

    public bool SelectDefense(string id)
    {
        if (Live || Complete || LastResult != null || !HumanOnDefense) return false;
        if (Engine != DefensiveEngineMode.Legacy)
        {
            if (!_compiledCalls.TryGetValue(id, out var plan)) { SelectionRejection = _unavailableCalls.GetValueOrDefault(id) ?? "This call is unavailable in the selected development engine."; return false; }
            ApplyCompiledPlan(plan, retainOffense: true); return true;
        }
        SelectedDefense = _defensiveCalls[id];
        // Keep the offensive actors, offensive call and all clocks intact while browsing.
        var defenders = SelectedDefense.CreateDefense(Match.Defense.DefensiveAttributes);
        Actors = new(Actors.Qb, Actors.Ball, Actors.Receivers, Actors.Blockers, defenders.Defenders,
            defenders.UsesZoneResponsibilities, defenders.IsUnderneathManCoverage, defenders.Blitzers, defenders.Scheme);
        return true;
    }

    internal void FlipOffensiveFormationForDevelopment()
    {
        if (Live) throw new InvalidOperationException("Cannot recompile after snap.");
        Plays.FlipSelectedPlay(); PrepareActors();
    }

    private void ApplyCompiledPlan(ResolvedDefensivePlan plan, bool retainOffense)
    {
        var setup = _setup.SetupCompiledPlay(Plays.SelectedPlay, plan, Match.Offense.OffensiveAttributes, Match.Defense.DefensiveAttributes);
        if (retainOffense)
        {
            var defenders = setup.Defenders;
            if (Actors.Defenders.Select(d => d.Slot).Order().SequenceEqual(defenders.Select(d => d.Slot).Order()))
            {
                defenders = Actors.Defenders;
                foreach (var actor in defenders)
                {
                    var job = plan.Assignments.Single(a => a.ActorId == actor.Slot);
                    actor.SetAlignment(job.Alignment); actor.Velocity = Vector2.Zero;
                    actor.IsRusher = job.Pass is RushResponsibility;
                    actor.CoverageReceiverIndex = (job.Pass as ManResponsibility)?.Target.Value ?? (job.Pass as MatchResponsibility)?.Provisional?.Value ?? -1;
                }
            }
            setup = new(Actors.Qb, Actors.Ball, Actors.Receivers, Actors.Blockers, defenders,
                setup.UsesZoneResponsibilities, setup.IsUnderneathManCoverage, setup.Blitzers, setup.CoverageScheme);
        }
        Actors = setup; CompiledPlan = plan; SelectionRejection = null;
        Execution.DefenseRuntime = new(plan);
        SelectedDefense = DefensivePlanPresentation.Project(plan, Actors.Defenders);
    }

    public bool SelectManAdjustments(ManCoverageAdjustments adjustments)
    {
        if (Engine is not (DefensiveEngineMode.ManDevelopment or DefensiveEngineMode.ZoneDevelopment or DefensiveEngineMode.MatchDevelopment or DefensiveEngineMode.StrategyDevelopment) || Live || LastResult != null || Complete || CompiledPlan == null) return false;
        try
        {
            var plan = DefensivePlanCompiler.Compile(CompiledPlan.Offense, CompiledPlan.Call, CompiledPlan.Package, CompiledPlan.ResolutionSeed, adjustments, CompiledPlan.Pressure.Selection, CompiledPlan.Front);
            ApplyCompiledPlan(plan, retainOffense: true); return true;
        }
        catch (ArgumentException error) { SelectionRejection = error.Message; return false; }
    }

    public bool SelectPressure(PressureSelection selection)
        => HumanOnDefense && SelectCompiledPressureForDevelopment(selection);

    internal bool SelectCompiledPressureForDevelopment(PressureSelection selection)
    {
        if (Engine == DefensiveEngineMode.Legacy || Live || LastResult != null || Complete || CompiledPlan == null) return false;
        try
        {
            var plan = DefensivePlanCompiler.Compile(CompiledPlan.Offense, CompiledPlan.Call, CompiledPlan.Package,
                CompiledPlan.ResolutionSeed, CompiledPlan.Adjustments, selection, CompiledPlan.Front);
            ApplyCompiledPlan(plan, retainOffense: true); return true;
        }
        catch (ArgumentException error) { SelectionRejection = error.Message; return false; }
        catch (InvalidOperationException error) { SelectionRejection = error.Message; return false; }
    }

    public bool FlipPressure() => CompiledPlan?.Pressure.Selection.Kind is not (null or PressureKind.Base)
        && SelectPressure(CompiledPlan.Pressure.Selection.Flip());

    public bool CyclePressure(int step)
    {
        if (CompiledPlan == null || Live || LastResult != null || Complete || !HumanOnDefense || step == 0) return false;
        var variants = PressureVariants.Available(CompiledPlan, Match.Defense.DefensiveAttributes);
        if (variants.Count < 2) return false;
        int current = variants.ToList().FindIndex(v => v == CompiledPlan.Pressure.Selection);
        return SelectPressure(variants[(Math.Max(0, current) + Math.Sign(step) + variants.Count) % variants.Count]);
    }

    public bool SelectFormation(DefensePackage? package, DefensiveFront front)
        => HumanOnDefense && SelectFormationForDevelopment(package, front);

    internal bool SelectFormationForDevelopment(DefensePackage? package, DefensiveFront front)
    {
        if (Engine == DefensiveEngineMode.Legacy || Live || Complete || LastResult != null || CompiledPlan == null) return false;
        try
        {
            var prior = CompiledPlan;
            var selected = DefensivePlanCompiler.Compile(prior.Offense, prior.RequestedCall, package, prior.ResolutionSeed,
                prior.RequestedCall.Match == MatchFamily.None ? prior.Adjustments : null, prior.Pressure.Selection, front);
            var available = new Dictionary<string, ResolvedDefensivePlan>(); var rejected = new Dictionary<string, string>();
            foreach (var call in DefensiveEngineCalls.For(Engine))
            {
                try { available[call.Id] = DefensivePlanCompiler.Compile(prior.Offense, call, package, prior.ResolutionSeed,
                    call.Match == MatchFamily.None && call.Zone == SpotZoneFamily.None ? new(switchPairs: true) : null, front: front); }
                catch (ArgumentException error) { rejected[call.Id] = error.Message; }
            }
            ApplyCompiledPlan(selected, retainOffense: true);
            _compiledCalls.Clear(); foreach (var pair in available) _compiledCalls.Add(pair.Key, pair.Value);
            _unavailableCalls.Clear(); foreach (var pair in rejected) _unavailableCalls.Add(pair.Key, pair.Value);
            if (HumanOnDefense) { _requestedPackage = package; _selectedFront = front; }
            return true;
        }
        catch (ArgumentException error) { SelectionRejection = error.Message; return false; }
        catch (InvalidOperationException error) { SelectionRejection = error.Message; return false; }
    }

    public bool CyclePersonnel()
    {
        DefensePackage? next = _requestedPackage switch { null => DefensePackage.Base, DefensePackage.Base => DefensePackage.Nickel,
            DefensePackage.Nickel => DefensePackage.Dime, _ => null };
        return CompiledPlan != null && SelectFormation(next, CompiledPlan.Front);
    }

    public bool CycleFront() => CompiledPlan != null && SelectFormation(_requestedPackage,
        (DefensiveFront)(((int)CompiledPlan.Front + 1) % Enum.GetValues<DefensiveFront>().Length));

    internal bool SelectCompiledDefenseForDevelopment(string id)
    {
        if (Engine == DefensiveEngineMode.Legacy || Live || !_compiledCalls.TryGetValue(id, out var plan)) return false;
        ApplyCompiledPlan(plan, retainOffense: true); return true;
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
        SelectedDefense = null!; CompiledPlan = null; _requestedPackage = null; _selectedFront = DefensiveFront.Even; Prepare();
    }

    internal void PrepareSpecialTeams() { Prepare(); LastResult = null; }

    public PlayResolution ResolveSpecial(PlayEndReason reason, float spot, KickReturnResult? kickReturn = null)
    {
        LastResult = Timed.Resolve(new(Match.ActivePlay!.Id, Match.ActivePlay.OffenseId, reason, spot,
            reason == PlayEndReason.Kneel ? new(Rush: RushingRole.Quarterback) : null, KickReturn: kickReturn));
        return LastResult;
    }

    public bool Snap()
    {
        if (Live || Complete) return false;
        if (Timed.Advance(0, Plays.SelectedPlay.Id) == null) return false;
        Plays.StartPlay(); LastResult = null;
        _defenseOutcome = CompiledPlan == null ? null : new(CompiledPlan, Match.Defense.Definition.Id);
        Match.Defense.RecordCall(SelectedDefense.Definition.Id);
        _recorder.Begin(Match.History.Count + 1);
        var controlled = Execution.Control.ControlledDefender(Actors.Defenders);
        _replayContext = new(Match.PossessionId, Match.Defense.Definition.Id, Match.Stage, Timed.Clock.Snapshot(),
            Match.User.Score, Match.Opponent.Score, Match.User.Definition.Id, Match.Opponent.Definition.Id,
            controlled?.Slot, controlled == null ? -1 : Actors.Defenders.IndexOf(controlled), SelectedDefense, Plays.SelectedPlay.Name)
            { CompiledPlan = CompiledPlan, CpuSelection = CpuSelection, DefensiveRuntimeVersion = Execution.DefenseRuntime == null ? null : DefensiveRuntime.Version };
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
                    Plays.SelectedPlay.Id == "pass.four-verts" ? 10 : 2)).ToArray();
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
        Execution.DefenseRuntime?.Stop();
        ReceiverSlot? target = _ball.PassCatcher?.Slot ?? _ball.ThrowTargetSlot;
        var stats = new OffensivePlayStats(_ball.PassAttemptedThisPlay, _ball.PassCompletedThisPlay, target,
            _ball.PassAttemptedThisPlay || contact.Reason == PlayEndReason.Sack ? RushingRole.None
                : Actors.Ball.Holder is Receiver ? RushingRole.RunningBack : RushingRole.Quarterback);
        CaptureReplay(0);
        LastReplayDefense = SelectedDefense;
        var controlled = Execution.Control.ControlledDefender(Actors.Defenders);
        LastReplayControlledIndex = controlled == null ? -1 : Actors.Defenders.IndexOf(controlled);
        LastResult = Timed.Resolve(contact.ToEvent(Match.ActivePlay!, stats, Actors.CoverageScheme) with
            { ControlledDefender = controlled?.Slot, DefenseOutcome = _defenseOutcome?.Complete() });
        LastReplay = _recorder.FinalizeClip(contact.Reason switch
        {
            PlayEndReason.Touchdown => PlayOutcome.Touchdown, PlayEndReason.Interception => PlayOutcome.Interception,
            PlayEndReason.Incomplete => PlayOutcome.Incomplete, PlayEndReason.PassDefended => PlayOutcome.PassDefended,
            PlayEndReason.Safety => PlayOutcome.Safety, _ => PlayOutcome.Tackle
        });
        if (LastReplay != null) LastReplay.MatchContext = _replayContext;
    }

    private void CaptureReplay(float dt)
    {
        if (Execution.DefenseRuntime?.Current is { } frame)
            _defenseOutcome?.Observe(frame, Actors.Ball.State, Execution.Control.ControlledDefender(Actors.Defenders)?.Slot);
        _recorder.Capture(Actors.Qb, Actors.Ball, Actors.Receivers, Actors.Blockers, Actors.Defenders,
            Plays.LineOfScrimmage, Plays.FirstDownLine, dt, Plays.Down, Execution.DefenseRuntime?.Current);
    }

    private void Clamp(Entity actor)
    {
        if (Engine != DefensiveEngineMode.Legacy && actor is Defender) { actor.Position = DefenderMotor.Clamp(actor.Position); return; }
        bool carrier = Actors.Ball.State == BallState.HeldByQB ? actor == Actors.Qb : actor == Actors.Ball.Holder;
        actor.Position = new(carrier ? actor.Position.X : Math.Clamp(actor.Position.X, .5f, Constants.FieldWidth - .5f),
            Math.Clamp(actor.Position.Y, .5f, Constants.FieldLength - .5f));
    }
}
