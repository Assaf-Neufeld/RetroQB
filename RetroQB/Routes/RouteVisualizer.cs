using System.Numerics;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.Routes;

public static class RouteVisualizer
{
    public static IReadOnlyList<Vector2> GetRouteWaypoints(Receiver receiver) => RouteGeometry.GetPath(receiver).Points;

    /// <summary>Pre-snap assignment, including the exchange before a run or play-action route.</summary>
    public static IReadOnlyList<Vector2> GetRouteWaypoints(Receiver receiver, ResolvedPlay play, float lineOfScrimmage)
    {
        var plan = play.Backfield;
        if (plan.Action == BackfieldAction.None || plan.Participant != receiver.Slot)
            return GetRouteWaypoints(receiver);

        Vector2 start = play.Players.Single(p => p.Slot == receiver.Slot).Position.AtLineOfScrimmage(lineOfScrimmage);
        Vector2 mesh = plan.GetMeshPoint(play.Quarterback.AtLineOfScrimmage(lineOfScrimmage));
        if (plan.Action == BackfieldAction.Handoff)
        {
            // Show the called lane after the exchange; possession gives the player control.
            Vector2 lane = RoutePath.Clamp(mesh + RouteRunner.GetBallCarrierDirection(receiver, play) * 14);
            return new[] { start, mesh, lane };
        }

        var release = new RoutePath(RouteGeometry.GetPath(receiver).Definition, mesh, receiver.RouteSide);
        return release.Points.Prepend(start).ToArray();
    }

    public static string GetRouteLabel(RouteType route) => route switch
    {
        RouteType.Go => "Go", RouteType.Slant => "Slant",
        RouteType.OutShallow => "Out S", RouteType.OutDeep => "Out D",
        RouteType.InShallow => "In S", RouteType.InDeep => "In D",
        RouteType.PostShallow => "Post S", RouteType.PostDeep => "Post D",
        RouteType.DoubleMove => "Dbl Move", RouteType.Flat => "Flat", _ => route.ToString()
    };
}
