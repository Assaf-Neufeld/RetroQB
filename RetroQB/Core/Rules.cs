using System.Numerics;

namespace RetroQB.Core;

public static class Rules
{
    public const int WinningScore = 24;

    public static bool IsTouchdown(Vector2 ballPos)
    {
        return ballPos.Y >= Constants.EndZoneDepth + 100f;
    }

    public static bool IsInBounds(Vector2 pos)
    {
        return pos.X >= 0 && pos.X <= Constants.FieldWidth && pos.Y >= 0 && pos.Y <= Constants.FieldLength;
    }
}
