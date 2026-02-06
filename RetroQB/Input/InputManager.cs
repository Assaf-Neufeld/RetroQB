using System.Numerics;
using Raylib_cs;

namespace RetroQB.Input;

/// <summary>
/// Centralizes all input handling. All key checks should go through this class.
/// </summary>
public sealed class InputManager
{
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
    public bool IsSpacePressed() => Raylib.IsKeyPressed(KeyboardKey.Space);

    /// <summary>
    /// Returns team selection index (0-2) from number keys 1-3, or null if no team key pressed.
    /// </summary>
    public int? GetTeamSelection()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.One)) return 0;
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) return 1;
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) return 2;
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
}
