using RetroQB.Gameplay;

namespace RetroQB.AI;

public enum MatchAction { Scrimmage, FieldGoal, Punt, Kneel, Kickoff }
public sealed record StrategyContext(int Quarter, double Seconds, bool Overtime, int ScoreMargin,
    int Down, float Yard, float Distance, int OpponentTimeouts, double PlaySeconds, bool ClockRunning);

/// <summary>Deliberately conservative clock management. All inputs are public game state.</summary>
public static class MatchStrategy
{
    public static bool CanKneelOut(StrategyContext c)
    {
        if (c.Overtime || c.Quarter != 4 || c.ScoreMargin <= 0 || c.Down >= 4) return false;
        int kneels = 4 - c.Down;
        if (c.Yard <= kneels + 1) return false;
        double guaranteed = (c.ClockRunning ? Math.Max(0, c.PlaySeconds - 1) : 0)
            + Math.Max(0, kneels - c.OpponentTimeouts) * 19 + kneels * .75;
        return c.Seconds < guaranteed;
    }

    public static MatchAction Choose(StrategyContext c)
    {
        if (CanKneelOut(c)) return MatchAction.Kneel;
        float kickDistance = 117 - c.Yard;
        bool range = kickDistance <= FieldGoalAttempt.MaxDistance;
        bool urgent = !c.Overtime && c.Quarter is 2 or 4 && c.Seconds <= 30;
        if (urgent && range && c.Seconds <= 8 && (c.Quarter == 2 || c.ScoreMargin >= -3))
            return MatchAction.FieldGoal;
        if (c.Down != 4) return MatchAction.Scrimmage;
        if (c.Quarter == 4 && !c.Overtime && c.Seconds <= 120 && c.ScoreMargin <= -4)
            return MatchAction.Scrimmage;
        if (range && kickDistance <= 65) return MatchAction.FieldGoal;
        bool gameChangingLongKick = range && kickDistance > 65
            && (c.Overtime || c.Quarter == 4 && c.Seconds <= 3
                && c.ScoreMargin <= 0 && c.ScoreMargin >= -3);
        if (gameChangingLongKick) return MatchAction.FieldGoal;
        if (c.Overtime || c.Quarter == 4 && c.Seconds <= 120 && c.ScoreMargin < 0
            || c.Yard >= 45 && c.Distance <= 2) return MatchAction.Scrimmage;

        // On ordinary fourth downs, take the field-position gain instead of
        // repeatedly risking a turnover on downs. A backed-up team can still
        // try to convert fourth-and-inches; longer gaps favor the punt. Keep
        // short-yardage attempts near midfield, and let urgency override above.
        bool inchesToGo = c.Distance <= 0.5f;
        if (c.Yard <= 35 && !inchesToGo) return MatchAction.Punt;
        return c.Distance > 2 ? MatchAction.Punt : MatchAction.Scrimmage;
    }

    public static double Cadence(StrategyContext c, double normal)
        => !c.Overtime && c.Quarter == 4 && c.ScoreMargin > 0 ? Math.Max(.05, c.PlaySeconds - 1)
        : !c.Overtime && c.Quarter is 2 or 4 && c.Seconds <= 60 && c.ScoreMargin <= 0 ? .75 : normal;

    public static bool DefensiveTimeout(StrategyContext offense)
        => !offense.Overtime && offense.Quarter == 4 && offense.ScoreMargin > 0
            && offense.Seconds <= 120 && offense.ClockRunning;

    public static double KickProbability(float distance)
        => distance > FieldGoalAttempt.MaxDistance ? 0
            : distance > 75 ? .005
            : distance > 70 ? .015
            : distance > 65 ? .04
            : distance > 60 ? .10
            : distance > 55 ? .24
            : distance > 50 ? .45
            : Math.Clamp(.96 - Math.Max(0, distance - 20) * .012, .60, .96);
}
