namespace RetroQB.Data;

/// <summary>Permanent roster designations, independent of the season round.</summary>
public static class RosterStars
{
    public static IReadOnlySet<string> ForOffense(string team) => new HashSet<string>(team switch
    {
        "Ballers" => ["QB"],
        "Lightning" => ["QB", "WR1"],
        "Bulldozers" => ["RB1"],
        "Cyclones" => ["WR2"],
        "Firebirds" => ["WR1", "TE1"],
        "Mustangs" => ["RB1"],
        "Bombers" => ["QB"],
        "Sharks" => [],
        "Vipers" => ["QB", "WR1"],
        "Golden Legion" => ["QB", "WR1", "RB1"],
        "Scarlet Guard" => ["TE1"],
        "Crimson Rush" => ["QB", "WR1"],
        "Bloodline Bastion" => ["RB1", "TE1"],
        "Lockdown" => ["QB"],
        _ => Array.Empty<string>()
    });

    public static string Summary(TeamDefinition team)
    {
        var roster = team.Offense.Roster;
        var names = new List<string>();
        if (roster.Quarterback.IsStarPlayer) names.Add($"QB {roster.Quarterback.Name}");
        names.AddRange(Enum.GetValues<Entities.ReceiverSlot>().Where(roster.IsStarPlayer)
            .Select(slot => $"{slot} {roster.GetReceiverName(slot)}"));
        names.AddRange(team.Defense.Roster.Defenders.Where(p => p.Value.IsStarPlayer)
            .Select(p => $"{p.Key} {p.Value.Name}"));
        return "* " + string.Join(" / ", names);
    }
}
