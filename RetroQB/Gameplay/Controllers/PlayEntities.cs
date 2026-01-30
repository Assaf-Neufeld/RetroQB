using System.Numerics;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Contains the complete game entity state for a play.
/// Used to pass entity collections between controllers.
/// </summary>
public interface IPlayEntities
{
    Entities.Quarterback Qb { get; }
    Entities.Ball Ball { get; }
    IReadOnlyList<Entities.Receiver> Receivers { get; }
    IReadOnlyList<Entities.Blocker> Blockers { get; }
    IReadOnlyList<Entities.Defender> Defenders { get; }
}

/// <summary>
/// Mutable container for all play entities.
/// </summary>
public sealed class PlayEntities : IPlayEntities
{
    private readonly List<Entities.Receiver> _receivers = new();
    private readonly List<Entities.Defender> _defenders = new();
    private readonly List<Entities.Blocker> _blockers = new();

    public Entities.Quarterback Qb { get; set; } = null!;
    public Entities.Ball Ball { get; set; } = null!;
    
    public IReadOnlyList<Entities.Receiver> Receivers => _receivers;
    public IReadOnlyList<Entities.Defender> Defenders => _defenders;
    public IReadOnlyList<Entities.Blocker> Blockers => _blockers;
    
    public List<Entities.Receiver> ReceiversMutable => _receivers;
    public List<Entities.Defender> DefendersMutable => _defenders;
    public List<Entities.Blocker> BlockersMutable => _blockers;

    public void Clear()
    {
        _receivers.Clear();
        _defenders.Clear();
        _blockers.Clear();
        Qb = null!;
        Ball = null!;
    }

    public void LoadFrom(PlaySetupResult result)
    {
        Clear();
        Qb = result.Qb;
        Ball = result.Ball;
        _receivers.AddRange(result.Receivers);
        _blockers.AddRange(result.Blockers);
        _defenders.AddRange(result.Defenders);
    }
}
