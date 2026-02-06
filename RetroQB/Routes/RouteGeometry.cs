using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Routes;

/// <summary>
/// Shared route geometry values used by both movement and visualization.
/// </summary>
public static class RouteGeometry
{
    public const float OutBreakLength = 6f;
    public const float PostBreakLength = 6f;
    public const float InBreakLength = 6f;
    public const float SlantLength = 9f;
    public const float OutBreakY = 0.18f; // 80° break (slight upfield drift)
    public const float PostXFactorShallow = 0.6f;
    public const float PostXFactorDeep = 0.9f;

    public static (float Shallow, float Deep, float PostAngleShallow, float PostAngleDeep) GetStemDistances(Receiver receiver)
    {
        float stemShallow = receiver.IsRunningBack ? 5f : receiver.IsTightEnd ? 7f : 9f;
        float stemDeep = receiver.IsRunningBack ? 12f : receiver.IsTightEnd ? 16f : 20f;
        return (stemShallow, stemDeep, 1.2f, 1.0f);
    }

    public static (float Stem, float Lateral, float Deep) GetDoubleMoveValues(Receiver receiver)
    {
        float stem = receiver.IsRunningBack ? 3f : receiver.IsTightEnd ? 4f : 5f;
        float lateral = receiver.IsRunningBack ? 2f : receiver.IsTightEnd ? 2.5f : 3f;
        float deep = receiver.IsRunningBack ? 8f : receiver.IsTightEnd ? 12f : 16f;
        return (stem, lateral, deep);
    }

    public static Vector2 GetOutBreakDirection(int routeSide)
    {
        return Vector2.Normalize(new Vector2(routeSide, OutBreakY));
    }

    public static Vector2 GetDoubleMoveLateralDirection(int routeSide, bool cutInside)
    {
        int cutDir = cutInside ? -routeSide : routeSide;
        return new Vector2(cutDir, 0);
    }

    public static Vector2 GetPostBreakDirection(int routeSide, float xFactor, float postAngle)
    {
        return Vector2.Normalize(new Vector2(-xFactor * routeSide, postAngle));
    }

    public static Vector2 GetSlantDirection(int routeSide, bool slantInside)
    {
        float slantSide = slantInside ? -routeSide : routeSide;
        return Vector2.Normalize(new Vector2(0.7f * slantSide, 1f));
    }
}
