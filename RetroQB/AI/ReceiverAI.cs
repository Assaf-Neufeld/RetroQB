using System.Numerics;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.AI;

/// <summary>
/// Facade for receiver AI functionality. Delegates to specialized classes:
/// - RouteAssigner: Route assignment logic
/// - RouteRunner: Route execution and movement
/// - RouteVisualizer: Route waypoints and labels for rendering
/// </summary>
public static class ReceiverAI
{
    /// <summary>
    /// Assigns routes to all receivers based on the play definition.
    /// </summary>
    public static void AssignRoutes(IReadOnlyList<Receiver> receivers, PlayDefinition play, Random rng)
        => RouteAssigner.AssignRoutes(receivers, play, rng);

    /// <summary>
    /// Updates a receiver's movement based on their assigned route.
    /// </summary>
    public static void UpdateRoute(Receiver receiver, float dt)
        => RouteRunner.UpdateRoute(receiver, dt);

    /// <summary>
    /// Gets the waypoints for visualizing a receiver's route.
    /// </summary>
    public static IReadOnlyList<Vector2> GetRouteWaypoints(Receiver receiver)
        => RouteVisualizer.GetRouteWaypoints(receiver);

    /// <summary>
    /// Gets a display label for a route type.
    /// </summary>
    public static string GetRouteLabel(RouteType route)
        => RouteVisualizer.GetRouteLabel(route);
}
