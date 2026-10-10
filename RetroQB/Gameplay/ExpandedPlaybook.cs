using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

/// <summary>Forty passing and forty running calls built from authored concept/formation pairings.</summary>
public static class ExpandedPlaybook
{
    private sealed record PassConcept(string Id, string Name, PlayCategory Category, string Read, bool Fake = false);
    private static readonly PassConcept[] Concepts =
    [
        new("spacing", "Spacing", PlayCategory.Quick, "Read the inside stop; use the flat or outside hitch."),
        new("drive", "Drive", PlayCategory.Crossers, "Read the dig over the shallow cross."),
        new("sail", "Sail", PlayCategory.Intermediate, "Read the corner over the flat outlet."),
        new("scissors", "Scissors", PlayCategory.Vertical, "Use max protection to read the post/corner split."),
        new("pa-cross", "PA Cross", PlayCategory.PlayAction, "Fake the handoff, then read the deep cross and wheel.", true),
        new("stick", "Stick", PlayCategory.Quick, "Read the inside stop against the flat defender."),
        new("mesh-wheel", "Mesh Wheel", PlayCategory.Crossers, "Cross underneath; watch the wheel up the sideline."),
        new("levels", "Levels", PlayCategory.Intermediate, "Read the two inside routes at different depths."),
        new("mills", "Mills", PlayCategory.Vertical, "Use max protection to read the post over the dig."),
        new("pa-boot", "PA Flood", PlayCategory.PlayAction, "Fake the run, then work the corner over the flat.", true)
    ];

    public static IEnumerable<PlayDefinition> Build()
    {
        for (int f = 0; f < ExpandedFormations.All.Count; f++)
        {
            var (id, name, formation) = ExpandedFormations.All[f];
            for (int c = 0; c < 5; c++)
            {
                var concept = Concepts[c + (f % 2) * 5];
                var assignments = formation.AlignmentSlots.ToDictionary(slot => slot,
                    _ => new PlayerAssignment(AssignmentRole.Route));
                yield return PassingConceptAuthor.Author($"pass.{id}.{concept.Id}", concept.Name, formation,
                    new(name, concept.Name, concept.Category, concept.Read), assignments,
                    concept.Fake ? new BackfieldSequence(BackfieldAction.PlayAction, ReceiverSlot.RB1,
                        new(.7f, -.5f), approachTimeout: 2.5f, speedMultiplier: .9f) : null);
            }
            RunConcept[] runs = f % 2 == 0
                ? [RunConcept.Dive, RunConcept.Power, RunConcept.Counter, RunConcept.Stretch, RunConcept.Draw]
                : [RunConcept.Dive, RunConcept.Power, RunConcept.Counter, RunConcept.Sweep, RunConcept.Draw];
            foreach (var run in runs)
            {
                int side = run is RunConcept.Dive or RunConcept.Draw ? 0 : f % 2 == 0 ? -1 : 1;
                var assignments = formation.AlignmentSlots.ToDictionary(slot => slot, slot =>
                    slot == ReceiverSlot.RB1 ? new PlayerAssignment(AssignmentRole.BallCarrier) :
                    new PlayerAssignment(AssignmentRole.Block, Blocking: RunJob(slot, run, side,
                        formation.Personnel.Slots.Any(s => s is ReceiverSlot.FB or ReceiverSlot.RB2))));
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

    private static BlockingAssignment RunJob(ReceiverSlot slot, RunConcept run, int side, bool leadBack)
    {
        if (slot == ReceiverSlot.FB || slot == ReceiverSlot.RB2)
            return new(run is RunConcept.Power or RunConcept.Counter ? BlockingJob.KickOut : BlockingJob.Lead,
                new(side * (run == RunConcept.Power ? 5 : 3), 3), BlockingAnchor.FieldCenter,
                new(side * 2, -2), new(side * .5f, 1));
        if (slot == ReceiverSlot.TE1 && run == RunConcept.Power && !leadBack)
            return new(BlockingJob.KickOut, new(side * 6, 2), BlockingAnchor.FieldCenter,
                new(side * 5, -1), new(side, .3f));
        return new(slot.IsTightEndSlot() ? BlockingJob.SealEdge : BlockingJob.Drive,
            new(side * 1.5f, 2), driveDirection: new(side * .5f, 1));
    }
}
