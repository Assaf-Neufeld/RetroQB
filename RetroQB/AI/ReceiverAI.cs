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
    Post
}

public static class ReceiverAI
{
    public static void AssignRoutes(IReadOnlyList<Receiver> receivers, PlayType playType, Random rng)
    {
        foreach (var receiver in receivers)
        {
            receiver.RouteStart = receiver.Position;
            receiver.RouteProgress = 0f;
            receiver.HasBall = false;
            receiver.Eligible = true;
            receiver.Route = PickRoute(playType, rng);
        }
    }

    private static RouteType PickRoute(PlayType playType, Random rng)
    {
        int roll = rng.Next(100);
        return playType switch
        {
            PlayType.QuickPass => roll < 45 ? RouteType.Slant : roll < 75 ? RouteType.Out : RouteType.Go,
            PlayType.LongPass => roll < 50 ? RouteType.Go : RouteType.Post,
            PlayType.QbRunFocus => roll < 40 ? RouteType.Out : RouteType.Slant,
            _ => RouteType.Go
        };
    }

    public static void UpdateRoute(Receiver receiver, float dt)
    {
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
                dir = Vector2.Normalize(new Vector2(receiver.Index % 2 == 0 ? 0.7f : -0.7f, 1));
                break;
            case RouteType.Out:
                dir = progress < 8f ? new Vector2(0, 1) : new Vector2(receiver.Index % 2 == 0 ? 1 : -1, 0);
                break;
            case RouteType.Post:
                dir = progress < 10f ? new Vector2(0, 1) : Vector2.Normalize(new Vector2(receiver.Index % 2 == 0 ? -0.6f : 0.6f, 1));
                break;
        }

        receiver.Velocity = dir * speed;
        receiver.RouteProgress += speed * dt;
    }
}
