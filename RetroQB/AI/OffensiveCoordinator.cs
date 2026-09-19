using RetroQB.Gameplay;

namespace RetroQB.AI;

/// <summary>Initial supported CPU call set; expansion to the full catalog is a later gate.</summary>
public sealed class OffensiveCoordinator(Random random)
{
    public static IReadOnlyList<string> Passes { get; } = Array.AsReadOnly(new[]
        { "pass.mesh", "pass.slant-flat", "pass.four-verts", "pass.gun-doubles.pa-cross" });
    public static IReadOnlyList<string> Runs { get; } = Array.AsReadOnly(new[] { "run.hb-dive" });
    public string Select(DriveStart series, CpuTendencies tendencies)
    {
        float chance = Math.Clamp(tendencies.PassPreference + (series.Distance > 7 && series.Down >= 3 ? .25f : 0), .15f, .85f);
        var calls = random.NextDouble() < chance ? Passes : Runs;
        return calls[random.Next(calls.Count)];
    }
}
