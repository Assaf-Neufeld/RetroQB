using Raylib_cs;

namespace RetroQB.Data;

public sealed record CpuTendencies(float PassPreference = .55f, float ReadIntervalSeconds = .25f);

/// <summary>Team identity never changes when its offensive and defensive units swap roles.</summary>
public sealed record TeamDefinition(string Id, OffensiveTeamAttributes Offense,
    DefensiveTeamAttributes Defense, CpuTendencies Tendencies)
{
    public string Name => Offense.Name;
    public Color PrimaryColor => Offense.PrimaryColor;
    public Color SecondaryColor => Offense.SecondaryColor;
    public int OffenseScore => (int)MathF.Round(50 + 50 * Offense.Skills.Average);
    public int DefenseScore => RateDefense(Defense);
    public static int RateDefense(DefensiveTeamAttributes defense) =>
        (int)MathF.Round(Math.Clamp(75 + 100 * (defense.OverallRating - 1), 50, 99));
    public static int CalculateScore(OffensiveTeamSkills offense, DefensiveTeamAttributes defense) =>
        (int)MathF.Round((50 + 50 * offense.Average + RateDefense(defense)) / 2);
}

public static class TeamCatalog
{
    public static IReadOnlyList<TeamDefinition> Selectable { get; } = Array.AsReadOnly(new[]
    {
        FromOffense("ballers", OffensiveTeamPresets.Ballers),
        FromOffense("lightning", OffensiveTeamPresets.Lightning),
        FromOffense("bulldozers", OffensiveTeamPresets.Bulldozers),
        FromOffense("phantoms", OffensiveTeamPresets.Phantoms),
        FromOffense("cyclones", OffensiveTeamPresets.Cyclones),
        FromOffense("ironclad", OffensiveTeamPresets.Ironclad),
        FromOffense("firebirds", OffensiveTeamPresets.Firebirds),
        FromOffense("mustangs", OffensiveTeamPresets.Mustangs),
        FromOffense("bombers", OffensiveTeamPresets.Bombers),
        FromOffense("sentinels", OffensiveTeamPresets.Sentinels),
        FromOffense("sharks", OffensiveTeamPresets.Sharks),
        FromOffense("vipers", OffensiveTeamPresets.Vipers),
        FromOffense("golden-legion", OffensiveTeamPresets.GoldenLegion)
    });

    public static IReadOnlyList<TeamDefinition> Opponents { get; } = Array.AsReadOnly(new[]
    {
        FromDefense("scarlet-guard", DefensiveTeamPresets.ScarletGuard,
            new() { RbPower = .56f, RbSpeed = .46f, QbThrowPower = .40f, QbThrowAccuracy = .64f, WrSpeed = .42f, WrSkill = .58f, OlStrength = .48f },
            new(.46f, .32f), "Short passes and TE; limited deep speed", "Marshal", ["Rose", "Banner", "Pennant", "Crest"], "Herald", "March"),
        FromDefense("crimson-rush", DefensiveTeamPresets.CrimsonRush,
            new() { RbPower = .40f, RbSpeed = .76f, QbThrowPower = .82f, QbThrowAccuracy = .72f, WrSpeed = .84f, WrSkill = .72f, OlStrength = .42f },
            new(.72f, .25f), "Explosive passing; weak protection", "Flint", ["Flame", "Scorch", "Heat", "Ash"], "Flicker", "Sprint"),
        FromDefense("bloodline-bastion", DefensiveTeamPresets.BloodlineBastion,
            new() { RbPower = .92f, RbSpeed = .70f, QbThrowPower = .62f, QbThrowAccuracy = .76f, WrSpeed = .48f, WrSkill = .72f, OlStrength = .86f },
            new(.36f, .23f), "Power runs and TE; limited wideout speed", "Regent", ["Heir", "Duke", "Baron", "Count"], "Monarch", "Reign"),
        FromDefense("lockdown", DefensiveTeamPresets.Lockdown,
            new() { RbPower = .44f, RbSpeed = .52f, QbThrowPower = .46f, QbThrowAccuracy = .86f, WrSpeed = .50f, WrSkill = .78f, OlStrength = .58f },
            new(.62f, .27f), "Accurate possession passing; little power", "Cipher", ["Code", "Key", "Latch", "Seal"], "Vault", "Escape")
    });

    public static TeamDefinition ForStage(SeasonStage stage) => stage switch
    {
        SeasonStage.RegularSeason => Opponents[0],
        SeasonStage.Playoff => Opponents[1],
        SeasonStage.SuperBowl => Opponents[2],
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    public static TeamDefinition Get(string id) => Selectable.Concat(Opponents)
        .SingleOrDefault(t => t.Id == id) ?? throw new ArgumentException($"Unknown team: {id}", nameof(id));

    private static TeamDefinition FromOffense(string id, OffensiveTeamAttributes offense)
    {
        var defense = PlayableDefensePresets.For(offense);
        float pass = Math.Clamp(.5f + .3f * (offense.Skills.QbThrowAccuracy - offense.Skills.RbPower), .3f, .7f);
        return new(id, offense, defense, new(pass));
    }

    private static TeamDefinition FromDefense(string id, DefensiveTeamAttributes defense, OffensiveTeamSkills skills,
        CpuTendencies tendencies, string description, string qb, string[] receivers, string te, string rb)
    {
        var offense = new OffensiveTeamAttributes
        {
            Name = defense.Name, PrimaryColor = defense.PrimaryColor, SecondaryColor = defense.SecondaryColor,
            OverallRating = OffensiveRosterFactory.ComputeOverallRating(skills), TeamScore = TeamDefinition.CalculateScore(skills, defense), Skills = skills,
            Description = description,
            Roster = OffensiveRosterFactory.Create(skills, qb, receivers, te, rb, RosterStars.ForOffense(defense.Name))
        };
        return new(id, offense, defense, tendencies);
    }
}
