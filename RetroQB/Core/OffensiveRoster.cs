using System;
using RetroQB.Entities;

namespace RetroQB.Core;

public sealed record QuarterbackProfile(string Name, float SkillRating);

public sealed record ReceiverProfile(string Name, float SkillRating);

/// <summary>
/// Represents the offensive roster (QB + skill positions) with per-player skill ratings.
/// </summary>
public sealed class OffensiveRoster
{
    public QuarterbackProfile Quarterback { get; init; } = new("QB", 1.0f);

    public IReadOnlyDictionary<ReceiverSlot, ReceiverProfile> Receivers { get; init; }
        = new Dictionary<ReceiverSlot, ReceiverProfile>();

    public static OffensiveRoster Default => new()
    {
        Quarterback = new QuarterbackProfile("QB", 1.0f),
        Receivers = new Dictionary<ReceiverSlot, ReceiverProfile>
        {
            [ReceiverSlot.WR1] = new ReceiverProfile("WR1", 1.0f),
            [ReceiverSlot.WR2] = new ReceiverProfile("WR2", 1.0f),
            [ReceiverSlot.WR3] = new ReceiverProfile("WR3", 1.0f),
            [ReceiverSlot.WR4] = new ReceiverProfile("WR4", 1.0f),
            [ReceiverSlot.TE1] = new ReceiverProfile("TE1", 1.0f),
            [ReceiverSlot.TE2] = new ReceiverProfile("TE2", 1.0f),
            [ReceiverSlot.RB1] = new ReceiverProfile("RB1", 1.0f),
            [ReceiverSlot.RB2] = new ReceiverProfile("RB2", 1.0f)
        }
    };

    public float GetQuarterbackSkillMultiplier()
        => ClampSkill(Quarterback.SkillRating);

    public float GetReceiverSkillMultiplier(ReceiverSlot slot)
        => Receivers.TryGetValue(slot, out var profile) ? ClampSkill(profile.SkillRating) : 1.0f;

    public string GetReceiverName(ReceiverSlot slot)
        => Receivers.TryGetValue(slot, out var profile) ? profile.Name : slot.GetLabel();

    private static float ClampSkill(float rating)
        => Math.Clamp(rating, 0.8f, 1.2f);
}
