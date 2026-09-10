using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public enum BackfieldAction { None, Handoff, PlayAction }

/// <summary>A coordinated exchange/fake at a QB-relative mesh point. Timing is simulation time.</summary>
public sealed record BackfieldSequence
{
    public BackfieldAction Action { get; }
    public ReceiverSlot? Participant { get; }
    public Vector2 MeshOffset { get; }
    public float MinimumDelay { get; }
    public float FakeDuration { get; }
    public float ApproachTimeout { get; }
    public float MeshRadius { get; }
    public float SpeedMultiplier { get; }

    public BackfieldSequence(BackfieldAction action = BackfieldAction.None, ReceiverSlot? participant = null,
        Vector2 meshOffset = default, float minimumDelay = 0, float fakeDuration = 0.35f,
        float approachTimeout = 1.5f, float meshRadius = 1.2f, float speedMultiplier = 0.6f)
    {
        if (!Enum.IsDefined(action) || !float.IsFinite(meshOffset.X) || !float.IsFinite(meshOffset.Y)
            || !float.IsFinite(minimumDelay) || minimumDelay < 0 || !float.IsFinite(fakeDuration) || fakeDuration <= 0
            || !float.IsFinite(approachTimeout) || approachTimeout <= minimumDelay
            || !float.IsFinite(meshRadius) || meshRadius <= 0 || meshRadius > 3.2f
            || !float.IsFinite(speedMultiplier) || speedMultiplier <= 0
            || (action != BackfieldAction.None && (!participant.HasValue || !participant.Value.IsRunningBackSlot()))
            || (action == BackfieldAction.None && participant.HasValue))
            throw new ArgumentException("Invalid backfield participant, landmark, or timing.");
        Action = action;
        Participant = participant;
        MeshOffset = meshOffset;
        MinimumDelay = minimumDelay;
        FakeDuration = fakeDuration;
        ApproachTimeout = approachTimeout;
        MeshRadius = meshRadius;
        SpeedMultiplier = speedMultiplier;
    }

    public BackfieldSequence Flip() => new(Action, Participant, new(-MeshOffset.X, MeshOffset.Y),
        MinimumDelay, FakeDuration, ApproachTimeout, MeshRadius, SpeedMultiplier);

    public static BackfieldSequence ForRun(RunConcept concept, ReceiverSlot carrier, int side)
    {
        Vector2 offset = concept switch
        {
            RunConcept.Dive => new(0, -0.65f), RunConcept.Power => new(side * 0.85f, -0.25f),
            RunConcept.Counter => new(-side * 0.6f, -0.2f), RunConcept.Sweep => new(side * 1.45f, -0.1f),
            RunConcept.Stretch => new(side * 1.1f, -0.2f), _ => new(0, -1.05f)
        };
        float speed = concept switch
        {
            RunConcept.Dive => 0.62f, RunConcept.Power => 0.57f, RunConcept.Counter => 0.52f,
            RunConcept.Sweep => 0.66f, RunConcept.Stretch => 0.6f, _ => 0.46f
        };
        return new(BackfieldAction.Handoff, carrier, offset, minimumDelay: concept == RunConcept.Draw ? 0.55f : 0,
            meshRadius: 3.2f, speedMultiplier: speed);
    }
}
