using System.Text;
using RetroQB.Input;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Handles main-menu team selection and post-game return-to-menu logic.
/// </summary>
public sealed class MenuController
{
    private const string SecretTeamPassword = "1473";
    private const int SecretTeamPasswordLength = 4;

    private readonly InputManager _input;
    private int _selectedTeamIndex;
    private bool _showLeaderboard;
    private bool _showSecretTeamPrompt;
    private string _secretPasswordInput = string.Empty;
    private string _secretPasswordMessage = string.Empty;

    public int SelectedTeamIndex => _selectedTeamIndex;
    public bool ShowLeaderboard => _showLeaderboard;
    public bool ShowSecretTeamPrompt => _showSecretTeamPrompt;
    public string SecretPasswordInput => _secretPasswordInput;
    public string SecretPasswordMessage => _secretPasswordMessage;

    public MenuController(InputManager input)
    {
        _input = input;
    }

    /// <summary>
    /// Processes team selection input. Returns true when Enter is pressed to confirm.
    /// </summary>
    public bool UpdateMainMenu(int baseTeamCount)
    {
        if (_showSecretTeamPrompt)
        {
            UpdateSecretTeamPrompt(baseTeamCount);
            return false;
        }

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

        if (_input.IsZeroPressed())
        {
            OpenSecretTeamPrompt();
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

    public void OpenLeaderboard()
    {
        _showLeaderboard = true;
    }

    public void ResetSecretTeamSelection(int baseTeamCount)
    {
        if (_selectedTeamIndex >= baseTeamCount)
        {
            _selectedTeamIndex = 0;
        }

        CloseSecretTeamPrompt();
    }

    private void OpenSecretTeamPrompt()
    {
        _showSecretTeamPrompt = true;
        _showLeaderboard = false;
        _secretPasswordInput = string.Empty;
        _secretPasswordMessage = string.Empty;
    }

    private void CloseSecretTeamPrompt()
    {
        _showSecretTeamPrompt = false;
        _secretPasswordInput = string.Empty;
        _secretPasswordMessage = string.Empty;
    }

    private void UpdateSecretTeamPrompt(int baseTeamCount)
    {
        string typedText = _input.ReadTextInput(SecretTeamPasswordLength);
        if (!string.IsNullOrEmpty(typedText) && _secretPasswordInput.Length < SecretTeamPasswordLength)
        {
            var acceptedDigits = new StringBuilder(SecretTeamPasswordLength);
            foreach (char ch in typedText)
            {
                if (!char.IsDigit(ch))
                {
                    continue;
                }

                acceptedDigits.Append(ch);
                if (_secretPasswordInput.Length + acceptedDigits.Length >= SecretTeamPasswordLength)
                {
                    break;
                }
            }

            if (acceptedDigits.Length > 0)
            {
                int available = SecretTeamPasswordLength - _secretPasswordInput.Length;
                string acceptedText = acceptedDigits.ToString();
                if (acceptedText.Length > available)
                {
                    acceptedText = acceptedText[..available];
                }

                _secretPasswordInput += acceptedText;
            }
        }

        if (_input.IsBackspacePressed() && _secretPasswordInput.Length > 0)
        {
            _secretPasswordInput = _secretPasswordInput[..^1];
        }

        if (_input.IsEscapePressed())
        {
            CloseSecretTeamPrompt();
            return;
        }

        if (!_input.IsEnterPressed())
        {
            return;
        }

        if (_secretPasswordInput == SecretTeamPassword)
        {
            _selectedTeamIndex = baseTeamCount;
            CloseSecretTeamPrompt();
            return;
        }

        _secretPasswordInput = string.Empty;
        _secretPasswordMessage = "Wrong password.";
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
