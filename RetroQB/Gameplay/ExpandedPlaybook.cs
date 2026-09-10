using System.Numerics;
using RetroQB.Entities;
using static RetroQB.Routes.RouteType;

namespace RetroQB.Gameplay;

/// <summary>Forty passing and forty running calls built from authored concept/formation pairings.</summary>
public static class ExpandedPlaybook
{
    private sealed record PassConcept(string Id, string Name, PlayCategory Category, RouteType[] Routes, string Read, bool Fake = false);
    // Routes bind in formation order: outside receivers, inside players, then the primary back.
    private static readonly PassConcept[] Concepts =
    [
        new("spacing", "Spacing", PlayCategory.Quick, [Hitch, Hitch, Curl, Flat, Angle], "Find the open stop; the back works inside."),
        new("drive", "Drive", PlayCategory.Crossers, [Drag, InDeep, Seam, Drag, Flat], "Read the shallow cross underneath the dig."),
        new("sail", "Sail", PlayCategory.Intermediate, [Go, Comeback, Corner, Flat, Flat], "Read the corner over the flat outlet."),
        new("scissors", "Scissors", PlayCategory.Vertical, [PostDeep, Corner, Seam, Curl, Flat], "Look for the post/corner split behind coverage."),
        new("pa-cross", "PA Cross", PlayCategory.PlayAction, [PostDeep, InDeep, Drag, Flat, Wheel], "Fake the handoff, then read the deep cross.", true),
        new("stick", "Stick", PlayCategory.Quick, [Slant, Hitch, Hitch, Flat, Angle], "Read the short stop against the flat defender."),
        new("mesh-wheel", "Mesh Wheel", PlayCategory.Crossers, [Drag, Drag, Corner, Curl, Wheel], "Cross underneath; watch the wheel up the sideline."),
        new("levels", "Levels", PlayCategory.Intermediate, [InDeep, InShallow, Curl, Seam, Flat], "Read the two inside routes at different depths."),
        new("mills", "Mills", PlayCategory.Vertical, [PostDeep, InDeep, Go, Seam, Angle], "Read the post over the dig; check down inside."),
        new("pa-boot", "PA Flood", PlayCategory.PlayAction, [Go, Corner, OutShallow, Flat, Flat], "Fake the run, then work deep to flat.", true)
    ];

    public static IEnumerable<PlayDefinition> Build()
    {
        for (int f = 0; f < ExpandedFormations.All.Count; f++)
        {
            var (id, name, formation) = ExpandedFormations.All[f];
            for (int c = 0; c < 5; c++)
            {
                var concept = Concepts[c + (f % 2) * 5];
                var assignments = formation.AlignmentSlots.Select((slot, i) => (slot, assignment:
                    new PlayerAssignment(AssignmentRole.Route, concept.Routes[i])))
                    .ToDictionary(p => p.slot, p => p.assignment);
                // An attached TE or second back protects before becoming a late outlet.
                var support = formation.AlignmentSlots[3];
                if (support.IsRunningBackSlot() || (concept.Fake && support.IsTightEndSlot()))
                    assignments[support] = assignments[support] with
                    {
                        Blocking = new BlockingAssignment(BlockingJob.PassProtection, new(-2, 0), BlockingAnchor.Quarterback),
                        ReleaseAfterSeconds = concept.Fake ? 1.1f : .65f
                    };
                yield return new PlayDefinition($"pass.{id}.{concept.Id}", concept.Name, PlayType.Pass, formation, assignments,
                    backfield: concept.Fake ? new BackfieldSequence(BackfieldAction.PlayAction, ReceiverSlot.RB1,
                        new(.7f, -.5f), approachTimeout: 2.5f, speedMultiplier: .9f) : null,
                    info: new(name, concept.Name, concept.Category, concept.Read));
            }
            RunConcept[] runs = f % 2 == 0
                ? [RunConcept.Dive, RunConcept.Power, RunConcept.Counter, RunConcept.Stretch, RunConcept.Draw]
                : [RunConcept.Dive, RunConcept.Power, RunConcept.Counter, RunConcept.Sweep, RunConcept.Draw];
            foreach (var run in runs)
            {
                int side = run is RunConcept.Dive or RunConcept.Draw ? 0 : f % 2 == 0 ? -1 : 1;
                var assignments = formation.AlignmentSlots.ToDictionary(slot => slot, slot =>
                    slot == ReceiverSlot.RB1 ? new PlayerAssignment(AssignmentRole.BallCarrier) :
                    new PlayerAssignment(AssignmentRole.Block, Blocking: RunJob(slot, run, side)));
                string label = run == RunConcept.Dive && formation.Personnel.Slots.Contains(ReceiverSlot.FB) ? "Lead Iso" : run.ToString();
                if (side != 0) label += side < 0 ? " Left" : " Right";
                yield return new PlayDefinition($"run.{id}.{run.ToString().ToLowerInvariant()}", label,
                    PlayType.Run, formation, assignments, run, side,
                    info: new(name, label, PlaybookInfo.RunCategory(run),
                        run == RunConcept.Draw ? "Show pass protection, then follow the inside blocks." :
                        formation.Personnel.Slots.Contains(ReceiverSlot.FB) ? "Follow the fullback through the called gap." :
                        run == RunConcept.Counter ? "Let the pulling blocker clear the counter lane." : "Press the called lane and cut behind the blocks."));
            }
        }
    }

    private static BlockingAssignment RunJob(ReceiverSlot slot, RunConcept run, int side)
    {
        if (slot == ReceiverSlot.FB || slot == ReceiverSlot.RB2)
            return new(run == RunConcept.Power ? BlockingJob.KickOut : BlockingJob.Lead,
                new(side * (run == RunConcept.Power ? 5 : 3), 3), BlockingAnchor.FieldCenter,
                new(side * 2, -2), new(side * .5f, 1));
        return new(slot.IsTightEndSlot() ? BlockingJob.SealEdge : BlockingJob.Drive,
            new(side * 1.5f, 2), driveDirection: new(side * .5f, 1));
    }
}
