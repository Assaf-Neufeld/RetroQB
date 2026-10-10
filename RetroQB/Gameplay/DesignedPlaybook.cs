using System.Numerics;
using RetroQB.Entities;
using RetroQB.Routes;

namespace RetroQB.Gameplay;

/// <summary>New pressure answers with permanent IDs; existing pocket PA calls remain available.</summary>
public static class DesignedPlaybook
{
    public static IEnumerable<PlayDefinition> Build()
    {
        yield return Screen("gun-doubles", false);
        yield return Screen("split-backs", false);
        yield return Screen("gun-trips", true);
        yield return Screen("gun-doubles", true);
        yield return MovingPocket("pistol-twins", true);
        yield return MovingPocket("ace", true);
        yield return MovingPocket("i-pro", true);
        yield return MovingPocket("gun-doubles", false);
    }
    private static PlayDefinition Screen(string formationId, bool wide)
    {
        var (_, name, f) = ExpandedFormations.All.Single(f => f.Id == formationId);
        int side = wide || formationId == "split-backs" ? 1 : -1;
        bool isolated = wide && formationId == "gun-doubles";
        var target = wide ? isolated ? ReceiverSlot.WR2 : ReceiverSlot.WR3 : ReceiverSlot.RB1;
        var catchOffset = new Vector2(side * (wide ? isolated ? 18 : 13 : 10), wide ? -1.5f : -2.5f);
        float release = wide ? 0 : .65f;
        var assignments = f.AlignmentSlots.ToDictionary(s => s, s => new PlayerAssignment(AssignmentRole.Route,
            RouteType.Go, RouteDefinition: Route(new Vector2(0, 24))));
        int index = f.AlignmentSlots.ToList().IndexOf(target);
        var start = f.Alignment.SkillPositions[index];
        assignments[target] = new(AssignmentRole.Route, RouteType.Flat, 1,
            RouteDefinition: new([new(new(Constants.FieldWidth / 2 + catchOffset.X - start.XFraction * Constants.FieldWidth,
                catchOffset.Y + start.Depth))], RouteFinish.Settle, delaySeconds: release));
        var skills = new Dictionary<ReceiverSlot, BlockingAssignment>();
        var escorts = wide ? isolated ? new[] { ReceiverSlot.TE1 } : new[] { ReceiverSlot.WR2, ReceiverSlot.TE1 }
            : f.AlignmentSlots.Where(s => s is ReceiverSlot.TE1 or ReceiverSlot.RB2).ToArray();
        for (int i = 0; i < escorts.Length; i++)
        {
            var slot = escorts[i];
            var job = new BlockingAssignment(BlockingJob.Lead, new((i == 0 ? side * 2 : -side * 2), 3), BlockingAnchor.BallCarrier,
                driveDirection: new(side * .35f, 1));
            skills[slot] = job;
            assignments[slot] = new(AssignmentRole.Block, Blocking: wide ? job : new(BlockingJob.PassProtection, new(side * 3, -4), BlockingAnchor.FieldCenter));
        }
        var line = new Dictionary<int, BlockingAssignment>();
        if (!wide)
        {
            line[side < 0 ? 0 : 4] = new(BlockingJob.SealEdge, new(side * 2, 3), BlockingAnchor.BallCarrier, new(side, 1), new(side * .5f, 1));
            line[side < 0 ? 1 : 3] = new(BlockingJob.Lead, new(-side * 1.5f, 4.5f), BlockingAnchor.BallCarrier, new(-side * 2, 1.5f));
        }
        var reads = new[] { new PassRead(target, wide ? .25f : .85f, 1, true) }
            .Concat(assignments.Where(p => p.Value.Role == AssignmentRole.Route && p.Key != target)
                .Select(p => new PassRead(p.Key, 1.8f, 0))).ToArray();
        return new($"pass.{formationId}.{(wide ? "wr" : "rb")}-screen", wide ? "WR Screen" : "RB Screen", PlayType.Pass, f, assignments,
            info: new(name, wide ? "WR Screen" : "RB Screen", PlayCategory.Screen,
                wide ? "Throw the perimeter screen behind the line; follow the outside blocks." : "Let the rush come, then throw behind the releasing linemen."),
            reads: reads, screen: new(target, release, catchOffset, line, skills));
    }
    private static PlayDefinition MovingPocket(string formationId, bool fake)
    {
        var (_, name, f) = ExpandedFormations.All.Single(f => f.Id == formationId);
        int side = 1;
        var assignments = f.AlignmentSlots.ToDictionary(s => s, s => new PlayerAssignment(AssignmentRole.Route,
            s.IsRunningBackSlot() ? RouteType.Flat : RouteType.Go, side,
            RouteDefinition: s.IsRunningBackSlot() ? Route(new Vector2(8, 2)) : Route(new Vector2(0, 24))));
        var flat = f.Personnel.Slots.Contains(ReceiverSlot.TE2) ? ReceiverSlot.TE2 : ReceiverSlot.TE1;
        var middle = ReceiverSlot.WR2;
        void Landmark(ReceiverSlot slot, Vector2 point, RouteType type)
        {
            int i = f.AlignmentSlots.ToList().IndexOf(slot); var start = f.Alignment.SkillPositions[i];
            assignments[slot] = new(AssignmentRole.Route, type, 1,
                RouteDefinition: new([new(new(0, MathF.Min(3 + start.Depth, point.Y + start.Depth))),
                    new(new(Constants.FieldWidth / 2 + point.X - start.XFraction * Constants.FieldWidth, point.Y + start.Depth))],
                    RouteFinish.Settle, compressToField: true, fieldDepthBudget: 28, originDepth: start.Depth));
        }
        Landmark(flat, new(12, 2), RouteType.Flat);
        Landmark(middle, new(17, 11), RouteType.OutDeep);
        if (flat != ReceiverSlot.TE1) Landmark(ReceiverSlot.TE1, new(15, 18), RouteType.OutDeep);
        if (f.Personnel.Slots.Contains(ReceiverSlot.FB)) assignments[ReceiverSlot.FB] = new(AssignmentRole.Block,
            Blocking: new(BlockingJob.PassProtection, new(side * 2, .2f), BlockingAnchor.Quarterback));
        var reads = new[] { new PassRead(middle, 1.8f, 2), new PassRead(flat, .8f, 1, true) }
            .Concat(assignments.Where(a => a.Value.Role == AssignmentRole.Route && a.Key != middle && a.Key != flat)
                .Select(a => new PassRead(a.Key, 1.5f, 0, a.Key.IsRunningBackSlot())));
        var line = f.Alignment.Linemen.Select(p => new BlockingAssignment(BlockingJob.PassProtection,
            new(side * 2, -2.5f - MathF.Abs(p.XFraction - .5f) * 10))).ToArray();
        line[^1] = new(BlockingJob.PassProtection, new(2, .5f), BlockingAnchor.Quarterback);
        return new($"pass.{formationId}.{(fake ? "bootleg" : "rollout")}", fake ? "PA Bootleg Right" : "Rollout Right", PlayType.Pass, f, assignments,
            lineBlocking: line, backfield: fake ? new(BackfieldAction.PlayAction, ReceiverSlot.RB1, new(-2, -.4f),
                approachTimeout: 2.2f, speedMultiplier: .9f) : null,
            info: new(name, fake ? "PA Bootleg" : "Rollout", PlayCategory.MovingPocket,
                fake ? "Fake left, boot right; read the sideline layers." : "Roll right and read the out over the flat."),
            reads: reads, protection: new(ProtectionMode.SlideRight),
            quarterbackPath: new([new(3, -4.5f), new(9, -4), new(12, -3.5f)]));
    }
    private static RouteDefinition Route(params Vector2[] points) => new(points.Select(p => new RouteStep(p)),
        compressToField: true, fieldDepthBudget: 28);
}
