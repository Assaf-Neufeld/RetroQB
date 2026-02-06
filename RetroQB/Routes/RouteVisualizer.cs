using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Routes;

/// <summary>
/// Provides route visualization data including waypoints and labels.
/// </summary>
public static class RouteVisualizer
{
    public static IReadOnlyList<Vector2> GetRouteWaypoints(Receiver receiver)
    {
        Vector2 start = receiver.RouteStart;
        int side = receiver.RouteSide == 0 ? 1 : receiver.RouteSide;
        var distances = GetRouteDistances(receiver);

        return receiver.Route switch
        {
            RouteType.Go => GetGoWaypoints(start, distances),
            RouteType.Slant => GetSlantWaypoints(start, side, distances, receiver.SlantInside),
            RouteType.OutShallow => GetOutWaypoints(start, side, distances.Shallow),
            RouteType.OutDeep => GetOutWaypoints(start, side, distances.Deep),
            RouteType.InShallow => GetInWaypoints(start, side, distances.Shallow),
            RouteType.InDeep => GetInWaypoints(start, side, distances.Deep),
            RouteType.PostShallow => GetPostWaypoints(start, side, distances.Shallow, distances.PostXFactorShallow, distances.PostAngleShallow),
            RouteType.PostDeep => GetPostWaypoints(start, side, distances.Deep, distances.PostXFactorDeep, distances.PostAngleDeep),
            RouteType.Curl => GetCurlWaypoints(start, side, distances),
            RouteType.Flat => GetFlatWaypoints(start, side, distances),
            _ => new[] { start, start + new Vector2(0, distances.Deep) }
        };
    }

    public static string GetRouteLabel(RouteType route)
    {
        return route switch
        {
            RouteType.Go => "Go",
            RouteType.Slant => "Slant",
            RouteType.OutShallow => "Out S",
            RouteType.OutDeep => "Out D",
            RouteType.InShallow => "In S",
            RouteType.InDeep => "In D",
            RouteType.PostShallow => "Post S",
            RouteType.PostDeep => "Post D",
            RouteType.Curl => "Curl",
            RouteType.Flat => "Flat",
            _ => "Route"
        };
    }

    private static RouteDistances GetRouteDistances(Receiver receiver)
    {
        var stems = RouteGeometry.GetStemDistances(receiver);
        var curl = RouteGeometry.GetCurlValues(receiver);
        return new RouteDistances
        {
            Stem = stems.Deep,
            Deep = stems.Deep,
            Shallow = stems.Shallow,
            FlatWidth = receiver.IsRunningBack ? 9f : receiver.IsTightEnd ? 6f : 7f,
            PostXFactorShallow = RouteGeometry.PostXFactorShallow,
            PostXFactorDeep = RouteGeometry.PostXFactorDeep,
            PostAngleShallow = stems.PostAngleShallow,
            PostAngleDeep = stems.PostAngleDeep,
            CurlStem = curl.Stem,
            CurlReturn = curl.Return
        };
    }

    private static Vector2[] GetGoWaypoints(Vector2 start, RouteDistances distances)
    {
        return new[] { start, start + new Vector2(0, distances.Deep + 8f) };
    }

    private static Vector2[] GetSlantWaypoints(Vector2 start, int side, RouteDistances distances, bool slantInside)
    {
        Vector2 dir = RouteGeometry.GetSlantDirection(side, slantInside);
        return new[] { start, start + dir * distances.Deep };
    }

    private static Vector2[] GetOutWaypoints(Vector2 start, int side, float stem)
    {
        Vector2 stemPoint = start + new Vector2(0, stem);
        Vector2 breakDir = RouteGeometry.GetOutBreakDirection(side);
        return new[] { start, stemPoint, stemPoint + breakDir * RouteGeometry.OutBreakLength };
    }

    private static Vector2[] GetInWaypoints(Vector2 start, int side, float stem)
    {
        Vector2 stemPoint = start + new Vector2(0, stem);
        return new[] { start, stemPoint, stemPoint + new Vector2(RouteGeometry.InBreakLength * -side, 0) };
    }

    private static Vector2[] GetPostWaypoints(Vector2 start, int side, float stem, float postXFactor, float postAngle)
    {
        Vector2 stemPoint = start + new Vector2(0, stem);
        Vector2 breakDir = RouteGeometry.GetPostBreakDirection(side, postXFactor, postAngle);
        return new[] { start, stemPoint, stemPoint + breakDir * RouteGeometry.PostBreakLength };
    }

    private static Vector2[] GetCurlWaypoints(Vector2 start, int side, RouteDistances distances)
    {
        Vector2 curlStemPoint = start + new Vector2(0, distances.CurlStem);
        Vector2 comebackDir = RouteGeometry.GetCurlComebackDirection(side);
        return new[] { start, curlStemPoint, curlStemPoint + comebackDir * distances.CurlReturn };
    }

    private static Vector2[] GetFlatWaypoints(Vector2 start, int side, RouteDistances distances)
    {
        return new[] { start, start + new Vector2(distances.FlatWidth * side, 2f) };
    }

    private struct RouteDistances
    {
        public float Stem;
        public float Deep;
        public float Shallow;
        public float FlatWidth;
        public float PostXFactorShallow;
        public float PostXFactorDeep;
        public float PostAngleShallow;
        public float PostAngleDeep;
        public float CurlStem;
        public float CurlReturn;
    }
}
