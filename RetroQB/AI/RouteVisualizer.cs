using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.AI;

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
            RouteType.PostShallow => GetPostWaypoints(start, side, distances.Shallow, distances.PostAngleShallow),
            RouteType.PostDeep => GetPostWaypoints(start, side, distances.Deep, distances.PostAngleDeep),
            RouteType.Curl => GetCurlWaypoints(start, distances),
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
        return new RouteDistances
        {
            Stem = receiver.IsRunningBack ? 4f : receiver.IsTightEnd ? 7.5f : 9f,
            Deep = receiver.IsRunningBack ? 5.5f : receiver.IsTightEnd ? 7.5f : 9f,
            Shallow = receiver.IsRunningBack ? 5f : receiver.IsTightEnd ? 7.5f : 9f,
            FlatWidth = receiver.IsRunningBack ? 9f : receiver.IsTightEnd ? 6f : 7f,
            PostAngleShallow = 4.8f,
            PostAngleDeep = 7.2f,
            CurlStem = receiver.IsRunningBack ? 4f : receiver.IsTightEnd ? 6f : 7f,
            CurlReturn = receiver.IsRunningBack ? 1.5f : receiver.IsTightEnd ? 1.8f : 2f
        };
    }

    private static Vector2[] GetGoWaypoints(Vector2 start, RouteDistances distances)
    {
        return new[] { start, start + new Vector2(0, distances.Deep + 8f) };
    }

    private static Vector2[] GetSlantWaypoints(Vector2 start, int side, RouteDistances distances, bool slantInside)
    {
        float slantDirection = slantInside ? -side : side;
        return new[] { start, start + new Vector2(3.5f * slantDirection, distances.Deep) };
    }

    private static Vector2[] GetOutWaypoints(Vector2 start, int side, float stem)
    {
        Vector2 stemPoint = start + new Vector2(0, stem);
        return new[] { start, stemPoint, stemPoint + new Vector2(6f * side, 0) };
    }

    private static Vector2[] GetInWaypoints(Vector2 start, int side, float stem)
    {
        Vector2 stemPoint = start + new Vector2(0, stem);
        return new[] { start, stemPoint, stemPoint + new Vector2(6f * -side, 0) };
    }

    private static Vector2[] GetPostWaypoints(Vector2 start, int side, float stem, float postAngle)
    {
        Vector2 stemPoint = start + new Vector2(0, stem);
        return new[] { start, stemPoint, stemPoint + new Vector2(postAngle * -side, postAngle) };
    }

    private static Vector2[] GetCurlWaypoints(Vector2 start, RouteDistances distances)
    {
        Vector2 curlStemPoint = start + new Vector2(0, distances.CurlStem);
        return new[] { start, curlStemPoint, curlStemPoint + new Vector2(0, -distances.CurlReturn) };
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
        public float PostAngleShallow;
        public float PostAngleDeep;
        public float CurlStem;
        public float CurlReturn;
    }
}
