namespace RetroQB.Entities;

public enum ReceiverSlot
{
    WR1,
    WR2,
    WR3,
    WR4,
    TE1,
    TE2,
    RB1,
    RB2
}

public static class ReceiverSlotExtensions
{
    public static string GetLabel(this ReceiverSlot slot) => slot switch
    {
        ReceiverSlot.WR1 => "WR1",
        ReceiverSlot.WR2 => "WR2",
        ReceiverSlot.WR3 => "WR3",
        ReceiverSlot.WR4 => "WR4",
        ReceiverSlot.TE1 => "TE1",
        ReceiverSlot.TE2 => "TE2",
        ReceiverSlot.RB1 => "RB1",
        ReceiverSlot.RB2 => "RB2",
        _ => slot.ToString()
    };

    public static int GetPriorityOrder(this ReceiverSlot slot) => slot switch
    {
        ReceiverSlot.WR1 => 1,
        ReceiverSlot.WR2 => 2,
        ReceiverSlot.WR3 => 3,
        ReceiverSlot.WR4 => 4,
        ReceiverSlot.TE1 => 5,
        ReceiverSlot.TE2 => 6,
        ReceiverSlot.RB1 => 7,
        ReceiverSlot.RB2 => 8,
        _ => int.MaxValue
    };

    public static bool IsRunningBackSlot(this ReceiverSlot slot) => slot is ReceiverSlot.RB1 or ReceiverSlot.RB2;

    public static bool IsTightEndSlot(this ReceiverSlot slot) => slot is ReceiverSlot.TE1 or ReceiverSlot.TE2;

    public static bool IsWideReceiverSlot(this ReceiverSlot slot) => slot is ReceiverSlot.WR1 or ReceiverSlot.WR2 or ReceiverSlot.WR3 or ReceiverSlot.WR4;

    public static IReadOnlyList<ReceiverSlot> DefaultReceivingStatOrder { get; } = new[]
    {
        ReceiverSlot.WR1,
        ReceiverSlot.WR2,
        ReceiverSlot.WR3,
        ReceiverSlot.WR4,
        ReceiverSlot.TE1,
        ReceiverSlot.RB1
    };
}
