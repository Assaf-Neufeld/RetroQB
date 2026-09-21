using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Replay;
using RetroQB.Development;

namespace RetroQB.Tests;

public sealed class TeamBalanceTests
{
    [Theory]
    [InlineData("ballers")] [InlineData("lightning")] [InlineData("bulldozers")]
    [InlineData("phantoms")] [InlineData("cyclones")] [InlineData("ironclad")]
    [InlineData("firebirds")] [InlineData("mustangs")] [InlineData("bombers")]
    [InlineData("sentinels")] [InlineData("sharks")] [InlineData("vipers")]
    [InlineData("golden-legion")]
    public void EveryTeamCompletesLiveRunAndPassOnBothSidesAgainstEveryRound(string teamId)
    {
        var user = TeamCatalog.Get(teamId);
        foreach (var stage in Enum.GetValues<SeasonStage>())
        foreach (bool defending in new[] { false, true })
        foreach (string call in new[] { "pass.mesh", "run.hb-dive" })
        {
            var cpu = TeamCatalog.ForStage(stage);
            var input = new ScriptedInput();
            var match = new TimedMatch(user, cpu, defending ? cpu.Id : user.Id, stage, new(30));
            var drive = new DefensiveDrive(input, 101, fixedCall: call, match: match);
            Assert.True(drive.Snap());
            for (int tick = 0; tick < 1800 && drive.Live; tick++)
            {
                var delta = defending
                    ? (drive.Actors.Ball.Holder?.Position ?? drive.Actors.Ball.Position) - drive.Linebacker.Position
                    : call.StartsWith("run.") || drive.Actors.Ball.State == BallState.HeldByReceiver ? Vector2.UnitY : Vector2.Zero;
                input.Frame = new(Movement: delta.LengthSquared() > .01f ? Vector2.Normalize(delta) : Vector2.Zero,
                    Sprint: true, ThrowTarget: !defending && drive.LiveSeconds > .8f ? 0 : null);
                drive.Update(1f / 60);
            }
            Assert.False(drive.Live, $"{teamId}, {stage}, defending={defending}, {call} did not finish");
            Assert.Single(match.Match.History);
            Assert.Equal(defending ? cpu.Id : user.Id, drive.LastResult!.Event.OffenseId);
            Assert.All(drive.Players, p => Assert.True(float.IsFinite(p.Position.X) && float.IsFinite(p.Position.Y)));
        }
    }

    [Fact]
    public void StandardTeamsHaveComparableBudgetsAndDefensiveStars()
    {
        var teams = TeamCatalog.Selectable.Where(t => t.Id != "golden-legion").ToArray();
        Assert.Equal(12, teams.Length);
        Assert.Equal(70, teams.Min(t => t.Offense.TeamScore));
        Assert.Equal(91, teams.Max(t => t.Offense.TeamScore));
        foreach (var team in teams)
        {
            Assert.True(CountOffensiveStars(team) + team.Defense.Roster.Defenders.Count(p => p.Value.IsStarPlayer) >= 2);
            Assert.Contains(team.Defense.Roster.Defenders, player => player.Value.IsStarPlayer);
            Assert.Equal(12, team.Defense.Roster.Defenders.Count);
            Assert.Equal(TeamDefinition.CalculateScore(team.Offense.Skills, team.Defense), team.Offense.TeamScore);
        }
        Assert.Equal(2, CountOffensiveStars(TeamCatalog.Get("lightning")));
        Assert.Equal(0, CountOffensiveStars(TeamCatalog.Get("phantoms")));
        Assert.Equal(1, CountOffensiveStars(TeamCatalog.Get("mustangs")));
        Assert.True(TeamCatalog.Get("vipers").Offense.TeamScore > TeamCatalog.Get("sharks").Offense.TeamScore);
        Assert.Equal(90, TeamCatalog.Get("golden-legion").Offense.TeamScore);
    }

