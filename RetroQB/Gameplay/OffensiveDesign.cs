using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

/// <summary>Only the release lineman can climb; the anchor owns the first-level defender.</summary>
public sealed record CombinationBlock(int AnchorSlot, int ReleaseSlot, Vector2 SecondLevel,
    float MinimumSeconds = .45f);

public sealed class RunPlan
{
    public IReadOnlyList<Vector2> Path { get; }
    public IReadOnlyList<CombinationBlock> Combinations { get; }
    public RunPlan(IEnumerable<Vector2> path, IEnumerable<CombinationBlock>? combinations = null)
    {
        var points = path.ToArray(); var pairs = (combinations ?? []).ToArray();
        if (points.Length == 0 || points.Any(p => !OffensiveGeometry.Finite(p))
            || pairs.Any(p => p.AnchorSlot < 0 || p.ReleaseSlot < 0 || p.AnchorSlot == p.ReleaseSlot
                || !OffensiveGeometry.Finite(p.SecondLevel) || !float.IsFinite(p.MinimumSeconds) || p.MinimumSeconds < 0)
            || pairs.SelectMany(p => new[] { p.AnchorSlot, p.ReleaseSlot }).Distinct().Count() != pairs.Length * 2)
            throw new ArgumentException("Run paths and disjoint combinations require valid landmarks and slots.");
        Path = Array.AsReadOnly(points); Combinations = Array.AsReadOnly(pairs);
    }
    public RunPlan Flip() => new(Path.Select(OffensiveGeometry.Mirror),
        Combinations.Select(c => c with { SecondLevel = OffensiveGeometry.Mirror(c.SecondLevel) }));
    public IReadOnlyList<Vector2> At(float los) => Path.Select(p => OffensiveGeometry.At(p, los)).ToArray();
    internal static RunPlan For(PlayDefinition play)
    {
        int s = play.RunningBackSide;
        Vector2[] path = play.RunConcept switch
        {
            RunConcept.Power => [new(s * 2.5f, -1), new(s * 4, 3), new(s * 4, 12)],
            RunConcept.Counter => [new(-s * 2, -2), new(s * 2, -1), new(s * 4.5f, 3), new(s * 4.5f, 12)],
            RunConcept.Stretch => [new(s * 6, -1), new(s * 9, 3), new(s * 9, 12)],
            RunConcept.Sweep => [new(s * 9, -2), new(s * 13, 2), new(s * 13, 12)],
            RunConcept.Draw => [new(0, -2), new(0, 3), new(0, 12)],
            _ => [new(0, 1), new(0, 12)]
        };
        var line = play.Formation.Alignment.Linemen;
        if (play.RunConcept is RunConcept.Dive or RunConcept.Draw or RunConcept.Sweep) return new(path);
        int center = Enumerable.Range(0, line.Count).MinBy(i => MathF.Abs(line[i].XFraction - .5f));
        int partner = Enumerable.Range(0, line.Count).Where(i => i != center
            && (s == 0 || Math.Sign(line[i].XFraction - line[center].XFraction) == s))
            .MinBy(i => MathF.Abs(line[i].XFraction - line[center].XFraction));
        return new(path, [new(center, partner, new(s * 2.5f, 6))]);
    }
}

public sealed class ScreenPlan
{
    public ReceiverSlot Target { get; }
    public float ReleaseSeconds { get; }
    public Vector2 CatchOffset { get; }
    public IReadOnlyDictionary<int, BlockingAssignment> LineEscorts { get; }
    public IReadOnlyDictionary<ReceiverSlot, BlockingAssignment> SkillEscorts { get; }
    public ScreenPlan(ReceiverSlot target, float releaseSeconds, Vector2 catchOffset,
        IReadOnlyDictionary<int, BlockingAssignment> lineEscorts,
        IReadOnlyDictionary<ReceiverSlot, BlockingAssignment> skillEscorts)
    {
        if (!Enum.IsDefined(target) || !float.IsFinite(releaseSeconds) || releaseSeconds < 0
            || !OffensiveGeometry.Finite(catchOffset) || lineEscorts.Any(p => p.Key < 0 || p.Value == null || !p.Value.IsRunBlock)
            || skillEscorts.Any(p => p.Key == target || p.Value == null || !p.Value.IsRunBlock))
            throw new ArgumentException("Screens require a legal target, timing and escort jobs.");
        Target = target; ReleaseSeconds = releaseSeconds; CatchOffset = catchOffset;
        LineEscorts = new System.Collections.ObjectModel.ReadOnlyDictionary<int, BlockingAssignment>(lineEscorts.ToDictionary());
        SkillEscorts = new System.Collections.ObjectModel.ReadOnlyDictionary<ReceiverSlot, BlockingAssignment>(skillEscorts.ToDictionary());
    }
    public ScreenPlan Flip() => new(Target, ReleaseSeconds, OffensiveGeometry.Mirror(CatchOffset),
        LineEscorts.ToDictionary(p => p.Key, p => p.Value.Flip()), SkillEscorts.ToDictionary(p => p.Key, p => p.Value.Flip()));
}

public sealed class QuarterbackPath
{
    public IReadOnlyList<Vector2> Points { get; }
    public QuarterbackPath(IEnumerable<Vector2> points)
    {
        var copy = points.ToArray();
        if (copy.Length == 0 || copy.Any(p => !OffensiveGeometry.Finite(p) || p.Y > -.5f))
            throw new ArgumentException("Moving-pocket landmarks must stay behind the line.");
        Points = Array.AsReadOnly(copy);
    }
    public QuarterbackPath Flip() => new(Points.Select(OffensiveGeometry.Mirror));
    public IReadOnlyList<Vector2> At(float los) => Points.Select(p => OffensiveGeometry.At(p, los)).ToArray();
}

internal static class OffensiveGeometry
{
    public static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);
    public static Vector2 Mirror(Vector2 p) => new(-p.X, p.Y);
    public static Vector2 At(Vector2 offset, float los) => new(Math.Clamp(Constants.FieldWidth / 2 + offset.X, 1, Constants.FieldWidth - 1),
        Math.Clamp(los + offset.Y, 1, Constants.FieldLength - 1));
}
