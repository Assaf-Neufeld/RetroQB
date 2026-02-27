using RetroQB.Entities;

namespace RetroQB.Stats;

public interface IStatisticsTracker
{
    void Reset();
    void RecordPassAttempt();
    void RecordTarget(ReceiverSlot receiverSlot);
    void RecordSack(int yardsLost);
    void RecordCompletion(ReceiverSlot receiverSlot);
    void RecordPassYards(ReceiverSlot receiverSlot, int yards, bool isTouchdown);
    void RecordInterception();
    void RecordRushYards(int yards, bool isTouchdown);
    void RecordQbRushYards(int yards, bool isTouchdown);
    SkillStatLine GetReceiverStats(ReceiverSlot receiverSlot);
    GameStatsSnapshot BuildSnapshot();
}

public sealed class StatisticsTracker : IStatisticsTracker
{
    private readonly QbStatLine _qbStats = new();
    private readonly RushStatLine _rbStats = new();
    private readonly Dictionary<ReceiverSlot, SkillStatLine> _receiverStats = new();

    public void Reset()
    {
        _qbStats.Reset();
        _rbStats.Reset();
        _receiverStats.Clear();
    }

    public void RecordPassAttempt()
    {
        _qbStats.Attempts++;
    }

    public void RecordTarget(ReceiverSlot receiverSlot)
    {
        GetOrCreateReceiverStats(receiverSlot).Targets++;
    }

    public void RecordSack(int yardsLost)
    {
        _qbStats.Sacks++;
        _qbStats.SackYardsLost += Math.Max(0, yardsLost);
    }

    public void RecordCompletion(ReceiverSlot receiverSlot)
    {
        _qbStats.Completions++;
        GetOrCreateReceiverStats(receiverSlot).Receptions++;
    }

    public void RecordPassYards(ReceiverSlot receiverSlot, int yards, bool isTouchdown)
    {
        _qbStats.PassYards += yards;
        if (isTouchdown)
        {
            _qbStats.PassTds++;
        }

        var receiverStats = GetOrCreateReceiverStats(receiverSlot);
        receiverStats.Yards += yards;
        if (isTouchdown)
        {
            receiverStats.Tds++;
        }
    }

    public void RecordInterception()
    {
        _qbStats.Interceptions++;
    }

    public void RecordRushYards(int yards, bool isTouchdown)
    {
        _rbStats.Yards += yards;
        if (isTouchdown)
        {
            _rbStats.Tds++;
        }
    }

    public void RecordQbRushYards(int yards, bool isTouchdown)
    {
        _qbStats.RushYards += yards;
        if (isTouchdown)
        {
            _qbStats.RushTds++;
        }
    }

    public SkillStatLine GetReceiverStats(ReceiverSlot receiverSlot)
    {
        return GetOrCreateReceiverStats(receiverSlot);
    }

    public GameStatsSnapshot BuildSnapshot()
    {
        var receivers = new List<ReceiverStatsSnapshot>(ReceiverSlotExtensions.DefaultReceivingStatOrder.Count);
        foreach (var slot in ReceiverSlotExtensions.DefaultReceivingStatOrder)
        {
            if (_receiverStats.TryGetValue(slot, out var stats))
            {
                receivers.Add(new ReceiverStatsSnapshot(slot.GetLabel(), stats.Targets, stats.Receptions, stats.Yards, stats.Tds));
            }
            else
            {
                receivers.Add(new ReceiverStatsSnapshot(slot.GetLabel(), 0, 0, 0, 0));
            }
        }

        var qb = new QbStatsSnapshot(
            _qbStats.Completions,
            _qbStats.Attempts,
            _qbStats.PassYards,
            _qbStats.PassTds,
            _qbStats.Interceptions,
            _qbStats.Sacks,
            _qbStats.SackYardsLost,
            _qbStats.RushYards,
            _qbStats.RushTds);
        var rb = new RbStatsSnapshot(_rbStats.Yards, _rbStats.Tds);
        return new GameStatsSnapshot(qb, receivers, rb);
    }

    private SkillStatLine GetOrCreateReceiverStats(ReceiverSlot receiverSlot)
    {
        if (!_receiverStats.TryGetValue(receiverSlot, out var stats))
        {
            stats = new SkillStatLine();
            _receiverStats[receiverSlot] = stats;
        }
        return stats;
    }
}
