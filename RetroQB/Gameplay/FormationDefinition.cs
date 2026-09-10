using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public readonly record struct FormationPoint(float XFraction, float Depth)
{
    public Vector2 AtLineOfScrimmage(float lineOfScrimmage) =>
        new(Constants.FieldWidth * XFraction, MathF.Max(0.6f, lineOfScrimmage - Depth));
}

/// <summary>The active players, independent of where a formation places them.</summary>
public sealed class PersonnelPackage
{
    public string Id { get; }
    public IReadOnlyList<ReceiverSlot> Slots { get; }
    public int LinemanCount { get; }

    public PersonnelPackage(string id, IEnumerable<ReceiverSlot> slots, int linemanCount = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var copy = slots.ToArray();
        if (copy.Any(s => !Enum.IsDefined(s)) || copy.Distinct().Count() != copy.Length
            || linemanCount < 5 || copy.Length > 5 || 1 + copy.Length + linemanCount != 11)
            throw new ArgumentException("Personnel must contain 11 players including QB, at least five linemen, and unique skill slots.");
        Id = id;
        Slots = Array.AsReadOnly(copy);
        LinemanCount = linemanCount;
    }
}

/// <summary>Pure geometry: reusable with another personnel package of the same size.</summary>
public sealed class FormationAlignment
{
    public FormationPoint Quarterback { get; }
    public IReadOnlyList<FormationPoint> SkillPositions { get; }
    public IReadOnlyList<FormationPoint> Linemen { get; }

    public FormationAlignment(FormationPoint quarterback, IEnumerable<FormationPoint> skillPositions,
        IEnumerable<FormationPoint> linemen)
    {
        var skills = skillPositions.ToArray();
        var line = linemen.ToArray();
        var all = skills.Concat(line).Prepend(quarterback).ToArray();
        if (all.Any(p => !float.IsFinite(p.XFraction) || !float.IsFinite(p.Depth)
            || p.XFraction <= 0 || p.XFraction >= 1 || p.Depth < 0))
            throw new ArgumentException("Formation positions must be finite and within the field.");
        for (int i = 0; i < all.Length; i++)
        for (int j = i + 1; j < all.Length; j++)
        {
            var delta = new Vector2((all[i].XFraction - all[j].XFraction) * Constants.FieldWidth, all[i].Depth - all[j].Depth);
            // Legacy QB/center and bunch sets intentionally start within contact range.
            if (delta.Length() < 0.5f)
                throw new ArgumentException("Formation positions collapse onto one another.");
        }
        Quarterback = quarterback;
        SkillPositions = Array.AsReadOnly(skills);
        Linemen = Array.AsReadOnly(line);
    }
}

public sealed class FormationDefinition
{
    public FormationType Type { get; }
    public PersonnelPackage Personnel { get; }
    public FormationAlignment Alignment { get; }
    public IReadOnlyList<ReceiverSlot> AlignmentSlots { get; }

    public FormationDefinition(FormationType type, PersonnelPackage personnel, FormationAlignment alignment,
        IEnumerable<ReceiverSlot>? alignmentSlots = null)
    {
        ArgumentNullException.ThrowIfNull(personnel);
        ArgumentNullException.ThrowIfNull(alignment);
        var bindings = (alignmentSlots ?? personnel.Slots).ToArray();
        if (!Enum.IsDefined(type) || personnel.Slots.Count != alignment.SkillPositions.Count
            || personnel.LinemanCount != alignment.Linemen.Count || bindings.Length != personnel.Slots.Count
            || !personnel.Slots.ToHashSet().SetEquals(bindings))
            throw new ArgumentException("Formation alignment must match its personnel.");
        Type = type;
        Personnel = personnel;
        Alignment = alignment;
        AlignmentSlots = Array.AsReadOnly(bindings);
    }
}

public static class PersonnelPackages
{
    public static PersonnelPackage Eleven { get; } = new("11",
        [ReceiverSlot.WR1, ReceiverSlot.WR2, ReceiverSlot.WR3, ReceiverSlot.TE1, ReceiverSlot.RB1]);
    public static PersonnelPackage Empty { get; } = new("01",
        [ReceiverSlot.WR1, ReceiverSlot.WR2, ReceiverSlot.WR3, ReceiverSlot.WR4, ReceiverSlot.TE1]);
    public static PersonnelPackage HeavySixLinemen { get; } = new("heavy-six-ol",
        [ReceiverSlot.WR1, ReceiverSlot.TE1, ReceiverSlot.TE2, ReceiverSlot.RB1], 6);
}
