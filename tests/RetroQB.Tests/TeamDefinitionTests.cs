using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class TeamDefinitionTests
{
    [Fact]
    public void CatalogPreservesExistingProfilesAndSuppliesBothUnits()
    {
        Assert.Equal(11, TeamCatalog.Selectable.Count);
        Assert.Equal(4, TeamCatalog.Opponents.Count);
        var golden = OffensiveTeamPresets.GoldenLegion;
        var retained = TeamCatalog.Get("golden-legion").Offense;
        Assert.Equal(golden.OverallRating, retained.OverallRating);
        Assert.Equal(golden.Skills.QbThrowAccuracy, retained.Skills.QbThrowAccuracy);
        Assert.Equal(golden.PrimaryColor, retained.PrimaryColor);
        Assert.Equal(DefensiveTeamPresets.BloodlineBastion.CoverageTightness,
            TeamCatalog.ForStage(SeasonStage.SuperBowl).Defense.CoverageTightness);
        var all = TeamCatalog.Selectable.Concat(TeamCatalog.Opponents).ToArray();
        Assert.Equal(all.Length, all.Select(t => t.Id).Distinct().Count());
        foreach (var team in all)
        {
            Assert.Equal(team.Name, team.Defense.Name);
            Assert.Equal(team.PrimaryColor, team.Defense.PrimaryColor);
            Assert.Equal(team.SecondaryColor, team.Defense.SecondaryColor);
            Assert.NotNull(team.Offense.Roster);
            Assert.NotNull(team.Defense.Roster);
        }
    }

    [Theory]
    [InlineData(SeasonStage.RegularSeason)]
    [InlineData(SeasonStage.Playoff)]
    [InlineData(SeasonStage.SuperBowl)]
    public void DifficultyAndIdentityStayWithTeamsAcrossPossessions(SeasonStage stage)
    {
        var user = TeamCatalog.Get("lightning"); var cpu = TeamCatalog.ForStage(stage);
        var match = new MatchState(user, cpu, user.Id, stage);
        var defense = match.Opponent.DefensiveAttributes;
        Assert.Same(user.Defense, match.User.DefensiveAttributes);
        Assert.Same(user.Offense, match.User.OffensiveAttributes);
        Assert.Equal(cpu.Defense.SpeedMultiplier * stage.GetDifficultyMultiplier(), defense.SpeedMultiplier);
        Assert.Equal(cpu.Tendencies.ReadIntervalSeconds / stage.GetDifficultyMultiplier(), match.Opponent.Tendencies.ReadIntervalSeconds);
        for (int i = 0; i < 2; i++)
        {
            var play = match.BeginPlay("identity");
            match.Resolve(new(play.Id, play.OffenseId, PlayEndReason.Touchdown, 100));
            match.ContinuePossession();
            Assert.Same(user, match.User.Definition);
            Assert.Same(cpu, match.Opponent.Definition);
            Assert.Same(defense, match.Opponent.DefensiveAttributes);
        }
        Assert.Equal(user.Id, match.PossessionId);
    }
}
