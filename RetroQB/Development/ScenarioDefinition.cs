using RetroQB.Data;
using RetroQB.Gameplay;

namespace RetroQB.Development;

/// <summary>Only state supported by today's game; match clocks are added in M2.</summary>
internal sealed record ScenarioDefinition(string Name, string PlayId, DriveStart Start,
    bool FieldGoal = false, bool Replay = false, int ThrowTick = 45, bool IsRun = false)
{
    public const float FixedStep = 1f / 60;
    public const int MaxTicks = 60 * 90;
    public OffensiveTeamAttributes Offense { get; init; } = OffensiveTeamPresets.Ballers;
    public DefensiveTeamAttributes Defense { get; init; } = DefensiveTeamPresets.ScarletGuard;
    public SeasonStage Stage { get; init; } = SeasonStage.RegularSeason;
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
    {
        "offense-pass", "offense-run", "offense-play-action", "offense-field-goal", "offense-replay"
    });

    public static ScenarioDefinition Get(string name) => name switch
    {
        "offense-pass" => new(name, "pass.mesh", new()),
        "offense-run" => new(name, "run.hb-dive", new(), IsRun: true),
        "offense-play-action" => new(name, "pass.gun-doubles.pa-cross", new(), ThrowTick: 90),
        "offense-field-goal" => new(name, "pass.mesh", new(70, 4, 5), FieldGoal: true),
        "offense-replay" => new(name, "pass.mesh", new(), Replay: true),
        _ => throw new ArgumentException($"Unknown scenario '{name}'. Available: {string.Join(", ", Names)}")
    };
}
