using System.Numerics;
using Raylib_cs;

namespace RetroQB.Input;

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

    public bool IsAimHeld()
    {
        return Raylib.IsKeyDown(KeyboardKey.Space);
    }

    public bool IsAimReleased()
    {
        return Raylib.IsKeyReleased(KeyboardKey.Space);
    }

    public bool IsAimPressed()
    {
        return Raylib.IsKeyPressed(KeyboardKey.Space);
    }
}
