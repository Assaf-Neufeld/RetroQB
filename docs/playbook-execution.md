# Playbook execution — phase 2

Phase 2 was committed as `4312a3b`. This document records that phase's boundary;
see [phase 3 expansion](playbook-expansion.md) for the current catalog and interface.

Phase 1 is committed as `50325f8`. Phase 2 adds route stages, explicit blocking jobs, and
coordinated backfield actions. The catalog still contains the original 18 named calls and
two wildcards. New personnel (including a named FB), formation families, route names,
situational call-sheet selection, and playbook organization remain phase 3 work.

## Route execution

`PlayerAssignment.RouteDefinition` optionally supplies immutable `RouteStep` endpoints,
per-step holds, an initial delay, a coverage read point, and a finish policy. Offsets are
relative to the route origin; positive X means outward from the player's side and positive
Y means upfield. Existing `RouteType` values resolve to default definitions.

`RouteGeometry.GetPath` creates the one field-clamped path used by `RouteRunner` and
`RouteVisualizer`. The runner visits endpoints in order, limits movement to avoid skipping
turns, and advances from actual position. A blocked or displaced receiver cannot advance
just because time passed or because its Y coordinate crossed a stem depth. Coverage read
completion also uses route-stage progress.
Intermediate turns without a hold accept a half-yard arrival radius so contact and
avoidance do not trap runners beside a waypoint. Holds and final endpoints remain exact.

`RouteExecutionState` belongs to each receiver. Waiting, running, holding, continuing,
settled, completed, and scrambling are distinct states. Deliberate stops do not trigger
the previous implicit scramble fallback, and man-coverage shakes cannot move a stationary
receiver off a hold. QB scramble support requests scrambling explicitly. Targeted receivers
can still adjust to an airborne pass, and a catch transfers movement to the player.

For example, a comeback can be authored as:

```csharp
new RouteDefinition(
    [new RouteStep(new Vector2(0, 8)), new RouteStep(new Vector2(2, 5))],
    RouteFinish.Settle);
```

A wheel uses an outward segment followed by a vertical segment. A hold-and-go adds a hold
to the intermediate endpoint. These capabilities are tested without adding catalog plays.
Custom-route situational scoring uses authored depth rather than its legacy route label.

## Blocking jobs

`BlockingAssignment` names a job and its landmark geometry: pass protection, drive, edge
seal, lead, kick-out, or pull. Landmarks can be relative to home X and LOS, field center and
LOS, or the live QB. An optional approach landmark provides a path behind the line before
engagement. Drive direction and backside status are also explicit.

`BlockingPlanner` translates existing concept rules into default jobs once during play
resolution. A play may override individual skill-player jobs and every lineman's job.
`ResolvedPlay` mirrors all job offsets and drive directions together with player geometry.
Linemen receive their resolved jobs when instantiated; skill blockers use slot assignments.

`BlockingSteering` handles landmarks and target selection for both groups. Lead/pull jobs
finish their approach before engaging, gap blockers choose threats near the called landmark,
kick-outs favor edge defenders, and stale targets are released. Offensive-line and
skill-player contact calculations retain their existing strength, slowdown, double-team,
and shedding formulas. Target selection is now separate from those calculations.

Pre-snap blocking lines use the same landmarks, including pulling paths and skill-player
jobs. Catch/scramble support remains dynamic rather than a pre-snap job.
Default pass-protecting backs hold their formation position in the pocket when idle,
instead of following the live QB. Defender selection and engagement still react to
threats; explicit blocking assignments can specify other landmarks.

For a block-and-release assignment, use role `Route`, provide a blocking job, and set
`ReleaseAfterSeconds` to a positive duration. The player remains eligible and keeps the same
target number while blocking, then runs the route. A catch during the blocking phase still
gives control to the ball carrier.

## Backfield actions

`BackfieldSequence` defines no action, a handoff, or a play-action fake. It specifies the
participant, QB-relative mesh offset, minimum delay, movement speed, mesh radius, fake
duration, and fake-approach timeout. Runs default to the designated carrier and the legacy
concept's mesh geometry. Explicit sequences are validated against personnel and play family.

`BackfieldController` owns simulation-time progression and possession changes. The execution
controller advances it once per active frame, moves the participant, and attempts the
exchange after movement. Play setup resets it; a different ball instance also resets it.
Possession changing away from the QB cancels a pending exchange.

- A handoff requires the designated back to reach the mesh and any minimum delay to expire.
  The QB loses possession exactly once. Draws now wait 0.55 seconds before exchange.
- During a play-action approach/fake, throwing is disabled and the RB never
  actually receives the ball. The actors show the fake using existing animation poses.
  Throwing and the RB's normal assignment resume after the fake. A blocked approach times
  out so the QB cannot remain locked indefinitely.
  Movement input immediately cancels the fake and gives the QB normal movement and
  throwing control while the RB releases into his route. The QB and exchange participant
  can touch during the fake without collision separation moving the exchange point.
- The RB's pre-snap diagram includes the approach to the exchange point. Run diagrams
  then show the called lane (movement becomes player-controlled after possession).
  Play-action route offsets begin at the exchange point, shared by the diagram and
  execution, so a released back does not retrace waypoints from his original alignment.
  An aborted fake starts its release route from the back's current position instead.
- Optional opening line jobs transition to the main jobs when the exchange/fake completes.
  Draws use pass-protection opening jobs and then their run jobs. Existing plays without
  distinct opening jobs simply use the same jobs throughout.

The existing `PA Deep` still has its legacy empty personnel; phase 2 does not silently
change its formation. Play-action and block-and-release are exercised through authored test
fixtures and are ready for production calls in phase 3.

## Intentional behavior changes and limits

- `DoubleMove` now actually breaks inward and then turns upfield; previously it executed a dig.
- Route diagrams now end at shared, field-clamped waypoints. Finishing a route may continue
  along the final segment, but deliberate settles no longer wander.
- Pullers use an approach behind the line. Lead and kick-out jobs do not reuse RB pocket
  protection. Blocking movement and target choice consequently differ, while contact
  strength formulas are retained. Phase 3 will need gameplay balance tuning across its
  larger catalog.
- A route step consumes at least one movement update when traveled; large timesteps are
  capped at the next waypoint rather than skipping corners. The tests cover common frame
  steps and unusually large updates.
- No adaptive option-route decisions, new run concepts, screen-specific line rules, or
  full football formation-legality system are introduced here.

## Verification

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj
dotnet run --project tests/VisualPreview/VisualPreview.csproj -- --readme artifacts/phase2/playbook-presnap.png
```

The suite covers the phase 1 invariants, shared route endpoints, backward/multi-turn routes,
holds and delays, progress under displacement, explicit scrambling, mirrored paths and
blocking landmarks, gap/edge targeting, delayed release and catches, timed draws,
play-action possession/throw gates/timeouts, invalid plans, and finite simulated snaps for
every current call in both orientations.
