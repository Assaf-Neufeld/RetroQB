using System.Numerics;
using System.Text.Json;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Development;

internal static class DefenseRuntimeMatrix
{
    internal sealed record RuntimeSample(string Play, string Call, int Seed, int Hz, bool HumanDefense,
        DefenseInputMode InputMode, float Seconds, string? Outcome, string? Failure, int Switches,
        IReadOnlyList<DefensiveDecisionFrame> Decisions)
    {
        public string? ResolvedCall { get; init; }
        public string? Check { get; init; }
    }

    public static int Run(ScenarioLaunchOptions options)
    {
        if (!options.Headless || options.Engine == DefensiveEngineMode.Legacy)
            throw new ArgumentException("defense-runtime-matrix requires --headless --engine cover1|man|zone.");
        bool fits = options.Definition.Name == "defense-fit-matrix";
        string phase = fits ? "p05" : options.Engine == DefensiveEngineMode.Cover1Development ? "p02" : options.Engine == DefensiveEngineMode.ZoneDevelopment ? "p04" : "p03";
        string output = Path.GetFullPath(options.OutputDirectory ?? $"artifacts/defense-redesign/{phase}/runtime-{options.Hz}");
        Directory.CreateDirectory(output); int failures = 0; var rows = new List<object>();
        string[] plays = ["pass.mesh", "pass.four-verts", "pass.bunch-quick", "pass.pistol-twins.scissors",
            "pass.split-backs.mesh-wheel", "pass.gun-doubles.pa-cross", "run.hb-dive", "run.stretch-right"];
        if (fits) plays = ["run.hb-dive", "run.counter-left", "run.counter-right", "run.stretch-right", "pass.pa-deep", "pass.gun-doubles.pa-cross", "pass.mesh", "pass.four-verts"];
        foreach (var call in fits ? DefensiveEngineCalls.For(options.Engine) : options.Engine == DefensiveEngineMode.ZoneDevelopment ? DefensiveCallDefinition.SpotZones : DefensiveEngineCalls.For(options.Engine))
        foreach (string play in plays)
        foreach (bool human in new[] { false, true })
        foreach (var mode in Enum.GetValues<DefenseInputMode>())
        {
            var sample = Exercise(play, call.Id, options.Seed, options.Hz, human, mode, options.Engine);
            if (sample.Failure != null) failures++;
            string key = $"{play}-{call.Id}-{human}-{mode}";
            if (sample.Failure != null || play == "pass.mesh" && mode == DefenseInputMode.Stationary)
                File.WriteAllText(Path.Combine(output, key + ".json"), JsonSerializer.Serialize(sample, DefenseRedesignMatrix.Json));
            rows.Add(new { sample.Play, sample.Call, sample.Seed, sample.Hz, sample.HumanDefense, sample.InputMode,
                sample.Seconds, sample.Outcome, sample.Failure, sample.Switches, Frames = sample.Decisions.Count,
                Reads = sample.Decisions.Where(d => d.Run != null).GroupBy(d => d.Run!.Read).ToDictionary(g => g.Key.ToString(), g => g.Count()) });
        }
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
        {
            DefensiveRuntime.Version, options.Engine, options.Seed, options.Hz, Cases = rows.Count, Failures = failures,
            Teams = new[] { "ballers", "scarlet-guard" }, Notes = "Production controllers; 30-second terminal deadline. AI offense when defending; scripted first-target throw when attacking. P5 coordinated fits/pursuit; added rushers use fixed entries until P6.", Rows = rows
        }, DefenseRedesignMatrix.Json));
        Console.WriteLine($"Live defense: {rows.Count} cases; {failures} failures; {output}");
        return failures == 0 ? 0 : 1;
    }

    internal static RuntimeSample Exercise(string play, string call, int seed, int hz, bool humanDefense,
        DefenseInputMode mode, DefensiveEngineMode engine, float yard = 50, bool flipped = false, PressureSelection? pressure = null, DefensePackage? package = null, DefensiveFront front = DefensiveFront.Even, bool advanceHumanRunner = false)
    {
        var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.Get("scarlet-guard");
        var frames = new List<DefensiveDecisionFrame>(); DefensiveDrive? drive = null; string? failure = null;
        try
        {
            drive = new(input, seed, fixedCall: play, match: new(user, cpu, humanDefense ? cpu.Id : user.Id,
                start: new(yard, 1, MathF.Min(10, 100 - yard))), engine: engine);
            if (flipped) drive.FlipOffensiveFormationForDevelopment();
            if (!drive.SelectCompiledDefenseForDevelopment(call)) throw new InvalidOperationException("Unsupported development call.");
            if ((package != null || front != DefensiveFront.Even) && !drive.SelectFormationForDevelopment(package, front)) throw new InvalidOperationException(drive.SelectionRejection ?? "Unsupported formation.");
            if (pressure != null && !drive.SelectCompiledPressureForDevelopment(pressure)) throw new InvalidOperationException(drive.SelectionRejection ?? "Unsupported pressure.");
            if (mode == DefenseInputMode.FullAI && humanDefense) drive.Execution.Control = new(true) { AutomateDefense = true };
            if (!drive.Snap()) throw new InvalidOperationException("Snap rejected.");
            for (int tick = 0; drive.Live && tick < hz * 30; tick++)
            {
                Vector2 movement = Vector2.Zero;
                if (advanceHumanRunner && !humanDefense && drive.Actors.Ball.State == BallState.HeldByReceiver) movement = Vector2.UnitY;
                var manual = drive.Execution.Control.ControlledDefender(drive.Actors.Defenders);
                if (manual != null && mode is DefenseInputMode.AssignmentFollowing or DefenseInputMode.Abandonment)
                {
                    var target = drive.Execution.DefenseRuntime!.Current?.Decisions.Single(i => i.Actor == manual.Slot).Target
                        ?? drive.CompiledPlan!.Assignments.Single(a => a.ActorId == manual.Slot).Alignment;
                    var delta = target - manual.Position;
                    movement = delta.LengthSquared() > .1f ? Vector2.Normalize(delta) : Vector2.Zero;
                    if (mode == DefenseInputMode.Abandonment && tick >= hz) movement = -Vector2.UnitY;
                }
                input.Frame = new(Movement: movement, ThrowTarget: !humanDefense && tick >= hz ? 0 : null);
                drive.Update(1f / hz);
                if (drive.Execution.DefenseRuntime!.Current is { } current)
                {
                    frames.Add(current);
                    if (current.Decisions.Count != 11 || current.Decisions.Any(i => !float.IsFinite(i.Target.X) || !float.IsFinite(i.Target.Y)))
                        throw new InvalidOperationException("Invalid decision frame.");
                    var primaries = current.Decisions.Where(i => i.Primary != null).Select(i => i.Primary!.Value.Value).ToArray();
                    if (primaries.Distinct().Count() != primaries.Length) throw new InvalidOperationException("Duplicate primary ownership.");
                    var conditional = drive.CompiledPlan!.Pressure.Conditional;
                    // Only the declared green-dog release key can lose live man ownership during its rush cue.
                    var conceded = conditional?.ReleaseKey != null && current.Decisions.Any(d => d.Actor == conditional.Actor && d.Kind == DefensiveIntentKind.Rush)
                        ? conditional.ReleaseKey : null;
                    if (drive.CompiledPlan.Call.Zone == SpotZoneFamily.None && !current.Observation.ConfirmedCarrier && !primaries.Order()
                        .SequenceEqual(drive.CompiledPlan.Offense.Skills.Where(s => s.Id != conceded).Select(s => s.Id.Value).Order()))
                        throw new InvalidOperationException("Primary ownership lost or duplicated.");
                    if (current.Pressure?.ActualCount != current.Decisions.Count(d => d.ActiveRusher))
                        throw new InvalidOperationException("Pressure participation disagrees with executed intents.");
                    if (current.Observation.ConfirmedCarrier && (primaries.Length != 0 || current.Run?.Jobs.Count != 11
                        || current.Run.Jobs.Select(j => j.Actor).Distinct().Count() != 11
                        || current.Decisions.Any(i => i.ActiveRusher))) throw new InvalidOperationException("Contradictory carrier/coverage jobs.");
                    foreach (var actor in drive.Actors.Defenders)
                        if (!float.IsFinite(actor.Position.X) || !float.IsFinite(actor.Position.Y) || actor.Velocity.Length() > actor.Speed * drive.Plays.DefenderSpeedMultiplier + .001f)
                            throw new InvalidOperationException($"Invalid physical state: {actor.Slot}");
                }
            }
            if (drive.Live) failure = "No production whistle within 30 seconds.";
            else if (drive.Match.History.Count != 1) failure = "Missing/duplicate resolution.";
        }
        catch (Exception error) { failure = error.ToString(); }
        return new(play, call, seed, hz, humanDefense, mode, drive?.LiveSeconds ?? 0,
            drive?.LastResult?.Event.Reason.ToString(), failure, drive?.Execution.DefenseRuntime?.SwitchCommitCount ?? 0, frames.AsReadOnly())
            { ResolvedCall = drive?.CompiledPlan?.Call.Id, Check = drive?.CompiledPlan?.Match?.Check ?? drive?.CompiledPlan?.FallbackReason };
    }
}
