using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Stats;

namespace RetroQB.Development;

internal sealed record ScenarioTrace(int Tick, InputFrame Input, SessionSnapshot State);

/// <summary>A small fixed-step driver of the production session, not a second simulation.</summary>
internal sealed class ScenarioRun : IDisposable
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true, IncludeFields = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ScriptedInput _input = new();
    private readonly List<ScenarioTrace> _trace = new();
    private int _activeTicks;
    private bool _replayEntered;
    public ScenarioDefinition Definition { get; }
    public int Seed { get; }
    public string OutputDirectory { get; }
    public string RecordPath { get; }
    internal PlayerRecordStore Records { get; }
    public GameSession Session { get; }
    public SessionSnapshot Current { get; private set; }
    public IReadOnlyList<ScenarioTrace> Trace => _trace;
    public int Tick { get; private set; }
    public bool Complete { get; private set; }
    public string? Failure { get; private set; }

    public ScenarioRun(ScenarioDefinition definition, int seed, string? outputDirectory = null)
    {
        Definition = definition;
        Seed = seed;
        OutputDirectory = Path.GetFullPath(outputDirectory ?? Path.Combine(Path.GetTempPath(),
            "RetroQB", "scenarios", $"{definition.Name}-{seed}-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(OutputDirectory);
        // Saves ALWAYS live in a fresh temp directory, even when reports go to a supplied output directory.
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RetroQB", "scenario-saves", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveDirectory);
        RecordPath = Path.Combine(saveDirectory, "player-records.json");
        Records = new PlayerRecordStore(RecordPath);
        Session = new GameSession(_input, new Random(seed), Records);
        Session.StartScenario(definition.Start, definition.PlayId, definition.Offense, definition.Defense, definition.Stage);
        Current = Session.CaptureSnapshot();
        _trace.Add(new(0, new(), Current));
    }

    public void Step(InputFrame? scriptedOverride = null)
    {
        if (Complete) return;
        var previous = Current;
        _input.Frame = scriptedOverride ?? NextInput();
        Session.Update(ScenarioDefinition.FixedStep);
        Tick++;
        Current = Session.CaptureSnapshot();
        if (Current.State == GameState.Replay) _replayEntered = true;
        bool terminal = Current.Outcome.HasValue && Current.State != GameState.FieldGoal;
        Complete = terminal && (!Definition.Replay || _replayEntered && Current.State != GameState.Replay);
        if (!float.IsFinite(Current.BallPosition.X) || !float.IsFinite(Current.BallPosition.Y)
            || Current.Actors.Any(a => !float.IsFinite(a.Position.X) || !float.IsFinite(a.Position.Y)
            || !float.IsFinite(a.Velocity.X) || !float.IsFinite(a.Velocity.Y)))
        {
            Failure = "Non-finite ball or actor state.";
            Complete = true;
        }
        if (!Complete && Tick >= ScenarioDefinition.MaxTicks)
        {
            Failure = "Scenario exceeded 90 simulated seconds without completing. No result was forced.";
            Complete = true;
        }
        if (Complete || Tick % 60 == 0 || _input.Frame != new InputFrame()
            || Current.State != previous.State || Current.BallState != previous.BallState
            || Current.KickPhase != previous.KickPhase || Current.Exchange != previous.Exchange)
            _trace.Add(new(Tick, _input.Frame, Current));
    }

    private InputFrame NextInput()
    {
        if (Definition.Replay && Current.Outcome.HasValue && !_replayEntered)
            return new(Replay: true);
        if (Current.State == GameState.PreSnap)
            return Definition.FieldGoal ? new(FieldGoal: true) : new(Space: true);
        if (Current.State == GameState.FieldGoal)
            return Current.KickPhase switch
            {
                KickPhase.Setup or KickPhase.Ready => new(Space: true),
                KickPhase.Power when Current.KickMarker >= .5f => new(Space: true),
                KickPhase.Accuracy when Current.KickMarker <= .5f => new(Space: true),
                KickPhase.Result => new(Enter: true),
                _ => new()
            };
        if (Current.State != GameState.PlayActive) return new();
        _activeTicks++;
        bool run = Definition.IsRun || Current.BallState == BallState.HeldByReceiver;
        return new(Movement: run ? Vector2.UnitY : Vector2.Zero,
            ThrowTarget: !run && _activeTicks == Definition.ThrowTick ? 0 : null);
    }

    public string WriteReport()
    {
        string path = Path.Combine(OutputDirectory, "report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, Definition.Name, Seed, Definition.PlayId, Definition.Start,
            Offense = Definition.Offense.Name, Defense = Definition.Defense.Name,
            Stage = Definition.Stage.ToString(), Clock = "No game clock in the baseline ruleset",
            StepSeconds = ScenarioDefinition.FixedStep, Tick, Complete, Failure, RecordPath,
            Final = Current, Trace = _trace
        }, JsonOptions));
        return path;
    }

    public void Dispose() => Session.Dispose();
}
