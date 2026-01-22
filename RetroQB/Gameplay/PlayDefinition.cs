using RetroQB.AI;

namespace RetroQB.Gameplay;

public enum RunningBackRole
{
    Block,
    Route
}

public sealed class PlayDefinition
{
    public string Name { get; }
    public PlayType Family { get; }
    public RunningBackRole RunningBackRole { get; }
    public int RunningBackSide { get; }
    public IReadOnlyDictionary<int, RouteType> Routes { get; }

    public PlayDefinition(
        string name,
        PlayType family,
        RunningBackRole runningBackRole,
        IReadOnlyDictionary<int, RouteType> routes,
        int runningBackSide = 0)
    {
        Name = name;
        Family = family;
        RunningBackRole = runningBackRole;
        Routes = routes;
        RunningBackSide = runningBackSide;
    }

    public bool TryGetRoute(int receiverIndex, out RouteType route)
    {
        return Routes.TryGetValue(receiverIndex, out route);
    }
}
