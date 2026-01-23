namespace RetroQB.Gameplay;

public readonly record struct QbStatsSnapshot(
    int Completions,
    int Attempts,
    int PassYards,
    int PassTds,
    int Interceptions);

public readonly record struct ReceiverStatsSnapshot(
    string Label,
    int Receptions,
    int Yards,
    int Tds);

public readonly record struct RbStatsSnapshot(
    int Yards,
    int Tds);

public readonly record struct GameStatsSnapshot(
    QbStatsSnapshot Qb,
    IReadOnlyList<ReceiverStatsSnapshot> Receivers,
    RbStatsSnapshot Rb);
