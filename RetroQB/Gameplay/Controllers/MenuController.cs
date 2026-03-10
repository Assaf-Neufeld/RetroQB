using RetroQB.Input;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles main-menu team selection and post-game return-to-menu logic.
/// </summary>
public sealed class MenuController
{
    private readonly InputManager _input;
    private int _selectedTeamIndex;
    private bool _showLeaderboard;

    public int SelectedTeamIndex => _selectedTeamIndex;
    public bool ShowLeaderboard => _showLeaderboard;

    public MenuController(InputManager input)
    {
        _input = input;
    }

    /// <summary>
    /// Processes team selection input. Returns true when Enter is pressed to confirm.
    /// </summary>
    public bool UpdateMainMenu()
    {
        if (_input.IsLeaderboardPressed())
        {
            _showLeaderboard = !_showLeaderboard;
            return false;
        }

        if (_showLeaderboard)
        {
            if (_input.IsEscapePressed() || _input.IsBackspacePressed())
            {
                _showLeaderboard = false;
            }

            return false;
        }

        int? teamChoice = _input.GetTeamSelection();
        if (teamChoice.HasValue)
        {
            _selectedTeamIndex = teamChoice.Value;
        }

        return _input.IsEnterPressed();
    }

    public void CloseLeaderboard()
    {
        _showLeaderboard = false;
    }

    /// <summary>
    /// Checks if the player has confirmed to proceed (Enter key).
    /// Used for DriveOver and GameOver screens.
    /// </summary>
    public bool IsConfirmPressed() => _input.IsEnterPressed();

    /// <summary>
    /// Checks if the player wants to restart from the end-of-stage or end-of-season screen.
    /// </summary>
    public bool IsRestartPressed() => _input.IsRestartPressed();
}
