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
        new QbStatsSnapshot(0, 0, 0, 0, 0, 0, 0),
        Array.Empty<ReceiverStatsSnapshot>(),
        new RbStatsSnapshot(0, 0));

    public void SetStatsSnapshot(GameStatsSnapshot stats)
    {
        _stats = stats;
    }

    public void DrawScoreboard(PlayManager play, string resultText, GameState state, OffensiveTeamAttributes offensiveTeam, SeasonStage stage)
    {
        _scoreboard.Draw(play, resultText, state, offensiveTeam, _stats, stage);
    }

    public void DrawSidePanel(PlayManager play, string resultText, string selectedReceiverLabel, GameState state, SeasonStage stage)
    {
        _sidePanel.Draw(play, resultText, selectedReceiverLabel, state, stage);
    }

    public void DrawMainMenu(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams)
    {
        _menu.Draw(selectedTeamIndex, teams);
    }

    public void DrawPause()
    {
        _menu.DrawPause();
    }

    public void DrawStageCompleteBanner(int finalScore, int awayScore, SeasonStage completedStage)
    {
        _banner.DrawStageCompleteBanner(finalScore, awayScore, completedStage, _stats);
    }

    public void DrawChampionBanner(int finalScore, int awayScore, SeasonStage stage, SeasonSummary seasonSummary)
    {
        _banner.DrawChampionBanner(finalScore, awayScore, _stats, seasonSummary);
    }

    public void DrawEliminationBanner(int finalScore, int awayScore, SeasonStage stage, SeasonSummary seasonSummary)
    {
        _banner.DrawEliminationBanner(finalScore, awayScore, stage, _stats, seasonSummary);
    }

    public void DrawTouchdownPopup()
    {
        _banner.DrawTouchdownPopup();
    }
}
