using System.Reflection;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class PregameTests
{
    [Fact]
    public void EveryNewGameShowsItsCurrentOpponentAndStage()
    {
        using var session = new GameSession();
        var state = (GameStateManager)typeof(GameSession)
            .GetField("_stateManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        void Invoke(string name) => typeof(GameSession)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null);

        session.SetOffensiveTeam(OffensiveTeamPresets.Ballers);
        Invoke("StartSeasonFromMenu");
        AssertMatchup(SeasonStage.RegularSeason, "Scarlet Guard");
        Invoke("AdvanceToNextStage");
        AssertMatchup(SeasonStage.Playoff, "Crimson Rush");
        Invoke("AdvanceToNextStage");
        AssertMatchup(SeasonStage.SuperBowl, "Bloodline Bastion");
        Invoke("HandleRestart");
        AssertMatchup(SeasonStage.SuperBowl, "Bloodline Bastion");

        // A new drive within this game must not show another matchup introduction.
        state.SetState(GameState.PreSnap);
        Invoke("InitializeDrive");
        Assert.Equal(GameState.PreSnap, state.State);

        void AssertMatchup(SeasonStage stage, string opponent)
        {
            Assert.Equal(GameState.Pregame, state.State);
            Assert.Equal(stage, session.CurrentStage);
            Assert.Equal(opponent, session.DefensiveTeam.Name);
            Assert.Equal(OffensiveTeamPresets.Ballers.Name, session.OffensiveTeam.Name);
        }
    }

    [Fact]
    public void ScoutingReflectsTheOpponentAttributes()
    {
        Assert.Equal("SURE TACKLING", OpponentScoutingReport.FromTeam(DefensiveTeamPresets.ScarletGuard).Strength);
        Assert.Equal("RELENTLESS BLITZ", OpponentScoutingReport.FromTeam(DefensiveTeamPresets.CrimsonRush).Strength);
        Assert.Equal("SURE TACKLING", OpponentScoutingReport.FromTeam(DefensiveTeamPresets.BloodlineBastion).Strength);
        var custom = new DefensiveTeamAttributes { PassRushAbility = 2f };
        Assert.Equal("PASS RUSH", OpponentScoutingReport.FromTeam(custom).Strength);
        Assert.False(string.IsNullOrWhiteSpace(OpponentScoutingReport.FromTeam(custom).Tip));
    }
}
