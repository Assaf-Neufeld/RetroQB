namespace RetroQB.Gameplay.Defense;

public sealed record ShadowAssignment(DefenderSlot Defender, ReceiverId Receiver);
public sealed record BracketAssignment(DefenderSlot Helper, ReceiverId Receiver);
public sealed record BunchSwitchGroup(int Id, ReceiverId First, ReceiverId Second, DefenderSlot OutsideDefender, DefenderSlot InsideDefender);

/// <summary>Explicit pre-snap instructions, never inferred from stars or early route success.</summary>
public sealed class ManCoverageAdjustments
{
    public static ManCoverageAdjustments None { get; } = new();
    public bool SwitchPairs { get; }
    public IReadOnlyList<ShadowAssignment> Shadows { get; }
    public BracketAssignment? Bracket { get; }
    public DefenderSlot? Spy { get; }
    public ManCoverageAdjustments(bool switchPairs = false, IEnumerable<ShadowAssignment>? shadows = null, BracketAssignment? bracket = null, DefenderSlot? spy = null)
    {
        SwitchPairs = switchPairs; Shadows = Array.AsReadOnly((shadows ?? []).OrderBy(s => s.Defender).ToArray()); Bracket = bracket; Spy = spy;
    }
    public bool Equivalent(ManCoverageAdjustments other) => SwitchPairs == other.SwitchPairs && Bracket == other.Bracket && Spy == other.Spy && Shadows.SequenceEqual(other.Shadows);
}
