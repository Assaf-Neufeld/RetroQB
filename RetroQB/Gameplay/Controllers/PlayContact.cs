using System.Numerics;
using RetroQB.AI;
using RetroQB.Data;

namespace RetroQB.Gameplay.Controllers;

/// <summary>Immutable terminal observation at the actual contact, independent of the old HUD result enums.</summary>
public sealed record PlayContact(PlayEndReason Reason, Vector2 Position, DefenderSlot? Defender = null)
{
    public PlayEnded ToEvent(PlayStart start, OffensivePlayStats? stats = null, CoverageScheme? coverage = null)
        => new(start.Id, start.OffenseId, Reason,
            Reason == PlayEndReason.Incomplete ? start.Series.OwnYardLine : Position.Y - FieldGeometry.EndZoneDepth,
            stats, Defender, coverage);
}
