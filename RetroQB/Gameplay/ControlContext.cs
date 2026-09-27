using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public sealed record OffensiveIntent(Vector2 Movement, bool Sprint = false, int? ReceiverIndex = null, bool ThrowAway = false, bool PocketMovement = false);

/// <summary>Human ownership is independent of ball ownership.</summary>
public sealed record ControlContext(bool HumanOnDefense = false)
{
    // Development harness only: keep CPU offense while exercising every defensive AI actor.
    internal bool AutomateDefense { get; init; }
    public Defender? ControlledDefender(IReadOnlyList<Defender> defenders) => !HumanOnDefense || AutomateDefense ? null
        : defenders.FirstOrDefault(d => d.Slot == DefenderSlot.MLB)
            ?? defenders.FirstOrDefault(d => d.Slot == DefenderSlot.OLB1);
}