    [Fact]
    public void DefensiveSpecialistsTradeOffAgainstOffensiveSpecialists()
    {
        var lightning = TeamCatalog.Get("lightning");
        var ironclad = TeamCatalog.Get("ironclad");
        var phantoms = TeamCatalog.Get("phantoms");
        Assert.True(lightning.OffenseScore > ironclad.OffenseScore);
        Assert.True(ironclad.DefenseScore > lightning.DefenseScore);
        Assert.True(ironclad.Defense.TackleAbility > phantoms.Defense.TackleAbility);
        Assert.True(phantoms.Defense.CoverageTightness > ironclad.Defense.CoverageTightness);
        Assert.True(TeamCatalog.Get("bombers").Defense.PassRushAbility > phantoms.Defense.PassRushAbility);
        Assert.True(phantoms.Defense.CoverageTightness > TeamCatalog.Get("bombers").Defense.CoverageTightness);
    }

    [Fact]
    public void StarsHaveRealIndividualAdvantagesAndBackupsDoNotInheritDesignation()
    {
        var team = TeamCatalog.Get("mustangs");
        var roster = team.Offense.Roster;
        Assert.True(roster.GetReceiverSpeed(ReceiverSlot.RB1) > roster.GetReceiverSpeed(ReceiverSlot.RB2));
        Assert.True(roster.GetRbTackleBreakChance(ReceiverSlot.RB1) > roster.GetRbTackleBreakChance(ReceiverSlot.RB2));
        Assert.True(new Receiver(0, ReceiverSlot.RB1, Vector2.Zero, team.Offense).IsStarPlayer);
        Assert.False(new Receiver(0, ReceiverSlot.RB2, Vector2.Zero, team.Offense).IsStarPlayer);
        Assert.False(TeamCatalog.Get("firebirds").Offense.Roster.IsStarPlayer(ReceiverSlot.TE2));
        var star = new Defender(Vector2.Zero, DefensivePosition.LB, DefenderSlot.MLB, team.Defense);
        var regular = new Defender(Vector2.Zero, DefensivePosition.LB, DefenderSlot.OLB2, team.Defense);
        Assert.True(star.IsStarPlayer);
        Assert.True(star.Speed > regular.Speed);
        Assert.True(star.TackleMultiplier > regular.TackleMultiplier);
        Assert.True(star.BlockShedMultiplier > regular.BlockShedMultiplier);
    }

    [Fact]
    public void ResolvedStarsKeepRatingsExactlyOnceAcrossStagesAndPersonnel()
    {
        var catalog = new PlayManager().Catalog;
        foreach (var team in TeamCatalog.Selectable.Concat(TeamCatalog.Opponents))
        foreach (var stage in Enum.GetValues<SeasonStage>())
        foreach (var definition in catalog.Plays.GroupBy(p => p.Formation).Select(g => g.First()))
        {
            var attrs = TeamDifficulty.ScaleDefense(team.Defense, stage);
            var formation = new FormationFactory().CreateFormation(PlayResolver.Resolve(definition), 30, team.Offense);
            var resolved = DefensivePlayResolver.Resolve(DefensivePlaybook.All[0], VisibleOffense.From(formation),
                new(30, 10, 1, 0, 0, stage), attrs, new Random(17));
            foreach (var defender in resolved.Mirror().CreateDefense(attrs).Defenders)
            {
                var direct = new Defender(Vector2.Zero, defender.PositionRole, defender.Slot, attrs);
                Assert.Equal(direct.IsStarPlayer, defender.IsStarPlayer);
                Assert.Equal(direct.Speed, defender.Speed);
                Assert.Equal(direct.TackleMultiplier, defender.TackleMultiplier);
                Assert.Equal(direct.InterceptionMultiplier, defender.InterceptionMultiplier);
                Assert.Equal(direct.BlockShedMultiplier, defender.BlockShedMultiplier);
            }
        }
    }

