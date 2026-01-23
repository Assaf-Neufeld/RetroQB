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

public enum FormationType
{
    SinglebackTrips,
    SpreadFour,
    Twins,
    Heavy,
    ShotgunTrips,
    Bunch,
    Spread,
    Slot,
    Shotgun,
    Pistol,
    Trips
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
