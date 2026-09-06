namespace RetroQB.Core;

public sealed class GameStateManager
{
    public GameState State { get; private set; } = GameState.MainMenu;
    public bool IsPaused { get; private set; }

    public void SetState(GameState state)
    {
        State = state;
    }

    public void TogglePause()
    {
        // Menus handle Escape themselves (for example, closing the leaderboard).
        if (State is GameState.MainMenu or GameState.PlayerNameEntry or GameState.NameConflict)
        {
            return;
        }

        IsPaused = !IsPaused;
    }

    public void ClearPause()
    {
        IsPaused = false;
    }
}
