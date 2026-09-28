using System.Numerics;
using Raylib_cs;

namespace RetroQB.Input;

public interface IPlayerMovementInput
{
    Vector2 GetMovementDirection();
    bool IsSprintHeld();
}

/// <summary>
/// Centralizes all input handling. All key checks should go through this class.
/// </summary>
public sealed class InputManager : IGameInput
{
    public const int MaxPlayerNameLength = 18;
    private readonly Func<KeyboardKey, bool> _pressed;
    public InputManager() : this(key => Raylib.IsKeyPressed(key)) { }
    internal InputManager(Func<KeyboardKey, bool> pressed) => _pressed = pressed;
    public bool IsFocused() => Raylib.IsWindowFocused();

    public Vector2 GetMovementDirection()
    {
        float x = 0;
        float y = 0;

        if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) x -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) x += 1;
        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) y += 1;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) y -= 1;

        Vector2 dir = new(x, y);
        if (dir.LengthSquared() > 1f)
        {
            dir = Vector2.Normalize(dir);
        }
        return dir;
    }

    public bool IsSprintHeld()
    {
        return Raylib.IsKeyDown(KeyboardKey.LeftShift);
    }

    public bool IsEscapePressed() => _pressed(KeyboardKey.Escape);
    public bool IsEnterPressed() => _pressed(KeyboardKey.Enter);
    public bool IsFlipPlayPressed() => _pressed(KeyboardKey.X);
    public bool IsSpacePressed() => _pressed(KeyboardKey.Space);
    public bool IsFieldGoalPressed() => _pressed(KeyboardKey.K);
    public bool IsBackspacePressed() => _pressed(KeyboardKey.Backspace);
    public bool IsReplayPressed() => _pressed(KeyboardKey.F);
    public bool IsTimeoutPressed() => _pressed(KeyboardKey.C);
    public bool IsPausePressed() => _pressed(KeyboardKey.P);
    public bool IsPuntPressed() => _pressed(KeyboardKey.B);
    public bool IsKneelPressed() => _pressed(KeyboardKey.V);
    public bool IsStatisticsPressed() => _pressed(KeyboardKey.Tab);
    public int? GetDefensivePlaySelection() => GetPassPlaySelection();
    public int GetDefensiveVariantStep() => _pressed(KeyboardKey.Q) ? -1 : _pressed(KeyboardKey.E) ? 1 : 0;
    public bool IsReplaySkipPressed() => _pressed(KeyboardKey.Space);
    public bool IsRestartPressed() => _pressed(KeyboardKey.Z);
    public bool IsLeaderboardPressed() => _pressed(KeyboardKey.L);
    public bool IsSecretTeamPressed() => _pressed(KeyboardKey.G);
    public int GetTeamNavigation()
    {
        if (_pressed(KeyboardKey.Up)) return -1;
        if (_pressed(KeyboardKey.Down)) return 1;
        return 0;
    }
    public bool IsDriveSummaryScrollOlderPressed() => _pressed(KeyboardKey.PageUp);
    public bool IsDriveSummaryScrollNewerPressed() => _pressed(KeyboardKey.PageDown);
    public float GetMouseWheelMove() => Raylib.GetMouseWheelMove();

    public string ReadTextInput(int maxLength)
    {
        var buffer = new System.Text.StringBuilder();
        int codepoint;

        while ((codepoint = Raylib.GetCharPressed()) > 0)
        {
            if (buffer.Length >= maxLength)
            {
                continue;
            }

            char ch = (char)codepoint;
            if (IsSupportedTextCharacter(ch))
            {
                buffer.Append(ch);
            }
        }

        return buffer.ToString();
    }

    public int? GetNameConflictChoice()
    {
        if (_pressed(KeyboardKey.One)) return 1;
        if (_pressed(KeyboardKey.Two)) return 2;
        return null;
    }

    /// <summary>
    /// Returns team selection index (0-9) from keys 1-9 and 0, or null if none pressed.
    /// </summary>
    public int? GetTeamSelection()
    {
        if (_pressed(KeyboardKey.One)) return 0;
        if (_pressed(KeyboardKey.Two)) return 1;
        if (_pressed(KeyboardKey.Three)) return 2;
        if (_pressed(KeyboardKey.Four)) return 3;
        if (_pressed(KeyboardKey.Five)) return 4;
        if (_pressed(KeyboardKey.Six)) return 5;
        if (_pressed(KeyboardKey.Seven)) return 6;
        if (_pressed(KeyboardKey.Eight)) return 7;
        if (_pressed(KeyboardKey.Nine)) return 8;
        if (_pressed(KeyboardKey.Zero)) return 9;
        return null;
    }

    /// <summary>
    /// Returns pass play index (0-9) from number keys.
    /// Key 1 maps to index 0 (wildcard), keys 2-9 map to indices 1-8, key 0 maps to index 9.
    /// </summary>
    public int? GetPassPlaySelection()
    {
        if (_pressed(KeyboardKey.One)) return 0;
        if (_pressed(KeyboardKey.Two)) return 1;
        if (_pressed(KeyboardKey.Three)) return 2;
        if (_pressed(KeyboardKey.Four)) return 3;
        if (_pressed(KeyboardKey.Five)) return 4;
        if (_pressed(KeyboardKey.Six)) return 5;
        if (_pressed(KeyboardKey.Seven)) return 6;
        if (_pressed(KeyboardKey.Eight)) return 7;
        if (_pressed(KeyboardKey.Nine)) return 8;
        if (_pressed(KeyboardKey.Zero)) return 9;
        return null;
    }

    /// <summary>
    /// Returns run play index (0-9) from letter keys.
    /// Q=0 (wildcard), W=1, E=2, R=3, T=4, Y=5, U=6, I=7, O=8, P=9
    /// </summary>
    public int? GetRunPlaySelection()
    {
        if (_pressed(KeyboardKey.Q)) return 0;
        if (_pressed(KeyboardKey.W)) return 1;
        if (_pressed(KeyboardKey.E)) return 2;
        if (_pressed(KeyboardKey.R)) return 3;
        if (_pressed(KeyboardKey.T)) return 4;
        if (_pressed(KeyboardKey.Y)) return 5;
        if (_pressed(KeyboardKey.U)) return 6;
        if (_pressed(KeyboardKey.I)) return 7;
        if (_pressed(KeyboardKey.O)) return 8;
        if (_pressed(KeyboardKey.P)) return 9;
        return null;
    }

    /// <summary>
    /// Returns throw target index (0-4) from number keys 1-5, or null if no throw key pressed.
    /// </summary>
    public int? GetThrowTarget()
    {
        if (_pressed(KeyboardKey.One)) return 0;
        if (_pressed(KeyboardKey.Two)) return 1;
        if (_pressed(KeyboardKey.Three)) return 2;
        if (_pressed(KeyboardKey.Four)) return 3;
        if (_pressed(KeyboardKey.Five)) return 4;
        return null;
    }

    private static bool IsSupportedTextCharacter(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_' or '\'' or '.';
    }
}
