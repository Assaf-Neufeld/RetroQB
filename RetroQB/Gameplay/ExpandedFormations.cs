using RetroQB.Entities;
using static RetroQB.Entities.ReceiverSlot;

namespace RetroQB.Gameplay;

/// <summary>Five skill players and five linemen in each new formation.</summary>
public static class ExpandedFormations
{
    public static IReadOnlyList<(string Id, string Name, FormationDefinition Formation)> All { get; } =
    [
        Make("gun-doubles", "Gun Doubles", FormationType.GunDoubles, "11", 5,
            [WR1, WR2, WR3, TE1, RB1], [new(.16f, 0), new(.84f, 0), new(.29f, 1.2f), new(.62f, 1.2f), new(.43f, 5.7f)]),
        Make("gun-trips", "Gun Trips", FormationType.GunTrips, "11", 5,
            [WR1, WR2, WR3, TE1, RB1], [new(.15f, 0), new(.84f, 0), new(.72f, 1.2f), new(.63f, 1.2f), new(.43f, 5.7f)]),
        Make("pistol-twins", "Pistol Twins", FormationType.PistolTwins, "12", 3.5f,
            [WR1, WR2, TE1, TE2, RB1], [new(.17f, 0), new(.30f, 1.2f), new(.62f, 0), new(.38f, 1.2f), new(.50f, 7)]),
        Make("ace", "Singleback Ace", FormationType.SinglebackAce, "12", 1.4f,
            [WR1, WR2, TE1, TE2, RB1], [new(.16f, 0), new(.84f, 1.2f), new(.38f, 0), new(.62f, 1.2f), new(.50f, 6.5f)]),
        Make("wing", "Wing Tight", FormationType.WingTight, "12", 1.4f,
            [WR1, WR2, TE1, TE2, RB1], [new(.24f, 0), new(.76f, 0), new(.38f, 1.2f), new(.34f, 2.2f), new(.50f, 6.5f)]),
        Make("i-pro", "I Pro", FormationType.IPro, "21", 1.4f,
            [WR1, WR2, TE1, FB, RB1], [new(.16f, 0), new(.84f, 1.2f), new(.38f, 0), new(.50f, 4), new(.50f, 7)]),
        Make("strong-i", "Strong I", FormationType.StrongI, "21", 1.4f,
            [WR1, WR2, TE1, FB, RB1], [new(.16f, 1.2f), new(.84f, 0), new(.62f, 0), new(.57f, 4), new(.50f, 7)]),
        Make("split-backs", "Split Backs", FormationType.SplitBacks, "20", 3,
            [WR1, WR2, WR3, RB2, RB1], [new(.16f, 0), new(.84f, 0), new(.70f, 1.2f), new(.43f, 5.5f), new(.57f, 5.5f)])
    ];

    private static (string, string, FormationDefinition) Make(string id, string name, FormationType type, string personnel,
        float qbDepth, ReceiverSlot[] slots, FormationPoint[] points) => (id, name,
        new(type, new PersonnelPackage(personnel, slots), new FormationAlignment(new(.5f, qbDepth), points,
            Enumerable.Range(-2, 5).Select(i => new FormationPoint(.5f + i * .035f, 0)))));
}
