using System.Collections.ObjectModel;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public enum AssignmentRole { Route, Block, BallCarrier }

/// <summary>Direction is relative to the unflipped field. Null follows the player's alignment.</summary>
public sealed record PlayerAssignment(
    AssignmentRole Role,
    RouteType Route = RouteType.Flat,
    int? RouteSide = null,
    bool SlantInside = true);

public enum RunConcept
{
    None,
    Dive,
    Power,
    Counter,
    Sweep,
    Stretch,
    Draw
}

/// <summary>
/// Legacy formation identifiers used for situational scoring.
/// These names do not restrict play family or personnel; FormationDefinition supplies both bindings and geometry.
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

    // Run formations: 1 WR, 2 TE, 1 RB (heavy to called side)
    RunPowerRight,     // WR left, TE right, RB offset right
    RunPowerLeft,      // WR right, TE left, RB offset left
    RunIForm,          // WR split, TE inline, RB directly behind QB
    RunSweepRight,     // WR left, TE right, RB wide right
    RunSweepLeft,      // WR right, TE left, RB wide left
    RunStretchRight,   // WR left, TE right, RB behind OL — outside zone right
    RunStretchLeft,    // WR right, TE left, RB behind OL — outside zone left
    RunPistolStrongRight,
    RunPistolStrongLeft,
    RunSinglebackTripsRight,
    RunSinglebackTripsLeft
}

/// <summary>An immutable authored call. IDs are independent of names and keyboard slots.</summary>
public sealed class PlayDefinition
{
    public string Id { get; }
    public string Name { get; }
    public PlayType Family { get; }
    public FormationDefinition Formation { get; }
    public PersonnelPackage Personnel => Formation.Personnel;
    public IReadOnlyDictionary<ReceiverSlot, PlayerAssignment> Assignments { get; }
    public RunConcept RunConcept { get; }
    public int RunningBackSide { get; }
    public bool IsWildcard { get; }
    public ReceiverSlot? BallCarrierSlot { get; }

    public PlayDefinition(string id, string name, PlayType family, FormationDefinition formation,
        IReadOnlyDictionary<ReceiverSlot, PlayerAssignment> assignments,
        RunConcept runConcept = RunConcept.None, int runningBackSide = 0, bool isWildcard = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(formation);
        ArgumentNullException.ThrowIfNull(assignments);
        if (!Enum.IsDefined(family) || !Enum.IsDefined(runConcept) || runningBackSide is < -1 or > 1)
            throw new ArgumentException("Invalid play family, run concept, or direction.");
        if (!formation.Personnel.Slots.ToHashSet().SetEquals(assignments.Keys))
            throw new ArgumentException($"{id}: every active player must have exactly one assignment.");
        foreach (var assignment in assignments.Values)
        {
            if (assignment is null || !Enum.IsDefined(assignment.Role) || !Enum.IsDefined(assignment.Route)
                || assignment.RouteSide is < -1 or > 1)
                throw new ArgumentException($"{id}: invalid player assignment.");
        }
        var carriers = assignments.Where(a => a.Value.Role == AssignmentRole.BallCarrier).Select(a => a.Key).ToArray();
        if (family == PlayType.Run && (carriers.Length != 1 || runConcept == RunConcept.None))
            throw new ArgumentException($"{id}: a run needs one designated carrier and a run concept.");
        if (family == PlayType.Pass && (carriers.Length != 0 || runConcept != RunConcept.None))
            throw new ArgumentException($"{id}: a pass cannot have a designed carrier or run concept.");
        // Phase 1 retains RB handoffs; other exchange mechanics belong to phase 2.
        if (carriers.Any(slot => !slot.IsRunningBackSlot()))
            throw new ArgumentException($"{id}: only running-back handoffs are supported.");

        Id = id;
        Name = name;
        Family = family;
        Formation = formation;
        Assignments = new ReadOnlyDictionary<ReceiverSlot, PlayerAssignment>(assignments.ToDictionary());
        RunConcept = runConcept;
        RunningBackSide = runningBackSide;
        IsWildcard = isWildcard;
        BallCarrierSlot = carriers.Length == 1 ? carriers[0] : null;
    }
}
