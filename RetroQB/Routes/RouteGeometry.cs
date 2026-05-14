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

    public static bool HasCompletedBreak(Receiver receiver)
    {
        float yProgress = receiver.Position.Y - receiver.RouteStart.Y;
        var stems = GetStemDistances(receiver);

        return receiver.Route switch
        {
            RouteType.Go => yProgress >= stems.Deep,
            RouteType.Slant => Vector2.Distance(receiver.Position, receiver.RouteStart) >= SlantLength * 0.9f,
            RouteType.OutShallow => HasCompletedHorizontalBreak(receiver, stems.Shallow, receiver.RouteSide, OutBreakLength),
            RouteType.OutDeep => HasCompletedHorizontalBreak(receiver, stems.Deep, receiver.RouteSide, OutBreakLength),
            RouteType.InShallow => HasCompletedHorizontalBreak(receiver, stems.Shallow, -receiver.RouteSide, InBreakLength),
            RouteType.InDeep => HasCompletedHorizontalBreak(receiver, stems.Deep, -receiver.RouteSide, InBreakLength),
            RouteType.DoubleMove => HasCompletedHorizontalBreak(receiver, stems.Deep, -receiver.RouteSide, InBreakLength),
            RouteType.PostShallow => yProgress >= stems.Shallow + PostBreakLength * 0.75f,
            RouteType.PostDeep => yProgress >= stems.Deep + PostBreakLength * 0.75f,
            RouteType.Flat => yProgress >= 2.5f,
            _ => false
        };
    }

    private static bool HasCompletedHorizontalBreak(Receiver receiver, float stemDistance, int breakSide, float breakLength)
    {
        if (breakSide == 0 || receiver.Position.Y - receiver.RouteStart.Y < stemDistance)
        {
            return false;
        }

        const float tolerance = 1.0f;
        float breakProgress = (receiver.Position.X - receiver.RouteStart.X) * breakSide;
        return breakProgress >= breakLength - tolerance;
    }

    public static Vector2 GetOutBreakDirection(int routeSide)
    {
        return Vector2.Normalize(new Vector2(routeSide, OutBreakY));
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
