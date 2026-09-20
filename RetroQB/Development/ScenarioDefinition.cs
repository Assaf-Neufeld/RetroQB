using RetroQB.Data;
using RetroQB.Gameplay;

namespace RetroQB.Development;

/// <summary>Reproducible offensive baselines and the first defensive-drive checkpoint.</summary>
internal sealed record ScenarioDefinition(string Name, string PlayId, DriveStart Start,
    bool FieldGoal = false, bool Replay = false, int ThrowTick = 45, bool IsRun = false)
{
    public const float FixedStep = 1f / 60;
    public const int MaxTicks = 60 * 90;
    public OffensiveTeamAttributes Offense { get; init; } = OffensiveTeamPresets.Ballers;
    public DefensiveTeamAttributes Defense { get; init; } = DefensiveTeamPresets.ScarletGuard;
    public SeasonStage Stage { get; init; } = SeasonStage.RegularSeason;
    public bool Defending { get; init; }
    public bool ScriptedOffense { get; init; }
    public bool FullMatch { get; init; }
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[]
    {
        "offense-pass", "offense-run", "offense-play-action", "offense-field-goal", "offense-replay",
        "defense-drive", "defense-calls", "defense-control-base", "defense-control-nickel",
        "defense-quick", "defense-deep", "defense-play-action",
        "timed-game", "timed-short", "late-tying-kick", "late-protect-lead", "overtime-pairs", "timed-season", "timed-season-short", "timed-layout", "release-catalog", "release-matches", "release-default"
    });

    public static ScenarioDefinition Get(string name) => name switch
    {
        "release-catalog" or "release-matches" or "release-default" => new(name, "", new()),
        "timed-game" or "timed-short" or "late-tying-kick" or "late-protect-lead" or "overtime-pairs" or "timed-season" or "timed-season-short" or "timed-layout"
            => new(name, "", new()) { FullMatch = true },
        "defense-drive" => new(name, "", new()) { Defending = true },
        "defense-calls" => new(name, "", new()) { Defending = true },
        "defense-quick" => new(name, "pass.mesh", new()) { Defending = true },
        "defense-deep" => new(name, "pass.four-verts", new()) { Defending = true },
        "defense-play-action" => new(name, "pass.gun-doubles.pa-cross", new()) { Defending = true },
        "defense-control-base" => new(name, "run.hb-dive", new()) { Defending = true, ScriptedOffense = true },
        "defense-control-nickel" => new(name, "pass.mesh", new()) { Defending = true, ScriptedOffense = true },
        "offense-pass" => new(name, "pass.mesh", new()),
        "offense-run" => new(name, "run.hb-dive", new(), IsRun: true),
        "offense-play-action" => new(name, "pass.gun-doubles.pa-cross", new(), ThrowTick: 90),
        "offense-field-goal" => new(name, "pass.mesh", new(70, 4, 5), FieldGoal: true),
        "offense-replay" => new(name, "pass.mesh", new(), Replay: true),
        _ => throw new ArgumentException($"Unknown scenario '{name}'. Available: {string.Join(", ", Names)}")
    };
}
