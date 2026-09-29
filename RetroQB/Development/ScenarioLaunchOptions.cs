using System.Globalization;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Development;

internal sealed record ScenarioLaunchOptions(ScenarioDefinition Definition, int Seed,
    bool Headless, string? OutputDirectory, bool Capture = false, int Hz = 60, DefensiveEngineMode Engine = DefensiveEngineMode.Legacy)
{
    public static ScenarioLaunchOptions? Parse(string[] args, bool developmentBuild)
    {
        if (args.Length == 0) return null;
        if (!developmentBuild) throw new ArgumentException("Development scenarios are available only in Debug builds.");
        string? name = null, output = null;
        int seed = 101, hz = 60;
        bool headless = false, capture = false;
        var engine = DefensiveEngineMode.Legacy;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (!seen.Add(option)) throw new ArgumentException($"Repeated argument: {option}");
            if (option == "--headless") { headless = true; continue; }
            if (option == "--capture") { capture = true; continue; }
            if (option is not ("--scenario" or "--seed" or "--output" or "--hz" or "--engine"))
                throw new ArgumentException($"Unknown argument: {option}");
            if (++i == args.Length) throw new ArgumentException($"Missing value for {option}");
            switch (option)
            {
                case "--scenario": name = args[i]; break;
                case "--output": output = args[i]; break;
                case "--engine": engine = args[i] switch
                {
                    "legacy" => DefensiveEngineMode.Legacy, "cover1" => DefensiveEngineMode.Cover1Development,
                    "man" => DefensiveEngineMode.ManDevelopment, "zone" => DefensiveEngineMode.ZoneDevelopment, "match" => DefensiveEngineMode.MatchDevelopment, "strategy" => DefensiveEngineMode.StrategyDevelopment,
                    _ => throw new ArgumentException("--engine must be legacy, cover1, man, zone, match or strategy.")
                }; break;
                case "--hz":
                    if (!int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out hz) || hz is not (30 or 60 or 120))
                        throw new ArgumentException("--hz must be 30, 60 or 120.");
                    break;
                case "--seed":
                    if (!int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                        throw new ArgumentException("Seed must be a 32-bit integer.");
                    break;
            }
        }
        if (name == null) throw new ArgumentException("--scenario is required for development arguments.");
        if (headless && capture) throw new ArgumentException("--capture renders a hidden window; it cannot be combined with --headless.");
        if (seen.Contains("--hz") && name is not ("defense-strategy-matrix" or "defense-formation-matrix" or "defense-match-matrix" or "defense-pressure-matrix" or "defense-redesign-matrix" or "defense-runtime-matrix" or "defense-fit-matrix")) throw new ArgumentException("--hz is supported only by defense matrix scenarios.");
        var definition = ScenarioDefinition.Get(name);
        if (seen.Contains("--engine") && !definition.Defending && !definition.FullMatch && name is not ("defense-strategy-matrix" or "defense-formation-matrix" or "defense-match-matrix" or "defense-pressure-matrix" or "defense-runtime-matrix" or "defense-fit-matrix"))
            throw new ArgumentException("--engine requires a defensive live scenario or defense-runtime-matrix.");
        return new(definition, seed, headless, output, capture, hz, engine);
    }
}
