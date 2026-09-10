using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.Routes;

/// <summary>Applies resolved assignments by identity. No random choices or route substitutions.</summary>
public static class RouteAssigner
{
    public static void AssignRoutes(IReadOnlyList<Receiver> receivers, ResolvedPlay play)
    {
        if (receivers.Count != play.Players.Count || receivers.Select(r => r.Slot).Distinct().Count() != receivers.Count
            || receivers.Any(r => !play.Assignments.ContainsKey(r.Slot)))
            throw new ArgumentException("Receivers do not match the resolved personnel.");

        foreach (var receiver in receivers)
        {
            var assignment = play.Assignments[receiver.Slot];
            receiver.RouteStart = receiver.Position;
            receiver.RouteProgress = 0f;
            receiver.RouteDefinition = assignment.RouteDefinition;
            receiver.RouteState.Reset();
            receiver.AssignmentElapsed = 0f;
            receiver.BlockingState.Reset();
            receiver.HasBall = false;
            receiver.IsBlocking = assignment.Role == AssignmentRole.Block || assignment.ReleaseAfterSeconds > 0;
            receiver.Eligible = assignment.Role != AssignmentRole.Block;
            receiver.Route = assignment.Route;
            receiver.RouteSide = assignment.RouteSide!.Value;
            receiver.SlantInside = assignment.SlantInside;
        }
    }
}
