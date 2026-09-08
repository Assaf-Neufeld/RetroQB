namespace RetroQB.Data;

public readonly record struct OpponentScoutingReport(string Strength, string Tip)
{
    public static OpponentScoutingReport FromTeam(DefensiveTeamAttributes team)
    {
        // Compare the same baseline-relative multipliers used by the defense.
        (float Rating, string Strength, string Tip)[] traits =
        [
            (team.TackleAbility, "SURE TACKLING", "Expect contact to end the play. Find space before turning upfield."),
            (team.PassRushAbility, "PASS RUSH", "The pocket can close quickly. Have a quick outlet ready."),
            (team.CoverageTightness, "TIGHT COVERAGE", "Passing windows are small. Wait for separation and throw on time."),
            (team.InterceptionAbility, "BALL HAWKS", "They punish risky throws. Look for a clear passing lane."),
            (team.SpeedMultiplier, "SIDELINE SPEED", "They close ground fast. Get the ball out before help arrives."),
            (team.BlitzFrequency, "RELENTLESS BLITZ", "Expect extra rushers. Find your hot receiver and release early.")
        ];
        var best = traits.MaxBy(trait => trait.Rating);
        return new(best.Strength, best.Tip);
    }
}
