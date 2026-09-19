using System.Globalization;

namespace RetroQB.Development;

internal sealed record ScenarioLaunchOptions(ScenarioDefinition Definition, int Seed,
    bool Headless, string? OutputDirectory, bool Capture = false)
{
    public static ScenarioLaunchOptions? Parse(string[] args, bool developmentBuild)
    {
        if (args.Length == 0) return null;
        if (!developmentBuild) throw new ArgumentException("Development scenarios are available only in Debug builds.");
        string? name = null, output = null;
        int seed = 101;
        bool headless = false, capture = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (!seen.Add(option)) throw new ArgumentException($"Repeated argument: {option}");
            if (option == "--headless") { headless = true; continue; }
            if (option == "--capture") { capture = true; continue; }
            if (option is not ("--scenario" or "--seed" or "--output"))
                throw new ArgumentException($"Unknown argument: {option}");
            if (++i == args.Length) throw new ArgumentException($"Missing value for {option}");
            switch (option)
            {
                case "--scenario": name = args[i]; break;
                case "--output": output = args[i]; break;
                case "--seed":
                    if (!int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                        throw new ArgumentException("Seed must be a 32-bit integer.");
                    break;
            }
        }
        if (name == null) throw new ArgumentException("--scenario is required for development arguments.");
        if (headless && capture) throw new ArgumentException("--capture renders a hidden window; it cannot be combined with --headless.");
        return new(ScenarioDefinition.Get(name), seed, headless, output, capture);
    }
}
