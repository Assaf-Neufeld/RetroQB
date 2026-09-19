using RetroQB.Core;
using RetroQB.AI;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.Tests;

public sealed class MatchRulesTests
{
    internal static MatchState Create(bool opponent = false, DriveStart? start = null)
    {
        var user = TeamCatalog.Get("ballers");
        var cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        return new(user, cpu, opponent ? cpu.Id : user.Id, start: start);
    }

    internal static PlayResolution End(MatchState match, PlayEndReason reason, float spot,
        OffensivePlayStats? stats = null, string call = "test.call")
    {
        var play = match.BeginPlay(call);
        return match.Resolve(new(play.Id, play.OffenseId, reason, spot, stats));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RULE01_TouchdownCreditsOnlyOffenseAndQueuesOtherTeam(bool cpu)
    {
        var match = Create(cpu);
        var offense = match.Offense; var defense = match.Defense;
        var result = End(match, PlayEndReason.Touchdown, 103);
        Assert.Equal(7, offense.Score); Assert.Equal(0, defense.Score);
        Assert.Equal(80, result.Gain);
        Assert.Equal(defense.Definition.Id, match.PendingPossession!.TeamId);
        Assert.Equal(20, match.PendingPossession.Series.OwnYardLine);
        Assert.Same(offense, match.Offense); // Summary keeps the completed possession until continuation.
        Assert.True(match.ContinuePossession());
        Assert.False(match.ContinuePossession());
        Assert.Same(defense, match.Offense);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RULE02_FailedFourthDownKeepsOriginalGainAndFlipsSpot(bool cpu)
    {
        var match = Create(cpu, new(33, 4, 5));
        var result = End(match, PlayEndReason.Tackle, 35);
        Assert.Equal(0, match.User.Score + match.Opponent.Score);
        Assert.True(result.TurnoverOnDowns);
        Assert.Equal(PlayEndReason.Tackle, result.Event.Reason);
        Assert.Equal(2, result.Gain);
        Assert.Equal(35, result.Event.Spot);
        match.ContinuePossession();
        Assert.Equal(new DriveStart(65), match.Series);
    }

    [Theory]
    [InlineData(false, 70, 30)] [InlineData(true, 70, 30)]
    [InlineData(false, 105, 20)] [InlineData(true, 105, 20)]
    public void RULE03_InterceptionUsesContactSpotOrTouchback(bool cpu, float contact, float next)
    {
        var match = Create(cpu);
        var result = End(match, PlayEndReason.Interception, contact, new(PassAttempt: true));
        Assert.Equal(0, match.User.Score + match.Opponent.Score);
        Assert.Equal(1, match.Offense.Stats.Qb.Interceptions);
        Assert.Equal(next, result.NextPossession!.Series.OwnYardLine);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RULE04_SafetyAwardsDefenseTwoAndTheBall(bool cpu)
    {
        var match = Create(cpu, new(2));
        var defense = match.Defense;
        var result = End(match, PlayEndReason.Safety, -2);
        Assert.Equal(defense.Definition.Id, result.ScoringTeamId);
        Assert.Equal(2, defense.Score);
        match.ContinuePossession();
        Assert.Same(defense, match.Offense);
        Assert.Equal(new DriveStart(), match.Series);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RULE05_FirstDownNearGoalAndFourthDownIncompletion(bool cpu)
    {
        var match = Create(cpu, new(88));
        End(match, PlayEndReason.Tackle, 98);
        Assert.Equal(new DriveStart(98, 1, 2), match.Series);
        var fourth = Create(cpu, new(35, 4, 7));
        var result = End(fourth, PlayEndReason.Incomplete, 80);
        Assert.True(result.TurnoverOnDowns);
        Assert.Equal(0, result.Gain);
        Assert.Equal(65, result.NextPossession!.Series.OwnYardLine);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RULE06_RepeatedResultDoesNotDuplicateScoreStatsHistoryOrPossession(bool cpu)
    {
        var match = Create(cpu);
        var play = match.BeginPlay("pass.mesh");
        var ended = new PlayEnded(play.Id, play.OffenseId, PlayEndReason.Touchdown, 100,
            new(true, true, ReceiverSlot.WR1), Coverage: CoverageScheme.Cover3Zone);
        var result = match.Resolve(ended);
        var offense = match.Offense;
        var snapshot = offense.Stats;
        Assert.Same(result, match.Resolve(ended with { }));
        match.ContinuePossession();
        Assert.Same(result, match.Resolve(ended));
        Assert.Single(match.History);
        Assert.Equal(snapshot.Qb, offense.Stats.Qb);
        Assert.Equal(1, offense.GetCallCount("pass.mesh"));
        Assert.Equal(7, offense.Score);
        Assert.Null(match.PendingPossession);
        Assert.Throws<InvalidOperationException>(() => match.Resolve(ended with { Spot = 101 }));
    }

    [Theory]
    [InlineData(false, PlayEndReason.FieldGoalGood, 63, 3, 20)]
    [InlineData(true, PlayEndReason.FieldGoalGood, 63, 3, 20)]
    [InlineData(false, PlayEndReason.FieldGoalMissed, 63, 0, 37)]
    [InlineData(true, PlayEndReason.FieldGoalMissed, 90, 0, 20)]
    public void KicksNeverAwardSyntheticOpponentPoints(bool cpu, PlayEndReason reason, float spot, int points, float next)
    {
        var match = Create(cpu, new(70, 4, 5));
        var offense = match.Offense; var defense = match.Defense;
        var result = End(match, reason, spot);
        Assert.Equal(points, offense.Score); Assert.Equal(0, defense.Score);
        Assert.Equal(next, result.NextPossession!.Series.OwnYardLine);
    }

    [Theory]
    [InlineData(false, 30, 30)] [InlineData(true, 30, 30)]
    [InlineData(false, 65, 20)] [InlineData(true, 65, 20)]
    public void FourthDownPuntUsesFortyNetYardsOrTouchback(bool cpu, float own, float next)
    {
        var match = Create(cpu, new(own, 4, 10));
        Assert.Equal(next, End(match, PlayEndReason.Punt, own).NextPossession!.Series.OwnYardLine);
        Assert.Equal(0, match.User.Score + match.Opponent.Score);
    }

    [Fact]
    public void TeamStatsCallUsageAndMemoryStayWithTheirTeamAcrossSwaps()
    {
        var match = Create();
        End(match, PlayEndReason.Touchdown, 100, new(true, true, ReceiverSlot.WR1), "pass.mesh");
        match.ContinuePossession();
        End(match, PlayEndReason.Tackle, 24, new(Rush: RushingRole.RunningBack), "run.hb-dive");
        Assert.Equal(80, match.User.Stats.Qb.PassYards);
        Assert.Equal(0, match.Opponent.Stats.Qb.Attempts);
        Assert.Equal(4, match.Opponent.Stats.Rb.Yards);
        Assert.Equal(0, match.User.Stats.Rb.Attempts);
        Assert.Equal(1, match.User.GetCallCount("pass.mesh"));
        Assert.Equal(0, match.Opponent.GetCallCount("pass.mesh"));
        for (int i = 0; i < 30; i++) match.User.DefensiveMemory.RecordOutcome(new PlayRecord
            { CoverageScheme = CoverageScheme.Cover3Zone, Gain = 30, Outcome = PlayOutcome.Touchdown });
        Assert.NotEqual(match.User.DefensiveMemory.GetSchemeMultiplier(CoverageScheme.Cover3Zone),
            match.Opponent.DefensiveMemory.GetSchemeMultiplier(CoverageScheme.Cover3Zone));
    }

    [Theory]
    [InlineData(PlayEndReason.Sack, 16, 4)]
    [InlineData(PlayEndReason.OutOfBounds, 25, 0)]
    public void SackAndSidelineOutcomesRemainDistinct(PlayEndReason reason, float spot, int lost)
    {
        var match = Create();
        var result = End(match, reason, spot);
        Assert.Equal(lost, match.User.Stats.Qb.SackYardsLost);
        Assert.Equal(reason == PlayEndReason.OutOfBounds, result.StopsClock);
        Assert.Equal(2, match.Series.Down);
    }

    [Fact]
    public void InvalidEventsAreAtomicAndRestartRejectsStaleEvents()
    {
        var match = Create();
        var play = match.BeginPlay("pass.mesh");
        var valid = new PlayEnded(play.Id, play.OffenseId, PlayEndReason.Touchdown, 100);
        Assert.Throws<ArgumentException>(() => match.Resolve(valid with { Spot = float.NaN }));
        Assert.Throws<ArgumentException>(() => match.Resolve(valid with { OffenseId = "unknown" }));
        Assert.Throws<ArgumentException>(() => match.Resolve(valid with { Stats = new(Completion: true) }));
        Assert.Empty(match.History); Assert.Equal(0, match.User.Score);
        Assert.Equal(play, match.ActivePlay);
        match.Resolve(valid);
        match.Reset();
        var restarted = match.BeginPlay("pass.mesh");
        Assert.NotEqual(play.Id, restarted.Id);
        Assert.Throws<ArgumentException>(() => match.Resolve(valid));
        Assert.Equal(0, match.User.Score);
        Assert.Equal(0, match.User.Stats.Qb.Attempts);
    }
}


