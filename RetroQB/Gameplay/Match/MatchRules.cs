namespace RetroQB.Gameplay;

/// <summary>The single pure scoring/series resolver for the timed ruleset. Spots are offense-relative yards.</summary>
public static class MatchRules
{
    public static PlayResolution Resolve(PlayStart play, PlayEnded ended, string defenseId)
    {
        play.Series.Validate();
        if (ended.PlayId != play.Id || ended.OffenseId != play.OffenseId || string.IsNullOrWhiteSpace(defenseId)
            || defenseId == play.OffenseId) throw new ArgumentException("Play/team identity mismatch.");
        if (!Enum.IsDefined(ended.Reason) || !float.IsFinite(ended.Spot) || ended.Spot < -10 || ended.Spot > 110
            || ended.Defender.HasValue && !Enum.IsDefined(ended.Defender.Value)
            || ended.ControlledDefender.HasValue && !Enum.IsDefined(ended.ControlledDefender.Value)
            || ended.Coverage.HasValue && !Enum.IsDefined(ended.Coverage.Value))
            throw new ArgumentException("Invalid terminal reason/spot.");
        var stats = ended.Stats ?? new();
        if (!Enum.IsDefined(stats.Rush) || stats.Target.HasValue && !Enum.IsDefined(stats.Target.Value)
            || stats.Target.HasValue && !stats.PassAttempt
            || stats.Completion && (!stats.PassAttempt || !stats.Target.HasValue)
            || stats.PassAttempt && stats.Rush != RushingRole.None
            || stats.Completion && ended.Reason is PlayEndReason.Interception or PlayEndReason.Incomplete or PlayEndReason.PassDefended
            || ended.Reason == PlayEndReason.Sack && (stats.PassAttempt || stats.Rush != RushingRole.None))
            throw new ArgumentException("Inconsistent offensive statistics.");
        bool incomplete = ended.Reason is PlayEndReason.Incomplete or PlayEndReason.PassDefended;
        float spot = incomplete ? play.Series.OwnYardLine : ended.Spot;
        float gain = incomplete || ended.Reason is PlayEndReason.Interception or PlayEndReason.Punt
            or PlayEndReason.FieldGoalGood or PlayEndReason.FieldGoalMissed ? 0 : MathF.Min(100, spot) - play.Series.OwnYardLine;

        PlayResolution Change(string team, float yard, string? scorer = null, int points = 0, bool downs = false)
            => new(ended, gain, downs, scorer, points, new(team, new(yard, 1, MathF.Min(10, 100 - yard))), null);

        switch (ended.Reason)
        {
            case PlayEndReason.Touchdown:
                if (spot < 100) throw new ArgumentException("Touchdown must reach the goal line.");
                return Change(defenseId, 20, play.OffenseId, 7);
            case PlayEndReason.Safety:
                if (spot > 0) throw new ArgumentException("Safety must end in the offense's end zone.");
                return Change(defenseId, 20, defenseId, 2);
            case PlayEndReason.Interception:
                // An interception in the throwing team's end zone is already a defensive touchdown, even without returns.
                if (spot <= 0) return Change(play.OffenseId, 20, defenseId, 7);
                return Change(defenseId, spot >= 100 ? 20 : 100 - spot);
            case PlayEndReason.FieldGoalGood: return Change(defenseId, 20, play.OffenseId, 3);
            case PlayEndReason.FieldGoalMissed:
                if (spot <= 0) throw new ArgumentException("Kick spot must be in the field of play.");
                return Change(defenseId, MathF.Max(20, 100 - spot));
            case PlayEndReason.Punt:
                if (play.Series.Down != 4) throw new ArgumentException("Punts require fourth down.");
                float puntSpot = play.Series.OwnYardLine + 40;
                return Change(defenseId, puntSpot >= 100 ? 20 : 100 - puntSpot);
        }
        if (!incomplete && spot <= 0) return Change(defenseId, 20, defenseId, 2);
        if (!incomplete && spot >= 100) return Change(defenseId, 20, play.OffenseId, 7);
        if (gain >= play.Series.Distance)
            return new(ended, gain, false, null, 0, null, new(spot, 1, MathF.Min(10, 100 - spot)));
        if (play.Series.Down == 4) return Change(defenseId, 100 - spot, downs: true);
        return new(ended, gain, false, null, 0, null, new(spot, play.Series.Down + 1, play.Series.Distance - gain));
    }
}