    [Fact]
    public void LateRoundOpponentsKeepTheirWeaknessesAndDifferentOffenses()
    {
        var rush = TeamCatalog.Get("crimson-rush");
        var bastion = TeamCatalog.Get("bloodline-bastion");
        var scaled = TeamDifficulty.ScaleDefense(bastion.Defense, SeasonStage.SuperBowl);
        Assert.Equal(bastion.Defense.DbSpeed, scaled.DbSpeed);
        Assert.True(scaled.CoverageTightness < scaled.TackleAbility);
        Assert.True(rush.Offense.GetReceiverSpeed(ReceiverSlot.WR1) > bastion.Offense.GetReceiverSpeed(ReceiverSlot.WR1));
        Assert.True(bastion.Offense.BlockingStrength > rush.Offense.BlockingStrength);
        Assert.True(bastion.Offense.GetRbTackleBreakChance(ReceiverSlot.RB1) > rush.Offense.GetRbTackleBreakChance(ReceiverSlot.RB1));
        foreach (var opponent in TeamCatalog.Opponents)
        {
            Assert.True(CountOffensiveStars(opponent) > 0);
            Assert.Contains(opponent.Defense.Roster.Defenders, p => p.Value.IsStarPlayer);
            Assert.NotEqual("QB", opponent.Offense.Roster.Quarterback.Name);
        }
    }

    [Fact]
    public void CpuCallingReflectsTeamPreferenceAndStillRespondsToDownAndDistance()
    {
        int PassCount(string team, int down = 1, float distance = 10)
        {
            var coordinator = new OffensiveCoordinator(new Random(721));
            var tendencies = TeamCatalog.Get(team).Tendencies;
            return Enumerable.Range(0, 1000).Count(_ => OffensiveCoordinator.Passes.Contains(
                coordinator.Select(new(30, down, distance), tendencies)));
        }
        Assert.InRange(PassCount("crimson-rush"), 670, 770);
        Assert.InRange(PassCount("bloodline-bastion"), 310, 410);
        Assert.True(PassCount("bloodline-bastion", 3) > PassCount("bloodline-bastion"));
        Assert.True(PassCount("crimson-rush", 2, 1) < PassCount("crimson-rush"));
    }

    [Fact]
    public void AssignmentSkillsAffectMovementWithoutBuffingBallCarrierPursuit()
    {
        float Move(float coverage, float rush, bool rusher, bool runner)
        {
            var qb = new Quarterback(new(20, 25));
            var receiver = new Receiver(0, ReceiverSlot.WR1, new(20, 35));
            var ball = new Ball(qb.Position); ball.SetHeld(qb, BallState.HeldByQB);
            var defender = new Defender(new(15, 35), DefensivePosition.DB, DefenderSlot.CB1,
                new() { CoverageTightness = coverage, PassRushAbility = rush })
                { IsRusher = rusher, CoverageReceiverIndex = 0 };
            DefenderTargeting.UpdateDefender(defender, qb, [receiver], ball, 1, .1f, runner, false, false, 30);
            return defender.Velocity.Length();
        }
        Assert.True(Move(1.2f, 1, false, false) > Move(.8f, 1, false, false));
        Assert.True(Move(1, 1.2f, true, false) > Move(1, .8f, true, false));
        Assert.Equal(Move(1.2f, 1.2f, true, true), Move(.8f, .8f, true, true));
    }

    [Fact]
    public void ReplayPreservesStarsOnBothSides()
    {
        var team = TeamCatalog.Get("ballers");
        var qb = new Quarterback(new(25, 25), team.Offense);
        var defender = new Defender(new(25, 35), DefensivePosition.LB, DefenderSlot.MLB, team.Defense);
        var recorder = new ReplayRecorder(); recorder.Begin(1);
        recorder.Capture(qb, new Ball(qb.Position), [], [], [defender], 30, 40, .1f);
        var frame = recorder.FinalizeClip(PlayOutcome.Incomplete)!.Frames[0];
        Assert.True(frame.Quarterback.IsStarPlayer);
        Assert.True(frame.Defenders[0].IsStarPlayer);
    }

    private static int CountOffensiveStars(TeamDefinition team) =>
        (team.Offense.Roster.Quarterback.IsStarPlayer ? 1 : 0)
        + Enum.GetValues<ReceiverSlot>().Count(team.Offense.Roster.IsStarPlayer);
}
