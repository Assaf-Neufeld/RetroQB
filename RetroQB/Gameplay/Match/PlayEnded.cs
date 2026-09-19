using RetroQB.AI;
using RetroQB.Data;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public enum PlayEndReason
{
    Tackle, Sack, OutOfBounds, Incomplete, PassDefended, Interception,
    Touchdown, Safety, FieldGoalGood, FieldGoalMissed, Punt, Kneel
}

public enum RushingRole { None, Quarterback, RunningBack }

public sealed record OffensivePlayStats(bool PassAttempt = false, bool Completion = false,
    ReceiverSlot? Target = null, RushingRole Rush = RushingRole.None);

/// <param name="Spot">Yards from the possessing offense's own goal, including end zones (-10 to 110).</param>
public sealed record PlayEnded(long PlayId, string OffenseId, PlayEndReason Reason, float Spot,
    OffensivePlayStats? Stats = null, DefenderSlot? Defender = null, CoverageScheme? Coverage = null);

public sealed record PlayStart(long Id, string OffenseId, string CallId, DriveStart Series);
public sealed record NextPossession(string TeamId, DriveStart Series);
public sealed record PlayResolution(PlayEnded Event, float Gain, bool TurnoverOnDowns,
    string? ScoringTeamId, int Points, NextPossession? NextPossession, DriveStart? NextSeries)
{
    public bool DriveEnded => NextPossession != null;
    public bool StopsClock => DriveEnded || Event.Reason is PlayEndReason.Incomplete
        or PlayEndReason.PassDefended or PlayEndReason.OutOfBounds;
}
