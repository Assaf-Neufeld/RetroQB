using RetroQB.AI;

namespace RetroQB.Gameplay;

/// <summary>Builds a reproducible, diverse call sheet once per situation, never per frame.</summary>
public static class SituationalCallSheet
{
    public static string Label(PlaySituation s) => s.IsTightRedZone ? "Goal-line / tight red zone" :
        s.IsRedZone ? "Red zone" : s.IsShortYardage ? "Short yardage" :
        s.IsMustConvert && s.IsLongYardage ? "Long conversion" : s.IsMustConvert ? "Conversion down" :
        s.IsLongYardage ? "Open field / long yardage" : "Balanced field";

    public static PlayCallSheet Build(PlayCatalog catalog, PlaySituation situation, int seed)
        => Build(catalog, new OffensiveCallContext(situation), OffensiveTeamAttributes.Default, seed);

    public static PlayCallSheet Build(PlayCatalog catalog, OffensiveCallContext context, OffensiveTeamAttributes roster,
        int seed, OffensiveStrategyMemory? memory = null)
    {
        var situation = context.Situation;
        var rng = new Random(seed);
        return new(catalog, Pick(PlayType.Pass), Pick(PlayType.Run));

        IEnumerable<string> Pick(PlayType family)
        {
            var pool = catalog.Plays.Where(p => p.Family == family).ToList();
            var selected = new List<PlayDefinition>();
            var fits = pool.ToDictionary(p => p.Id, p => OffensiveCallScoring.Weight(PlayResolver.Resolve(p), context, roster, memory));
            while (selected.Count < PlayCallSheet.PlaysPerFamily)
            {
                float Weight(PlayDefinition p)
                {
                    float fit = fits[p.Id];
                    float categoryFit = p.Info.Category switch
                    {
                        PlayCategory.Quick => situation.IsShortYardage || situation.IsTightRedZone ? 2f : 1f,
                        PlayCategory.Vertical => situation.IsRedZone ? .12f : situation.IsLongYardage ? 1.6f : 1f,
                        PlayCategory.PlayAction => situation.IsMustConvert ? .7f : 1.3f,
                        _ => 1f
                    };
                    float diversity = 1f / (1 + selected.Count(x => x.Info.Formation == p.Info.Formation));
                    diversity /= 1 + selected.Count(x => x.Info.Concept == p.Info.Concept) * 2;
                    return MathF.Max(.001f, fit * categoryFit * diversity);
                }
                // Six strongest fits, then four weighted alternatives. All catalog calls remain reachable.
                PlayDefinition choice;
                if (selected.Count < 6)
                    choice = pool.OrderByDescending(Weight).ThenBy(p => p.Id, StringComparer.Ordinal).First();
                else
                {
                    float pick = (float)rng.NextDouble() * pool.Sum(Weight);
                    choice = pool[^1];
                    foreach (var p in pool) { pick -= Weight(p); if (pick <= 0) { choice = p; break; } }
                }
                selected.Add(choice);
                pool.Remove(choice);
            }
            return selected.OrderBy(p => p.Info.Category).ThenBy(p => p.Info.Formation, StringComparer.Ordinal)
                .ThenBy(p => p.Info.Concept, StringComparer.Ordinal).Select(p => p.Id);
        }
    }
}
