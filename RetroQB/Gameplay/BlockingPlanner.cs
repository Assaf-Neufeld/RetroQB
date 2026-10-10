using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

/// <summary>Resolves legacy concept rules once, leaving movement code to execute explicit jobs.</summary>
public static class BlockingPlanner
{
    public static BlockingAssignment PassProtection(float homeX) => new(BlockingJob.PassProtection,
        new(0, -1.4f - (0.8f + 1.7f * Math.Clamp(MathF.Abs(homeX - Constants.FieldWidth * 0.5f) / 8f, 0, 1))));

    public static BlockingAssignment Line(PlayDefinition play, float homeX)
    {
        if (play.Family == PlayType.Pass)
        {
            var balanced = PassProtection(homeX);
            int slide = play.Protection.Mode == ProtectionMode.SlideLeft ? -1 : play.Protection.Mode == ProtectionMode.SlideRight ? 1 : 0;
            return new(BlockingJob.PassProtection, balanced.TargetOffset + new Vector2(slide * 1.5f, 0));
        }
        int side = play.RunningBackSide;
        float offset = homeX - Constants.FieldWidth / 2;
        bool backside = side != 0 && offset * side < -.1f;
        var positions = play.Formation.Alignment.Linemen.Select(p => p.XFraction * Constants.FieldWidth).ToArray();
        var back = positions.Where(x => (x - Constants.FieldWidth / 2) * side < -.1f)
            .OrderBy(x => MathF.Abs(x - Constants.FieldWidth / 2)).ToArray();
        bool guard = side != 0 && back.Length > 0 && MathF.Abs(homeX - back[0]) < .01f;
        bool tackle = side != 0 && back.Length > 1 && MathF.Abs(homeX - back[^1]) < .01f;
        bool leadBack = play.Personnel.Slots.Any(s => s is ReceiverSlot.FB or ReceiverSlot.RB2);
        if (play.RunConcept == RunConcept.Dive)
            return new(BlockingJob.Drive, new(0, 2.4f), driveDirection: Vector2.UnitY);
        if (play.RunConcept is RunConcept.Power or RunConcept.Counter)
        {
            if (guard) return new(BlockingJob.Pull, new(side * 4, 5), BlockingAnchor.FieldCenter,
                new(side * 2.5f, -2), new(side * .3f, 1));
            if (play.RunConcept == RunConcept.Counter && tackle && !leadBack)
                return new(BlockingJob.KickOut, new(side * 6, 2),
                    BlockingAnchor.FieldCenter, new(side * 5, -2.8f), new(side, .2f));
            return new(backside ? BlockingJob.SealEdge : BlockingJob.Drive,
                new(backside ? -side * .6f : -side * 1.1f, 2),
                driveDirection: new(-side * .7f, 1), isBackside: backside);
        }
        if (play.RunConcept == RunConcept.Stretch)
            return new(BlockingJob.SealEdge, new(side * (backside ? 2 : 4.5f), 2.5f),
                approachOffset: new(side * 2.5f, .3f), driveDirection: new(side, backside ? 1 : .35f), isBackside: backside);
        if (play.RunConcept == RunConcept.Sweep && (guard || tackle))
            return new(guard ? BlockingJob.KickOut : BlockingJob.Pull, new(side * (guard ? 11 : 9), guard ? 2 : 5),
                BlockingAnchor.FieldCenter, new(side * (guard ? 7 : 5), -2.5f), new(side * .8f, 1));
        return new(backside ? BlockingJob.SealEdge : BlockingJob.Drive,
            new(side * (play.RunConcept == RunConcept.Sweep ? 3 : .3f), play.RunConcept == RunConcept.Draw ? 3.5f : 2.5f),
            driveDirection: new(side * .3f, 1), isBackside: backside);
    }

    public static BlockingAssignment Skill(PlayDefinition play, ReceiverSlot slot, int side)
    {
        if (play.Family == PlayType.Pass && slot.IsRunningBackSlot())
        {
            // Protect from the back's assigned pocket position. Using the live QB as
            // the landmark makes an idle protector shadow every dropback and scramble.
            int index = play.Formation.AlignmentSlots.ToList().IndexOf(slot);
            float depth = play.Formation.Alignment.SkillPositions[index].Depth;
            return new(BlockingJob.PassProtection, new(0, -depth));
        }
        if (play.Family == PlayType.Pass)
            return new(BlockingJob.PassProtection, new(side * 1.7f, -0.4f), BlockingAnchor.Quarterback);
        if (slot.IsTightEndSlot() && play.RunningBackSide != 0)
        {
            if (slot == ReceiverSlot.TE1 && play.RunConcept == RunConcept.Power
                && !play.Personnel.Slots.Any(s => s is ReceiverSlot.FB or ReceiverSlot.RB2))
                return new(BlockingJob.KickOut, new(play.RunningBackSide * 6, 2), BlockingAnchor.FieldCenter,
                    new(play.RunningBackSide * 5, -1), new(play.RunningBackSide, .3f));
            float width = play.RunConcept switch { RunConcept.Sweep => 2f, RunConcept.Stretch => 1.6f, RunConcept.Counter => 0.9f, _ => 1.2f };
            float depth = play.RunConcept switch { RunConcept.Sweep => 3.2f, RunConcept.Stretch => 2.9f, RunConcept.Counter => 2.4f, _ => 2.6f };
            return new(BlockingJob.SealEdge, new(play.RunningBackSide * width, depth), driveDirection: new(play.RunningBackSide * 0.85f, 1));
        }
        return new(BlockingJob.Drive, new(play.RunningBackSide * 1.2f, 2.6f), driveDirection: new(play.RunningBackSide * 0.7f, 1));
    }
}
