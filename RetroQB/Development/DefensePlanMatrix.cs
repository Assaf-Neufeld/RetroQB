using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RetroQB.AI;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Development;

/// <summary>Passive P1 compiler/preview evidence. This runner never starts the new engine.</summary>
internal static class DefensePlanMatrix
{
    public static int Run(ScenarioLaunchOptions options)
    {
        if (!options.Headless) throw new ArgumentException("defense-plan-matrix requires --headless; it writes passive preview evidence.");
        string output = Path.GetFullPath(options.OutputDirectory ?? "artifacts/defense-redesign/p01");
        Directory.CreateDirectory(output);
        var rows = new List<object>(); int failures = 0, checkedPlans = 0;
        var factory = new FormationFactory();
        var setup = new PlaySetupController(factory, new DefenseFactory(), new DefensiveCoordinator(new()), new Random(options.Seed));
        var catalog = new PlayManager().Catalog;
        foreach (var definition in catalog.Plays)
        foreach (bool flipped in new[] { false, true })
        foreach (float los in new[] { 11f, 60f, 109f })
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var call in DefensiveCallDefinition.Supported)
        {
            string? failure = null; string? digest = null;
            try
            {
                var play = PlayResolver.Resolve(definition, flipped);
                var snapshot = OffensiveAlignmentSnapshot.From(factory.CreateFormation(play, los), los);
                var plan = DefensivePlanCompiler.Compile(snapshot, call, package, options.Seed);
                var preview = setup.SetupPreview(play, plan, OffensiveTeamAttributes.Default, DefensiveTeamAttributes.Default);
                var repeated = setup.SetupPreview(play, plan, OffensiveTeamAttributes.Default, DefensiveTeamPresets.ScarletGuard);
                if (!plan.Assignments.Select(a => a.Alignment).SequenceEqual(preview.Defenders.Select(d => d.Position))
                    || !plan.Assignments.Select(a => a.ActorId).SequenceEqual(repeated.Defenders.Select(d => d.Slot)))
                    throw new InvalidOperationException("Preview/spawn parity failed.");
                digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(plan.Assignments, DefenseRedesignMatrix.Json))));
                if (los == 60 && !flipped && package == DefensePackage.Nickel && call.Id == "def.cover2-man"
                    && definition.Id is "pass.mesh" or "pass.pistol-twins.scissors" or "run.hb-dive")
                    File.WriteAllText(Path.Combine(output, definition.Id + ".json"), JsonSerializer.Serialize(plan, DefenseRedesignMatrix.Json));
                checkedPlans++;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException)
            { failure = error.Message; failures++; }
            rows.Add(new { Offense = definition.Id, flipped, Los = los, Package = package, Call = call.Id, call.Version, Digest = digest, Failure = failure });
        }
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
        {
            Version = ResolvedDefensivePlan.RulesVersion, Engine = "passive-preview-only", options.Seed,
            CatalogCount = catalog.Plays.Count, CheckedPlans = checkedPlans, Failures = failures,
            Teams = new[] { "Default", "ScarletGuard" }, Rules = new[] { "ALIGN-01", "ALIGN-02", "ALIGN-03", "ALIGN-05", "ALIGN-06", "ALIGN-07" },
            Rows = rows
        }, DefenseRedesignMatrix.Json));
        Console.WriteLine($"Defensive plans: {checkedPlans} valid previews; {failures} failures; {output}");
        return failures == 0 ? 0 : 1;
    }
}
