using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.Stats;

public sealed record DefenderStatsSnapshot(DefenderSlot Slot, int Tackles, int Sacks, int Interceptions, int PassesDefended);
public sealed record DefenseStatsSnapshot(int Tackles, int ControlledTackles, int Sacks, int Interceptions,
    int PassesDefended, int Safeties, int YardsAllowed, int PointsAllowed, int ThirdDownStops, int FourthDownStops,
    IReadOnlyList<DefenderStatsSnapshot> Players, int ControlledSacks = 0, int ControlledInterceptions = 0, int ControlledPassesDefended = 0);

public sealed class DefensiveStatistics
{
    private readonly Dictionary<DefenderSlot, DefenderStatsSnapshot> _players = new();
    private int _tackles, _controlled, _sacks, _interceptions, _defended, _safeties, _yards, _points, _third, _fourth;
    private int _controlledSacks, _controlledInterceptions, _controlledDefended;
    public DefenseStatsSnapshot Snapshot() => new(_tackles, _controlled, _sacks, _interceptions, _defended,
        _safeties, _yards, _points, _third, _fourth, Array.AsReadOnly(_players.Values.OrderBy(p => p.Slot).ToArray()),
        _controlledSacks, _controlledInterceptions, _controlledDefended);

    internal void Record(PlayStart play, PlayResolution result)
    {
        var e = result.Event;
        bool tackle = e.Defender != null && e.Reason is PlayEndReason.Tackle or PlayEndReason.Sack or PlayEndReason.Safety;
        int t = tackle ? 1 : 0, s = e.Reason == PlayEndReason.Sack ? 1 : 0;
        int i = e.Reason == PlayEndReason.Interception ? 1 : 0, p = e.Reason == PlayEndReason.PassDefended ? 1 : 0;
        _tackles += t; _sacks += s; _interceptions += i; _defended += p;
        if (tackle && e.Defender == e.ControlledDefender) _controlled++;
        if (e.Defender != null && e.Defender == e.ControlledDefender)
        { _controlledSacks += s; _controlledInterceptions += i; _controlledDefended += p; }
        if (result.Points == 2 && result.ScoringTeamId != e.OffenseId) _safeties++;
        _yards += (int)MathF.Round(result.Gain);
        if (result.ScoringTeamId == e.OffenseId) _points += result.Points;
        bool stopped = result.ScoringTeamId != e.OffenseId
            && (result.TurnoverOnDowns || i > 0 || result.NextSeries?.Down > play.Series.Down);
        if (stopped && play.Series.Down == 3) _third++;
        if (stopped && play.Series.Down == 4) _fourth++;
        if (e.Defender is { } slot)
        {
            var old = _players.GetValueOrDefault(slot) ?? new(slot, 0, 0, 0, 0);
            _players[slot] = old with { Tackles = old.Tackles + t, Sacks = old.Sacks + s,
                Interceptions = old.Interceptions + i, PassesDefended = old.PassesDefended + p };
        }
    }

    internal void Reset()
    {
        _players.Clear(); _tackles = _controlled = _sacks = _interceptions = _defended = _safeties = _yards = _points = _third = _fourth = 0;
        _controlledSacks = _controlledInterceptions = _controlledDefended = 0;
    }
}
