namespace RetroQB.Gameplay.Stats;

public interface IStatisticsTracker
{
    void Reset();
    void RecordPassAttempt();
    void RecordCompletion(int receiverIndex);
    void RecordPassYards(int receiverIndex, int yards, bool isTouchdown);
    void RecordInterception();
    void RecordRushYards(int yards, bool isTouchdown);
    void RecordQbRushYards(int yards, bool isTouchdown);
    SkillStatLine GetReceiverStats(int receiverIndex);
    GameStatsSnapshot BuildSnapshot(Func<int, bool> tryGetReceiverIndex);
}

public sealed class StatisticsTracker : IStatisticsTracker
{
    private readonly QbStatLine _qbStats = new();
    private readonly RushStatLine _rbStats = new();
    private readonly Dictionary<int, SkillStatLine> _receiverStats = new();

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

    public void RecordCompletion(int receiverIndex)
    {
        _qbStats.Completions++;
        GetOrCreateReceiverStats(receiverIndex).Receptions++;
    }

    public void RecordPassYards(int receiverIndex, int yards, bool isTouchdown)
    {
        _qbStats.PassYards += yards;
        if (isTouchdown)
        {
            _qbStats.PassTds++;
        }

        var receiverStats = GetOrCreateReceiverStats(receiverIndex);
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

    public SkillStatLine GetReceiverStats(int receiverIndex)
    {
        return GetOrCreateReceiverStats(receiverIndex);
    }

    public GameStatsSnapshot BuildSnapshot(Func<int, bool> tryGetReceiverIndex)
    {
        var receivers = new List<ReceiverStatsSnapshot>(5);
        for (int i = 1; i <= 5; i++)
        {
            int receiverIndex = i - 1;
            bool hasIndex = tryGetReceiverIndex(i);
            
            if (hasIndex && _receiverStats.TryGetValue(receiverIndex, out var stats))
            {
                receivers.Add(new ReceiverStatsSnapshot($"WR{i}", stats.Receptions, stats.Yards, stats.Tds));
            }
            else
            {
                receivers.Add(new ReceiverStatsSnapshot($"WR{i}", 0, 0, 0));
            }
        }

        var qb = new QbStatsSnapshot(
            _qbStats.Completions,
            _qbStats.Attempts,
            _qbStats.PassYards,
            _qbStats.PassTds,
            _qbStats.Interceptions,
            _qbStats.RushYards,
            _qbStats.RushTds);
        var rb = new RbStatsSnapshot(_rbStats.Yards, _rbStats.Tds);
        return new GameStatsSnapshot(qb, receivers, rb);
    }

    private SkillStatLine GetOrCreateReceiverStats(int receiverIndex)
    {
        if (!_receiverStats.TryGetValue(receiverIndex, out var stats))
        {
            stats = new SkillStatLine();
            _receiverStats[receiverIndex] = stats;
        }
        return stats;
    }
}
