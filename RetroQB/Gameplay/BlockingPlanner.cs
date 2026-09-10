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
        if (play.Family == PlayType.Pass) return PassProtection(homeX);
        int side = play.RunningBackSide;
        var concept = play.RunConcept;
        float offset = homeX - Constants.FieldWidth * 0.5f;
        bool backside = side != 0 && (Math.Sign(offset) == -side || MathF.Abs(offset) < 0.001f);
        bool puller = concept == RunConcept.Counter && side != 0 && offset * side is < -0.9f and > -6.8f;
        float lateral = side * (concept switch
        {
            RunConcept.Dive => 0.35f, RunConcept.Counter => 2.8f, RunConcept.Sweep => 4.4f,
            RunConcept.Stretch => 3.9f, RunConcept.Draw => 0.8f, _ => 3.5f
        });
        float depth = concept switch
        {
            RunConcept.Dive => 2.4f, RunConcept.Counter => 3.6f, RunConcept.Sweep => 3.4f,
            RunConcept.Stretch => 3.1f, RunConcept.Draw => 2.8f, _ => 3.2f
        };
        float lane = concept switch
        {
            RunConcept.Dive => 1.4f, RunConcept.Counter => 4.1f, RunConcept.Sweep => 5.4f,
            RunConcept.Stretch => 4.6f, RunConcept.Draw => 1.8f, _ => 3.8f
        };
        float seal = concept switch
        {
            RunConcept.Dive => 0.8f, RunConcept.Counter => 3.8f, RunConcept.Sweep => 2.2f,
            RunConcept.Stretch => 1.8f, RunConcept.Draw => 1.2f, _ => 2.8f
        };
        float delta = offset - side * lane;
        int laneSide = Math.Sign(delta) == 0 ? -side : Math.Sign(delta);
        float separation = (concept switch { RunConcept.Dive => 0.7f, RunConcept.Stretch => 1.1f, RunConcept.Draw => 0.85f, _ => 1.4f })
            + Math.Clamp(MathF.Abs(delta) / 5.5f, 0, 1) * (concept switch { RunConcept.Dive => 0.25f, RunConcept.Stretch => 0.5f, RunConcept.Draw => 0.35f, _ => 0.7f });
        float targetX = lateral + (side == 0 ? 0 : laneSide * separation) - (backside ? side * seal : 0) + (puller ? side * 2.4f : 0);
        float driveX = puller ? side * 1.1f : side * (concept switch
        {
            RunConcept.Dive => backside ? -0.12f : 0.18f,
            RunConcept.Counter => backside ? 0.45f : 0.78f,
            RunConcept.Sweep => backside ? 0.25f : 0.95f,
            RunConcept.Stretch => backside ? 0.18f : 0.72f,
            RunConcept.Draw => backside ? 0.08f : 0.18f,
            _ => backside ? -0.45f : 0.7f
        });
        return new(puller ? BlockingJob.Pull : backside ? BlockingJob.SealEdge : BlockingJob.Drive,
            new(targetX, depth), approachOffset: puller ? new Vector2(targetX, -1.2f) : null,
            driveDirection: new(driveX, 1), isBackside: backside);
    }

    public static BlockingAssignment Skill(PlayDefinition play, ReceiverSlot slot, int side)
    {
        if (play.Family == PlayType.Pass)
            return new(BlockingJob.PassProtection, new(side * 1.7f, -0.4f), BlockingAnchor.Quarterback);
        if (slot.IsTightEndSlot() && play.RunningBackSide != 0)
        {
            float width = play.RunConcept switch { RunConcept.Sweep => 2f, RunConcept.Stretch => 1.6f, RunConcept.Counter => 0.9f, _ => 1.2f };
            float depth = play.RunConcept switch { RunConcept.Sweep => 3.2f, RunConcept.Stretch => 2.9f, RunConcept.Counter => 2.4f, _ => 2.6f };
            return new(BlockingJob.SealEdge, new(play.RunningBackSide * width, depth), driveDirection: new(play.RunningBackSide * 0.85f, 1));
        }
        return new(BlockingJob.Drive, new(play.RunningBackSide * 1.2f, 2.6f), driveDirection: new(play.RunningBackSide * 0.7f, 1));
    }
}
