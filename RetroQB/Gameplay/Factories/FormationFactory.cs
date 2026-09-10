using RetroQB.Entities;

namespace RetroQB.Gameplay;

public interface IFormationFactory
{
    FormationResult CreateFormation(ResolvedPlay play, float lineOfScrimmage, OffensiveTeamAttributes? teamAttributes = null);
}

public sealed class FormationResult
{
    public Quarterback Qb { get; init; } = null!;
    public Ball Ball { get; init; } = null!;
    public List<Receiver> Receivers { get; init; } = new();
    public List<Blocker> Blockers { get; init; } = new();
}

/// <summary>Instantiates resolved geometry without choosing formations or changing assignments.</summary>
public sealed class FormationFactory : IFormationFactory
{
    public FormationResult CreateFormation(ResolvedPlay play, float lineOfScrimmage, OffensiveTeamAttributes? teamAttributes = null)
    {
        var team = teamAttributes ?? OffensiveTeamAttributes.Default;
        var qb = new Quarterback(play.Quarterback.AtLineOfScrimmage(lineOfScrimmage), team);
        var ball = new Ball(qb.Position);
        ball.SetHeld(qb, BallState.HeldByQB);
        return new FormationResult
        {
            Qb = qb,
            Ball = ball,
            Receivers = play.Players.Select((p, index) =>
                new Receiver(index, p.Slot, p.Position.AtLineOfScrimmage(lineOfScrimmage), team)).ToList(),
            Blockers = play.Linemen.Select((p, i) => new Blocker(p.AtLineOfScrimmage(lineOfScrimmage), team)
            {
                BlockingAssignment = play.LineBlocking[i], OpeningAssignment = play.OpeningLineBlocking[i]
            }).ToList()
        };
    }
}
