using System;
using System.Collections.Generic;
using RetroQB.Core;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

/// <summary>
/// Coordinator for all HUD rendering. Delegates to specialized sub-renderers.
/// </summary>
public sealed class HudRenderer
{
    private readonly ScoreboardRenderer _scoreboard = new();
    private readonly SidePanelRenderer _sidePanel = new();
    private readonly MenuRenderer _menu = new();
    private readonly BannerRenderer _banner = new();

    private GameStatsSnapshot _stats = new(
        new QbStatsSnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        Array.Empty<ReceiverStatsSnapshot>(),
        new RbStatsSnapshot(0, 0, 0));

    public void SetStatsSnapshot(GameStatsSnapshot stats)
    {
        _stats = stats;
    }

    public void DrawScoreboard(PlayManager play, string resultText, GameState state, OffensiveTeamAttributes offensiveTeam, DefensiveTeamAttributes defensiveTeam, SeasonStage stage, int driveSummaryScrollOffsetFromLatest)
    {
        _scoreboard.Draw(play, resultText, state, offensiveTeam, defensiveTeam, _stats, stage, driveSummaryScrollOffsetFromLatest);
    }

    public void DrawSidePanel(PlayManager play, string resultText, string selectedReceiverLabel, GameState state, SeasonStage stage, bool replayAvailable)
    {
        _sidePanel.Draw(play, resultText, selectedReceiverLabel, state, stage, replayAvailable);
    }

    public void DrawMainMenu(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams, LeaderboardSummary leaderboardSummary, bool showLeaderboard)
    {
        _menu.Draw(selectedTeamIndex, teams, leaderboardSummary, showLeaderboard);
    }

    public void DrawPlayerNameEntry(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams, string currentName, string message, LeaderboardSummary leaderboardSummary, bool isPostSeasonSaveMode)
    {
        _menu.DrawNameEntry(selectedTeamIndex, teams, currentName, message, leaderboardSummary, isPostSeasonSaveMode);
    }

    public void DrawNameConflict(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams, string duplicateName)
    {
        _menu.DrawNameConflict(selectedTeamIndex, teams, duplicateName);
    }

    public void DrawPause()
    {
        _menu.DrawPause();
    }

    public void DrawStageCompleteBanner(int finalScore, int awayScore, SeasonStage completedStage, SeasonSummary seasonSummary, LeaderboardSummary leaderboardSummary)
    {
        _banner.DrawStageCompleteBanner(finalScore, awayScore, completedStage, _stats, seasonSummary, leaderboardSummary);
    }

    public void DrawChampionBanner(int finalScore, int awayScore, SeasonStage stage, SeasonSummary seasonSummary, LeaderboardSummary leaderboardSummary)
    {
        _banner.DrawChampionBanner(finalScore, awayScore, _stats, seasonSummary, leaderboardSummary);
    }

    public void DrawEliminationBanner(int finalScore, int awayScore, SeasonStage stage, SeasonSummary seasonSummary, LeaderboardSummary leaderboardSummary)
    {
        _banner.DrawEliminationBanner(finalScore, awayScore, stage, _stats, seasonSummary, leaderboardSummary);
    }

    public void DrawTouchdownPopup()
    {
        _banner.DrawTouchdownPopup();
    }
}
