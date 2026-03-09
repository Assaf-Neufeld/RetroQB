using RetroQB.Gameplay.Replay;
using RetroQB.Input;

namespace RetroQB.Gameplay.Controllers;

public sealed class ReplayStateHandler
{
    public GameState ReturnState { get; private set; } = GameState.PlayOver;

    public bool CanEnterReplay(GameState state)
    {
        return state is GameState.PreSnap or GameState.PlayOver or GameState.DriveOver or GameState.StageComplete or GameState.GameOver;
    }

    public bool TryEnterReplay(
        GameState currentState,
        ReplayClipStore clipStore,
        ReplayPlayer player,
        GameStateManager stateManager)
    {
        if (!CanEnterReplay(currentState) || !clipStore.HasClip || clipStore.Current is null)
        {
            return false;
        }

        ReturnState = currentState;
        player.Load(clipStore.Current, GetPlaybackSpeed(clipStore.Current));
        stateManager.SetState(GameState.Replay);
        return true;
    }

    public void Update(float dt, InputManager input, ReplayPlayer player, GameStateManager stateManager)
    {
        if (stateManager.State != GameState.Replay)
        {
            return;
        }

        if (input.IsReplaySkipPressed())
        {
            ExitReplay(player, stateManager);
            return;
        }

        player.Update(dt);
        if (player.IsComplete)
        {
            ExitReplay(player, stateManager);
        }
    }

    public void ExitReplay(ReplayPlayer player, GameStateManager stateManager)
    {
        player.Unload();
        stateManager.SetState(ReturnState);
    }

    private static float GetPlaybackSpeed(ReplayClip clip)
    {
        if (clip.DurationSeconds >= 8f)
        {
            return 0.26f;
        }

        if (clip.DurationSeconds >= 5.5f)
        {
            return 0.29f;
        }

        return ReplayPlayer.DefaultPlaybackSpeed;
    }
}
