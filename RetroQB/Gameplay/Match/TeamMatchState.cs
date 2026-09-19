using RetroQB.AI;
using RetroQB.Data;

namespace RetroQB.Gameplay;

public enum MatchRuleset { LegacyOffenseOnly = 1, TwoSidedTimed = 2 }

/// <summary>Per-match mutable state belongs to a team, never to its current unit role.</summary>
public sealed class TeamMatchState
{
    private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);
    internal StatisticsTracker Statistics { get; } = new();
    public GameStatsSnapshot Stats => Statistics.BuildSnapshot();
    public TeamDefinition Definition { get; }
    public OffensiveTeamAttributes OffensiveAttributes => Definition.Offense;
    public DefensiveTeamAttributes DefensiveAttributes { get; }
    public DefensiveMemory DefensiveMemory { get; } = new();
    public float DifficultyMultiplier { get; }
    public CpuTendencies Tendencies { get; }
    public int Score { get; internal set; }
    public int GetCallCount(string id) => _calls.GetValueOrDefault(id);
    internal void RecordCall(string id) => _calls[id] = GetCallCount(id) + 1;

    internal TeamMatchState(TeamDefinition definition, bool opponent, SeasonStage stage)
    {
        Definition = definition;
        DifficultyMultiplier = opponent ? stage.GetDifficultyMultiplier() : 1f;
        DefensiveAttributes = opponent ? TeamDifficulty.ScaleDefense(definition.Defense, stage) : definition.Defense;
        Tendencies = definition.Tendencies with
        {
            ReadIntervalSeconds = definition.Tendencies.ReadIntervalSeconds / DifficultyMultiplier
        };
    }

    internal void Reset()
    {
        Score = 0;
        Statistics.Reset();
        DefensiveMemory.Reset();
        _calls.Clear();
    }
}
