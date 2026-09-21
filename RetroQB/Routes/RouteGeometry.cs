using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Routes;

/// <summary>The single source of route geometry for movement, diagrams, and coverage read points.</summary>
public static class RouteGeometry
{
    public const float OutBreakLength = 6f;
    public const float PostBreakLength = 6f;
    public const float InBreakLength = 6f;
    public const float SlantLength = 9f;
    public const float OutBreakY = 0.18f;
    public const float PostXFactorShallow = 0.6f;
    public const float PostXFactorDeep = 0.9f;

    public static (float Shallow, float Deep, float PostAngleShallow, float PostAngleDeep) GetStemDistances(Receiver receiver) =>
        (receiver.IsRunningBack ? 5f : receiver.IsTightEnd ? 7f : 9f,
         receiver.IsRunningBack ? 12f : receiver.IsTightEnd ? 16f : 20f, 1.2f, 1f);

    public static RoutePath GetPath(Receiver receiver)
    {
        var key = (receiver.Route, receiver.RouteSide, receiver.SlantInside, receiver.RouteStart, receiver.RouteDefinition);
        var state = receiver.RouteState;
        if (state.Path is null || state.Key != key)
        {
            state.Reset();
            state.Key = key;
            state.Path = new RoutePath(receiver.RouteDefinition ?? CreateDefault(receiver), receiver.RouteStart, receiver.RouteSide);
        }
        return state.Path;
    }

    public static bool HasCompletedBreak(Receiver receiver)
    {
        var path = GetPath(receiver);
        return receiver.RouteState.StepIndex >= path.Definition.ReadAfterStep;
    }

    private static RouteDefinition CreateDefault(Receiver receiver)
    {
        var s = GetStemDistances(receiver);
        float width = receiver.IsRunningBack ? 9f : receiver.IsTightEnd ? 6f : 7f;
        return receiver.Route switch
        {
            RouteType.Go => new([new(new(0, s.Deep)), new(new(0, s.Deep + 8))], readAfterStep: 1),
            RouteType.Slant => new([new(GetSlantDirection(1, receiver.SlantInside) * SlantLength)]),
            RouteType.OutShallow => Break(s.Shallow, GetOutBreakDirection(1) * OutBreakLength),
            RouteType.OutDeep => Break(s.Deep, GetOutBreakDirection(1) * OutBreakLength),
            RouteType.InShallow => Break(s.Shallow, new(-InBreakLength, 0)),
            RouteType.InDeep => Break(s.Deep, new(-InBreakLength, 0)),
            RouteType.PostShallow => Break(s.Shallow, GetPostBreakDirection(1, PostXFactorShallow, s.PostAngleShallow) * PostBreakLength),
            RouteType.PostDeep => Break(s.Deep, GetPostBreakDirection(1, PostXFactorDeep, s.PostAngleDeep) * PostBreakLength),
            RouteType.DoubleMove => new([new(new(0, s.Shallow)), new(new(-3, s.Shallow), 0.1f), new(new(-3, s.Deep + 8))]),
            RouteType.Flat => new([new(new(width, width * 0.25f))]),
            RouteType.Hitch => new([new(new(0, 5)), new(new(-4, 5))], readAfterStep: 1),
            RouteType.Curl => new([new(new(0, 12)), new(new(-1, 10)), new(new(-5, 10))], readAfterStep: 2),
            RouteType.Comeback => new([new(new(0, 15)), new(new(3, 11))], RouteFinish.Settle),
            RouteType.Corner => new([new(new(0, 9)), new(new(7, 17))]),
            RouteType.Wheel => new([new(new(6, 2)), new(new(7, 8)), new(new(7, 22))]),
            RouteType.Angle => new([new(new(4, 2)), new(new(-3, 8))]),
            RouteType.Drag => new([new(new(0, 3)), new(new(-14, 4))]),
            RouteType.Seam => new([new(new(-1, 8)), new(new(-1, 24))]),
            _ => throw new ArgumentOutOfRangeException(nameof(receiver.Route))
        };
    }

    private static RouteDefinition Break(float stem, Vector2 turn) => new([new(new(0, stem)), new(new Vector2(0, stem) + turn)]);
    public static Vector2 GetOutBreakDirection(int side) => Vector2.Normalize(new Vector2(side, OutBreakY));
    public static Vector2 GetPostBreakDirection(int side, float xFactor, float angle) => Vector2.Normalize(new Vector2(-xFactor * side, angle));
    public static Vector2 GetSlantDirection(int side, bool inside) => Vector2.Normalize(new Vector2(0.7f * (inside ? -side : side), 1));
}
