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
    public bool CompressToField { get; }
    public float FieldDepthBudget { get; }
    public float OriginDepth { get; }

    public RouteDefinition(IEnumerable<RouteStep> steps, RouteFinish finish = RouteFinish.Continue,
        float delaySeconds = 0, int? readAfterStep = null, bool compressToField = false,
        float fieldDepthBudget = 0, float originDepth = 0)
    {
        var copy = steps.ToArray();
        if (copy.Length == 0 || !Enum.IsDefined(finish) || !float.IsFinite(delaySeconds) || delaySeconds < 0
            || !float.IsFinite(fieldDepthBudget) || fieldDepthBudget < 0 || !float.IsFinite(originDepth) || originDepth < 0
            || copy.Any(s => !float.IsFinite(s.Offset.X) || !float.IsFinite(s.Offset.Y)
                || !float.IsFinite(s.HoldSeconds) || s.HoldSeconds < 0))
            throw new ArgumentException("Routes need finite waypoints and non-negative timing.");
        int read = readAfterStep ?? copy.Length;
        if (read < 1 || read > copy.Length) throw new ArgumentException("Read point must reference a route step.");
        Steps = Array.AsReadOnly(copy);
        Finish = finish;
        DelaySeconds = delaySeconds;
        ReadAfterStep = read;
        CompressToField = compressToField;
        FieldDepthBudget = fieldDepthBudget;
        OriginDepth = originDepth;
    }
}

public sealed class RoutePath
{
    public RouteDefinition Definition { get; }
    public IReadOnlyList<Vector2> Points { get; }

    internal RoutePath(RouteDefinition definition, Vector2 origin, int side)
    {
        Definition = definition;
        float deepest = definition.Steps.Max(s => s.Offset.Y);
        float referenceY = origin.Y + definition.OriginDepth;
        float budget = definition.FieldDepthBudget > 0 ? definition.FieldDepthBudget : deepest;
        float scale = definition.CompressToField && deepest > 0
            ? Math.Clamp((Constants.FieldLength - Constants.ReceiverRadius - referenceY) / budget, 0, 1) : 1;
        Points = Array.AsReadOnly(definition.Steps.Select(s => Clamp(new(origin.X + s.Offset.X * side,
                referenceY + (s.Offset.Y - definition.OriginDepth) * scale)))
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
    internal Vector2? ExpectedPosition;
    internal RoutePath? Path;
    internal (RouteType Type, int Side, bool Inside, Vector2 Start, RouteDefinition? Custom) Key;

    public void Reset()
    {
        StepIndex = 0;
        Phase = RoutePhase.Waiting;
        DelayElapsed = HoldElapsed = 0;
        LastPosition = null;
        ExpectedPosition = null;
        Path = null;
    }
}
