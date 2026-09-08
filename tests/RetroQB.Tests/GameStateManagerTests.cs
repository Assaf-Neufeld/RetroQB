using RetroQB.Core;

namespace RetroQB.Tests;

public sealed class GameStateManagerTests
{
    [Theory]
    [InlineData(GameState.PreSnap)]
    [InlineData(GameState.Pregame)]
    [InlineData(GameState.PlayActive)]
    [InlineData(GameState.Replay)]
    [InlineData(GameState.PlayOver)]
    [InlineData(GameState.DriveOver)]
    [InlineData(GameState.StageComplete)]
    [InlineData(GameState.GameOver)]
    public void PauseAndResumePreserveGameState(GameState state)
    {
        var manager = new GameStateManager();
        manager.SetState(state);
        manager.TogglePause();
        Assert.True(manager.IsPaused);
        Assert.Equal(state, manager.State);
        manager.TogglePause();
        Assert.False(manager.IsPaused);
        Assert.Equal(state, manager.State);
    }

    [Theory]
    [InlineData(GameState.MainMenu)]
    [InlineData(GameState.PlayerNameEntry)]
    [InlineData(GameState.NameConflict)]
    public void EscapeIsLeftToMenus(GameState state)
    {
        var manager = new GameStateManager();
        manager.SetState(state);
        manager.TogglePause();
        Assert.False(manager.IsPaused);
    }
}
