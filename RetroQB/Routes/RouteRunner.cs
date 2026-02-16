using System.Numerics;
using RetroQB.Entities;
using RetroQB.Core;

namespace RetroQB.Routes;

/// <summary>
/// Handles route execution and movement updates for receivers during play.
/// </summary>
public static class RouteRunner
{
    public static void UpdateRoute(Receiver receiver, float dt)
    {
        if (receiver.IsBlocking)
        {
            receiver.Velocity = Vector2.Zero;
            return;
        }

        if (receiver.HasBall)
        {
            UpdateWithBall(receiver);
            return;
        }

        UpdateRouteMovement(receiver, dt);
    }

    private static void UpdateWithBall(Receiver receiver)
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
    }

    private static void UpdateRouteMovement(Receiver receiver, float dt)
    {
        float speed = receiver.Speed;
        Vector2 dir = CalculateRouteDirection(receiver);

        if (!receiver.IsRunningBack && !receiver.IsTightEnd && dir == Vector2.Zero)
        {
            dir = CalculateScrambleDirection(receiver);
        }

        receiver.Velocity = dir * speed;
        if (dir != Vector2.Zero)
        {
            receiver.RouteProgress += speed * dt;
        }
    }

    private static Vector2 CalculateRouteDirection(Receiver receiver)
    {
        // Use actual Y-distance traveled from route start, not time-based progress
        // This ensures routes break at correct positions even if receiver is blocked/pushed
        float progress = receiver.Position.Y - receiver.RouteStart.Y;
        var stems = RouteGeometry.GetStemDistances(receiver);

        return receiver.Route switch
        {
            RouteType.Go => CalculateGoDirection(),
            RouteType.Slant => CalculateSlantDirection(receiver),
            RouteType.OutShallow => CalculateOutDirection(receiver, progress, stems.Shallow),
            RouteType.OutDeep => CalculateOutDirection(receiver, progress, stems.Deep),
            RouteType.InShallow => CalculateInDirection(progress, stems.Shallow, receiver.RouteSide),
            RouteType.InDeep => CalculateInDirection(progress, stems.Deep, receiver.RouteSide),
            RouteType.PostShallow => CalculatePostDirection(progress, stems.Shallow, receiver.RouteSide, RouteGeometry.PostXFactorShallow, stems.PostAngleShallow),
            RouteType.PostDeep => CalculatePostDirection(progress, stems.Deep, receiver.RouteSide, RouteGeometry.PostXFactorDeep, stems.PostAngleDeep),
            RouteType.DoubleMove => CalculateInDirection(progress, stems.Deep, receiver.RouteSide),
            RouteType.Flat => CalculateFlatDirection(receiver.RouteSide),
            _ => Vector2.Zero
        };
    }

    private static Vector2 CalculateGoDirection() => new Vector2(0, 1);

    private static Vector2 CalculateSlantDirection(Receiver receiver)
    {
        return RouteGeometry.GetSlantDirection(receiver.RouteSide, receiver.SlantInside);
    }

    private static Vector2 CalculateOutDirection(Receiver receiver, float progress, float stem)
    {
        if (progress < stem)
        {
            return new Vector2(0, 1);
        }

        float sidelineBuffer = 0.6f;
        if (receiver.Position.X <= sidelineBuffer || receiver.Position.X >= Constants.FieldWidth - sidelineBuffer)
        {
            return Vector2.Zero;
        }

        return RouteGeometry.GetOutBreakDirection(receiver.RouteSide);
    }

    private static Vector2 CalculateInDirection(float progress, float stem, int routeSide)
    {
        return progress < stem ? new Vector2(0, 1) : new Vector2(-routeSide, 0);
    }

    private static Vector2 CalculatePostDirection(float progress, float stem, int routeSide, float xFactor, float postAngle)
    {
        return progress < stem
            ? new Vector2(0, 1)
            : RouteGeometry.GetPostBreakDirection(routeSide, xFactor, postAngle);
    }

    private static Vector2 CalculateFlatDirection(int routeSide)
    {
        return Vector2.Normalize(new Vector2(routeSide, 0.25f));
    }

    private static Vector2 CalculateScrambleDirection(Receiver receiver)
    {
        Vector2 scramble = new Vector2(receiver.RouteSide * 0.35f, 0.65f);
        if (scramble.LengthSquared() > 0.001f)
        {
            scramble = Vector2.Normalize(scramble);
        }
        return scramble;
    }
}
