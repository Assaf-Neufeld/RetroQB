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

    public bool IsEscapePressed() => Raylib.IsKeyPressed(KeyboardKey.Escape);
    public bool IsEnterPressed() => Raylib.IsKeyPressed(KeyboardKey.Enter);
    public bool IsFlipPlayPressed() => Raylib.IsKeyPressed(KeyboardKey.X);
    public bool IsSpacePressed() => Raylib.IsKeyPressed(KeyboardKey.Space);
    public bool IsFieldGoalPressed() => Raylib.IsKeyPressed(KeyboardKey.K);
    public bool IsBackspacePressed() => Raylib.IsKeyPressed(KeyboardKey.Backspace);
    public bool IsReplayPressed() => Raylib.IsKeyPressed(KeyboardKey.F);
    public bool IsTimeoutPressed() => Raylib.IsKeyPressed(KeyboardKey.C);
    public bool IsPausePressed() => Raylib.IsKeyPressed(KeyboardKey.P);
    public bool IsPuntPressed() => Raylib.IsKeyPressed(KeyboardKey.B);
    public bool IsKneelPressed() => Raylib.IsKeyPressed(KeyboardKey.V);
    public bool IsStatisticsPressed() => Raylib.IsKeyPressed(KeyboardKey.Tab);
    public int? GetDefensivePlaySelection() => GetPassPlaySelection();
    public bool IsReplaySkipPressed() => Raylib.IsKeyPressed(KeyboardKey.Space);
    public bool IsRestartPressed() => Raylib.IsKeyPressed(KeyboardKey.Z);
    public bool IsLeaderboardPressed() => Raylib.IsKeyPressed(KeyboardKey.L);
    public bool IsSecretTeamPressed() => Raylib.IsKeyPressed(KeyboardKey.G);
    public int GetTeamNavigation()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Up)) return -1;
        if (Raylib.IsKeyPressed(KeyboardKey.Down)) return 1;
        return 0;
    }
    public bool IsDriveSummaryScrollOlderPressed() => Raylib.IsKeyPressed(KeyboardKey.PageUp);
    public bool IsDriveSummaryScrollNewerPressed() => Raylib.IsKeyPressed(KeyboardKey.PageDown);
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
        if (Raylib.IsKeyPressed(KeyboardKey.One)) return 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) return 2;
        return null;
    }

    /// <summary>
    /// Returns team selection index (0-9) from keys 1-9 and 0, or null if none pressed.
    /// </summary>
    public int? GetTeamSelection()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.One)) return 0;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) return 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) return 2;
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) return 3;
        if (Raylib.IsKeyPressed(KeyboardKey.Five)) return 4;
        if (Raylib.IsKeyPressed(KeyboardKey.Six)) return 5;
        if (Raylib.IsKeyPressed(KeyboardKey.Seven)) return 6;
        if (Raylib.IsKeyPressed(KeyboardKey.Eight)) return 7;
        if (Raylib.IsKeyPressed(KeyboardKey.Nine)) return 8;
        if (Raylib.IsKeyPressed(KeyboardKey.Zero)) return 9;
        return null;
    }

    /// <summary>
    /// Returns pass play index (0-9) from number keys.
    /// Key 1 maps to index 0 (wildcard), keys 2-9 map to indices 1-8, key 0 maps to index 9.
    /// </summary>
    public int? GetPassPlaySelection()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.One)) return 0;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) return 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) return 2;
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) return 3;
        if (Raylib.IsKeyPressed(KeyboardKey.Five)) return 4;
        if (Raylib.IsKeyPressed(KeyboardKey.Six)) return 5;
        if (Raylib.IsKeyPressed(KeyboardKey.Seven)) return 6;
        if (Raylib.IsKeyPressed(KeyboardKey.Eight)) return 7;
        if (Raylib.IsKeyPressed(KeyboardKey.Nine)) return 8;
        if (Raylib.IsKeyPressed(KeyboardKey.Zero)) return 9;
        return null;
    }

    /// <summary>
    /// Returns run play index (0-9) from letter keys.
    /// Q=0 (wildcard), W=1, E=2, R=3, T=4, Y=5, U=6, I=7, O=8, P=9
    /// </summary>
    public int? GetRunPlaySelection()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) return 0;
        if (Raylib.IsKeyPressed(KeyboardKey.W)) return 1;
        if (Raylib.IsKeyPressed(KeyboardKey.E)) return 2;
        if (Raylib.IsKeyPressed(KeyboardKey.R)) return 3;
        if (Raylib.IsKeyPressed(KeyboardKey.T)) return 4;
        if (Raylib.IsKeyPressed(KeyboardKey.Y)) return 5;
        if (Raylib.IsKeyPressed(KeyboardKey.U)) return 6;
        if (Raylib.IsKeyPressed(KeyboardKey.I)) return 7;
        if (Raylib.IsKeyPressed(KeyboardKey.O)) return 8;
        if (Raylib.IsKeyPressed(KeyboardKey.P)) return 9;
        return null;
    }

    /// <summary>
    /// Returns throw target index (0-4) from number keys 1-5, or null if no throw key pressed.
    /// </summary>
    public int? GetThrowTarget()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.One)) return 0;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) return 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) return 2;
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) return 3;
        if (Raylib.IsKeyPressed(KeyboardKey.Five)) return 4;
        return null;
    }

    private static bool IsSupportedTextCharacter(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_' or '\'' or '.';
    }
}
