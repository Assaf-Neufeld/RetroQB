using RetroQB.Entities;

namespace RetroQB.Gameplay.Replay;

public sealed record ReplayMatchContext(string OffenseId, string DefenseId, SeasonStage Stage,
    MatchClockSnapshot Clock, int UserScore, int OpponentScore, string UserId, string OpponentId,
    DefenderSlot? ControlledDefender, int ControlledIndex, ResolvedDefensivePlay Assignments, string OffensiveCall);
