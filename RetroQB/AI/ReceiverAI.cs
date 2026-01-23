using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.AI;

public enum RouteType
{
    Go,
    Slant,
    OutShallow,
    OutDeep,
    InShallow,
    InDeep,
    PostShallow,
    PostDeep,
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
                else if (play.Family == PlayType.QbRunFocus)
                {
                    receiver.RouteSide = 0;
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

            if (receiver.IsTightEnd && play.TightEndRole == TightEndRole.Block)
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

            if (receiver.IsRunningBack && play.Family != PlayType.QbRunFocus && play.RunningBackRole == RunningBackRole.Route)
            {
                route = route switch
                {
                    RouteType.Curl => RouteType.Flat,
                    RouteType.Go => RouteType.Flat,
                    RouteType.PostDeep => RouteType.Flat,
                    RouteType.PostShallow => RouteType.Flat,
                    RouteType.InDeep => RouteType.OutShallow,
                    RouteType.OutDeep => RouteType.OutShallow,
                    _ => route
                };
            }

            receiver.Route = route;
            receiver.SlantInside = true;
            if (receiver.Route == RouteType.Slant && play.TryGetSlantDirection(receiver.Index, out var slantInside))
            {
                receiver.SlantInside = slantInside;
            }
        }
    }

    private static RouteType PickRoute(PlayType playType, Random rng)
    {
        int roll = rng.Next(100);
        return playType switch
        {
            PlayType.QuickPass => roll < 30 ? RouteType.Slant : roll < 55 ? RouteType.OutShallow : roll < 80 ? RouteType.InShallow : RouteType.Curl,
            PlayType.LongPass => roll < 35 ? RouteType.Go : roll < 65 ? RouteType.PostDeep : roll < 85 ? RouteType.InDeep : RouteType.OutDeep,
            PlayType.QbRunFocus => roll < 50 ? RouteType.OutShallow : RouteType.Flat,
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
            if (receiver.IsRunningBack)
            {
                Vector2 runDir = new Vector2(receiver.RouteSide * 0.55f, 1f);
                if (runDir.LengthSquared() > 0.001f)
                {
                    runDir = Vector2.Normalize(runDir);
                }
                receiver.Velocity = runDir * receiver.Speed;
            }
            else
            {
                receiver.Velocity = new Vector2(0, receiver.Speed);
            }
            return;
        }

        float speed = receiver.Speed;
        Vector2 dir = Vector2.Zero;
        float progress = receiver.RouteProgress;
        float stemShallow = receiver.IsRunningBack ? 3.8f : receiver.IsTightEnd ? 5.5f : 6.5f;
        float stemDeep = receiver.IsRunningBack ? 6.2f : receiver.IsTightEnd ? 8.2f : 10.5f;
        float postAngleShallow = 4.8f;
        float postAngleDeep = 7.2f;

        switch (receiver.Route)
        {
            case RouteType.Go:
                dir = new Vector2(0, 1);
                break;
            case RouteType.Slant:
                float slantSide = receiver.SlantInside ? -receiver.RouteSide : receiver.RouteSide;
                dir = Vector2.Normalize(new Vector2(0.7f * slantSide, 1));
                break;
            case RouteType.OutShallow:
                dir = progress < stemShallow ? new Vector2(0, 1) : new Vector2(receiver.RouteSide, 0);
                break;
            case RouteType.OutDeep:
                dir = progress < stemDeep ? new Vector2(0, 1) : new Vector2(receiver.RouteSide, 0);
                break;
            case RouteType.InShallow:
                dir = progress < stemShallow ? new Vector2(0, 1) : new Vector2(-receiver.RouteSide, 0);
                break;
            case RouteType.InDeep:
                dir = progress < stemDeep ? new Vector2(0, 1) : new Vector2(-receiver.RouteSide, 0);
                break;
            case RouteType.PostShallow:
                dir = progress < stemShallow ? new Vector2(0, 1) : Vector2.Normalize(new Vector2(-0.6f * receiver.RouteSide, postAngleShallow));
                break;
            case RouteType.PostDeep:
                dir = progress < stemDeep ? new Vector2(0, 1) : Vector2.Normalize(new Vector2(-0.6f * receiver.RouteSide, postAngleDeep));
                break;
            case RouteType.Curl:
                float curlStem = receiver.IsRunningBack ? 4f : receiver.IsTightEnd ? 6f : 7f;
                float curlReturn = receiver.IsRunningBack ? 1.5f : receiver.IsTightEnd ? 1.8f : 2f;
                if (progress < curlStem)
                {
                    dir = new Vector2(0, 1);
                }
                else if (progress < curlStem + curlReturn)
                {
                    dir = new Vector2(0, -1);
                }
                else
                {
                    dir = Vector2.Zero;
                }
                break;
            case RouteType.Flat:
                dir = Vector2.Normalize(new Vector2(receiver.RouteSide, 0.25f));
                break;
        }

        if (!receiver.IsRunningBack && !receiver.IsTightEnd && dir == Vector2.Zero)
        {
            Vector2 scramble = new Vector2(receiver.RouteSide * 0.35f, 0.65f);
            if (scramble.LengthSquared() > 0.001f)
            {
                scramble = Vector2.Normalize(scramble);
            }
            dir = scramble;
        }

        receiver.Velocity = dir * speed;
        if (dir != Vector2.Zero)
        {
            receiver.RouteProgress += speed * dt;
        }
    }

    public static IReadOnlyList<Vector2> GetRouteWaypoints(Receiver receiver)
    {
        Vector2 start = receiver.RouteStart;
        int side = receiver.RouteSide == 0 ? 1 : receiver.RouteSide;

        float stem = receiver.IsRunningBack ? 4f : receiver.IsTightEnd ? 7.5f : 9f;
        float deep = receiver.IsRunningBack ? 6.5f : receiver.IsTightEnd ? 11f : 14f;
        float shallow = receiver.IsRunningBack ? 5f : receiver.IsTightEnd ? 7.5f : 9f;
        float flatWidth = receiver.IsRunningBack ? 9f : receiver.IsTightEnd ? 6f : 7f;
        float postAngleShallow = 4.8f;
        float postAngleDeep = 7.2f;
        float curlStem = receiver.IsRunningBack ? 4f : receiver.IsTightEnd ? 6f : 7f;
        float curlReturn = receiver.IsRunningBack ? 1.5f : receiver.IsTightEnd ? 1.8f : 2f;

        Vector2 stemPoint = start + new Vector2(0, stem);
        Vector2 deepPoint = start + new Vector2(0, deep);
        Vector2 shallowPoint = start + new Vector2(0, shallow);
        Vector2 curlStemPoint = start + new Vector2(0, curlStem);

        return receiver.Route switch
        {
            RouteType.Go => new[] { start, start + new Vector2(0, deep + 8f) },
            RouteType.Slant => new[] { start, start + new Vector2(3.5f * (receiver.SlantInside ? -side : side), deep) },
            RouteType.OutShallow => new[] { start, shallowPoint, shallowPoint + new Vector2(6f * side, 0) },
            RouteType.OutDeep => new[] { start, deepPoint, deepPoint + new Vector2(6f * side, 0) },
            RouteType.InShallow => new[] { start, shallowPoint, shallowPoint + new Vector2(6f * -side, 0) },
            RouteType.InDeep => new[] { start, deepPoint, deepPoint + new Vector2(6f * -side, 0) },
            RouteType.PostShallow => new[] { start, shallowPoint, shallowPoint + new Vector2(postAngleShallow * -side, postAngleShallow) },
            RouteType.PostDeep => new[] { start, deepPoint, deepPoint + new Vector2(postAngleDeep * -side, postAngleDeep) },
            RouteType.Curl => new[] { start, curlStemPoint, curlStemPoint + new Vector2(0, -curlReturn) },
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
}
