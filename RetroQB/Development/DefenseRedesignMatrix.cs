using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using RetroQB.AI;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;

namespace RetroQB.Development;

internal enum DefenseInputMode { AssignmentFollowing, Abandonment, Stationary, FullAI }

/// <summary>Frozen acceptance intent plus an executable legacy baseline. Unsupported transitions are never reported as passes.</summary>
internal sealed record DefenseFixture(string Id, string Play, string Defense, string Geometry,
    string Expected, string RequiredCapability, float Yard = 50, float Duration = 6)
{
    public string OffenseTeam { get; init; } = "scarlet-guard";
    public string DefenseTeam { get; init; } = "ballers";
    public int Down { get; init; } = 1;
    public float Distance { get; init; } = 10;
}

internal static class DefenseRedesignMatrix
{
    public const string Version = "defense-baseline-v1";
    public const float AlignmentTolerance = 5f;
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, IncludeFields = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() }
    };
    public static IReadOnlyList<DefenseFixture> Fixtures { get; } = Array.AsReadOnly(new DefenseFixture[]
    {
        new("DM-01", "pass.mesh", "def.cover2-man", "Four detached WRs and TE; nickel", "Five unique primaries, two reserved safeties, explicit LB mismatch, order invariant", "structural pairing"),
        new("DM-02", "pass.pistol-twins.scissors", "def.cover1", "Twins left, attached TE right; repeat flipped", "Closed TE recognized; owners align over final duties", "structural pairing"),
        new("DM-03", "pass.bunch-quick", "def.cover1", "Bunch; crossing release and recross", "One atomic switch or provisional locks; no second switch", "bunch switch runtime"),
        new("DM-04", "pass.four-verts", "def.cover3", "Four detached verticals plus TE vertical", "Outside protection, accountable seam carriers, no double safety promise", "coverage board"),
        new("DM-05", "pass.gun-doubles.sail", "def.cover2-zone", "Fade/corner/flat baseline; authored fade/seam/flat acceptance", "Declare short concession while protecting both deep threats", "Cover 2 conflict policy"),
        new("DM-06", "pass.pistol-twins.scissors", "def.cover3", "Post crosses seam", "Offer, accept/decline, bounded overlap, recovery; no simultaneous release", "transfer protocol"),
        new("DM-07", "pass.gun-trips.mesh-wheel", "def.quarters-match", "Wheel release against legacy quarters baseline", "Apex carries out; viable wheel acceptance precedes changed safety help", "Quarters Match"),
        new("DM-08", "pass.gun-trips.mills", "def.quarters-match", "Trips vertical baseline with isolated outside target", "Bind #1/#2/#3 and isolated fade separately; RB retains owner", "Quarters trips check"),
        new("DM-09", "pass.gun-trips.mesh-wheel", "def.cover3-match", "Trips shallow plus opposite RB release baseline", "Keep RB owner; shallow transfer only to free reachable actor", "match transfer"),
        new("DM-10", "pass.split-backs.mesh-wheel", "def.cover1", "Back blocks .65s then releases", "Observed block triggers green dog; release cancels or causes explicit recovery", "green dog"),
        new("DM-11", "pass.mesh", "def.edge", "Edge pressure; CPU pocket movement baseline", "Ordered entries, near contain, opposite reversal protection", "coordinated pressure"),
        new("DM-12", "pass.mesh", "def.edge", "Blocked stunt acceptance; straight pressure baseline", "Bounded loop wait then declared fallback; contain retained", "stunt"),
        new("DM-13", "pass.mesh", "def.cover3", "Zone/simulated pressure acceptance; four-rush zone baseline", "Five/four actual rushers, named dropper, protection reads active state", "zone and simulated pressure"),
        new("DM-14", "pass.gun-doubles.pa-cross", "def.cover3", "Fake exchange followed by pass", "Bounded box false step; deep pass leverage and observed throw recognition", "observation runtime"),
        new("DM-15", "run.stretch-right", "def.cover3", "Outside run; CPU carrier chooses cuts", "Separate force, cutback and deepest support; interception pursuit", "run fits"),
        new("DM-16", "pass.mesh", "def.cover3", "Human follows assignment for 1s then leaves", "Reachable acceptance then human abandonment, no automatic steering", "human transfer"),
        new("DM-17", "pass.mesh", "def.edge", "Call inputs 8/9 (keyboard 9/0), flip twice, snap", "Pressure paths restored, offense/deadline stable, stationary manual blitzer", "pressure flip input"),
        new("DM-18", "pass.mesh", "def.cover1", "Identical geometry with changed hidden route/block/eligibility", "Identical preview until visible movement differs", "structural hidden-data boundary"),
        new("DM-19", "pass.pistol-twins.scissors", "def.cover3", "Goal-line seam/corner", "Compressed vertical gate, legal cushions, identity under orientation", "goal-line runtime", Yard: 98) { Distance = 2 },
        new("DM-20", "pass.mesh", "def.cover3", "Repeated situation; seed batches and changed down/distance", "Legal varied CPU frequencies shift with situation; memory keys agree", "CPU strategy")
    });

    internal sealed record ActorTrace(string Id, Vector2 Position, Vector2 Velocity, Vector2? LegacyTarget = null,
        int? ManTargetId = null, string? Zone = null, bool Rushing = false, int BlockContacts = 0);
    internal sealed record Frame(int Tick, string BallState, Vector2 Ball, string? Holder,
        string? ControlledActor, Vector2 Input, IReadOnlyList<string> Events, IReadOnlyList<ActorTrace> Actors);
    internal sealed record Sample(string Matrix, DefenseFixture Fixture, int Seed, int Hz, bool Flipped,
        DefenseInputMode InputMode, IReadOnlyList<DefensiveAssignment> Preview, IReadOnlyList<Frame> Frames,
        string DesiredStatus, IReadOnlyList<string> Findings, string Outcome, string? Failure);
    internal sealed record Measurement(string Sample, int Frames, double MicrosecondsPerStep, double AllocatedBytesPerStep);

    public static int Run(ScenarioLaunchOptions options)
    {
        if (!options.Headless) throw new ArgumentException("defense-redesign-matrix requires --headless; it writes JSON traces.");
        string output = Path.GetFullPath(options.OutputDirectory ?? "artifacts/defense-redesign/p00");
        Directory.CreateDirectory(output);
        var manifest = new List<object>(); var measurements = new List<Measurement>(); int failures = 0;
        // Warm JIT/controller caches outside measured samples.
        Exercise(Fixtures[0], options.Seed, options.Hz, DefenseInputMode.FullAI, false, out _);
        foreach (var fixture in Fixtures)
        foreach (var mode in Enum.GetValues<DefenseInputMode>())
        foreach (bool flip in fixture.Id == "DM-02" ? new[] { false, true } : new[] { false })
        {
            string key = $"{fixture.Id}-{mode}-{(flip ? "flipped" : "normal")}";
            Sample sample; Measurement measurement;
            try { sample = Exercise(fixture, options.Seed, options.Hz, mode, flip, out measurement); }
            catch (Exception error)
            {
                sample = new(Version, fixture, options.Seed, options.Hz, flip, mode, [], [], "harness-failure", [], "setup-or-trace-failed", error.ToString());
                measurement = new(key, 0, 0, 0);
            }
            if (sample.Failure != null) failures++;
            File.WriteAllText(Path.Combine(output, key + ".json"), JsonSerializer.Serialize(sample, Json));
            manifest.Add(new { key, fixture.Id, sample.DesiredStatus, sample.Findings, sample.Outcome, sample.Failure });
            measurements.Add(measurement with { Sample = key });
        }
        var pressure = ProbePressureInput(options.Seed);
        File.WriteAllText(Path.Combine(output, "pressure-input.json"), JsonSerializer.Serialize(pressure, Json));
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
        {
            Version, Engine = "legacy", PlanVersion = "legacy-unversioned", options.Seed,
            options.Hz, AlignmentTolerance, Samples = manifest, PressureInput = pressure,
            Note = "Future runtime transitions remain unsupported. Proximity/target telemetry is not a decision-rule event."
        }, Json));
        File.WriteAllText(Path.Combine(output, "performance.json"), JsonSerializer.Serialize(new
        {
            Version, Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Note = "Warmed production Drive.Update only; trace serialization excluded. Replay allocations included. Timing is nondeterministic and separate from replay traces.",
            Measurements = measurements
        }, Json));
        Console.WriteLine($"Defense baseline: {manifest.Count} traces written to {output}");
        return failures == 0 && measurements.All(m => m.Frames > 0) ? 0 : 1;
    }

    internal static Sample Exercise(DefenseFixture fixture, int seed, int hz, DefenseInputMode mode, bool flipped,
        out Measurement measurement)
    {
        if (hz is not (30 or 60 or 120)) throw new ArgumentOutOfRangeException(nameof(hz));
        var input = new ScriptedInput();
        var user = TeamCatalog.Get(fixture.DefenseTeam); var cpu = TeamCatalog.Get(fixture.OffenseTeam);
        var drive = new DefensiveDrive(input, seed, fixedCall: fixture.Play,
            match: new(user, cpu, cpu.Id, SeasonStage.RegularSeason, new(fixture.Yard, fixture.Down, fixture.Distance)));
        if (flipped) drive.FlipOffensiveFormationForDevelopment();
        drive.SelectDefense(fixture.Defense);
        if (mode == DefenseInputMode.FullAI) drive.Execution.Control = new(true) { AutomateDefense = true };
        var preview = drive.SelectedDefense.Assignments;
        var frames = new List<Frame>(); var findings = new List<string>();
        foreach (var a in preview.Where(a => a.ManTarget >= 0))
        {
            var receiver = drive.Actors.Receivers.Single(r => r.Index == a.ManTarget);
            if (MathF.Abs(a.Position.X - receiver.Position.X) > AlignmentTolerance)
                findings.Add($"Legacy lateral alignment exceeds {AlignmentTolerance}yd: {a.Slot} -> receiver:{receiver.Index} ({MathF.Abs(a.Position.X - receiver.Position.X):F3}yd)");
        }
        if (fixture.Id is "DM-04" or "DM-06") findings.Add("Legacy has no team ownership/transfer events; deep failure remains unresolved by this baseline, inspect trajectories.");
        if (!drive.Snap()) throw new InvalidOperationException("Baseline snap rejected.");
        long elapsed = 0, allocated = 0; int tick = 0; string? failure = null;
        string? previousBall = null; var previousTargets = new Dictionary<DefenderSlot, Vector2>();
        Capture();
        while (drive.Live && tick < fixture.Duration * hz)
        {
            var manual = drive.Execution.Control.ControlledDefender(drive.Actors.Defenders);
            var direction = Vector2.Zero;
            if (manual != null && mode != DefenseInputMode.Stationary)
            {
                var delta = Target(manual) - manual.Position;
                direction = delta.LengthSquared() > .01f ? Vector2.Normalize(delta) : Vector2.Zero;
                if (mode == DefenseInputMode.Abandonment && tick >= hz) direction = -Vector2.UnitY;
            }
            input.Frame = new(Movement: direction);
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            try { drive.Update(1f / hz); }
            catch (Exception error) { failure = error.ToString(); break; }
            elapsed += Stopwatch.GetTimestamp() - start; allocated += GC.GetAllocatedBytesForCurrentThread() - bytes;
            tick++; Capture();
            if (drive.Players.Any(p => !float.IsFinite(p.Position.X) || !float.IsFinite(p.Position.Y)))
            { failure = "Non-finite actor position"; break; }
        }
        measurement = new("", tick, tick == 0 ? 0 : elapsed * 1_000_000d / Stopwatch.Frequency / tick,
            tick == 0 ? 0 : allocated / (double)tick);
        string status = findings.Any(f => f.StartsWith("Legacy lateral")) ? "reproduced-alignment-defect" : "unresolved";
        return new(Version, fixture, seed, hz, flipped, mode, preview, frames, status, findings,
            drive.LastResult?.Event.Reason.ToString() ?? "observation-window-ended-without-forced-whistle", failure);

        Vector2 Target(Defender d) => DefenderTargeting.GetTarget(d, drive.Actors.Qb, drive.Actors.Receivers,
            drive.Actors.Ball, drive.Actors.Qb.Position.Y > drive.Plays.LineOfScrimmage + .1f,
            BlockingUtils.IsRunPlayActiveWithRunningBack(drive.Plays.SelectedPlayType, drive.Actors.Ball),
            drive.Actors.UsesZoneResponsibilities, drive.Plays.LineOfScrimmage);
        void Capture()
        {
            var events = new List<string>(); var actors = new List<ActorTrace>();
            string state = drive.Actors.Ball.State.ToString();
            if (previousBall != state) events.Add($"legacy-ball-state:{previousBall ?? "start"}->{state}");
            previousBall = state;
            actors.Add(new("qb", drive.Actors.Qb.Position, drive.Actors.Qb.Velocity));
            actors.AddRange(drive.Actors.Receivers.OrderBy(r => r.Index).Select(r => new ActorTrace($"receiver:{r.Index}", r.Position, r.Velocity)));
            actors.AddRange(drive.Actors.Blockers.Select((b, i) => new ActorTrace($"lineman:{i}", b.Position, b.Velocity)));
            foreach (var d in drive.Actors.Defenders.OrderBy(d => d.Slot))
            {
                Vector2 target = Target(d);
                if (previousTargets.TryGetValue(d.Slot, out var last) && Vector2.DistanceSquared(last, target) > .25f)
                    events.Add($"legacy-target-moved:{d.Slot}");
                previousTargets[d.Slot] = target;
                if (d.ActiveBlockersCount > 0) events.Add($"legacy-block-contact:{d.Slot}:{d.ActiveBlockersCount}");
                actors.Add(new($"defender:{d.Slot}", d.Position, d.Velocity, target,
                    d.CoverageReceiverIndex >= 0 ? d.CoverageReceiverIndex : null, d.ZoneRole.ToString(), d.IsRusher, d.ActiveBlockersCount));
            }
            if (drive.LastResult != null) events.Add($"legacy-terminal:{drive.LastResult.Event.Reason}");
            var holder = drive.Actors.Ball.Holder;
            frames.Add(new(tick, state, drive.Actors.Ball.Position,
                holder is Receiver r ? $"receiver:{r.Index}" : holder == drive.Actors.Qb ? "qb" : null,
                mode == DefenseInputMode.FullAI ? null : $"defender:{drive.Linebacker.Slot}", input.Frame.Movement, events, actors));
        }
    }

    internal static object ProbePressureInput(int seed)
    {
        var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.Get("scarlet-guard");
        var session = new FullMatchSession(input, seed, new(user, cpu, cpu.Id));
        var rows = new List<object>();
        foreach (int call in new[] { 8, 9 })
        {
            session.Update(0, new(Call: call));
            var original = session.Drive.SelectedDefense;
            string offense = session.Drive.Plays.SelectedPlay.Id; double deadline = session.SnapRemaining;
            session.Update(0, new(Flip: true)); var once = session.Drive.SelectedDefense;
            session.Update(0, new(Flip: true)); var twice = session.Drive.SelectedDefense;
            rows.Add(new { Keyboard = call == 8 ? "9" : "0", InputIndex = call, Original = original.Definition.Id,
                FirstFlipChangesPlan = !original.Assignments.SequenceEqual(once.Assignments),
                SecondFlipRestoresPlan = original.Assignments.SequenceEqual(twice.Assignments),
                OffenseStable = offense == session.Drive.Plays.SelectedPlay.Id, DeadlineStable = deadline == session.SnapRemaining });
        }
        return new { Boundary = "Production MatchInput (keyboard mapping read separately; no native key injection)", Rows = rows,
            Status = "Pressure flip behavior is unsupported until P6; two ignored flips are not acceptance." };
    }
}
