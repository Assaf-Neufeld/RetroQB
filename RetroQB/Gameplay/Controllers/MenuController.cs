using RetroQB.Input;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles main-menu team selection and post-game return-to-menu logic.
/// </summary>
public sealed class MenuController
{
    private readonly InputManager _input;
    private int _selectedTeamIndex;

    public int SelectedTeamIndex => _selectedTeamIndex;

    public MenuController(InputManager input)
    {
        _input = input;
    }

    /// <summary>
    /// Processes team selection input. Returns true when Enter is pressed to confirm.
    /// </summary>
    public bool UpdateMainMenu()
    {
        int? teamChoice = _input.GetTeamSelection();
        if (teamChoice.HasValue)
        {
            _selectedTeamIndex = teamChoice.Value;
        }

        return _input.IsEnterPressed();
    }

    /// <summary>
    /// Checks if the player has confirmed to proceed (Enter key).
    /// Used for DriveOver and GameOver screens.
    /// </summary>
    public bool IsConfirmPressed() => _input.IsEnterPressed();
}
