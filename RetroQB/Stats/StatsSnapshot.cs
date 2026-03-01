namespace RetroQB.Stats;

public readonly record struct QbStatsSnapshot(
    int Completions,
    int Attempts,
    int PassYards,
    int PassTds,
    int Interceptions,
    int Sacks,
    int SackYardsLost,
    int RushAttempts,
    int RushYards,
    int RushTds);

public readonly record struct ReceiverStatsSnapshot(
    string Label,
    int Targets,
    int Receptions,
    int Yards,
    int Tds);

public readonly record struct RbStatsSnapshot(
    int Attempts,
    int Yards,
    int Tds);

public readonly record struct GameStatsSnapshot(
    QbStatsSnapshot Qb,
    IReadOnlyList<ReceiverStatsSnapshot> Receivers,
    RbStatsSnapshot Rb);
