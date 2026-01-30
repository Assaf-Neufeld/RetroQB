using RetroQB.AI;

namespace RetroQB.Gameplay;

public enum RunningBackRole
{
    Block,
    Route
}

public enum TightEndRole
{
    Block,
    Route
}

/// <summary>
/// Base: 3 WR, 1 RB, 1 TE (5 skill + 5 OL)
/// Pass: 4 WR, 1 TE (5 skill + 5 OL)  
/// Run: 1 WR, 1 TE, 1 RB (3 skill + 7 OL)
/// </summary>
public enum FormationType
{
    // Base formations: 3 WR, 1 RB, 1 TE
    BaseTripsRight,    // 3 WR trips right, TE left, RB in backfield
    BaseTripsLeft,     // 3 WR trips left, TE right, RB in backfield
    BaseSplit,         // 2 WR left, 1 WR right, TE right, RB in backfield
    BaseBunchRight,    // 3 WR bunched right, TE left, RB in backfield
    BaseBunchLeft,     // 3 WR bunched left, TE right, RB in backfield

    // Pass formations: 4 WR, 1 TE
    PassSpread,        // 4 WR spread wide, TE inline
    PassBunchRight,    // 3 WR bunched right, 1 WR isolated left, TE inline
    PassBunchLeft,     // 3 WR bunched left, 1 WR isolated right, TE inline
    PassEmpty,         // 4 WR spread, TE detached as receiver

    // Run formations: 1 WR, 1 TE, 1 RB (heavy with extra OL)
    RunPowerRight,     // WR left, TE right, RB offset right
    RunPowerLeft,      // WR right, TE left, RB offset left
    RunIForm,          // WR split, TE inline, RB directly behind QB
    RunSweepRight,     // WR left, TE right, RB wide right
    RunSweepLeft,      // WR right, TE left, RB wide left
    RunTossRight,      // WR left, TE right, RB closer right (toss)
    RunTossLeft        // WR right, TE left, RB closer left (toss)
}

public sealed class PlayDefinition
{
    public string Name { get; }
    public PlayType Family { get; }
    public FormationType Formation { get; }
    public RunningBackRole RunningBackRole { get; }
    public TightEndRole TightEndRole { get; }
    public int RunningBackSide { get; }
    public IReadOnlyDictionary<int, RouteType> Routes { get; }
    public IReadOnlyDictionary<int, bool> SlantDirections { get; }

    public PlayDefinition(
        string name,
        PlayType family,
        FormationType formation,
        RunningBackRole runningBackRole,
        TightEndRole tightEndRole,
        IReadOnlyDictionary<int, RouteType> routes,
        int runningBackSide = 0,
        IReadOnlyDictionary<int, bool>? slantDirections = null)
    {
        Name = name;
        Family = family;
        Formation = formation;
        RunningBackRole = runningBackRole;
        TightEndRole = tightEndRole;
        Routes = routes;
        RunningBackSide = runningBackSide;
        SlantDirections = slantDirections ?? new Dictionary<int, bool>();
    }

    public bool TryGetRoute(int receiverIndex, out RouteType route)
    {
        return Routes.TryGetValue(receiverIndex, out route);
    }

    public bool TryGetSlantDirection(int receiverIndex, out bool slantInside)
    {
        return SlantDirections.TryGetValue(receiverIndex, out slantInside);
    }
}
