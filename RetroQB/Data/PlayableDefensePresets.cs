using RetroQB.Entities;

namespace RetroQB.Data;

/// <summary>Independent defensive identities; offensive talent does not buy a better defense.</summary>
public static class PlayableDefensePresets
{
    public static DefensiveTeamAttributes For(OffensiveTeamAttributes offense)
    {
        // Speed, tackling, coverage, takeaways, rush, blitz. Stars are already included in roster ratings.
        var (speed, tackle, coverage, picks, rush, blitz, description, stars) = offense.Name switch
        {
            "Ballers" => (1f, 1f, 1f, 1f, .98f, .90f, "Balanced defense; no elite unit", new[] { DefenderSlot.MLB }),
            "Lightning" => (1.03f, .82f, .87f, .90f, .86f, .80f, "Fast pursuit; weak in contact", Array.Empty<DefenderSlot>()),
            "Bulldozers" => (.92f, 1.14f, .88f, .88f, 1.02f, .85f, "Stout front; slow secondary", new[] { DefenderSlot.DT1 }),
            "Phantoms" => (1.04f, .88f, 1.16f, 1.18f, .87f, .65f, "Ball hawks; vulnerable to runs", new[] { DefenderSlot.CB1, DefenderSlot.FS }),
            "Cyclones" => (1.08f, .92f, .95f, .98f, 1.10f, 1.25f, "Edge speed; light interior", new[] { DefenderSlot.OLB1 }),
            "Ironclad" => (.97f, 1.18f, 1.02f, .94f, 1.12f, .90f, "Run-stopping front; limited range", new[] { DefenderSlot.MLB, DefenderSlot.DT1 }),
            "Firebirds" => (.98f, .90f, .92f, 1.10f, .91f, 1.10f, "Opportunistic; gives up yards", Array.Empty<DefenderSlot>()),
            "Mustangs" => (1.04f, 1.03f, .94f, .87f, .91f, .85f, "Pursuit linebackers; soft coverage", new[] { DefenderSlot.MLB }),
            "Bombers" => (1f, .96f, .94f, .94f, 1.12f, 1.24f, "Strong pressure with a reliable secondary", new[] { DefenderSlot.DE1 }),
            "Sentinels" => (1f, 1.08f, 1.18f, 1.08f, .90f, .60f, "Disciplined coverage; little rush", new[] { DefenderSlot.CB1, DefenderSlot.OLB1 }),
            "Sharks" => (1.02f, .82f, .96f, 1.22f, .76f, .70f, "Ball-hawking secondary; weak front", new[] { DefenderSlot.CB1, DefenderSlot.FS }),
            "Vipers" => (1.10f, 1.12f, 1.08f, 1.05f, 1.12f, .92f, "Fast pressure defense; can be run on", Array.Empty<DefenderSlot>()),
            "Golden Legion" => (1.08f, 1.12f, 1.06f, 1.08f, 1.12f, .92f, "Elite playmakers; no perfect unit", new[] { DefenderSlot.MLB, DefenderSlot.DE1, DefenderSlot.CB1 }),
            _ => (1f, 1f, 1f, 1f, 1f, 1f, "Balanced defense", Array.Empty<DefenderSlot>())
        };
        var roster = Enum.GetValues<DefenderSlot>().ToDictionary(slot => slot, slot =>
        {
            bool star = stars.Contains(slot);
            bool front = slot.IsDefensiveLine() || slot.IsLinebacker();
            string name = (offense.Name, slot) switch
            {
                ("Ballers", DefenderSlot.MLB) => "Captain",
                ("Bulldozers", DefenderSlot.DT1) => "Quarry",
                ("Phantoms", DefenderSlot.CB1) => "Specter",
                ("Phantoms", DefenderSlot.FS) => "Mirage",
                ("Cyclones", DefenderSlot.OLB1) => "Tempest",
                ("Ironclad", DefenderSlot.MLB) => "Bastion",
                ("Ironclad", DefenderSlot.DT1) => "Crucible",
                ("Mustangs", DefenderSlot.MLB) => "Wrangler",
                ("Bombers", DefenderSlot.DE1) => "Warhead",
                ("Sentinels", DefenderSlot.CB1) => "Warden",
                ("Sentinels", DefenderSlot.OLB1) => "Vigil",
                ("Sharks", DefenderSlot.CB1) => "Mako",
                ("Sharks", DefenderSlot.FS) => "Finback",
                ("Golden Legion", DefenderSlot.MLB) => "Caesar",
                ("Golden Legion", DefenderSlot.DE1) => "Aureus",
                ("Golden Legion", DefenderSlot.CB1) => "Midas",
                _ => slot.ToString()
            };
            return new DefenderProfile(name,
                star ? 1.06f : 1f,
                star && front ? 1.14f : 1f,
                star && !front ? 1.16f : 1f,
                star && front ? 1.16f : 1f, star);
        });
        return new()
        {
            Name = offense.Name, PrimaryColor = offense.PrimaryColor, SecondaryColor = offense.SecondaryColor,
            Description = description, OverallRating = GetOverallRating(offense),
            SpeedMultiplier = speed, TackleAbility = tackle, CoverageTightness = coverage,
            InterceptionAbility = picks, PassRushAbility = rush, BlitzFrequency = blitz,
            Roster = new() { Defenders = roster }
        };
    }

    private static float GetOverallRating(OffensiveTeamAttributes offense)
    {
        int target = offense.Name switch
        {
            "Ballers" => 78, "Lightning" => 84, "Bulldozers" => 80, "Phantoms" => 78,
            "Cyclones" => 77, "Ironclad" => 86, "Firebirds" => 73, "Mustangs" => 76,
            "Bombers" => 87, "Sentinels" => 82, "Sharks" => 70, "Vipers" => 91,
            "Golden Legion" => 90, _ => 76
        };
        int offenseScore = (int)MathF.Round(50 + 50 * offense.Skills.Average);
        int defenseScore = target * 2 - offenseScore;
        return 1 + (defenseScore - 75) / 100f;
    }
}
