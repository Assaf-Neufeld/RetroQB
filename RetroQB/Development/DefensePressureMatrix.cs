using System.Text.Json;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Development;

internal static class DefensePressureMatrix
{
    public static int Run(ScenarioLaunchOptions options)
    {
        if (!options.Headless || options.Engine != DefensiveEngineMode.ZoneDevelopment)
            throw new ArgumentException("defense-pressure-matrix requires --headless --engine zone.");
        string output = Path.GetFullPath(options.OutputDirectory ?? $"artifacts/defense-redesign/p06/runtime-{options.Hz}");
        Directory.CreateDirectory(output);
        var rows = new List<object>(); int failures = 0;
        foreach (var kind in Enum.GetValues<PressureKind>())
        foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
        foreach (string play in new[] { "pass.mesh", "pass.four-verts", "pass.gun-doubles.pa-cross", "pass.split-backs.mesh-wheel", "run.hb-dive", "run.stretch-right" })
        foreach (bool human in new[] { false, true })
        foreach (var mode in Enum.GetValues<DefenseInputMode>())
        {
            // Empty formations cannot author a back-key green dog; the split-back base roster has no nickel.
            if (kind == PressureKind.GreenDog && play is "pass.mesh" or "pass.four-verts"
                || kind == PressureKind.Nickel && play == "pass.split-backs.mesh-wheel") continue;
            string call = kind is PressureKind.ZoneFive or PressureKind.Simulated ? "def.cover3"
                : kind == PressureKind.Zero ? "def.zero" : kind is PressureKind.Mike or PressureKind.Edge or PressureKind.Nickel ? "def.edge" : "def.cover1";
            var pressure = new PressureSelection(kind, side);
            var sample = DefenseRuntimeMatrix.Exercise(play, call, options.Seed, options.Hz, human, mode, options.Engine, pressure: pressure);
            if (sample.Failure != null) failures++;
            if (sample.Failure != null || play == "pass.gun-doubles.pa-cross" && human && mode == DefenseInputMode.FullAI)
                File.WriteAllText(Path.Combine(output, $"{play}-{kind}-{side}-{human}-{mode}.json"),
                    JsonSerializer.Serialize(new { Pressure = pressure, Sample = sample }, DefenseRedesignMatrix.Json));
            rows.Add(new { Pressure = pressure, sample.Play, sample.Call, sample.Seed, sample.Hz, sample.HumanDefense,
                sample.InputMode, sample.Seconds, sample.Outcome, sample.Failure, Frames = sample.Decisions.Count });
        }
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
        {
            DefensiveRuntime.Version, options.Engine, options.Seed, options.Hz, Cases = rows.Count, Failures = failures,
            Teams = new[] { "ballers", "scarlet-guard" },
            Notes = "Production controllers, both possession orientations, all four input modes; 30-second terminal deadline. Unsupported green-dog empty and base-roster nickel fixtures excluded explicitly.", Rows = rows
        }, DefenseRedesignMatrix.Json));
        Console.WriteLine($"Pressure: {rows.Count} cases; {failures} failures; {output}");
        return failures == 0 ? 0 : 1;
    }
}
