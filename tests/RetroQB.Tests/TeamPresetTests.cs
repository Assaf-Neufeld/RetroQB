using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Replay;

namespace RetroQB.Tests;

public sealed class TeamPresetTests
{
    [Fact]
    public void TenRegularTeamsRemainSortedAndSecretTeamIsAdditional()
    {
        var teams = OffensiveTeamPresets.GetMenuTeams(false);
        Assert.Equal(10, teams.Count);
        Assert.Equal(10, teams.Select(t => t.Name).Distinct().Count());
        Assert.Equal(teams.OrderByDescending(t => t.TeamScore), teams);
        var unlocked = OffensiveTeamPresets.GetMenuTeams(true);
        Assert.Equal(11, unlocked.Count);
        Assert.Equal(teams, unlocked.Take(10));
        Assert.Equal(OffensiveTeamPresets.GoldenLegion.Name, unlocked[^1].Name);
    }

    [Fact]
    public void SpecialistsProduceDifferentOnFieldStrengthsAndTradeoffs()
    {
        var bombers = OffensiveTeamPresets.Bombers;
        var phantoms = OffensiveTeamPresets.Phantoms;
        Assert.True(bombers.GetQbMaxThrowDistance() > phantoms.GetQbMaxThrowDistance());
        // Accuracy is a spread multiplier: lower means less throwing error.
        Assert.True(phantoms.GetQbAccuracyMultiplier(20) < bombers.GetQbAccuracyMultiplier(20));

        var mustangs = OffensiveTeamPresets.Mustangs;
        var bulldozers = OffensiveTeamPresets.Bulldozers;
        Assert.True(mustangs.GetReceiverSpeed(ReceiverSlot.RB1) > bulldozers.GetReceiverSpeed(ReceiverSlot.RB1));
        Assert.True(bulldozers.GetRbTackleBreakChance(ReceiverSlot.RB1) > mustangs.GetRbTackleBreakChance(ReceiverSlot.RB1));

        var sentinels = OffensiveTeamPresets.Sentinels;
        var lightning = OffensiveTeamPresets.Lightning;
        Assert.True(sentinels.BlockingStrength > lightning.BlockingStrength);
        Assert.True(sentinels.GetReceiverSpeed(ReceiverSlot.WR1) < lightning.GetReceiverSpeed(ReceiverSlot.WR1));
        Assert.True(sentinels.GetReceiverCatchingAbility(ReceiverSlot.WR1) > lightning.GetReceiverCatchingAbility(ReceiverSlot.WR1));
    }

    [Fact]
    public void SeasonOpponentUniformsAreSeparatedFromEveryPlayerPalette()
    {
        var opponents = DefensiveTeamPresets.All.Take(3).ToArray();
        foreach (var opponent in opponents)
        {
            foreach (var offense in OffensiveTeamPresets.GetMenuTeams(true))
            {
                AssertSeparated(opponent.PrimaryColor, offense.PrimaryColor);
                AssertSeparated(opponent.PrimaryColor, offense.GetUniformColor());
            }
            foreach (var other in opponents.Where(t => t.Name != opponent.Name))
                Assert.NotEqual(opponent.PrimaryColor, other.PrimaryColor);
        }

        static void AssertSeparated(Color a, Color b)
        {
            // A broad RGB separation guard; visual previews check the rendered kits.
            float distance = new Vector3(a.R - b.R, a.G - b.G, a.B - b.B).Length();
            Assert.True(distance >= 120, $"Uniform colors are too close: {distance:F1}");
        }
    }

    [Fact]
    public void DefenderAndReplayUseOpponentUniformColor()
    {
        foreach (var team in DefensiveTeamPresets.All.Take(3))
        {
            var defender = new Defender(new Vector2(25, 45), DefensivePosition.DB, DefenderSlot.CB1, team);
            Assert.Equal(team.PrimaryColor, defender.Color);
            var qb = new Quarterback(new Vector2(25, 40));
            var recorder = new ReplayRecorder();
            recorder.Begin(1);
            recorder.Capture(qb, new Ball(qb.Position), [], [], [defender], 40, 50, 0.1f);
            var clip = recorder.FinalizeClip(PlayOutcome.Incomplete)!;
            Assert.Equal(team.PrimaryColor, clip.Frames[0].Defenders[0].Color);
        }
    }
}
