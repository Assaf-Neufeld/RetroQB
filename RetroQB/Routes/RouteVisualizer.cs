using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Routes;

public static class RouteVisualizer
{
    public static IReadOnlyList<Vector2> GetRouteWaypoints(Receiver receiver) => RouteGeometry.GetPath(receiver).Points;

    public static string GetRouteLabel(RouteType route) => route switch
    {
        RouteType.Go => "Go", RouteType.Slant => "Slant",
        RouteType.OutShallow => "Out S", RouteType.OutDeep => "Out D",
        RouteType.InShallow => "In S", RouteType.InDeep => "In D",
        RouteType.PostShallow => "Post S", RouteType.PostDeep => "Post D",
        RouteType.DoubleMove => "Dbl Move", RouteType.Flat => "Flat", _ => route.ToString()
    };
}
