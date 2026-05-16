using RetroQB.Data;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public readonly record struct DefensivePersonnel(bool UsesNickel, IReadOnlySet<DefenderSlot> ActiveSlots)
{
    public bool IsDefined => ActiveSlots is { Count: > 0 };
}

internal static class DefensivePersonnelPolicy
{
    public static DefensivePersonnel Create(IReadOnlyList<Receiver> receivers, float lineOfScrimmage)
    {
        OffensiveSurface surface = DefensiveSurfaceAnalyzer.Analyze(receivers, lineOfScrimmage);
        return Create(surface);
    }

    public static DefensivePersonnel Create(IReadOnlyList<Receiver> receivers, DefensiveContext context)
    {
        OffensiveSurface surface = DefensiveSurfaceAnalyzer.Analyze(receivers, context.LineOfScrimmage);
        return Create(surface, context);
    }

    internal static DefensivePersonnel Create(OffensiveSurface surface)
    {
        bool usesNickel = surface.IsSpread && !surface.IsHeavy;
        return new DefensivePersonnel(usesNickel, BuildActiveSlots(usesNickel));
    }

    internal static DefensivePersonnel Create(OffensiveSurface surface, DefensiveContext context)
    {
        bool usesNickel = surface.IsSpread && !surface.IsHeavy;
        if (context.IsRedZone)
        {
            usesNickel = usesNickel && context.IsPassingDown && !context.IsTightRedZone;
        }

        return new DefensivePersonnel(usesNickel, BuildActiveSlots(usesNickel));
    }

    private static IReadOnlySet<DefenderSlot> BuildActiveSlots(bool usesNickel)
    {
        var slots = new HashSet<DefenderSlot>
        {
            DefenderSlot.DE1,
            DefenderSlot.DT1,
            DefenderSlot.DT2,
            DefenderSlot.DE2,
            DefenderSlot.OLB1,
            DefenderSlot.OLB2,
            DefenderSlot.CB1,
            DefenderSlot.CB2,
            DefenderSlot.FS,
            DefenderSlot.SS
        };

        slots.Add(usesNickel ? DefenderSlot.NB : DefenderSlot.MLB);
        return slots;
    }
}