using System.Collections.ObjectModel;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public sealed record ResolvedPlayer(ReceiverSlot Slot, FormationPoint Position, PlayerAssignment Assignment);

/// <summary>One immutable pre-snap description shared by setup, execution, diagrams and scoring.</summary>
public sealed class ResolvedPlay
{
    public PlayDefinition Definition { get; }
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public PlayType Family => Definition.Family;
    public FormationType Formation => Definition.Formation.Type;
    public PersonnelPackage Personnel => Definition.Personnel;
    public RunConcept RunConcept => Definition.RunConcept;
    public ReceiverSlot? BallCarrierSlot => Definition.BallCarrierSlot;
    public bool IsWildcard => Definition.IsWildcard;
    public bool IsFlipped { get; }
    public int RunningBackSide { get; }
    public FormationPoint Quarterback { get; }
    public IReadOnlyList<FormationPoint> Linemen { get; }
    public IReadOnlyList<BlockingAssignment> LineBlocking { get; }
    public IReadOnlyList<BlockingAssignment> OpeningLineBlocking { get; }
    public BackfieldSequence Backfield { get; }
    public IReadOnlyList<ResolvedPlayer> Players { get; }
    public IReadOnlyDictionary<ReceiverSlot, PlayerAssignment> Assignments { get; }
    public IReadOnlyDictionary<ReceiverSlot, RouteType> Routes { get; }

    internal ResolvedPlay(PlayDefinition definition, bool flipped)
    {
        Definition = definition;
        IsFlipped = flipped;
        Backfield = flipped ? definition.Backfield.Flip() : definition.Backfield;
        int direction = flipped ? -1 : 1;
        RunningBackSide = definition.RunningBackSide * direction;
        var alignment = definition.Formation.Alignment;
        Quarterback = Mirror(alignment.Quarterback, flipped);
        Linemen = Array.AsReadOnly(alignment.Linemen.Select(p => Mirror(p, flipped)).ToArray());
        BlockingAssignment Orient(BlockingAssignment job) => flipped ? job.Flip() : job;
        LineBlocking = Array.AsReadOnly(alignment.Linemen.Select((point, index) => Orient(
            definition.LineBlocking?[index] ?? BlockingPlanner.Line(definition, point.XFraction * Constants.FieldWidth))).ToArray());
        OpeningLineBlocking = definition.OpeningLineBlocking != null
            ? Array.AsReadOnly(definition.OpeningLineBlocking.Select(Orient).ToArray())
            : definition.RunConcept == RunConcept.Draw
                ? Array.AsReadOnly(alignment.Linemen.Select(point => Orient(BlockingPlanner.PassProtection(point.XFraction * Constants.FieldWidth))).ToArray())
                : LineBlocking;
        Players = Array.AsReadOnly(definition.Formation.AlignmentSlots.Select((slot, index) =>
        {
            var point = alignment.SkillPositions[index];
            var assignment = definition.Assignments[slot];
            int side = assignment.RouteSide ?? (assignment.Role == AssignmentRole.BallCarrier
                ? definition.RunningBackSide : point.XFraction < 0.5f ? -1 : 1);
            var block = assignment.Blocking ?? (assignment.Role == AssignmentRole.Block ? BlockingPlanner.Skill(definition, slot, side) : null);
            return new ResolvedPlayer(slot, Mirror(point, flipped), assignment with
            {
                RouteSide = side * direction,
                Blocking = block == null ? null : Orient(block)
            });
        }).ToArray());
        Assignments = new ReadOnlyDictionary<ReceiverSlot, PlayerAssignment>(Players.ToDictionary(p => p.Slot, p => p.Assignment));
        Routes = new ReadOnlyDictionary<ReceiverSlot, RouteType>(Players
            .Where(p => p.Assignment.Role != AssignmentRole.Block).ToDictionary(p => p.Slot, p => p.Assignment.Route));
    }

    /// <summary>Re-resolve from the original geometry so two flips restore it exactly.</summary>
    public ResolvedPlay Flip() => PlayResolver.Resolve(Definition, !IsFlipped);

    private static FormationPoint Mirror(FormationPoint point, bool flipped) =>
        flipped ? point with { XFraction = 1f - point.XFraction } : point;
}

public static class PlayResolver
{
    public static ResolvedPlay Resolve(PlayDefinition definition, bool flipped = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ResolvedPlay(definition, flipped);
    }
}
