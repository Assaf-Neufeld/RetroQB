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
        FromOffense("golden-legion", OffensiveTeamPresets.GoldenLegion)
    });

    public static IReadOnlyList<TeamDefinition> Opponents { get; } = Array.AsReadOnly(new[]
    {
        FromDefense("scarlet-guard", DefensiveTeamPresets.ScarletGuard, .50f),
        FromDefense("crimson-rush", DefensiveTeamPresets.CrimsonRush, .65f),
        FromDefense("bloodline-bastion", DefensiveTeamPresets.BloodlineBastion, .80f),
        FromDefense("lockdown", DefensiveTeamPresets.Lockdown, .65f)
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
        // Complementary unit defaults are deliberately modest; existing offensive profiles are retained verbatim.
        float strength = .85f + .25f * offense.Skills.Average;
        var defense = new DefensiveTeamAttributes
        {
            Name = offense.Name, PrimaryColor = offense.PrimaryColor, SecondaryColor = offense.SecondaryColor,
            OverallRating = strength, SpeedMultiplier = strength, TackleAbility = strength,
            CoverageTightness = strength, InterceptionAbility = strength,
            Description = "Balanced defensive unit", Roster = DefensiveRoster.Default
        };
        float pass = Math.Clamp(.5f + .3f * (offense.Skills.QbThrowAccuracy - offense.Skills.RbPower), .3f, .7f);
        return new(id, offense, defense, new(pass));
    }

    private static TeamDefinition FromDefense(string id, DefensiveTeamAttributes defense, float rating)
    {
        var skills = new OffensiveTeamSkills
        {
            QbThrowPower = rating, QbThrowAccuracy = rating, WrSpeed = rating, WrSkill = rating,
            RbPower = rating, RbSpeed = rating, OlStrength = rating
        };
        var offense = new OffensiveTeamAttributes
        {
            Name = defense.Name, PrimaryColor = defense.PrimaryColor, SecondaryColor = defense.SecondaryColor,
            OverallRating = defense.OverallRating, TeamScore = (int)(50 + rating * 40), Skills = skills,
            Description = "Balanced offensive unit",
            Roster = OffensiveRosterFactory.Create(skills, "QB", ["WR1", "WR2", "WR3", "WR4"], "TE", "RB")
        };
        return new(id, offense, defense, new());
    }
}
