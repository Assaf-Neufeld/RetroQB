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
        IsPaused = !IsPaused;
    }

    public void ClearPause()
    {
        IsPaused = false;
    }
}
