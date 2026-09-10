using System.Reflection;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Stats;

namespace RetroQB.Tests;

public sealed class GameRestartTests
{
    [Theory]
    [InlineData(SeasonStage.RegularSeason, GameState.PlayActive)]
    [InlineData(SeasonStage.Playoff, GameState.StageComplete)]
    [InlineData(SeasonStage.SuperBowl, GameState.GameOver)]
    [InlineData(SeasonStage.SuperBowl, GameState.Replay)]
    public void RestartPreservesEarlierGamesAndDiscardsCurrentGame(SeasonStage stage, GameState stateBeforeRestart)
    {
        using var session = new GameSession();
        var state = Field<GameStateManager>("_stateManager");
        var plays = Field<PlayManager>("_playManager");
        var stats = Field<IStatisticsTracker>("_statsTracker");
        var season = Field<SeasonSummary>("_seasonSummary");
        session.SetOffensiveTeam(OffensiveTeamPresets.Ballers);
        Invoke("StartSeasonFromMenu");

        while (session.CurrentStage != stage)
        {
            RecordStats();
            season.RecordPlay(new PlayRecord { Down = 1, Distance = 10, Gain = 20 });
            season.RecordGame(session.CurrentStage, 21, 7, stats.BuildSnapshot());
            Invoke("AdvanceToNextStage");
        }

        var previousGames = season.Games.ToArray();
        var previousStats = stats.BuildSnapshot();
        var previousDetails = season.BuildDominanceScoreDetails();
        float previousScore = season.ComputeDominanceScore();
        string opponent = session.DefensiveTeam.Name;

        // Repeated restarts must retain the same baseline without duplicating results.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            RecordStats();
            season.RecordPlay(new PlayRecord { Outcome = PlayOutcome.Incomplete });
            plays.StartPlay();
            plays.ResolvePlay(100f, false, false, false, true);
            if (stateBeforeRestart is GameState.StageComplete or GameState.GameOver)
                season.RecordGame(stage, 21, 14, stats.BuildSnapshot());
            state.SetState(stateBeforeRestart);
            state.TogglePause();

            Invoke("HandleRestart");

            Assert.Equal(stage, session.CurrentStage);
            Assert.Equal(opponent, session.DefensiveTeam.Name);
            Assert.Equal(OffensiveTeamPresets.Ballers.Name, session.OffensiveTeam.Name);
            Assert.Equal(GameState.Pregame, state.State);
            Assert.False(state.IsPaused);
            Assert.Equal(0, plays.Score);
            Assert.Equal(0, plays.AwayScore);
            Assert.Equal(1, plays.Down);
            Assert.Equal(10f, plays.Distance);
            Assert.Empty(plays.PlayRecords);
            Assert.Equal(previousGames, season.Games.ToArray());
            Assert.Equal(previousStats.Qb, stats.BuildSnapshot().Qb);
            Assert.Equal(previousStats.Rb, stats.BuildSnapshot().Rb);
            Assert.Equal(previousStats.Receivers.ToArray(), stats.BuildSnapshot().Receivers.ToArray());
            Assert.Equal(previousStats.Qb, season.CumulativeQbStats);
            Assert.Equal(previousStats.Rb, season.CumulativeRbStats);
            Assert.Equal(previousDetails, season.BuildDominanceScoreDetails());
            Assert.Equal(previousScore, season.ComputeDominanceScore());
        }

        void RecordStats()
        {
            stats.RecordPassAttempt();
            stats.RecordTarget(ReceiverSlot.TE2);
            stats.RecordCompletion(ReceiverSlot.TE2);
            stats.RecordPassYards(ReceiverSlot.TE2, 20, true);
            stats.RecordInterception();
            stats.RecordSack(4);
            stats.RecordQbRushYards(8, true);
            stats.RecordRushYards(-2, false);
        }

        T Field<T>(string name) => (T)typeof(GameSession)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        void Invoke(string name) => typeof(GameSession)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null);
    }
}
