using System.Numerics;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Input;
using RetroQB.Routes;

namespace RetroQB.Tests;

public sealed class PassProtectionMovementTests
{
    [Theory]
    [InlineData(-7.5f)]
    [InlineData(-5f)]
    [InlineData(5f)]
    [InlineData(7.5f)]
    public void ShiftedFrontGetsDistinctBlockersAndLiveContact(float shift)
    {
        const float los = 40;
        var play = PlayResolver.Resolve(PlaybookBuilder.BuildPassPlays()[0], false);
        var field = new FormationFactory().CreateFormation(play, los);
        float center = Constants.FieldWidth * .5f;
        var defenders = new List<Defender>();
        float[] offsets = [-5.3f, -2.1f, 2.1f, 5.3f];
        DefenderSlot[] slots = [DefenderSlot.DE1, DefenderSlot.DT1, DefenderSlot.DT2, DefenderSlot.DE2];
        for (int i = 0; i < offsets.Length; i++)
            defenders.Add(new Defender(new(center + shift + offsets[i], los + 1.8f),
                i is 0 or 3 ? DefensivePosition.DE : DefensivePosition.DL, slots[i])
                { IsRusher = true, RushLaneOffsetX = i < 2 ? -5 : 5 });

        void Step() => RetroQB.AI.OffensiveLinemanAI.UpdateBlockers(field.Blockers, defenders,
            play, los, 1f / 60, false, Clamp, field.Qb.Position, field.Qb.Position);
        Step();
        foreach (var defender in defenders)
            Assert.Single(field.Blockers, b => b.BlockingState.Target == defender);
        var matched = field.Blockers.OrderBy(b => b.Position.X).Where(b => b.BlockingState.Target != null).ToArray();
        Assert.Equal(defenders, matched.Select(b => b.BlockingState.Target));
        // Input enumeration must not decide which lineman gets a rusher.
        var reordered = PassProtectionPlanner.Assign(field.Blockers.AsEnumerable().Reverse().ToArray(),
            defenders.AsEnumerable().Reverse().ToArray(), false);
        Assert.All(matched, b => Assert.Same(b.BlockingState.Target, reordered[b]));

        var contacted = new HashSet<Defender>();
        for (int frame = 0; frame < 180; frame++)
        {
            foreach (var defender in defenders)
            {
                defender.IsBeingBlocked = false;
                defender.ActiveBlockersCount = 0;
                RetroQB.AI.DefenderTargeting.UpdateDefender(defender, field.Qb, field.Receivers,
                    field.Ball, 1f, 1f / 60, false, false, true, los);
            }
            Step();
            foreach (var defender in defenders.Where(d => d.IsBeingBlocked)) contacted.Add(defender);
        }
        Assert.Equal(defenders.Count, contacted.Count);

        defenders[0].IsRusher = false;
        Step();
        Assert.DoesNotContain(field.Blockers, b => b.BlockingState.Target == defenders[0]);
        foreach (var defender in defenders.Skip(1))
            Assert.Single(field.Blockers, b => b.BlockingState.Target == defender);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TacklesProtectBothEdgesWithAttachedTightEndAndLiveRush(bool flipped)
    {
        var definitions = PlaybookBuilder.BuildCatalog().Plays.Where(p => p.Family == PlayType.Pass);
        int formationsChecked = 0;
        foreach (var definition in definitions.DistinctBy(p => p.Formation.Type))
        {
            const float los = 40;
            var play = PlayResolver.Resolve(definition, flipped);
            var field = new FormationFactory().CreateFormation(play, los);
            if (!field.Receivers.Any(r => r.IsTightEnd && los - r.Position.Y <= 1.1f
                && MathF.Abs(r.Position.X - Constants.FieldWidth * .5f) <= 11.75f)) continue;
            formationsChecked++;
            var defense = new DefenseFactory().CreateDefense(
                new DefensiveContext(los, 10f, 1, 0, 0, SeasonStage.RegularSeason),
                new RetroQB.AI.DefensiveCallDecision(RetroQB.AI.CoverageScheme.Cover3Zone, BlitzDecision.None),
                default, field.Receivers, new Random(42));
            RouteAssigner.AssignRoutes(field.Receivers, play);
            var tackles = new[] { field.Blockers.MinBy(b => b.HomeX)!, field.Blockers.MaxBy(b => b.HomeX)! };
            var ends = new[] { defense.Defenders.Single(d => d.Slot == DefenderSlot.DE1),
                defense.Defenders.Single(d => d.Slot == DefenderSlot.DE2) };
            // Widen the front beside the TE and put a DT closer to each tackle.
            // Both tackles must still own the edge rather than double the inside rush.
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                ends[i].Position = new(tackles[i].HomeX + side * 4.5f, los + 1.8f);
                defense.Defenders.Single(d => d.Slot == (i == 0 ? DefenderSlot.DT1 : DefenderSlot.DT2))
                    .Position = new(tackles[i].HomeX - side, los + 1.8f);
            }
            RetroQB.AI.OffensiveLinemanAI.UpdateBlockers(field.Blockers, defense.Defenders, play,
                los, 1f / 60, false, Clamp, field.Qb.Position, field.Qb.Position);
            for (int i = 0; i < 2; i++)
            {
                Assert.Same(ends[i], tackles[i].BlockingState.Target);
                Assert.True(tackles[i].Velocity.Y < 0, definition.Name);
            }
            bool[] contacted = new bool[2];
            for (int frame = 0; frame < 150; frame++)
            {
                foreach (var defender in defense.Defenders)
                {
                    defender.IsBeingBlocked = false;
                    defender.ActiveBlockersCount = 0;
                    RetroQB.AI.DefenderTargeting.UpdateDefender(defender, field.Qb, field.Receivers,
                        field.Ball, 1f, 1f / 60, false, false, true, los);
                }
                RetroQB.AI.OffensiveLinemanAI.UpdateBlockers(field.Blockers, defense.Defenders, play,
                    los, 1f / 60, false, Clamp, field.Qb.Position, field.Qb.Position);
                for (int i = 0; i < 2; i++)
                    contacted[i] |= ends[i].IsBeingBlocked && tackles[i].BlockingState.Target == ends[i];
            }
            Assert.All(contacted, contact => Assert.True(contact, definition.Name));
        }
        Assert.True(formationsChecked > 0);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(6, false)]
    [InlineData(6, true)]
    public void BackHoldsPocketWhileQuarterbackDropsBackAndStrafes(int playIndex, bool flipped)
    {
        var manager = new PlayManager();
        manager.SelectPassPlay(playIndex, new Random(1));
        if (flipped) manager.FlipSelectedPlay();
        var field = new FormationFactory().CreateFormation(manager.SelectedPlay, manager.LineOfScrimmage);
        RouteAssigner.AssignRoutes(field.Receivers, manager.SelectedPlay);
        var rb = field.Receivers.Single(r => r.Slot == ReceiverSlot.RB1);
        var pocket = rb.Position;
        var qbStart = field.Qb.Position;
        var input = new MovementInput();
        var execution = new PlayExecutionController(input, new BlockingController());
        var overlap = new OverlapResolver();
        float maxRbMovement = 0;
        foreach (var direction in new[] { Vector2.Zero, -Vector2.UnitY, Vector2.UnitX, -Vector2.UnitX })
        {
            input.Direction = direction;
            for (int frame = 0; frame < 60; frame++)
            {
                execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], field.Blockers,
                    manager, false, false, false, Clamp, 1f / 60);
                overlap.ResolveOverlaps(field.Qb, field.Ball, field.Receivers, field.Blockers, [],
                    manager.LineOfScrimmage, Clamp, execution.Backfield);
                maxRbMovement = MathF.Max(maxRbMovement, Vector2.Distance(pocket, rb.Position));
            }
        }
        Assert.True(Vector2.Distance(qbStart, field.Qb.Position) > 3);
        Assert.InRange(maxRbMovement, 0, .1f);
        Assert.True(rb.IsBlocking);
        Assert.False(rb.Eligible);

        // A defender entering protection still gets picked up after the QB has moved.
        var threat = new Defender(rb.Position + new Vector2(flipped ? -2 : 2, 1),
            DefensivePosition.LB, DefenderSlot.MLB) { IsRusher = true };
        var receiverController = new ReceiverUpdateController(new BlockingController());
        var targetStart = threat.Position;
        receiverController.UpdateAll(field.Receivers, field.Qb, field.Ball, [threat], null,
            Vector2.Zero, false, false, false, manager, 1f / 60, Clamp, execution.Backfield, field.Blockers);
        Assert.Same(threat, rb.BlockingState.Target);
        Assert.True(Vector2.Dot(rb.Velocity, targetStart - pocket) > 0);
        Assert.True(Vector2.Distance(threat.Position, pocket) > Vector2.Distance(targetStart, pocket));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void TacklePursuesAndReengagesAnEdgeRusherAfterLosingContact(int side)
    {
        var manager = new PlayManager();
        manager.SelectPassPlay(0, new Random(1));
        const float los = 40;
        float homeX = Constants.FieldWidth * .5f + side * 5;
        var job = BlockingPlanner.PassProtection(homeX);
        var qb = new Vector2(Constants.FieldWidth * .5f, los - 10);
        var anchor = BlockingSteering.GetLandmarks(job, homeX, los, qb)[0];
        var tackle = new Blocker(new(homeX, los)) { BlockingAssignment = job, OpeningAssignment = job };
        tackle.Position = anchor;
        var edge = new Defender(anchor + new Vector2(side, 1), DefensivePosition.DE, DefenderSlot.MLB)
            { IsRusher = true };
        void Step() => RetroQB.AI.OffensiveLinemanAI.UpdateBlockers([tackle], [edge], manager.SelectedPlay,
            los, 1f / 60, false, Clamp, qb, qb);

        Step();
        Assert.True(edge.IsBeingBlocked);
        Assert.Same(edge, tackle.BlockingState.Target);

        // The end rounds the tackle, leaving both contact range and the set point.
        tackle.Position = anchor + new Vector2(side * 2, -2);
        edge.Position = tackle.Position + new Vector2(side, -3.5f);
        edge.IsBeingBlocked = false;
        float separation = Vector2.Distance(tackle.Position, edge.Position);
        Step();
        Assert.True(Vector2.Distance(tackle.Position, edge.Position) < separation);
        Assert.True(tackle.Velocity.Y < 0);
        for (int frame = 0; frame < 120 && !edge.IsBeingBlocked; frame++) Step();
        Assert.True(edge.IsBeingBlocked);

        // Keep recovering beyond the original landmark's retention radius.
        tackle.Position = new(homeX, los - 9);
        edge.Position = new(homeX, los - 12);
        Step();
        Assert.Same(edge, tackle.BlockingState.Target);
        Assert.True(tackle.Velocity.Y < 0);

        // Stop chasing a defender who leaves the pocket.
        edge.Position = new(2, los + 20);
        Step();
        Assert.Null(tackle.BlockingState.Target);
        Assert.True(tackle.Velocity.Y > 0);
    }
    private sealed class MovementInput : IPlayerMovementInput
    {
        public Vector2 Direction { get; set; }
        public Vector2 GetMovementDirection() => Direction;
        public bool IsSprintHeld() => false;
    }

    private static void Clamp(Entity entity) => entity.Position = new(
        Math.Clamp(entity.Position.X, entity.Radius, Constants.FieldWidth - entity.Radius),
        Math.Clamp(entity.Position.Y, entity.Radius, Constants.FieldLength - entity.Radius));
}
