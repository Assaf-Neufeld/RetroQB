using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Controllers;

/// <summary>
/// Manages receiver priority assignment and lookup.
/// Receivers are numbered 1-5 by roster slot order for pass targeting.
/// </summary>
public sealed class ReceiverPriorityManager
{
    private readonly Dictionary<int, int> _receiverPriorityByIndex = new();
    private readonly List<int> _priorityReceiverIndices = new();

    /// <summary>
    /// Assigns priority numbers (1-5) to eligible receivers based on roster slot order.
    /// </summary>
    public void AssignPriorities(IReadOnlyList<Receiver> receivers)
    {
        _receiverPriorityByIndex.Clear();
        _priorityReceiverIndices.Clear();

        var ordered = receivers
            .Where(r => r.Eligible)
            .OrderBy(r => r.Slot.GetPriorityOrder())
            .ToList();

        int count = Math.Min(5, ordered.Count);
        for (int i = 0; i < count; i++)
        {
            int receiverIndex = ordered[i].Index;
            _receiverPriorityByIndex[receiverIndex] = i + 1;
            _priorityReceiverIndices.Add(receiverIndex);
        }
    }

    /// <summary>
    /// Gets the first (leftmost) eligible receiver index, or 0 if none.
    /// </summary>
    public int GetFirstReceiverIndex()
    {
        return _priorityReceiverIndices.Count > 0 ? _priorityReceiverIndices[0] : 0;
    }

    /// <summary>
    /// Tries to get the receiver index for a given priority (1-5).
    /// </summary>
    public bool TryGetReceiverIndexForPriority(int priority, out int receiverIndex)
    {
        receiverIndex = -1;
        if (priority <= 0) return false;
        int listIndex = priority - 1;
        if (listIndex < 0 || listIndex >= _priorityReceiverIndices.Count) return false;
        receiverIndex = _priorityReceiverIndices[listIndex];
        return receiverIndex >= 0;
    }

    /// <summary>
    /// Gets the priority label for a receiver index.
    /// </summary>
    public string GetPriorityLabel(int receiverIndex)
    {
        return _receiverPriorityByIndex.TryGetValue(receiverIndex, out int priority)
            ? priority.ToString()
            : "-";
    }

    /// <summary>
    /// Checks if a receiver index has a priority assigned.
    /// </summary>
    public bool HasPriority(int receiverIndex)
    {
        return _receiverPriorityByIndex.ContainsKey(receiverIndex);
    }

    /// <summary>
    /// Gets all receiver indices in priority order.
    /// </summary>
    public IReadOnlyList<int> GetPriorityIndices() => _priorityReceiverIndices;
}
