using System.Numerics;
using RetroQB.Entities;

namespace RetroQB.Gameplay.Defense;

public sealed partial class PressureCoordinator
{
    private bool _stuntResolved, _stuntFallback, _conditionalActive, _conditionalCancelled;
    private float _conditionalSeconds;
    private readonly Dictionary<int, float> _screenEvidence = new();

    private static bool Participating(DefenderMotion actor, Vector2 target, DefenderSlot? human)
    {
        var direction = target - actor.Position;
        return actor.Id != human || actor.Contact || direction.LengthSquared() > .000001f
            && Vector2.Dot(actor.Velocity, Vector2.Normalize(direction)) > .5f;
    }

    private void ResolveStunt(DefensiveObservationFrame frame)
    {
        if (_stuntResolved || _plan.Pressure.Stunt is not { } stunt) return;
        if (frame.ConfirmedCarrier || frame.Visible.Ball.State != BallState.HeldByQB) { _stuntResolved = true; return; }
        var pen = frame.Visible.Defenders.Single(a => a.Id == stunt.Penetrator);
        var loop = frame.Visible.Defenders.Single(a => a.Id == stunt.Looper);
        var entry = _plan.Pressure.Entries.Single(e => e.Actor == stunt.Looper).Entry;
        bool clear = !pen.Contact && pen.Position.Y <= _plan.Offense.LineOfScrimmage - 1
            && frame.Visible.Defenders.Where(a => a.Id != loop.Id).All(a => SegmentDistance(a.Position, loop.Position, entry) > a.Radius + loop.Radius)
            && frame.Visible.Linemen.All(a => SegmentDistance(a.Position, loop.Position, entry) > a.Radius + loop.Radius);
        if (clear) _stuntResolved = true;
        else if (frame.Seconds + .00001f >= stunt.MaxWait)
        { _stuntResolved = _stuntFallback = true; _states[stunt.Looper].Gap = stunt.FallbackGap; }
    }

