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

    public void DrawScoreboard(PlayManager play, string resultText, GameState state, OffensiveTeamAttributes offensiveTeam)
    {
        _scoreboard.Draw(play, resultText, state, offensiveTeam, _stats);
    }

    public void DrawSidePanel(PlayManager play, string resultText, string selectedReceiverLabel, GameState state)
    {
        _sidePanel.Draw(play, resultText, selectedReceiverLabel, state);
    }

    public void DrawMainMenu(int selectedTeamIndex, IReadOnlyList<OffensiveTeamAttributes> teams)
    {
        _menu.Draw(selectedTeamIndex, teams);
    }

    public void DrawPause()
    {
        _menu.DrawPause();
    }

    public void DrawVictoryBanner(int finalScore, int awayScore)
    {
        _banner.DrawVictoryBanner(finalScore, awayScore, _stats);
    }

    public void DrawTouchdownPopup()
    {
        _banner.DrawTouchdownPopup();
    }
}
