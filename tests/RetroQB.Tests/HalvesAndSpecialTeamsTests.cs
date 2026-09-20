using System.Numerics;
using RetroQB.AI;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Development;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class HalvesAndSpecialTeamsTests
{
    private static readonly TeamDefinition User = TeamCatalog.Get("ballers");
    private static readonly TeamDefinition Cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
    private static TimedMatch Match(string? opener = null) => new(User, Cpu, opener ?? User.Id,
        quarterSeconds: 120, regulationPeriods: 2, specialTeams: true);

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void KickerJoinsCoverageAfterReleaseAndCanTackle(bool kickoff, bool receiving)
    {
        var kick = new SpecialTeamsPlay(kickoff, 35, new KickRollRandom(1));
        var initial = kick.Kicker.Position;
        kick.Update(1, Vector2.Zero, false, receiving);
        Assert.Equal(initial, kick.Kicker.Position); // Setup remains stationary.
        kick.Start();
        for (int tick = 0; kick.Phase == SpecialTeamsPhase.Snap && tick < 100; tick++)
            kick.Update(1f / 120, Vector2.Zero, false, receiving);
        Assert.Equal(SpecialTeamsPhase.Flight, kick.Phase);
        var released = kick.Kicker.Position;
        kick.Update(.3f, Vector2.Zero, false, receiving);
        Assert.True(kick.Kicker.Position.Y > released.Y + 1);
        Assert.True(kick.Kicker.Velocity.Length() > 0);
        for (int tick = 0; kick.Phase == SpecialTeamsPhase.Flight && tick < 500; tick++)
            kick.Update(1f / 120, Vector2.Zero, false, receiving);
        Assert.Equal(SpecialTeamsPhase.Return, kick.Phase);
        // Isolate the kicker as the only tackler near the returner.
        kick.Returner.Position = new(25, 65);
        kick.Kicker.Position = new(25, 64);
        for (int i = 0; i < kick.Coverage.Count; i++) kick.Coverage[i].Position = new(3 + i * 4, 20);
        for (int i = 0; i < kick.Blockers.Count; i++) kick.Blockers[i].Position = new(3 + i * 4, 90);
        kick.Update(1f / 120, Vector2.Zero, false, receiving);
        Assert.Equal(SpecialTeamsPhase.Result, kick.Phase);
        Assert.NotNull(kick.Result);
        Assert.False(kick.Result!.Touchdown);
        Assert.False(kick.Result.Touchback);
        Assert.InRange(kick.Result.ReceivingYard, 44, 46);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(50)]
    public void PuntHasElevenPerTeamWithSevenOnLineAndThreeProtectors(float yard)
    {
        var punt = new SpecialTeamsPlay(false, yard, new Random(101));
        Assert.Equal(11, punt.Coverage.Count + 1);
        Assert.Equal(11, punt.Blockers.Count + 1);
        Assert.Equal(7, punt.Coverage.Count(p => p.Position.Y == punt.LineOfScrimmage));
        Assert.Equal(3, punt.Coverage.Count(p => p.Position.Y < punt.LineOfScrimmage));
        var players = punt.Coverage.Append(punt.Kicker).Concat(punt.Blockers).Append(punt.Returner).ToArray();
        Assert.Equal(22, players.Select(p => p.Position).Distinct().Count());
        Assert.All(players, p =>
        {
            Assert.InRange(p.Position.X, 0, Constants.FieldWidth);
            Assert.InRange(p.Position.Y, 0, Constants.FieldLength);
        });
        punt.Start();
        for (int tick = 0; tick < 40; tick++) punt.Update(1f / 60, Vector2.Zero, false, true);
        Assert.Equal(SpecialTeamsPhase.Flight, punt.Phase);
    }

    [Fact]
    public void KickoffsHaveElevenPlayersOnEachSideAndUsuallyAllowAReturn()
    {
        int returns = 0, touchbacks = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            var kick = new SpecialTeamsPlay(true, 35, new Random(seed));
            Assert.Equal(11, kick.Coverage.Count + 1);
            Assert.Equal(11, kick.Blockers.Count + 1);
            var players = kick.Coverage.Append(kick.Kicker).Concat(kick.Blockers).Append(kick.Returner).ToArray();
            Assert.Equal(22, players.Select(p => p.Position).Distinct().Count());
            Assert.All(players, p => Assert.InRange(p.Position.X, 0, Constants.FieldWidth));
            kick.Start();
            for (int tick = 0; kick.Phase is SpecialTeamsPhase.Snap or SpecialTeamsPhase.Flight && tick < 300; tick++)
                kick.Update(1f / 60, Vector2.Zero, false, true);
            if (kick.Phase == SpecialTeamsPhase.Return) returns++;
            else if (kick.Result?.Touchback == true) touchbacks++;
        }
        Assert.InRange(returns, 70, 90);
        Assert.Equal(100, returns + touchbacks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefenseGetsSixSecondsEvenInHurryUpUnlessSpaceIsPressed(bool ready)
    {
        var m = new TimedMatch(User, Cpu, Cpu.Id, quarterSeconds: 30, regulationPeriods: 2);
        m.Clock.StartPeriod(2); m.Continue();
        var s = new FullMatchSession(new ScriptedInput(), 101, m);
        s.Update(5.9f);
        Assert.False(s.Drive.Live);
        s.Update(ready ? 0 : .11f, new(Ready: ready));
        Assert.True(s.Drive.Live);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KickBlockingContactKeepsPlayersApart(bool kickoff)
    {
        var kick = new SpecialTeamsPlay(kickoff, 35, new Random(101));
        kick.Start(); kick.Update(.7f, Vector2.Zero, false, true);
        var coverage = kick.Coverage[0]; var blocker = kick.Blockers[0];
        coverage.Position = new(20, 55); blocker.Position = new(20, 57);
        for (int frame = 0; frame < 10; frame++)
        {
            kick.Update(.1f, Vector2.Zero, false, true);
            Assert.True(Vector2.Distance(coverage.Position, blocker.Position) >= 1.79f);
            Assert.True(coverage.Position.Y < blocker.Position.Y);
        }
    }

    [Fact]
    public void LiveKickoffTouchdownCanBeAcknowledgedAndFollowedByAnotherKickoff()
    {
        // Seed 100 selects a returnable kick under the explicit five-outcome roll.
        var s = new FullMatchSession(new ScriptedInput(), 100, Match());
        s.Update(0, new(Ready: true));
        var kick = s.SpecialTeams!;
        for (int tick = 0; kick.Phase is SpecialTeamsPhase.Snap or SpecialTeamsPhase.Flight && tick < 300; tick++)
            s.Update(1f / 60);
        Assert.Equal(SpecialTeamsPhase.Return, kick.Phase);
        kick.Returner.Position = new(25, 9);
        s.Update(1f / 60);
        Assert.Equal(7, s.Match.User.Score);
        Assert.Contains("TOUCHDOWN", CompletedDriveSummary.From(s)!.Outcome);
        s.Update(0, new(Ready: true));
        Assert.Equal(MatchAction.Kickoff, s.Action);
        Assert.False(s.UserReceivingKick);
        s.Update(0, new(Ready: true));
        Assert.True(s.Drive.Live);
    }

    private sealed class KickRollRandom(int roll) : Random
    {
        public override int Next(int maxValue) => roll;
        public override double NextDouble() => .5;
    }

    [Fact]
    public void ExactlyOneOfFiveKickRollsIsATouchback()
    {
        int touchbacks = 0;
        for (int roll = 0; roll < 5; roll++)
        {
            var kick = new SpecialTeamsPlay(true, 35, new KickRollRandom(roll));
            kick.Start();
            for (int tick = 0; kick.Phase is SpecialTeamsPhase.Snap or SpecialTeamsPhase.Flight; tick++)
                kick.Update(1f / 60, Vector2.Zero, false, true);
            if (kick.Result?.Touchback == true) touchbacks++;
            else Assert.Equal(SpecialTeamsPhase.Return, kick.Phase);
        }
        Assert.Equal(1, touchbacks);
    }

    [Fact]
    public void QuarterbackDropsAndSlidesEvenWithoutClosePressure()
    {
        var ai = new QuarterbackAI();
        var read = new ReceiverRead(0, new(42, 35), Vector2.UnitY, true, false, 15);
        var view = new QuarterbackObservation(new(25, 25), 30, true, [read], []);
        var drop = ai.Decide(view, .2f);
        Assert.True(drop.PocketMovement); Assert.True(drop.Movement.Y < 0);
        var slide = ai.Decide(view with { Position = new(25, 22) }, .6f);
        Assert.True(slide.PocketMovement); Assert.True(slide.Movement.X > 0); Assert.Equal(0, slide.Movement.Y);
    }

    [Theory]
    [InlineData(10, 10, MatchAction.Punt)]
    [InlineData(10, 2, MatchAction.Scrimmage)]
    [InlineData(20, 10, MatchAction.Scrimmage)]
    [InlineData(40, 8, MatchAction.Scrimmage)]
    public void CpuPuntsOnlyWhenPinnedBackWithLongYardage(float yard, float distance, MatchAction expected)
        => Assert.Equal(expected, MatchStrategy.Choose(new(2, 100, false, 0, 4, yard, distance, 3, 20, false)));

    [Fact]
    public void TwoMinuteHalvesResetTimeoutsAtHalftimeAndFinishAfterSecondHalf()
    {
        var m = Match();
        m.Clock.TryTimeout(User.Id);
        var p = m.Advance(0, "fixture")!; m.Advance(120);
        m.Resolve(new(p.Id, p.OffenseId, PlayEndReason.Kneel, 19));
        Assert.Equal(2, m.Clock.Quarter); Assert.Equal(120, m.Clock.RemainingSeconds);
        Assert.Equal(3, m.Clock.Timeouts(User.Id)); Assert.Equal(Cpu.Id, m.KickoffReceiverId);
        m.Continue(); m.Match.User.Score = 7;
        p = m.Advance(0, "fixture")!; m.Advance(120);
        m.Resolve(new(p.Id, p.OffenseId, PlayEndReason.Kneel, 19));
        Assert.True(m.Finished); Assert.Equal(User.Id, m.WinnerId);
    }

    [Fact]
    public void TiedSecondHalfStartsPairedOvertimeWithoutKickoff()
    {
        var m = Match(); m.Clock.StartPeriod(2); m.Continue();
        var p = m.Advance(0, "fixture")!; m.Advance(120);
        m.Resolve(new(p.Id, p.OffenseId, PlayEndReason.Kneel, 19));
        Assert.True(m.Clock.IsOvertime); Assert.Null(m.KickoffReceiverId); Assert.Equal(75, m.Match.Series.OwnYardLine);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void OpeningKickoffPausesAndSetsReceivingPossessionExactlyOnce(bool userReceives)
    {
        var input = new ScriptedInput { Frame = new(Movement: -Vector2.UnitY, Sprint: true) };
        var s = new FullMatchSession(input, 101, Match(userReceives ? User.Id : Cpu.Id));
        Assert.Equal(MatchAction.Kickoff, s.Action); Assert.Equal(userReceives, s.UserReceivingKick);
        s.Update(0, new(Ready: true)); Assert.True(s.Drive.Live);
        s.Update(.2f); var position = s.SpecialTeams!.BallPosition; var clock = s.Clock.RemainingSeconds;
        s.Update(5, new(Pause: true)); Assert.Equal(position, s.SpecialTeams.BallPosition); Assert.Equal(clock, s.Clock.RemainingSeconds);
        s.Update(0, new(Pause: true));
        for (int tick = 0; s.Drive.Live && tick < 1800; tick++) s.Update(1f / 60);
        Assert.False(s.Drive.Live); Assert.Single(s.Match.History);
        Assert.Equal(PlayEndReason.Kickoff, s.Drive.LastResult!.Event.Reason);
        Assert.NotNull(s.Drive.LastResult.Event.KickReturn);
        Assert.Equal(0, s.Match.User.Stats.Qb.Attempts + s.Match.Opponent.Stats.Qb.Attempts);
        Assert.Equal(0, s.Match.User.Stats.Rb.Attempts + s.Match.Opponent.Stats.Rb.Attempts);
        s.Update(2); Assert.Single(s.Match.History); // Result waits for acknowledgment.
        s.Update(0, new(Ready: true));
        Assert.Equal(userReceives ? User.Id : Cpu.Id, s.Match.PossessionId);
        Assert.Null(s.SpecialTeams); Assert.Equal(1, s.Match.Series.Down);
        s.Restart(); Assert.Equal(MatchAction.Kickoff, s.Action); Assert.Empty(s.Match.History);
    }

    [Fact]
    public void ScoreSchedulesKickoffButHalftimeOverridesNormalReceiver()
    {
        var m = Match(); var p = m.Advance(0, "score")!;
        m.Resolve(new(p.Id, p.OffenseId, PlayEndReason.Touchdown, 100));
        Assert.Equal(Cpu.Id, m.KickoffReceiverId);
        m.Continue(); m.PrepareKickoff(); Assert.Equal(User.Id, m.Match.PossessionId); Assert.Null(m.KickoffReceiverId);
        // On a scoring final play of H1, the halftime receiver always wins.
        p = m.Advance(0, "score-at-half")!; m.Advance(120);
        m.Resolve(new(p.Id, p.OffenseId, PlayEndReason.Safety, 0));
        Assert.Equal(Cpu.Id, m.KickoffReceiverId); Assert.Equal(2, m.Clock.Quarter);
    }

    [Theory]
    [InlineData(PlayEndReason.Punt)] [InlineData(PlayEndReason.Kickoff)]
    public void ReturnTouchdownScoresReceivingTeamAndCreatesCorrectNextKickoff(PlayEndReason reason)
    {
        var m = Match(); m.Match.StartPossession(Cpu.Id, new(10, 4, 10));
        var p = m.Advance(0, "special")!;
        var e = new PlayEnded(p.Id, Cpu.Id, reason, 0, KickReturn: new(100, false, true, 80));
        var result = m.Resolve(e); Assert.Same(result, m.Resolve(e));
        Assert.Equal(7, m.Match.User.Score); Assert.Equal(0, m.Match.Opponent.Score);
        Assert.Equal(Cpu.Id, m.KickoffReceiverId); Assert.Single(m.Match.History);
    }

    [Fact]
    public void KickTouchbackAndReturnUseReceivingYardsAndRejectInvalidResults()
    {
        var p = new PlayStart(1, Cpu.Id, "punt", new(15, 4, 10));
        var e = new PlayEnded(1, Cpu.Id, PlayEndReason.Punt, 80, KickReturn: new(20, true, false, 0));
        Assert.Equal(20, MatchRules.Resolve(p, e, User.Id).NextPossession!.Series.OwnYardLine);
        Assert.Equal(34, MatchRules.Resolve(p, e with { KickReturn = new(34, false, false, 15) }, User.Id).NextPossession!.Series.OwnYardLine);
        Assert.Throws<ArgumentException>(() => MatchRules.Resolve(p, e with { KickReturn = new(float.NaN, false, false, 0) }, User.Id));
        Assert.Throws<ArgumentException>(() => MatchRules.Resolve(p, e with { KickReturn = new(30, true, false, 0) }, User.Id));
    }
    [Theory]
    [InlineData(30)] [InlineData(60)] [InlineData(120)]
    public void SpecialTeamsSeedSweepTerminatesFromContactOrGoalAtMultipleFrameRates(int hz)
    {
        for (int seed = 0; seed < 10; seed++)
        foreach (bool kickoff in new[] { false, true })
        foreach (bool receiving in new[] { false, true })
        {
            var play = new SpecialTeamsPlay(kickoff, kickoff ? 35 : 15, new Random(seed));
            play.Start();
            for (int tick = 0; play.Result == null && tick < hz * 30; tick++)
            {
                var movement = receiving ? -Vector2.UnitY : play.Returner.Position - play.Coverage[play.ControlledCoverageIndex].Position;
                play.Update(1f / hz, movement, true, receiving);
                Assert.True(float.IsFinite(play.BallPosition.X) && float.IsFinite(play.BallPosition.Y));
            }
            Assert.True(play.Result != null, $"seed={seed}, kickoff={kickoff}, receiving={receiving}, hz={hz}");
            Assert.InRange(play.Result!.ReceivingYard, 0, 100);
        }
    }

    [Fact]
    public void ReturnIntoOwnEndZoneScoresSafetyForKickingTeam()
    {
        var p = new PlayStart(1, Cpu.Id, "kickoff", new(35));
        var e = new PlayEnded(1, Cpu.Id, PlayEndReason.Kickoff, 100, KickReturn: new(0, false, false, -10, true));
        var result = MatchRules.Resolve(p, e, User.Id);
        Assert.Equal(2, result.Points); Assert.Equal(Cpu.Id, result.ScoringTeamId);
        Assert.Equal(Cpu.Id, result.NextPossession!.TeamId);
    }
}
