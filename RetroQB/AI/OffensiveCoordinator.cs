using RetroQB.Gameplay;

namespace RetroQB.AI;

/// <summary>Seeded run/pass selection over the catalog verified by the release sweep.</summary>
public sealed class OffensiveCoordinator(Random random)
{
    private static readonly PlayCatalog Catalog = PlaybookBuilder.BuildCatalog();
    private static readonly IReadOnlyDictionary<string, ResolvedPlay> Resolved = Catalog.Plays.ToDictionary(p => p.Id, p => PlayResolver.Resolve(p));
    public static IReadOnlyList<string> Passes { get; } = Array.AsReadOnly(Catalog.Plays.Where(p => p.Family == PlayType.Pass).Select(p => p.Id).ToArray());
    public static IReadOnlyList<string> Runs { get; } = Array.AsReadOnly(Catalog.Plays.Where(p => p.Family == PlayType.Run).Select(p => p.Id).ToArray());
    public string Select(DriveStart series, CpuTendencies tendencies)
        => Select(new(new(series.Down, series.Distance, FieldGeometry.EndZoneDepth + series.OwnYardLine,
                FieldGeometry.EndZoneDepth + series.OwnYardLine + series.Distance)),
            tendencies, OffensiveTeamAttributes.Default);

    public string Select(OffensiveCallContext context, CpuTendencies tendencies, OffensiveTeamAttributes roster,
        OffensiveStrategyMemory? memory = null)
    {
        var series = context.Situation;
        float chance = Math.Clamp(tendencies.PassPreference
            + (series.Distance > 7 && series.Down >= 3 ? .2f : 0)
            - (series.Distance <= 2 ? .2f : 0), .25f, .9f);
        if (context.LateGame && !context.Overtime && context.SecondsRemaining < 90)
            chance = Math.Clamp(chance + (context.ScoreMargin > 0 ? -.25f : .2f), .15f, .95f);
        var calls = random.NextDouble() < chance ? Passes : Runs;
        var weights = calls.Select(id => OffensiveCallScoring.Weight(Resolved[id], context, roster, memory)).ToArray();
        double pick = random.NextDouble() * weights.Sum();
        for (int i = 0; i < calls.Count; i++) { pick -= weights[i]; if (pick <= 0) return calls[i]; }
        return calls[^1];
    }
}
