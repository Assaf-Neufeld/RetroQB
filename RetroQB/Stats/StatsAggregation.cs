namespace RetroQB.Stats;

public static class StatsAggregation
{
    public static GameStatsSnapshot Sum(IEnumerable<GameStatsSnapshot> games)
    {
        var all = games.ToArray();
        return new(new(all.Sum(x => x.Qb.Completions), all.Sum(x => x.Qb.Attempts), all.Sum(x => x.Qb.PassYards),
            all.Sum(x => x.Qb.PassTds), all.Sum(x => x.Qb.Interceptions), all.Sum(x => x.Qb.Sacks), all.Sum(x => x.Qb.SackYardsLost),
            all.Sum(x => x.Qb.RushAttempts), all.Sum(x => x.Qb.RushYards), all.Sum(x => x.Qb.RushTds)),
            Array.AsReadOnly(all.SelectMany(x => x.Receivers).GroupBy(x => x.Label).Select(g => new ReceiverStatsSnapshot(g.Key,
                g.Sum(x => x.Targets), g.Sum(x => x.Receptions), g.Sum(x => x.Yards), g.Sum(x => x.Tds))).ToArray()),
            new(all.Sum(x => x.Rb.Attempts), all.Sum(x => x.Rb.Yards), all.Sum(x => x.Rb.Tds)));
    }
}
