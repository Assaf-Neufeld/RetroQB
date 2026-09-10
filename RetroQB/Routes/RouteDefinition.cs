using System.Numerics;

namespace RetroQB.Routes;

public enum RouteFinish { Continue, Settle }
public enum RoutePhase { Waiting, Running, Holding, Continuing, Settled, Completed, Scrambling }

/// <summary>Endpoint relative to the route origin. Positive X is outward; Y is upfield.</summary>
public readonly record struct RouteStep(Vector2 Offset, float HoldSeconds = 0f);

/// <summary>Reusable immutable route geometry, independent of a player's side or position.</summary>
public sealed class RouteDefinition
{
    public IReadOnlyList<RouteStep> Steps { get; }
    public RouteFinish Finish { get; }
    public float DelaySeconds { get; }
    public int ReadAfterStep { get; }

    public RouteDefinition(IEnumerable<RouteStep> steps, RouteFinish finish = RouteFinish.Continue,
        float delaySeconds = 0, int? readAfterStep = null)
    {
        var copy = steps.ToArray();
        if (copy.Length == 0 || !Enum.IsDefined(finish) || !float.IsFinite(delaySeconds) || delaySeconds < 0
            || copy.Any(s => !float.IsFinite(s.Offset.X) || !float.IsFinite(s.Offset.Y)
                || !float.IsFinite(s.HoldSeconds) || s.HoldSeconds < 0))
            throw new ArgumentException("Routes need finite waypoints and non-negative timing.");
        int read = readAfterStep ?? copy.Length;
        if (read < 1 || read > copy.Length) throw new ArgumentException("Read point must reference a route step.");
        Steps = Array.AsReadOnly(copy);
        Finish = finish;
        DelaySeconds = delaySeconds;
        ReadAfterStep = read;
    }
}

public sealed class RoutePath
{
    public RouteDefinition Definition { get; }
    public IReadOnlyList<Vector2> Points { get; }

    internal RoutePath(RouteDefinition definition, Vector2 origin, int side)
    {
        Definition = definition;
        Points = Array.AsReadOnly(definition.Steps.Select(s => Clamp(origin + new Vector2(s.Offset.X * side, s.Offset.Y)))
            .Prepend(origin).ToArray());
    }

    internal static Vector2 Clamp(Vector2 point) => new(
        Math.Clamp(point.X, Constants.ReceiverRadius, Constants.FieldWidth - Constants.ReceiverRadius),
        Math.Clamp(point.Y, Constants.ReceiverRadius, Constants.FieldLength - Constants.ReceiverRadius));
}

/// <summary>Per-receiver progress, reset on assignment. It never belongs to the shared play definition.</summary>
public sealed class RouteExecutionState
{
    public int StepIndex { get; internal set; }
    public RoutePhase Phase { get; internal set; } = RoutePhase.Waiting;
    internal float DelayElapsed;
    internal float HoldElapsed;
    internal Vector2? LastPosition;
    internal RoutePath? Path;
    internal (RouteType Type, int Side, bool Inside, Vector2 Start, RouteDefinition? Custom) Key;

    public void Reset()
    {
        StepIndex = 0;
        Phase = RoutePhase.Waiting;
        DelayElapsed = HoldElapsed = 0;
        LastPosition = null;
        Path = null;
    }
}
