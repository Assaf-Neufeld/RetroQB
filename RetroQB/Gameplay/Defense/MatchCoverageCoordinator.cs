using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public sealed partial class CoverageCoordinator
{
    private readonly HashSet<ReceiverId> _verticalLocks = new();
    private readonly Dictionary<ReceiverId, ReleaseDirection> _shortReads = new();
    private static ZoneResponsibility MatchZone(MatchResponsibility m) => new(m.Deep ? SpotZoneRole.DeepHalf : SpotZoneRole.Hook,
        m.Area, m.Home, Array.Empty<DefenderSlot>());

    private IReadOnlyList<DefensiveIntent> DecideMatch(DefensiveObservationFrame frame, DefenderSlot? human)
    {
        var match = _plan.Match!; var actors = frame.Visible.Defenders.ToDictionary(a => a.Id);
        var receivers = frame.Receivers.ToDictionary(r => r.Id);
        var jobs = _plan.Assignments.Where(a => a.Pass is MatchResponsibility).ToDictionary(a => a.ActorId, a => (MatchResponsibility)a.Pass);
        var transfers = new List<CoverageTransfer>(); var concessions = new List<string>();
        float gate = MathF.Min(6, MathF.Max(1.5f, .35f * (FieldGeometry.PlayableBackLine - _plan.Offense.LineOfScrimmage)));
        bool liveRead = !frame.ConfirmedCarrier && frame.Visible.Ball.State == BallState.HeldByQB;
        if (liveRead)
        {
            _transaction++;
            foreach (var r in frame.Receivers)
            {
                if (r.Position.Y >= _plan.Offense.LineOfScrimmage + gate && r.Vertical) _verticalLocks.Add(r.Id);
                if (!_verticalLocks.Contains(r.Id) && r.ConfidenceSeconds + .00001f >= DefensiveTuning.ReleaseSeconds
                    && r.Release is ReleaseDirection.Inward or ReleaseDirection.Outward) _shortReads[r.Id] = r.Release;
            }
            var offers = new List<Offer>();
            foreach (var rule in match.Releases)
            {
                var r = receivers[rule.Receiver]; var source = Owners[rule.Receiver];
                if (rule.LockAll || r.Blocking) continue;
                if (_verticalLocks.Contains(r.Id))
                {
                    if (rule.VerticalDestination is not { } destination || destination == source) continue;
                    if (_plan.Call.Match == MatchFamily.Cover3)
                    {
                        var outside = match.Releases.Single(q => q.Side == rule.Side && q.Number == 1);
                        if (!_shortReads.ContainsKey(outside.Receiver) || _verticalLocks.Contains(outside.Receiver)) continue;
                    }
                    OfferTo(source, destination, r, 100, rule.VerticalRule);
                }
                else if (source == rule.InitialOwner && _shortReads.TryGetValue(r.Id, out var shortRead))
                {
                    // An out stays with its apex through a wheel; Cover 3 may transfer a settled out.
                    bool canOffer = shortRead == ReleaseDirection.Inward || rule.Number == 1
                        || _plan.Call.Match == MatchFamily.Cover3 && r.Velocity.LengthSquared() < 1;
                    if (!canOffer) continue;
                    var destinations = rule.ShallowDestinations.OrderBy(a => _states[a].Primary != null)
                        .ThenBy(a => !Reachable(actors[a], r, _zones[a], human == a))
                        .ThenBy(a => Vector2.Distance(actors[a].Position, r.Position)).ThenBy(a => a).ToArray();
                    if (destinations.Length > 0) OfferTo(source, destinations[0], r, 80, shortRead == ReleaseDirection.Outward ? rule.OutRule : rule.ShallowRule);
                }
            }
            // M3-04: a shallow #1 / vertical #2 exchange is one complete bundle, never half an exchange.
            if (_plan.Call.Match == MatchFamily.Cover3)
            foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
            {
                var one = match.Releases.FirstOrDefault(r => r.Side == side && r.Number == 1);
                var two = match.Releases.FirstOrDefault(r => r.Side == side && r.Number == 2);
                if (one == null || two == null || !_verticalLocks.Contains(two.Receiver) || !_shortReads.ContainsKey(one.Receiver)
                    || Owners[one.Receiver] != one.InitialOwner) continue;
                int bundle = side == FieldSide.Left ? 1 : 2;
                var related = offers.Where(o => o.Receiver == one.Receiver || o.Receiver == two.Receiver).ToArray();
                if (related.Length != 2)
                {
                    foreach (var o in related) { offers.Remove(o); Log(o.Source, o.Destination, o.Receiver, "Decline", "M3-04:IncompleteChain;PriorOwnersRetained"); }
                }
                else foreach (var o in related) { offers.Remove(o); offers.Add(o with { Bundle = bundle }); }
            }
            CommitOffers(frame, human, offers, transfers);
            void OfferTo(DefenderSlot source, DefenderSlot destination, ObservedReceiver r, int priority, string rule)
            {
                bool reachable = Reachable(actors[destination], r, _zones[destination], human == destination);
                string evidence = $"{_version}:{reachable}:{_states[destination].Primary}:{_verticalLocks.Contains(r.Id)}";
                var key = (source, destination, r.Id);
                if (_declined.TryGetValue(key, out var old) && old.Until > frame.Seconds && old.Evidence == evidence) return;
                _declined[key] = new(frame.Seconds + ZoneTuning.OfferSeconds, evidence);
                offers.Add(new(source, destination, r.Id, priority, Urgency(r, actors[destination], _zones[destination])));
                Log(source, destination, r.Id, "Offer", rule + (human == destination ? ";XFER-04:ActualHumanState" : ";DeclaredMatchRole"));
            }
        }
        var intents = new List<DefensiveIntent>();
        foreach (var (actor, state) in _states.OrderBy(s => s.Key))
        {
            var job = jobs[actor]; var self = actors[actor]; Vector2 target = job.Home;
            string reason = "MatchHome;NoPrimaryPromise"; string ruleId = _plan.Call.Match == MatchFamily.Cover3 ? "M3-01" : "QM-01";
            var kind = DefensiveIntentKind.Hold;
            if (state.Primary is { } id)
            {
                var r = receivers[id]; var rule = match.Releases.Single(q => q.Receiver == id);
                ruleId = rule.LockAll || _verticalLocks.Contains(id) ? rule.VerticalRule
                    : _shortReads.GetValueOrDefault(id) == ReleaseDirection.Inward ? rule.ShallowRule
                    : _shortReads.GetValueOrDefault(id) == ReleaseDirection.Outward ? rule.OutRule : rule.VerticalRule;
                target = r.Position + r.Velocity * DefensiveTuning.ProjectionSeconds + new Vector2(r.Position.X < _plan.Surface.CenterX ? .6f : -.6f, _verticalLocks.Contains(id) ? 3 : 1.5f);
                state.Helping = null; kind = DefensiveIntentKind.Leverage;
                reason = _verticalLocks.Contains(id) ? "MatchedActorLock;SurvivesLaterCuts" : "ProvisionalOwner;RetainUntilAccepted";
                if (_shortReads.ContainsKey(id) && actor == rule.InitialOwner)
                { reason += ";CarryBeyondHomeUntilLegalExchange"; concessions.Add($"{rule.ShallowRule}:{actor}:VacatedUnderneathArea;Receiver{id.Value}Retained"); }
                if (r.Position.Y > self.Position.Y + .5f && r.Velocity.Y > 0)
                {
                    kind = DefensiveIntentKind.Recovery;
                    bool reachable = DefensiveRuntime.TryIntercept(self, r, out target);
                    reason += self.Contact ? ";Obstructed;RealRecovery" : reachable ? ";ReachableRecovery" : ";BeatenBySpeed;AvailableHelpOnly";
                }
                state.Abandoned = human == actor && !Reachable(self, r, _zones[actor], true);
                if (state.Abandoned) { reason += ";HumanAbandonment;NoAutomaticSteering"; concessions.Add($"XFER-04:{actor}:HumanAbandonment:{id.Value}"); }
            }
            else
            {
                var candidates = frame.Receivers.Where(r => !r.Blocking && _verticalLocks.Contains(r.Id) && MayHelp(job, r))
                    .OrderByDescending(r => Lost(r)).ThenByDescending(r => HelpPriority(job, r)).ThenByDescending(r => Urgency(r, self, _zones[actor])).ThenBy(r => r.Id.Value).ToArray();
                var chosen = candidates.FirstOrDefault();
                var previous = candidates.FirstOrDefault(r => r.Id == state.Helping);
                bool pendingWheel = previous != null && match.Releases.Any(r => r.VerticalDestination == actor
                    && _verticalLocks.Contains(r.Receiver) && _shortReads.GetValueOrDefault(r.Receiver) == ReleaseDirection.Outward
                    && Owners[r.Receiver] != actor);
                // Out/wheel checks retain the existing deep help until the safety actually accepts the wheel.
                if (pendingWheel) chosen = previous;
                if (previous != null && chosen != null && previous.Id != chosen.Id && !Lost(chosen)
                    && (frame.Seconds - state.HelpCommittedAt < DefensiveTuning.CommitmentSeconds
                        || HelpPriority(job, chosen) <= HelpPriority(job, previous)
                        && Urgency(chosen, self, _zones[actor]) < Urgency(previous, self, _zones[actor]) * DefensiveTuning.ChallengerRatio)) chosen = previous;
                if (chosen?.Id != state.Helping) state.HelpCommittedAt = frame.Seconds;
                state.Helping = chosen?.Id; state.Abandoned = false;
                if (chosen != null)
                {
                    kind = DefensiveIntentKind.Help; target = chosen.Position + chosen.Velocity * .6f + new Vector2(0, 3);
                    if (job.Deep) target.Y = MathF.Max(job.Home.Y, target.Y);
                    reason = "DeclaredHelp;ExistingPrimaryRetained";
                    if (job.Role == MatchRole.WeakSafety && match.Trips) { ruleId = "QM-06"; reason += ";PoachInsideOnly;IsolatedCornerHasNoHelp"; }
                    if (job.Role == MatchRole.MiddleSafety && match.Trips) ruleId = "M3-05";
                }
            }
            if (match.Trips && _plan.Call.Match == MatchFamily.Quarters)
                concessions.Add("QM-07:TripsPoach;WeakCornerLocked;NoPromisedDeepHelp");
            if (target.Y >= FieldGeometry.PlayableBackLine) { target.X += target.X < _plan.Surface.CenterX ? 1 : -1; reason += ";MOVE-04:BackLineLateralLeverage"; }
            state.Status = reason;
            intents.Add(new(actor, kind, DefenderMotor.Clamp(target), state.Primary, state.Helping, ruleId, reason, false));
        }
        Current = new(_version, Array.AsReadOnly(_states.OrderBy(s => s.Key).Select(s => new ZoneCommitment(s.Key,
            _zones[s.Key].Area, _zones[s.Key].Home, s.Value.Primary, s.Value.Helping, s.Value.Status, s.Value.Abandoned)).ToArray()),
            transfers.AsReadOnly(), Array.AsReadOnly(concessions.Distinct().ToArray()));
        return intents.AsReadOnly();

        bool Lost(ObservedReceiver r) => Owners.TryGetValue(r.Id, out var owner) && r.Position.Y + MathF.Max(0, r.Velocity.Y) * .6f > actors[owner].Position.Y;
        int HelpPriority(MatchResponsibility job, ObservedReceiver r)
        {
            var release = match.Releases.Single(q => q.Receiver == r.Id);
            return match.Trips && job.Role == MatchRole.MiddleSafety && release.Number == 3 ? 30 : release.Number >= 2 ? 20 : 10;
        }
        bool MayHelp(MatchResponsibility job, ObservedReceiver r)
        {
            var release = match.Releases.Single(q => q.Receiver == r.Id);
            if (!job.Deep) return Vector2.Distance(job.Home, r.Position) <= 7;
            if (job.Role == MatchRole.MiddleSafety) return true;
            if (match.Trips && job.Role == MatchRole.WeakSafety) return release.Side == match.StrongSide && release.Number >= 2;
            return release.Side == job.Side;
        }
        void Log(DefenderSlot from, DefenderSlot to, ReceiverId id, string stage, string reason)
            => transfers.Add(new(_transaction, _version, _plan.RuleVersion, frame.Seconds, from, to, id, stage, reason));
    }
}
