using RetroQB.Entities;

namespace RetroQB.Gameplay;

internal enum FormationStrength
{
    Balanced,
    Left,
    Right
}

internal readonly record struct OffensiveSurface(
    int DetachedCount,
    int LeftDetachedCount,
    int RightDetachedCount,
    int TightEndDetachedCount,
    int AttachedTightEndCount,
    int LeftAttachedTightEndCount,
    int RightAttachedTightEndCount,
    int BackfieldEligibleCount,
    int LeftWideReceiverIndex,
    int LeftInsideReceiverIndex,
    int MiddleReceiverIndex,
    int RightInsideReceiverIndex,
    int RightWideReceiverIndex,
    float CenterX,
    FormationStrength Strength)
{
    public bool IsSpread => DetachedCount >= 4 || LeftDetachedCount >= 3 || RightDetachedCount >= 3;
    public bool IsHeavy => DetachedCount <= 2 && AttachedTightEndCount >= 1 && BackfieldEligibleCount > 0;
}
