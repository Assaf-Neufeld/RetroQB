using System.Numerics;

namespace RetroQB.Gameplay;

public enum BlockingJob { PassProtection, Drive, SealEdge, Lead, KickOut, Pull }
public enum BlockingAnchor { Home, FieldCenter, Quarterback }

/// <summary>Landmarks are relative to home X/LOS, field center/LOS, or the live quarterback.</summary>
public sealed record BlockingAssignment
{
    public BlockingJob Job { get; }
    public BlockingAnchor Anchor { get; }
    public Vector2 TargetOffset { get; }
    public Vector2? ApproachOffset { get; }
    public Vector2 DriveDirection { get; }
    public bool IsBackside { get; }
    public bool IsRunBlock => Job != BlockingJob.PassProtection;

    public BlockingAssignment(BlockingJob job, Vector2 targetOffset, BlockingAnchor anchor = BlockingAnchor.Home,
        Vector2? approachOffset = null, Vector2? driveDirection = null, bool isBackside = false)
    {
        var drive = driveDirection ?? Vector2.UnitY;
        if (!Enum.IsDefined(job) || !Enum.IsDefined(anchor) || !Finite(targetOffset) || !Finite(drive)
            || (approachOffset.HasValue && !Finite(approachOffset.Value)))
            throw new ArgumentException("Blocking jobs require finite landmarks and directions.");
        Job = job;
        Anchor = anchor;
        TargetOffset = targetOffset;
        ApproachOffset = approachOffset;
        DriveDirection = drive.LengthSquared() > 0.001f ? Vector2.Normalize(drive) : Vector2.Zero;
        IsBackside = isBackside;
    }

    public BlockingAssignment Flip() => new(Job, Mirror(TargetOffset), Anchor,
        ApproachOffset is Vector2 via ? Mirror(via) : null, Mirror(DriveDirection), IsBackside);

    private static Vector2 Mirror(Vector2 v) => new(-v.X, v.Y);
    private static bool Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}
