using System.Text.Json;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Development;

internal static class DefenseMatchMatrix
{
    public static int Run(ScenarioLaunchOptions options)
    {
        if (!options.Headless || options.Engine != DefensiveEngineMode.MatchDevelopment)
            throw new ArgumentException("defense-match-matrix requires --headless --engine match.");
        string output = Path.GetFullPath(options.OutputDirectory ?? $"artifacts/defense-redesign/p07/runtime-{options.Hz}");
        Directory.CreateDirectory(output); var rows = new List<object>(); int failures = 0, matched = 0, fallback = 0;
        foreach (var call in DefensiveCallDefinition.Matches)
        foreach (string play in new[] { "pass.gun-trips.mills", "pass.gun-trips.mesh-wheel", "pass.bunch-quick", "pass.mesh",
            "pass.pistol-twins.scissors", "pass.gun-doubles.pa-cross", "run.hb-dive", "run.stretch-right" })
        foreach (bool human in new[] { false, true })
        foreach (var mode in Enum.GetValues<DefenseInputMode>())
        foreach (bool flipped in new[] { false, true })
        foreach (float yard in new[] { 50f, 98f })
        {
            var sample = DefenseRuntimeMatrix.Exercise(play, call.Id, options.Seed, options.Hz, human, mode, options.Engine, yard, flipped);
            if (sample.Failure != null) failures++;
            if (sample.ResolvedCall == call.Id) matched++; else fallback++;
            if (sample.Failure != null || human && mode == DefenseInputMode.FullAI && !flipped && yard == 50)
                File.WriteAllText(Path.Combine(output, $"{play}-{call.Id}-{human}-{mode}-{flipped}-{yard}.json"), JsonSerializer.Serialize(sample, DefenseRedesignMatrix.Json));
            rows.Add(new { sample.Play, sample.Call, sample.ResolvedCall, sample.Check, sample.Seed, sample.Hz,
                sample.HumanDefense, sample.InputMode, Flipped = flipped, Yard = yard, sample.Seconds, sample.Outcome, sample.Failure });
        }
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
        { DefensiveRuntime.Version, options.Engine, options.Seed, options.Hz, Cases = rows.Count, Failures = failures,
            Matched = matched, Fallback = fallback, Teams = new[] { "ballers", "scarlet-guard" }, Rows = rows }, DefenseRedesignMatrix.Json));
        Console.WriteLine($"Match: {rows.Count} cases; {failures} failures; {matched} match / {fallback} declared fallback; {output}");
        return failures == 0 ? 0 : 1;
    }
}
