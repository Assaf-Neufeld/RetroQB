using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.AI;

public enum RouteType
{
    Go,
    Slant,
    Out,
    Post,
    Curl,
    Flat
}

public static class ReceiverAI
{
    public static void AssignRoutes(IReadOnlyList<Receiver> receivers, PlayDefinition play, Random rng)
    {
        foreach (var receiver in receivers)
        {
            receiver.RouteStart = receiver.Position;
            receiver.RouteProgress = 0f;
            receiver.HasBall = false;
            receiver.Eligible = true;
            receiver.IsBlocking = false;

            receiver.RouteSide = receiver.Position.X < Constants.FieldWidth * 0.5f ? -1 : 1;
            if (receiver.IsRunningBack)
            {
                if (play.RunningBackSide != 0)
                {
                    receiver.RouteSide = Math.Sign(play.RunningBackSide);
                }
                else
                {
                    receiver.RouteSide = rng.Next(0, 2) == 0 ? -1 : 1;
                }
            }

            if (receiver.IsRunningBack && play.RunningBackRole == RunningBackRole.Block)
            {
                receiver.Eligible = false;
                receiver.IsBlocking = true;
                receiver.Route = RouteType.Flat;
                continue;
            }

            if (!play.TryGetRoute(receiver.Index, out var route))
            {
                route = PickRoute(play.Family, rng);
            }

            receiver.Route = route;
        }
    }

    private static RouteType PickRoute(PlayType playType, Random rng)
    {
        int roll = rng.Next(100);
        return playType switch
        {
            PlayType.QuickPass => roll < 35 ? RouteType.Slant : roll < 70 ? RouteType.Out : RouteType.Curl,
            PlayType.LongPass => roll < 45 ? RouteType.Go : RouteType.Post,
            PlayType.QbRunFocus => roll < 50 ? RouteType.Out : RouteType.Flat,
            _ => RouteType.Go
        };
    }

    public static void UpdateRoute(Receiver receiver, float dt)
    {
        if (receiver.IsBlocking)
        {
            receiver.Velocity = Vector2.Zero;
            return;
        }

        if (receiver.HasBall)
        {
            receiver.Velocity = new Vector2(0, Constants.ReceiverSpeed);
            return;
        }

        float speed = Constants.ReceiverSpeed;
        Vector2 dir = Vector2.Zero;
        float progress = receiver.RouteProgress;

        switch (receiver.Route)
        {
            case RouteType.Go:
                dir = new Vector2(0, 1);
                break;
            case RouteType.Slant:
                dir = Vector2.Normalize(new Vector2(0.7f * receiver.RouteSide, 1));
                break;
            case RouteType.Out:
                dir = progress < 8f ? new Vector2(0, 1) : new Vector2(receiver.RouteSide, 0);
                break;
            case RouteType.Post:
                dir = progress < 10f ? new Vector2(0, 1) : Vector2.Normalize(new Vector2(-0.6f * receiver.RouteSide, 1));
                break;
            case RouteType.Curl:
                dir = progress < 7f ? new Vector2(0, 1) : new Vector2(0, -1);
                break;
            case RouteType.Flat:
                dir = Vector2.Normalize(new Vector2(receiver.RouteSide, 0.25f));
                break;
        }

        receiver.Velocity = dir * speed;
        receiver.RouteProgress += speed * dt;
    }

    public static IReadOnlyList<Vector2> GetRouteWaypoints(Receiver receiver)
    {
        Vector2 start = receiver.RouteStart;
        int side = receiver.RouteSide == 0 ? 1 : receiver.RouteSide;

        float stem = receiver.IsRunningBack ? 4f : 9f;
        float deep = receiver.IsRunningBack ? 6.5f : 14f;
        float flatWidth = receiver.IsRunningBack ? 9f : 7f;
        float postAngle = 7f;

        Vector2 stemPoint = start + new Vector2(0, stem);
        Vector2 deepPoint = start + new Vector2(0, deep);

        return receiver.Route switch
        {
            RouteType.Go => new[] { start, start + new Vector2(0, deep + 8f) },
            RouteType.Slant => new[] { start, start + new Vector2(3.5f * side, deep) },
            RouteType.Out => new[] { start, deepPoint, deepPoint + new Vector2(6f * side, 0) },
            RouteType.Post => new[] { start, deepPoint, deepPoint + new Vector2(postAngle * -side, postAngle) },
            RouteType.Curl => new[] { start, stemPoint, stemPoint + new Vector2(0, -3.5f) },
            RouteType.Flat => new[] { start, start + new Vector2(flatWidth * side, 2f) },
            _ => new[] { start, deepPoint }
        };
    }

    public static string GetRouteLabel(RouteType route)
    {
        return route switch
        {
            RouteType.Go => "Go",
            RouteType.Slant => "Slant",
            RouteType.Out => "Out",
            RouteType.Post => "Post",
            RouteType.Curl => "Curl",
            RouteType.Flat => "Flat",
            _ => "Route"
        };
    }
}
