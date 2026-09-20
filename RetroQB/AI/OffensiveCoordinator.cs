using RetroQB.Gameplay;

namespace RetroQB.AI;

/// <summary>Seeded run/pass selection over the catalog verified by the release sweep.</summary>
public sealed class OffensiveCoordinator(Random random)
{
    private static readonly PlayCatalog Catalog = PlaybookBuilder.BuildCatalog();
    public static IReadOnlyList<string> Passes { get; } = Array.AsReadOnly(Catalog.Plays.Where(p => p.Family == PlayType.Pass).Select(p => p.Id).ToArray());
    public static IReadOnlyList<string> Runs { get; } = Array.AsReadOnly(Catalog.Plays.Where(p => p.Family == PlayType.Run).Select(p => p.Id).ToArray());
    public string Select(DriveStart series, CpuTendencies tendencies)
    {
        float chance = Math.Clamp(tendencies.PassPreference + (series.Distance > 7 && series.Down >= 3 ? .25f : 0), .15f, .85f);
        var calls = random.NextDouble() < chance ? Passes : Runs;
        return calls[random.Next(calls.Count)];
    }
}
