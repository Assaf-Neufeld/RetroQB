using System;
using RetroQB.Entities;

namespace RetroQB.Core;

/// <summary>
/// QB profile with individual speed, acceleration, and accuracy attributes.
/// </summary>
public sealed record QbProfile
{
    public string Name { get; init; } = "QB";
    public float MaxSpeed { get; init; } = Constants.QbMaxSpeed;
    public float SprintSpeed { get; init; } = Constants.QbSprintSpeed;
    public float Acceleration { get; init; } = Constants.QbAcceleration;
    public float Friction { get; init; } = Constants.QbFriction;
    
    /// <summary>Throwing inaccuracy (lower = more accurate). 1.0 = baseline.</summary>
    public float ThrowInaccuracy { get; init; } = 1.0f;
    /// <summary>Short pass accuracy (lower = more accurate). 0.9 = baseline.</summary>
    public float ShortAccuracy { get; init; } = 0.9f;
    /// <summary>Medium pass accuracy (lower = more accurate). 1.0 = baseline.</summary>
    public float MediumAccuracy { get; init; } = 1.0f;
    /// <summary>Long pass accuracy (lower = more accurate). 1.15 = baseline.</summary>
    public float LongAccuracy { get; init; } = 1.15f;

    public static QbProfile Default => new();
}

/// <summary>
/// Wide receiver profile with speed, catching, and catch radius attributes.
/// </summary>
public sealed record WrProfile
{
    public string Name { get; init; } = "WR";
    public float Speed { get; init; } = Constants.WrSpeed;
    /// <summary>Catching ability (higher = better). Range 0.0-1.0, baseline 0.7.</summary>
    public float CatchingAbility { get; init; } = 0.7f;
    /// <summary>Catch radius multiplier. 1.0 = baseline.</summary>
    public float CatchRadius { get; init; } = 1.0f;

    public static WrProfile Default => new();
}

/// <summary>
/// Tight end profile with speed, catching, and blocking attributes.
/// </summary>
public sealed record TeProfile
{
    public string Name { get; init; } = "TE";
    public float Speed { get; init; } = Constants.TeSpeed;
    /// <summary>Catching ability (higher = better). Range 0.0-1.0, baseline 0.65.</summary>
    public float CatchingAbility { get; init; } = 0.65f;
    /// <summary>Catch radius multiplier. 1.0 = baseline.</summary>
    public float CatchRadius { get; init; } = 1.0f;
    /// <summary>Blocking strength multiplier. 1.0 = baseline.</summary>
    public float BlockingStrength { get; init; } = 1.0f;

    public static TeProfile Default => new();
}

/// <summary>
/// Running back profile with speed, catching, and tackle break attributes.
/// </summary>
public sealed record RbProfile
{
    public string Name { get; init; } = "RB";
    public float Speed { get; init; } = Constants.RbSpeed;
    /// <summary>Catching ability (higher = better). Range 0.0-1.0, baseline 0.6.</summary>
    public float CatchingAbility { get; init; } = 0.6f;
    /// <summary>Catch radius multiplier. 1.0 = baseline.</summary>
    public float CatchRadius { get; init; } = 1.0f;
    /// <summary>Tackle break chance. Range 0.0-0.5, baseline 0.25.</summary>
    public float TackleBreakChance { get; init; } = 0.25f;

    public static RbProfile Default => new();
}

/// <summary>
/// Offensive line profile with speed and blocking attributes.
/// </summary>
public sealed record OLineProfile
{
    public float Speed { get; init; } = Constants.OlSpeed;
    /// <summary>Blocking strength multiplier. 1.0 = baseline.</summary>
    public float BlockingStrength { get; init; } = 1.0f;

    public static OLineProfile Default => new();
}

/// <summary>
/// Represents the offensive roster (QB + skill positions) with per-player profiles.
/// </summary>
public sealed class OffensiveRoster
{
    public QbProfile Quarterback { get; init; } = QbProfile.Default;
    public IReadOnlyDictionary<ReceiverSlot, WrProfile> WideReceivers { get; init; }
        = new Dictionary<ReceiverSlot, WrProfile>();
    public IReadOnlyDictionary<ReceiverSlot, TeProfile> TightEnds { get; init; }
        = new Dictionary<ReceiverSlot, TeProfile>();
    public IReadOnlyDictionary<ReceiverSlot, RbProfile> RunningBacks { get; init; }
        = new Dictionary<ReceiverSlot, RbProfile>();
    public OLineProfile OffensiveLine { get; init; } = OLineProfile.Default;

