namespace RetroQB.Gameplay.Defense;

public sealed partial class CoverageCoordinator
{
    // Both spot and match coverage use the same reservation, reachability and atomic commit engine.
    private void CommitOffers(DefensiveObservationFrame frame, DefenderSlot? human, IReadOnlyList<Offer> offers, List<CoverageTransfer> transfers)
    {
        var actors = frame.Visible.Defenders.ToDictionary(a => a.Id);
        var receivers = frame.Receivers.ToDictionary(r => r.Id);
        var selected = new Dictionary<DefenderSlot, Offer>();
        foreach (var offer in offers.OrderByDescending(o => o.Priority).ThenByDescending(o => o.Urgency)
            .ThenBy(o => _states[o.Destination].Primary == o.Receiver ? 0 : 1).ThenBy(o => o.Receiver.Value).ThenBy(o => o.Source))
        {
            if (!Reachable(actors[offer.Destination], receivers[offer.Receiver], _zones[offer.Destination], human == offer.Destination))
            { Log(offer, "Decline", "XFER-02:Unreachable"); continue; }
            if (!selected.TryAdd(offer.Destination, offer)) { Log(offer, "Decline", "XFER-03:DestinationReserved"); continue; }
            Log(offer, "Reserve", "XFER-01:OneIncomingPrimary");
        }
        var accepted = selected.Values.ToList();
        bool changed;
        do
        {
            changed = false;
            var outgoing = accepted.ToDictionary(o => o.Source);
            bool Complete(Offer o, HashSet<DefenderSlot> path)
            {
                if (!path.Add(o.Source)) return true;
                if (_states[o.Destination].Primary == null) return true;
                return outgoing.TryGetValue(o.Destination, out var next) && Complete(next, path);
            }
            var incompleteBundles = offers.Where(o => o.Bundle != 0).GroupBy(o => o.Bundle)
                .Where(g => g.Any(o => !accepted.Contains(o))).Select(g => g.Key).ToHashSet();
            foreach (var o in accepted.ToArray())
            {
                if (!incompleteBundles.Contains(o.Bundle) && Complete(o, new())) continue;
                accepted.Remove(o); changed = true;
                Log(o, "Decline", "XFER-02:OccupiedOrIncompleteExchange;PriorOwnersRetained");
            }
        } while (changed);
        if (accepted.Count == 0) return;
        foreach (var o in accepted) Log(o, "Accept", "DEC-04:CompleteTransaction");
        foreach (var o in accepted)
        {
            var source = _states[o.Source]; source.Primary = null; source.Helping = o.Receiver;
            source.HelpUntil = frame.Seconds + ZoneTuning.HelpOverlapSeconds; source.ExitedAt = -1;
            source.Status = "Accepted;RecoverHome"; source.Abandoned = false;
        }
        foreach (var o in accepted)
        {
            var dest = _states[o.Destination]; dest.Primary = o.Receiver; dest.Helping = null; dest.CommittedAt = frame.Seconds;
            dest.ExitedAt = -1; dest.Status = "AcceptedTransfer"; dest.Abandoned = false;
        }
        _version++;
        foreach (var o in accepted) Log(o, "Commit", "XFER-01:AtomicOwnership");
        void Log(Offer o, string stage, string reason) => transfers.Add(new(_transaction, _version, _plan.RuleVersion,
            frame.Seconds, o.Source, o.Destination, o.Receiver, stage, reason));
    }
}
