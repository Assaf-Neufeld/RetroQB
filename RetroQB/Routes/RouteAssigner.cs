using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Core;

namespace RetroQB.Routes;

/// <summary>
/// Handles assigning routes to receivers based on play definitions.
/// </summary>
public static class RouteAssigner
{
    public static void AssignRoutes(IReadOnlyList<Receiver> receivers, PlayDefinition play, Random rng)
    {
        ResolveOuterWideReceivers(receivers, out int leftOuterWrIndex, out int rightOuterWrIndex);

        foreach (var receiver in receivers)
        {
            InitializeReceiver(receiver);
            AssignRouteSide(receiver, play, rng);

            if (TryAssignBlockingRole(receiver, play))
            {
                continue;
            }

            AssignRoute(receiver, play, rng, leftOuterWrIndex, rightOuterWrIndex);
        }
    }

    private static void ResolveOuterWideReceivers(IReadOnlyList<Receiver> receivers, out int leftOuterWrIndex, out int rightOuterWrIndex)
    {
        leftOuterWrIndex = -1;
        rightOuterWrIndex = -1;
        float leftX = float.MaxValue;
        float rightX = float.MinValue;

        foreach (var receiver in receivers)
        {
            if (receiver.PositionRole != OffensivePosition.WR)
            {
                continue;
            }

            if (receiver.Position.X < leftX)
            {
                leftX = receiver.Position.X;
                leftOuterWrIndex = receiver.Index;
            }

            if (receiver.Position.X > rightX)
            {
                rightX = receiver.Position.X;
                rightOuterWrIndex = receiver.Index;
            }
        }
    }

    private static void InitializeReceiver(Receiver receiver)
    {
        receiver.RouteStart = receiver.Position;
        receiver.RouteProgress = 0f;
        receiver.HasBall = false;
        receiver.Eligible = true;
        receiver.IsBlocking = false;
    }

    private static void AssignRouteSide(Receiver receiver, PlayDefinition play, Random rng)
    {
        receiver.RouteSide = receiver.Position.X < Constants.FieldWidth * 0.5f ? -1 : 1;

        if (receiver.IsRunningBack)
        {
            if (play.RunningBackSide != 0)
            {
                receiver.RouteSide = Math.Sign(play.RunningBackSide);
            }
            else if (play.Family == PlayType.Run)
            {
                receiver.RouteSide = 0;
            }
            else
            {
                receiver.RouteSide = rng.Next(0, 2) == 0 ? -1 : 1;
            }
        }
    }

    private static bool TryAssignBlockingRole(Receiver receiver, PlayDefinition play)
    {
        if (receiver.IsRunningBack && play.RunningBackRole == RunningBackRole.Block)
        {
            SetAsBlocker(receiver);
            return true;
        }

        if (receiver.IsTightEnd && play.TightEndRole == TightEndRole.Block)
        {
            SetAsBlocker(receiver);
            return true;
        }

        return false;
    }

    private static void SetAsBlocker(Receiver receiver)
    {
        receiver.Eligible = false;
        receiver.IsBlocking = true;
        receiver.Route = RouteType.Flat;
    }

    private static void AssignRoute(Receiver receiver, PlayDefinition play, Random rng, int leftOuterWrIndex, int rightOuterWrIndex)
    {
        if (!play.TryGetRoute(receiver.Index, out var route))
        {
            route = PickRoute(play.Family, rng);
        }

        route = AdjustRouteForRunningBack(receiver, play, route);
        route = AdjustRouteForOuterWideReceiver(receiver, route, leftOuterWrIndex, rightOuterWrIndex);

        receiver.Route = route;
        receiver.SlantInside = true;

        if (receiver.Route == RouteType.Slant
            && play.TryGetSlantDirection(receiver.Index, out var slantInside))
        {
            receiver.SlantInside = slantInside;
        }
    }

    private static RouteType AdjustRouteForRunningBack(Receiver receiver, PlayDefinition play, RouteType route)
    {
        if (!receiver.IsRunningBack || play.Family == PlayType.Run || play.RunningBackRole != RunningBackRole.Route)
        {
            return route;
        }

        return route switch
        {
            RouteType.DoubleMove => RouteType.Flat,
            RouteType.Go => RouteType.Flat,
            RouteType.PostDeep => RouteType.Flat,
            RouteType.PostShallow => RouteType.Flat,
            RouteType.InDeep => RouteType.OutShallow,
            RouteType.OutDeep => RouteType.OutShallow,
            _ => route
        };
    }

    private static RouteType AdjustRouteForOuterWideReceiver(Receiver receiver, RouteType route, int leftOuterWrIndex, int rightOuterWrIndex)
    {
        if (receiver.PositionRole != OffensivePosition.WR)
        {
            return route;
        }

        if (receiver.Index != leftOuterWrIndex && receiver.Index != rightOuterWrIndex)
        {
            return route;
        }

        return route switch
        {
            RouteType.OutShallow => RouteType.InShallow,
            RouteType.OutDeep => RouteType.InDeep,
            _ => route
        };
    }

    private static RouteType PickRoute(PlayType playType, Random rng)
    {
        int roll = rng.Next(100);
        return playType switch
        {
            PlayType.Pass => PickPassRoute(roll),
            PlayType.Run => PickRunRoute(roll),
            _ => RouteType.Go
        };
    }

    private static RouteType PickPassRoute(int roll) =>
        roll < 20 ? RouteType.Slant :
        roll < 35 ? RouteType.OutShallow :
        roll < 50 ? RouteType.InShallow :
        roll < 62 ? RouteType.DoubleMove :
        roll < 74 ? RouteType.Go :
        roll < 84 ? RouteType.PostShallow :
        roll < 90 ? RouteType.PostDeep :
        roll < 95 ? RouteType.OutDeep :
        RouteType.InDeep;

    private static RouteType PickRunRoute(int roll) =>
        roll < 50 ? RouteType.OutShallow : RouteType.Flat;
}