    public static OffensiveRoster Default => new()
    {
        Quarterback = QbProfile.Default,
        WideReceivers = new Dictionary<ReceiverSlot, WrProfile>
        {
            [ReceiverSlot.WR1] = new WrProfile { Name = "WR1" },
            [ReceiverSlot.WR2] = new WrProfile { Name = "WR2" },
            [ReceiverSlot.WR3] = new WrProfile { Name = "WR3" },
            [ReceiverSlot.WR4] = new WrProfile { Name = "WR4" }
        },
        TightEnds = new Dictionary<ReceiverSlot, TeProfile>
        {
            [ReceiverSlot.TE1] = new TeProfile { Name = "TE1" },
            [ReceiverSlot.TE2] = new TeProfile { Name = "TE2" }
        },
        RunningBacks = new Dictionary<ReceiverSlot, RbProfile>
        {
            [ReceiverSlot.RB1] = new RbProfile { Name = "RB1" },
            [ReceiverSlot.RB2] = new RbProfile { Name = "RB2" }
        },
        OffensiveLine = OLineProfile.Default
    };

    // QB accessors
    public float GetQbMaxSpeed() => Quarterback.MaxSpeed;
    public float GetQbSprintSpeed() => Quarterback.SprintSpeed;
    public float GetQbAcceleration() => Quarterback.Acceleration;
    public float GetQbFriction() => Quarterback.Friction;
    public float GetQbThrowInaccuracy() => Math.Clamp(Quarterback.ThrowInaccuracy, 0.6f, 1.6f);
    
    public float GetQbDistanceAccuracy(float distance)
    {
        float baseMultiplier = distance <= Constants.ShortPassMaxDistance
            ? Quarterback.ShortAccuracy
            : distance <= Constants.MediumPassMaxDistance
                ? Quarterback.MediumAccuracy
                : Quarterback.LongAccuracy;
        return Math.Clamp(baseMultiplier, 0.6f, 1.6f);
    }

    // Receiver accessors by slot
    public float GetReceiverSpeed(ReceiverSlot slot)
    {
        if (slot.IsRunningBackSlot() && RunningBacks.TryGetValue(slot, out var rb))
            return rb.Speed;
        if (slot.IsTightEndSlot() && TightEnds.TryGetValue(slot, out var te))
            return te.Speed;
        if (WideReceivers.TryGetValue(slot, out var wr))
            return wr.Speed;
        return Constants.WrSpeed;
    }

    public float GetReceiverCatchingAbility(ReceiverSlot slot)
    {
        float ability;
        if (slot.IsRunningBackSlot() && RunningBacks.TryGetValue(slot, out var rb))
            ability = rb.CatchingAbility;
        else if (slot.IsTightEndSlot() && TightEnds.TryGetValue(slot, out var te))
            ability = te.CatchingAbility;
        else if (WideReceivers.TryGetValue(slot, out var wr))
            ability = wr.CatchingAbility;
        else
            ability = 0.7f;
        return Math.Clamp(ability, 0.4f, 0.95f);
    }

    public float GetReceiverCatchRadius(ReceiverSlot slot)
    {
        float radius;
        if (slot.IsRunningBackSlot() && RunningBacks.TryGetValue(slot, out var rb))
            radius = rb.CatchRadius;
        else if (slot.IsTightEndSlot() && TightEnds.TryGetValue(slot, out var te))
            radius = te.CatchRadius;
        else if (WideReceivers.TryGetValue(slot, out var wr))
            radius = wr.CatchRadius;
        else
            radius = 1.0f;
        return Math.Clamp(radius, 0.8f, 1.25f);
    }

    public float GetRbTackleBreakChance(ReceiverSlot slot)
    {
        if (slot.IsRunningBackSlot() && RunningBacks.TryGetValue(slot, out var rb))
            return Math.Clamp(rb.TackleBreakChance, 0.05f, 0.65f);
        return 0.1f; // Non-RBs have minimal tackle break ability
    }

    public string GetReceiverName(ReceiverSlot slot)
    {
        if (slot.IsRunningBackSlot() && RunningBacks.TryGetValue(slot, out var rb))
            return rb.Name;
        if (slot.IsTightEndSlot() && TightEnds.TryGetValue(slot, out var te))
            return te.Name;
        if (WideReceivers.TryGetValue(slot, out var wr))
            return wr.Name;
        return slot.GetLabel();
    }

    // OLine accessors
    public float GetOLineSpeed() => OffensiveLine.Speed;
    public float GetOLineBlockingStrength() => OffensiveLine.BlockingStrength;
    
    // TE blocking accessor
    public float GetTeBlockingStrength(ReceiverSlot slot)
    {
        if (slot.IsTightEndSlot() && TightEnds.TryGetValue(slot, out var te))
            return te.BlockingStrength;
        return 0.8f; // Non-TEs have reduced blocking
    }
}
