using System.Numerics;
using RetroQB.Entities;
using RetroQB.Core;

namespace RetroQB.AI;

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
        var stems = GetStemDistances(receiver);

        return receiver.Route switch
        {
            RouteType.Go => CalculateGoDirection(),
            RouteType.Slant => CalculateSlantDirection(receiver),
            RouteType.OutShallow => CalculateOutDirection(receiver, progress, stems.Shallow),
            RouteType.OutDeep => CalculateOutDirection(receiver, progress, stems.Deep),
            RouteType.InShallow => CalculateInDirection(progress, stems.Shallow, receiver.RouteSide),
            RouteType.InDeep => CalculateInDirection(progress, stems.Deep, receiver.RouteSide),
            RouteType.PostShallow => CalculatePostDirection(progress, stems.Shallow, receiver.RouteSide, 0.6f, stems.PostAngleShallow),
            RouteType.PostDeep => CalculatePostDirection(progress, stems.Deep, receiver.RouteSide, 0.9f, stems.PostAngleDeep),
            RouteType.Curl => CalculateCurlDirection(receiver, progress),
            RouteType.Flat => CalculateFlatDirection(receiver.RouteSide),
            _ => Vector2.Zero
        };
    }

    private static (float Shallow, float Deep, float PostAngleShallow, float PostAngleDeep) GetStemDistances(Receiver receiver)
    {
        float stemShallow = receiver.IsRunningBack ? 3.8f : receiver.IsTightEnd ? 5.5f : 6.5f;
        float stemDeep = receiver.IsRunningBack ? 5.5f : receiver.IsTightEnd ? 8.5f : 11f;
        // PostAngle values represent the Y-component of the post cut direction
        // Lower values = sharper cut toward the middle, higher = more gradual
        return (stemShallow, stemDeep, 1.2f, 1.0f);
    }

    private static Vector2 CalculateGoDirection() => new Vector2(0, 1);

    private static Vector2 CalculateSlantDirection(Receiver receiver)
    {
        float slantSide = receiver.SlantInside ? -receiver.RouteSide : receiver.RouteSide;
        return Vector2.Normalize(new Vector2(0.7f * slantSide, 1));
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

        return new Vector2(receiver.RouteSide, 0);
    }

    private static Vector2 CalculateInDirection(float progress, float stem, int routeSide)
    {
        return progress < stem ? new Vector2(0, 1) : new Vector2(-routeSide, 0);
    }

    private static Vector2 CalculatePostDirection(float progress, float stem, int routeSide, float xFactor, float postAngle)
    {
        return progress < stem
            ? new Vector2(0, 1)
            : Vector2.Normalize(new Vector2(-xFactor * routeSide, postAngle));
    }

    private static Vector2 CalculateCurlDirection(Receiver receiver, float progress)
    {
        float curlStem = receiver.IsRunningBack ? 4f : receiver.IsTightEnd ? 6f : 7f;
        float curlReturn = receiver.IsRunningBack ? 1.5f : receiver.IsTightEnd ? 1.8f : 2f;

        if (progress < curlStem)
        {
            return new Vector2(0, 1);
        }
        else if (progress < curlStem + curlReturn)
        {
            return new Vector2(0, -1);
        }
        else
        {
            return Vector2.Zero;
        }
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