    // Called after ordinary coverage decisions: a conditional launch replaces its old job
    // atomically, while a cancelled launch retains the normal coverage/recovery intent.
    public void ApplyConditionalAndScreens(DefensiveObservationFrame frame, IDictionary<DefenderSlot, DefensiveIntent> intents,
        DefenderSlot? human, List<string> events)
    {
        float dt = MathF.Max(0, frame.Seconds - _conditionalSeconds); _conditionalSeconds = frame.Seconds;
        var jobs = Current!.Jobs.ToList();
        bool released = frame.ConfirmedCarrier || frame.Visible.Ball.State != BallState.HeldByQB;
        if (_plan.Pressure.Conditional is { } conditional)
        {
            var actor = frame.Visible.Defenders.Single(a => a.Id == conditional.Actor);
            var receiver = frame.Receivers.FirstOrDefault(r => r.Id == conditional.ReleaseKey);
            bool green = conditional.ReleaseKey != null;
            if (released) _conditionalCancelled = true;
            bool free = _states.Values.All(s => s.Gap != conditional.Gap);
            bool trigger = green ? receiver is { Blocking: true, LateRelease: false }
                : frame.Seconds + .00001f >= conditional.Delay && frame.Run?.Read == RunRead.Pass;
            bool launch = !_conditionalCancelled && !released && trigger && free;
            if (_conditionalActive && !launch)
                events.Add(green ? $"PRESS-08:ReleaseKey{conditional.ReleaseKey!.Value.Value};CancelRush;RealRecovery;Contact={actor.Contact}"
                    : "PRESS-09:LaunchCancelled");
            if (!_conditionalActive && launch) events.Add(green ? "PRESS-08:ObservedSustainedBlock;ConditionalRush" : "PRESS-09:DelayAndVisibleTriggerSatisfied");
            _conditionalActive = launch;
            string rule = green ? "PRESS-08" : "PRESS-09";
            string reason = released ? "ThrowOrRun;LaunchCancelled" : !free ? "EntryOccupied;RetainPriorJob"
                : green ? receiver?.LateRelease == true ? "LateRelease;RecoverSameReceiver;AvailableHelpOnly" : "ObserveBack;RetainReleaseKey"
                : "HoldAuthoredStagingRole";
            Vector2 target = intents[actor.Id].Target;
            bool participating = false;
            if (launch)
            {
                target = actor.Position.Y <= _plan.Offense.LineOfScrimmage - 1 ? frame.Visible.Quarterback
                    : new(_plan.Surface.Gaps.Single(g => g.Index == conditional.Gap).X, _plan.Offense.LineOfScrimmage - 1.5f);
                target = DefenderMotor.Clamp(target); participating = Participating(actor, target, human);
                reason = green ? "ConditionalRush;BackNoLongerHasLivePrimary" : "TimedRush;AllReceiversRetainOwners";
                if (actor.Id == human && !participating) reason = "HUMAN-02:ConditionalCue;ManualActorNotParticipating";
                intents[actor.Id] = new(actor.Id, DefensiveIntentKind.Rush, target, null, null, rule, reason, participating);
            }
            else if (!released)
            {
                if (!green)
                {
                    target = _plan.Assignments.Single(a => a.ActorId == actor.Id).Alignment;
                    intents[actor.Id] = new(actor.Id, DefensiveIntentKind.Spy, target, null, null, rule, reason, false);
                }
                else intents[actor.Id] = intents[actor.Id] with { Rule = rule, Reason = reason };
            }
            jobs.Add(new(actor.Id, conditional.Gap, released ? PressureStage.Released : launch ? actor.Contact ? PressureStage.Engage : PressureStage.Approach : PressureStage.Staging,
                target, ContainRole.None, true, participating, actor.Id == human, reason));
        }

        var outlets = frame.Receivers.Where(r => !r.Blocking && r.Position.Y <= _plan.Offense.LineOfScrimmage + 4 && r.Velocity.LengthSquared() > 1).ToArray();
        ObservedReceiver? screen = null;
        foreach (var blocker in frame.Visible.Linemen)
        {
            var original = _plan.Offense.Linemen.FirstOrDefault(b => b.Id == blocker.Id);
            bool advancing = !released && original != null && blocker.Position.Y >= original.Position.Y + 1.5f && blocker.Velocity.Y > 1;
            _screenEvidence[blocker.Id] = advancing ? _screenEvidence.GetValueOrDefault(blocker.Id) + dt : 0;
            if (_screenEvidence[blocker.Id] + .00001f < DefensiveTuning.ReleaseSeconds) continue;
            screen = outlets.Where(r => r.Position.Y < blocker.Position.Y && MathF.Abs(r.Position.X - blocker.Position.X) <= 5
                && Vector2.Distance(r.Position, blocker.Position) <= 12).OrderBy(r => r.Id.Value).FirstOrDefault();
            if (screen != null) break;
        }
        if (screen != null)
        {
            var responder = jobs.Where(j => j.Participating && j.Contain == ContainRole.None && j.Actor != _plan.Pressure.Conditional?.Actor)
                .Select(j => frame.Visible.Defenders.Single(a => a.Id == j.Actor)).Where(a => !a.Contact && Vector2.Distance(a.Position, screen.Position) <= 10)
                .OrderBy(a => Vector2.Distance(a.Position, screen.Position)).ThenBy(a => a.Id).FirstOrDefault();
            if (responder != null)
            {
                var target = DefenderMotor.Clamp((frame.Visible.Quarterback + screen.Position) / 2 + Vector2.UnitY);
                intents[responder.Id] = new(responder.Id, DefensiveIntentKind.Help, target, null, screen.Id, "PRESS-12",
                    "ObservedReleasedBlockerAndOutlet;OccupyRallyLane;OwnerRetained", false);
                int index = jobs.FindIndex(j => j.Actor == responder.Id);
                jobs[index] = jobs[index] with { Stage = PressureStage.Screen, Target = target, Participating = false, Reason = "PRESS-12:CombinedVisibleMovement" };
            }
        }
        jobs = jobs.Select(j => j with { Participating = intents[j.Actor].ActiveRusher }).ToList();
        Current = new(jobs.Count, jobs.Count(j => j.Participating), jobs.AsReadOnly());
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var line = b - a; float t = line.LengthSquared() < .001f ? 0 : Math.Clamp(Vector2.Dot(p - a, line) / line.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + line * t);
    }
}
