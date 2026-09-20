using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public sealed record OffensiveIntent(Vector2 Movement, bool Sprint = false, int? ReceiverIndex = null, bool ThrowAway = false, bool PocketMovement = false);

/// <summary>Human ownership is independent of ball ownership.</summary>
public sealed record ControlContext(bool HumanOnDefense = false)
{
    public Defender? ControlledDefender(IReadOnlyList<Defender> defenders) => !HumanOnDefense ? null
        : defenders.FirstOrDefault(d => d.Slot == DefenderSlot.MLB)
            ?? defenders.FirstOrDefault(d => d.Slot == DefenderSlot.OLB1);
}
