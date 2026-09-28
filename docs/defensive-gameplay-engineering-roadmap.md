# Defensive gameplay: engineering execution roadmap

Updated September 27, 2026. Status: **P0–P5 implemented and verified in their scoped development modes**. P6 coordinated pressure and human blitz controls are next. See the [P0 report](defense-redesign-p00-verification.md), [P1 report](defense-redesign-p01-verification.md), [P2 report](defense-redesign-p02-verification.md), [P3 report](defense-redesign-p03-verification.md), [P4 report](defense-redesign-p04-verification.md), [P5 report](defense-redesign-p05-verification.md), and [rule implementation ledger](defense-redesign-rule-ledger.md).

This roadmap turns the [defensive redesign plan](defensive-gameplay-redesign-plan.md) into ordered engineering work. The design document owns football behavior, including section 12's 98 decision rules and DM-01 through DM-20 acceptance scenarios. This document owns implementation order, work packages, code responsibilities, and completion evidence. Change the corresponding design rule whenever implementation exposes an ambiguity; do not let code and specification silently diverge.

Phase numbers P0-P10 replace the earlier seven-phase outline. Checked P0–P5 packages have evidence in their phase reports. Names in later phases remain proposed unless identified as existing. A checked compiler package does not imply completion beyond its reported calls and DM-scenario scope.

**Delivery sequence**

| Phase | Outcome | Depends on | Milestone |
| --- | --- | --- | --- |
| [P0](#p0) | Reproducible failures, baseline, and diagnostic harness | None | Reliable evidence |
| [P1](#p1) | One defensive plan compiler and identity-safe assignments | P0 | Valid pre-snap defense |
| [P2](#p2) | Complete live Cover 1 slice with observation and movement | P1 | New engine executes a full play |
| [P3](#p3) | Complete man family, help, and bunch behavior | P2 | A: Man coverage correction |
| [P4](#p4) | Coordinated spot zones and deep protection | P3 | Zone correction |
| [P5](#p5) | Run fits, play-action reads, containment, and pursuit | P4 | Coherent pass-to-run transitions |
| [P6](#p6) | Coordinated pressure and usable human blitz controls | P5 | B: Core reported issues addressed |
| [P7](#p7) | Authored Cover 3 Match and Quarters Match | P6 | Genuine match coverage |
| [P8](#p8) | Front variety and complete dime personnel | P7 | Formation expansion |
| [P9](#p9) | Production CPU strategy and functioning package memory | P8 | C: Full redesign implemented |
| [P10](#p10) | Balance, performance, regression closure, and cutover | P9 | D: Release candidate |

Use this linear order by default. A phase can contain several reviewable changes, but its exit gate must pass before dependent gameplay work relies on it. Keep one coherent engine per snap. A development-only new-engine mode initially exposes only fully supported calls; unsupported calls are unavailable with a reason. The normal game stays on the existing engine until P10. Do not mix old assignment repair with a new runtime to fill unfinished responsibilities.

The first useful result is P2's end-to-end Cover 1. Milestone A completes the man family; milestone B addresses man, zone, blitz execution, and human call handling. Match coverage, additional personnel, and CPU strategy remain explicit work after milestone B.

**Completion contract for every phase**

- Check in or retain reviewable changes with a phase report identifying the code revision, engine/plan version, rule IDs, fixtures, seeds, team profiles, field positions, stepping rates, and commands used.
- Run targeted behavioral tests and affected regression suites. Run the full existing suite at each phase gate; compare failures by identity and cause against the refreshed P0 baseline, not only by count.
- Pass new rules' positive, negative, and exit/fallback cases. A screenshot or a correct target point alone does not establish correct live behavior.
- Retain previews, input ownership, clocks, replay, ratings, and physical contact where affected. Update their tests in the same phase as integration, rather than waiting for P10.
- Report what is complete, what remains unsupported, and any design amendments. Phase completion requires evidence; a successful compile is insufficient.
- Keep a row-level rule ledger with `planned`, `implemented`, or `verified`, plus test/trace references. Do not mark all 98 rules verified when only the 20 combined scenarios pass.

Suggested evidence locations are `artifacts/defense-redesign/pNN/` for generated reports and `docs/defense-redesign-pNN-verification.md` for concise phase summaries. These files should be created as work occurs, not filled with speculative passing results now. Confirm artifact ignore rules before adding large generated output to version control.

<a id="p0"></a>
**P0 — Establish the baseline and reproduce failures**

User-visible outcome: reported mistakes can be replayed and explained with a fixed input, formation, and seed.

Owns infrastructure and desired-behavior fixtures, not new defensive behavior. Use design sections 1, 9, 11, and 12.11. All 20 DM scenarios receive fixture definitions here; later phases own passing their new behavior.

Work packages:

- [x] **P0.1: Refresh the baseline.** Run the existing build/test suite. The September 26 planning run had 617 passing and five failing tests out of 622; record the current result instead of assuming it remains true. Triage field-goal, replay, and three punt-decision failures by name. Fix a prerequisite failure only if it blocks trustworthy defense verification, with that change separately explained.
- [x] **P0.2: Add deterministic scenario selection.** Extend the existing scenario registry/launcher with a proposed `defense-redesign-matrix` entry. Define DM fixtures with offensive geometry/call, defensive call, controlled actor input, teams, seed, start spot, duration, and expected transitions. Run production controllers; do not create a second simulator.
- [x] **P0.3: Separate inputs by purpose.** Provide scripted assignment-following input, deliberate human-abandonment input, stationary human input, and a full-AI fixture. The existing defensive launcher chases the ball with the controlled linebacker; that behavior must not become the only baseline for coverage quality. Keep input behavior fixed between old/new comparisons.
- [x] **P0.4: Capture evidence.** Record actor IDs and trajectories, preview assignments, ball/contact events, target changes, current rushers, and terminal outcomes. Label legacy facts accurately; decision-rule reasons become available only as the new runtime is implemented. Retain failing traces and a compact result manifest.
- [x] **P0.5: Freeze initial cases and measurements.** Start with DM-01/02 for pairing, DM-04/06 for deep seams, and DM-17 for input/pressure. Define expected ownership from the design, then capture old-engine behavior. If a report cannot be reproduced, preserve the case as unresolved rather than inventing a failure. Measure ordinary frame cost and allocation as the performance baseline.

Existing code areas: `RetroQB/Development/ScenarioDefinition.cs`, `ScenarioLaunchOptions.cs`, `DefenseScenarioLauncher.cs`, `ReleaseVerification.cs`, `ScriptedInput.cs`; tests in `DevelopmentScenarioTests.cs`, `CoverageAssignmentTests.cs`, `DefensivePlaybookTests.cs`, and `MatchCoverageTests.cs`.

Suggested changes in order: baseline report; fixture/trace harness; initial reproduction report. Desired behaviors that the old engine does not implement should be reported by the comparison harness without making unrelated default tests permanently fail. As a phase implements a rule, promote its behavior to required regression assertions.

Exit gate: a failing reproduction can be rerun with the same input and observed event sequence; known tests are accounted for; hidden offensive data is identified; fixture tolerances and sample manifests are recorded before tuning. New scenario names and arguments are documented only after they are implemented.

<a id="p1"></a>
**P1 — Build the shared plan compiler**

User-visible outcome: the assignment preview represents a legal, coherent set of 11 jobs with sensible man pairings.

Primary rules: ALIGN-01 through ALIGN-03 and ALIGN-05 through ALIGN-07. Implements the contracts in design sections 2 and 3. ALIGN-04's bunch switch and ALIGN-08's pressure adjustment receive final behavioral acceptance in P3 and P6 respectively.

Work packages:

- [x] **P1.1: Define immutable inputs and outputs.** Introduce `OffensiveAlignmentSnapshot`, `DefensiveCallDefinition`, `DefensiveAssignment`, and `ResolvedDefensivePlan`, with stable actor IDs, call/rule version, controlled slot, typed pass responsibility, conditional run role, helper roles, and pressure metadata. Separate runtime state from the plan. Snapshot actual ball, LOS, QB, linemen, and skill-player positions; exclude routes, `IsBlocking`, future releases, and offensive call ID.
- [x] **P1.2: Resolve formation roles.** Identify outside-in receiver order, strength, closed sides, attached threats, backfield players, bunch groups, and actual gaps. Keep these labels stable after snap. Begin with base/nickel and an even front. Preserve fewer-than-five skill threats in heavy/six-lineman formations. Offensive flips re-resolve visible geometry without rerolling unrelated RNG.
- [x] **P1.3: Implement exact man pairing.** Reserve the called shell/help and rush actors first; build legal defender/threat edges; solve globally with bitmask DP or another bounded exact solver. Prefer travel/leverage/locality and suitable player types. No post-solver star swap or safety borrowing. Align linebackers as well as DBs over their final duties, then adjust spacing without changing ownership.
- [x] **P1.4: Validate and instantiate.** Check 11 distinct actors, legal IDs, one active pass responsibility each, complete potential-threat ownership for man, exact shell/rush counts, finite in-bounds locations, and a valid controlled linebacker. Reject invalid plans before preview. Add a passive entity-instantiation adapter to `PlaySetupController`; rendering cannot mutate it.
- [x] **P1.5: Establish migration boundaries.** Compile the standard man-family budgets as data even though P2 initially executes only Cover 1. Retain the complete old engine as the default. Inventory every factory/setup caller, including legacy `GameSession`, and require a named migration/test path for each. Do not let the old uniqueness/repair passes alter a new plan.

Existing code areas: `Gameplay/DefensivePlaybook.cs`, `Factories/DefenseFactory.cs`, `CoverageSchemePolicies.cs`, `DefensiveSurfaceAnalyzer.cs`, `OffensiveSurfaceModel.cs`, `DefensivePersonnel.cs`, `Controllers/PlaySetupController.cs`, `DefensiveDrive.cs`, and `Entities/Defender.cs` (under `RetroQB/`). Proposed grouping: `Gameplay/Defense/` for plan/compiler/validation types; keep physical entities in their current layer.

Acceptance: structural portions of DM-01, DM-02, and DM-18; all current catalog formations and offensive flips against supported base/nickel man budgets; shuffled receiver-list order with unchanged IDs; heavy formations with fewer targets; repeated preview/spawn equality. Explicitly assert DM-01's five primaries and two reserved safeties, including the legal LB mismatch. Live behavior is not yet claimed.

Exit gate: no invalid advertised plan; preview/spawn parity; equal visible input/seed yields equal plans despite hidden route changes. The new compiler is the sole assignment authority for its plans.

<a id="p2"></a>
**P2 — Execute one complete Cover 1 play in the new engine**

User-visible outcome: Cover 1 works from selection through a live pass, catch/run, whistle, and replay using the new contracts.

Primary rules: OBS-01 through OBS-08; DEC-00, DEC-02/03, and DEC-05; MAN-02/03; MOVE-01/02/04/05; PRESS-01. DEC-04's team transfer transaction belongs to P4; DEC-01's coordinated carrier response belongs to P5. Basic pursuit remains an explicit temporary compatibility behavior until P5 supplies the full fit rules.

Work packages:

- [x] **P2.1: Build observation and state.** Introduce `DefensiveObservationFrame` and per-play actor state. Implement observed release/block/run/pass recognition using design 12.1: 0.15-second movement history, 0.12-second stable release recognition, the field-adjusted vertical gate, sustained-block and late-release signals, bounded throw recognition, and commitment hysteresis. Store these as named tuning values with matrix provenance, not scattered literals.
- [x] **P2.2: Implement motor intents.** Add target/leverage/contest/contain intents and a `DefenderMotor` with speed limits, turn limits, arrival slowing, legal field bounds, and teammate steering. Use time-based projection and reachable recovery. Preserve roster modifiers exactly once and keep ball contests in the existing ball controller.
- [x] **P2.3: Execute Cover 1 and four-man contain.** Maintain five locked targets, middle safety help, and an explicit low-helper job. Use MAN-02/03 to prevent unreachable help from justifying reckless trail coverage. Give four rushers real entry/contain intents under PRESS-01 rather than old QB-offset targets. Update the new engine from a common observation snapshot in stable actor order.
- [x] **P2.4: Integrate a complete step.** Wire `DefensiveDrive` and `PlayExecutionController` to new plan/runtime selection in development mode. Compute intents, expose active rush status to protection at the required point, and preserve physical blocking/contact integration. Account for AI and manual movement exactly once. Do not reorder unrelated offensive simulation casually.
- [x] **P2.5: Exercise lifecycle and rendering.** Carry rule/plan versions and diagnostics into replay. Reset state on new plays/restarts, freeze timers on pause/focus loss, and keep the fixed linebacker manual even when idle. Read plan/runtime cues without advancing gameplay during drawing. Run equivalent scenarios with either team defending.

Existing integration points: `AI/DefenderTargeting.cs`, `Controllers/PlayExecutionController.cs`, `BlockingController.cs`, `OverlapResolver.cs`, `BallController.cs`, `ControlContext.cs`, `Gameplay/Replay/`, and defensive/full-match renderers. Proposed runtime types: `DefensiveRuntime`, `DefensiveObservationBuilder`, `DefenderRuntimeState`, and `DefenderMotor`.

Acceptance: full DM-18; observable portions of DM-14 and DM-19; new Cover 1 fixtures for two crossing targets, a beaten corner, reachable/unreachable middle help, and a throw outside contest range. Test 30/60/120 Hz, zero human input, blocking slowdown, pause/resume, and restart. Reuse `DefensiveDriveTests`, `PlayContactTests`, `BallControllerTests`, `PossessionControlTests`, and replay tests where relevant.

Exit gate: one complete production-controller play works under each team role with correct target identity, no hidden route reads, correct physical outcomes, and a trace explaining every decision. No new movement, clock, or control regression. Do not expand the new call menu until this gate passes.

<a id="p3"></a>
**P3 — Complete man coverage and assignment-aware alignment**

User-visible outcome: Cover 0, Cover 1, Cover 2 Man, and Robber have distinct, reliable coverage behavior across alignments.

Primary rules: ALIGN-04; MAN-01 and MAN-04 through MAN-09. Requalifies P1/P2 rules across the man family. Advanced rush execution for pressure versions remains gated until P6; use declared fixed-rush fixtures to test their coverage budgets meanwhile.

Work packages:

- [x] **P3.1: Implement shell-specific leverage/help.** Add Cover 0's self-protected top leverage, Cover 2 Man's two separate deep helpers, and Robber's bounded low-middle jump. Preserve each primary when help changes. Extend the compiler's role bindings rather than appending a reassignment pass.
- [x] **P3.2: Handle traffic and delayed releases.** Keep identity through mesh/crossers and teammate avoidance. Hold/read an assigned back or TE while it blocks, then react to observed release. Verify pressure-replacement DB and LB alignments; no nearest-receiver reassignment when traffic causes separation.
- [x] **P3.3: Add bounded bunch switching.** Define supported two-player release groups, provisional lock pairing, and an atomic inside/outside partition. Commit at most once; retain locks if the 0.40-second classification window expires. A three-player bunch uses explicit locks until a separate three-player rule is authored and tested.
- [x] **P3.4: Make shadow/bracket policy explicit.** Implement MAN-09 only through declared call metadata and valid player budgets. Remove automatic post-resolution star target swaps from the new path. Show primary and helper distinctly in the preview.

Code areas: the new compiler/runtime/motor, `Gameplay/Factories/CoverageSchemePolicies.cs`, `DefensivePlaybook.cs`, and assignment rendering. Regression suites: `CoverageAssignmentTests`, `DefensivePlaybookTests`, `TeamBalanceTests`, `TeammateAvoidanceTests`, and `RedZoneDefenseTests`.

Acceptance: complete DM-01 and the man portion of DM-02; complete DM-03; dedicated Robber/two-high/zero-help conflict fixtures; full catalogue/flip structural sweeps plus multi-second representative runs. Test blocking backs that never release and those that release late. Validate that star ratings remain intact while star status alone cannot change ownership.

Exit gate: milestone A. Every supported man call has complete ownership, coherent final alignment, appropriate leverage/help, and stable crossing/bunch behavior. Losses through contact or speed are identified rather than hidden by reassignment.

<a id="p4"></a>
**P4 — Build coordinated spot zones and deep protection**

User-visible outcome: Cover 2, Cover 3/Buzz, and Cover 4 defend coherent areas while carrying and transferring threats across seams.

Primary rules: DEC-04; Z2-01 through Z2-03; Z3-01 through Z3-04; Z4-01 through Z4-03; ZONE-01; XFER-01 through XFER-04; HUMAN-03.

Work packages:

- [x] **P4.1: Add the coverage board.** Implement `CoverageCoordinator` with role-specific home areas, threat rankings, primary/help commitments, neighboring responsibilities, and explicit concessions. All defenders consume the same observation frame; tie-breaking is stable and commitments obey OBS-08.
- [x] **P4.2: Implement ownership transactions.** Add versioned offer/reserve/accept/decline/commit events. Use design 12.7's initial 0.30-second offer lifetime, 0.15-second post-acceptance help overlap, and reachability margin. Resolve conflicting offers or chained exchanges atomically. A human destination must be reachable from actual position/velocity and remain manually controlled.
- [x] **P4.3: Implement each zone family separately.** Cover 2 uses two deep/five underneath, Cover 3 uses three/four, and Cover 4 uses four/three. Author flat/seam/flood priorities from Z2/Z3/Z4. Implement Buzz as an actual rotation path without adding a defender or silently enabling match coverage.
- [x] **P4.4: Replace movement walls with responsible carries.** Execute ZONE-01's ordinary overlap limit (initial 0.60 seconds or five lateral yards), declined-transfer fallback, and logged emergency deep priority. Deep players anticipate verticals, accept only reachable help, and recover sensibly at the back line. Physical bounds remain hard constraints.
- [x] **P4.5: Update diagnostics and regressions.** Render home area separately from live target/carry, show transfer status and human abandonment, and record vacated responsibility. Preserve tests that express spot-zone intent; leave existing old-engine match restrictions until P7 introduces distinct new semantics.

Code areas: replace new-engine use of `AI/ZoneCoverage.cs` scoring/clamps; integrate with the compiler/runtime, renderers, and replay. Tests: new coverage-board/transfer fixtures alongside `MatchCoverageTests`, `RedZoneDefenseTests`, `Cover3AlignmentTests`, and `DefensivePlaybookTests`.

Acceptance: DM-04, DM-05, DM-06, DM-16, and the spot-zone portion of DM-19. Include both reachable and unavailable helper cases, conflicting transfer offers, a four-vertical conflict, repeated seam crossings, and an intentional flood concession. Row-level cases must also exercise all Z4 rules even though the headline wheel scenario DM-07 belongs to match coverage.

Exit gate: zero unaccepted transfers that release both owners, unexplained duplicate commitments, or zone-boundary abandonment in the fixtures. Deep threats with available defensive capacity remain accounted for; deliberate overload windows remain measurable. Human abandonment never triggers automatic player steering.

<a id="p5"></a>
**P5 — Integrate run fits, play action, and pursuit**

User-visible outcome: the defense retains gap, edge, and cutback structure and reacts coherently when a pass becomes a run or catch.

Primary rules: DEC-01, RUN-01 through RUN-08, and PRESS-11. Uses OBS-06, MOVE rules, and the P4 transfer protocol.

Work packages:

- [x] **P5.1: Compile conditional fits.** Give every supported call named gap owners, force, cutback, and deepest support based on actual offensive gaps. Validate that a primary pass assignment and conditional run role never issue two simultaneous movement intents. A called spy consumes a real helper slot.
- [x] **P5.2: Implement provisional versus confirmed reads.** Bound the read step during ambiguous mesh/play action by the actor's ability to recover to its receiving responsibility. Deep players retain pass priority until observation confirms a run. Remove new-engine shortcuts that make everyone chase a moving QB behind the LOS.
- [x] **P5.3: Implement pursuit roles and replacement.** On inside/outside runs, cutbacks, catches, and missed tackles, choose immediate tackler and support angles together. Transfer contain/fits using actual reachability. Preserve deepest support rather than targeting the carrier's current point with every defender.
- [x] **P5.4: Close the temporary compatibility behavior.** Replace P2's basic pursuit adapter with the new fit/runtime rules. Keep scoring, tackle breaks, and contact outcomes in existing controllers. Verify aborted exchanges, QB escape/reversal, and plays that remain live without progress.

Code areas: new defensive runtime and fit coordinator, `BackfieldController.cs`, `DefenderTargeting.cs` compatibility paths, `TackleController.cs`, `PlayContact.cs`, and `OverlapResolver.cs`. Regressions: `BlockingAndExchangeTests`, `PlayActionMovementTests`, `PlayContactTests`, `DefensiveDriveTests`, and full-match flow tests.

Acceptance: full DM-14 and DM-15; a four-man-contain version of QB escape (the five-man DM-11 completes in P6); after-catch pursuit and spy-budget cases. Exercise a blocked force actor, a missed first tackle, a late throw from outside the pocket, and confirmed QB runs at 30/60/120 Hz.

Exit gate: no premature universal pass-coverage abandonment; no duplicated active fit commands; real handoffs/catches/runs resolve through shared rules; run and pass weaknesses reflect observed decisions and physical execution.

<a id="p6"></a>
**P6 — Deliver coordinated pressure and human blitz controls**

User-visible outcome: blitz calls have understandable jobs, real coordinated paths, valid replacement coverage, and functioning side/variant controls.

Primary rules: ALIGN-08; MOVE-03; PRESS-02 through PRESS-10 and PRESS-12; HUMAN-01/02. Requalifies PRESS-01/11 with added rushers. This phase is four ordered work packages with separate checkable changes; do not combine the entire phase into an unreviewable patch.

- [x] **P6.1: Fixed pressure packages.** Implement five-man Mike, edge, and nickel plus six-man Cover 0 in the compiler and `PressureCoordinator`. Declare entry/alternate gaps, approach/engage/close states, contain actors, and complete replacement coverage. Add legal variant filtering and finite spacing/path checks before preview. Verify five-man Cover 1 has five primaries plus one deep helper, and six-man Cover 0 has five primaries with no deep help.
- [x] **P6.2: Real human workflow.** Wire number keys including 9/0 through `InputManager`, input contracts, `FullMatchSession`, and `DefensiveDrive`. Add X pressure-side re-resolution; never call the existing geometry-only mirror against unchanged offensive targets. After input-context checks, expose supported pre-snap Q/E variants and a clear call/personnel/rush/controlled-job display. Preserve actor identity within a personnel package, snap deadline, offensive call, and RNG state. Test call -> preview -> snap -> actual movement, including idle/manual blitzers.
- [ ] **P6.3: Zone and simulated pressure.** Add five-rusher three-deep/three-underneath packages and four-rusher simulated pressure with explicit line drops. Validate drop reachability and roster capability. Update protection with current active rush/drop status; a dropping defender must not retain a stale rusher flag or create a fake free runner.
- [ ] **P6.4: Conditional pressure and counters.** Add green dog on sustained observed blocks, late-release recovery, the named 0.45-second delayed package, and the two-player stunt with initial 0.50-second maximum wait. Every timer is cancellable by the specified observed event. Add PRESS-12 screen recognition without offensive play metadata. Track planned versus actual rush participation and failure reasons; manual actors receive cues rather than automatic launches.

Code areas: new pressure definitions/coordinator and call compiler, `PassProtectionPlanner.cs`, `Controllers/BlockingController.cs`, `BlockingSteering.cs`, input interfaces/manager, `FullMatchSession.cs`, `DefensiveDrive.cs`, `DefensivePlaySession.cs`, full-match/defensive renderers, and replay/outcome metadata contracts.

Acceptance: DM-10, DM-11, DM-12, DM-13, and DM-17 in their full forms. Exercise slide protection, real blocker contact, RB late release while a green-dog defender is already engaged, stalled stunts, QB reversals, early throws, and invalid replacement variants. Reuse `PassProtectionMovementTests`, `BlockingAndExchangeTests`, `DefensivePlaySessionTests`, and live-control tests. Simulated four-man pressure must remain four actual rushers; an idle human must reduce actual participation rather than trigger an extra AI rusher.

Exit gate: milestone B. All four original issue areas have a verified implementation in the new engine. Rushers use distinct/ordered entries; contain and coverage replacement remain explicit; the displayed call executes through real inputs and physics. Match rules, dime, and strategic CPU calling are still pending.

<a id="p7"></a>
**P7 — Implement authored match coverage**

User-visible outcome: Cover 3 Match and Quarters Match react to releases through their declared rules rather than acting as renamed spot zones.

Primary rules: M3-01 through M3-07 and QM-01 through QM-09. Requires P4's transaction engine and P5's pass/run transition behavior.

Work packages:

- [ ] **P7.1: Version match definitions.** Add explicit functional-role bindings, provisional target ownership, release tables, legal formations, and fallback checks. Keep existing Buzz/Quarters Zone calls distinct from new match behavior. Define an ID/version migration so old recorded calls retain their recorded label/meaning rather than being silently interpreted as the new scheme.
- [ ] **P7.2: Cover 3 Match.** Implement outside vertical locks, apex #2 decisions, shallow-transfer chains, the five-owner/two-helper trips check, and closed-side TE ownership. A transfer chain either commits completely or preserves previous owners.
- [ ] **P7.3: Quarters Match.** Implement two-by-two #1/#2 rules and named Trips Poach. For trips, strong safety starts accountable for #2, strong apex carries #3 until weak safety accepts, weak corner locks isolated #1, and middle owns RB. Never make strong apex provisionally carry both #2 and #3. Keep the isolated corner's lack of help explicit.
- [ ] **P7.4: Fallbacks and regression changes.** Unsupported geometries select a named legal check before preview or remain unavailable. Preserve matched IDs through subsequent cuts unless a legal transition occurs. Split old tests that universally require local zones: retain their spot-zone coverage and replace match restrictions with ownership/transfer assertions in this same change.

Code areas: match-call definitions/compiler, `CoverageCoordinator`, observation/runtime state, `DefensivePlaybook.cs`, call rendering and replay; retire or bypass old `ApplyMatchCoverageAssignments` behavior only where the new semantics are active.

Acceptance: complete DM-02's match portion, DM-07, DM-08, and both Cover 3/quarters versions of DM-09. Add matrix-row fixtures for short #1 with vertical #2, stopped/double-moving receivers, helper refusal, one-sided/closed formations, and supported fallback checks. Re-run DM-04/06 to prove spot-zone behavior still differs correctly.

Exit gate: all 16 M3/QM rules pass their applicable trigger/action/exit cases. Primaries and helpers remain distinguishable; backside help never abandons an isolated primary without a declared check; new calls have accurate names/previews and recorded semantics.

<a id="p8"></a>
**P8 — Expand fronts and add dime personnel**

User-visible outcome: defensive formation choices have useful, visible personnel and gap tradeoffs while preserving a controllable linebacker.

Owns design section 3's remaining formation expansion. Requalifies ALIGN, MAN, zone, match, pressure, and control rules in the new personnel/front combinations; adds no duplicate decision-rule ownership.

Work packages:

- [ ] **P8.1: Author front templates.** Complete over/under, wide, and short-yardage fronts against the actual line surface, complementing the even front from P1. Specify alignments, entry gaps, run fits, and containment for each supported call family. Do not make front changes an unrelated random position jitter.
- [ ] **P8.2: Add 4-1-6 dime.** Append the new DB slot without renumbering existing enum values; extend position helpers, default profiles, team presets, stars/ratings, controlled-slot selection, diagnostics, and rendering. Assign one valid linebacker to human control and test it explicitly.
- [ ] **P8.3: Define compatibility and user selection.** Add legal personnel/front/call combinations and clear invalid reasons. Retain Auto as the quick default; add an optional pre-snap selector, with its key chosen after an input audit, showing active personnel/front and the resulting controlled actor. Personnel may change control identity before snap; pressure-side changes within unchanged personnel may not. Neither can reset CPU cadence.
- [ ] **P8.4: Requalify the catalog.** Run the compiler across every supported offensive formation/flip and every advertised defensive combination. Add dime versions of DM-01, trips/match tests, and pressure-replacement cases. Validate ratings/star boosts exactly once and compatibility of stored records/replay metadata.

Code areas: `Data/DefensiveRoster.cs`, team presets/attributes, personnel/front templates, plan compiler, `ControlContext.cs`, input/call selection, renderers, replay, and any serialization using roster slots. Regressions: `TeamDefinitionTests`, `TeamPresetTests`, `TeamBalanceTests`, `DefensiveDriveTests`, `PlayerRecordStoreTests`, and formation/catalog sweeps.

Exit gate: base, nickel, and dime each field 11 valid actors with named responsibilities and one supported human linebacker; front changes affect the declared fits/paths; legal combinations pass representative live cases. Exotic 3-4 fronts and unrestricted pre-snap movement remain outside this roadmap.

<a id="p9"></a>
**P9 — Connect CPU strategy and adaptive memory to live games**

User-visible outcome: CPU defensive calls reflect situation, personnel, team strengths, and observed opponent behavior in normal timed games.

Primary rules: CALL-01 through CALL-08. Uses the validated catalog and actual pressure outcomes supplied by earlier phases.

Work packages:

- [ ] **P9.1: Replace production uniform selection.** Wire `DefensivePlaycaller` into `DefensiveDrive.PrepareActors` in the new engine. Filter legal calls/personnel/fronts first; then apply score/clock objective, visible alignment, team tendencies, and bounded history. Sample using a dedicated deterministic RNG stream; browsing offensive or defensive previews must not repeatedly reroll the CPU choice.
- [ ] **P9.2: Implement situational score terms.** Add short-yardage/heavy, long-yardage/observed slow passing, spread mismatch, mobile QB, quick throws/screens, lead protection, and urgent-stop terms exactly as CALL-01 through CALL-07 specify. Log the score contributions so a selected call is explainable. Add real spy variants only where their helper budget is legal.
- [ ] **P9.3: Repair the result-to-memory contract.** Extend `PlayEnded`/completed-play metadata with stable call/front/coverage/pressure IDs, plan version, actual rush participants, arrival/containment outcomes, and explicit human abandonment. Record once for the defending team. Update memory under the same IDs the caller reads; never derive package identity from display labels or counts.
- [ ] **P9.4: Bound and verify learning.** Calibrate learning for short matches, preserve legal-call variety, cap response to small samples, and avoid crediting every incompletion to pressure. Test duplicate-result handling, restart/reset, side changes, and equal-input plan parity between human and CPU selection.

Code areas: `AI/DefensiveCoordinator.cs`, `DefensiveMemory.cs`, `Gameplay/Factories/BlitzDecisionStrategy.cs`, `DefensiveDrive.cs`, `Match/PlayEnded.cs`, `MatchState.cs`, `TeamMatchState.cs`, and replay/stat metadata adapters. Retire the mismatched package-name versus blitzer-label memory path after all callers migrate.

Acceptance: DM-20, plus per-CALL-rule tests with one input varied at a time. Freeze seed batches and initial expected directional bands before tuning. Inspect actual timed-game selected calls, not just a helper's calculated weights. Re-run DM-18 with metadata permutations and verify neither the selection nor runtime has access to the current offensive script.

Exit gate: milestone C. All 98 rules have an implemented owner and passing phase evidence. Production-path CPU selections respond in the intended direction without illegal plans or hidden information; recorded outcomes affect the exact package selected later. Full release evidence remains P10 work.

<a id="p10"></a>
**P10 — Validate gameplay, optimize, and cut over**

User-visible outcome: the new defense becomes the normal game experience with verified tradeoffs, stable controls, and no new regression failures.

Owns the release gate for all rules, all DM scenarios, and design sections 9-10. No football rule is considered newly implemented here to fill earlier gaps.

Work packages:

- [ ] **P10.1: Run the broad matrix.** Sweep all current offensive catalog formations and flips, advertised defensive combinations, field positions, team matchups, both team/control roles, and 30/60/120 Hz stepping. Include failure-side variants: unavailable help, blocked paths, contact separation, late releases, and manual abandonment. Preserve the fixed seeds and compare old/new reports without assuming equal outcomes are desirable.
- [ ] **P10.2: Judge football quality.** Compare ownership gaps, accepted/declined handoffs, seam oscillation, deep leverage, time to pressure, contain escapes, sack/explosive-gain rates, run-fit integrity, and human contribution. Report sample counts and variation. Tune only within the named matrix values or amend the rule with rationale. Preserve genuine offensive answers to each coverage.
- [ ] **P10.3: Perform interactive review.** Play both sides at representative window sizes/team strengths. Verify that man lines, zones, rush paths, controls, and changing assignments are readable; compare deliberate good/poor linebacker positioning. Test six-second preparation/cadence behavior as currently implemented, rather than introducing a new timing rule. Record hands-on observations separately from simulation claims.
- [ ] **P10.4: Profile and harden.** Measure compiler latency, p95 runtime cost, allocations, and bounded trace/replay memory after warmup in release configuration, excluding rendering overhead. Compare the provisional under-5%-of-a-60-Hz-frame defensive-decision budget to the measured P0 baseline. Document any changed target before claiming compliance.
- [ ] **P10.5: Complete migration.** Route normal timed games and every still-supported legacy/development entry point through the compiler/runtime. Delete superseded repair passes, independent zone selection, old rush heuristics, temporary pursuit adapters, and development engine switch once comparison evidence is preserved. Unsupported legacy adapters should be explicitly retired with their tests/docs, not left as a second behavior source.
- [ ] **P10.6: Release documentation and gate.** Update README controls/call descriptions, team/front tradeoffs, diagnostics instructions, and the rule/scenario ledger. Validate score-record compatibility, replay after possession change, overtime, period transitions, restarts, and special teams. Account for the refreshed P0 failures by name; no newly introduced failure is acceptable.

Acceptance: DM-01 through DM-20 plus row-level rule assertions, existing full-suite regressions, full-match/season headless runs, real input and render captures, and interactive review. The current `release-catalog` launcher samples a defensive call per case rather than the full defensive Cartesian product; extend its manifest or use the new matrix harness to cover all promised combinations. Do not describe the old launcher alone as exhaustive verification.

Exit gate: milestone D. A release report links every rule and scenario to evidence, reports any known unrelated issue accurately, confirms normal gameplay uses the new engine, and separates automated guarantees from hands-on balance judgment. No missing phase work is hidden behind tuning or completion of the test suite.

**Rule ownership ledger**

Each of the 98 design rule IDs has exactly one phase accountable for its initial completed behavior. Earlier phases may scaffold inputs and later phases must requalify integrations. P0 provides the harness; P8 expands the combinations; P10 owns release verification.

| Rule IDs (inclusive) | Owning phase |
| --- | --- |
| OBS-01 through OBS-08 | P2 |
| DEC-00 | P2 |
| DEC-01 | P5 |
| DEC-02 through DEC-03 | P2 |
| DEC-04 | P4 |
| DEC-05 | P2 |
| ALIGN-01 through ALIGN-03 | P1 |
| ALIGN-04 | P3 |
| ALIGN-05 through ALIGN-07 | P1 |
| ALIGN-08 | P6 |
| MAN-01 | P3 |
| MAN-02 through MAN-03 | P2 |
| MAN-04 through MAN-09 | P3 |
| Z2-01 through Z2-03 | P4 |
| Z3-01 through Z3-04 | P4 |
| Z4-01 through Z4-03 | P4 |
| ZONE-01 | P4 |
| M3-01 through M3-07 | P7 |
| QM-01 through QM-09 | P7 |
| XFER-01 through XFER-04 | P4 |
| MOVE-01 through MOVE-02 | P2 |
| MOVE-03 | P6 |
| MOVE-04 through MOVE-05 | P2 |
| PRESS-01 | P2 |
| PRESS-02 through PRESS-10 | P6 |
| PRESS-11 | P5 |
| PRESS-12 | P6 |
| RUN-01 through RUN-08 | P5 |
| CALL-01 through CALL-08 | P9 |
| HUMAN-01 through HUMAN-02 | P6 |
| HUMAN-03 | P4 |

**Scenario completion ledger**

These phases own the first full passing form of each DM scenario. Structural/preliminary subsets are named in earlier phases and must not be reported as full scenario completion. P8 repeats applicable cases for new fronts/dime; P10 reruns everything.

| Scenario | Owning phase | Concrete acceptance focus |
| --- | --- | --- |
| DM-01 | P3 | Four-WR nickel Cover 2 Man, complete primaries, preserved two-high help, stable IDs |
| DM-02 | P7 | Twins/closed-side TE across man and match, including offensive flip |
| DM-03 | P3 | Bunch switch commits once; later crossing preserves identities |
| DM-04 | P4 | Four verticals versus Cover 3 with two accountable seam carriers |
| DM-05 | P4 | Cover 2 fade/seam/flat conflict and deliberate short concession |
| DM-06 | P4 | Deep post handoff, refusal, overlap, and corner recovery |
| DM-07 | P7 | Quarters short out becomes wheel; safety accepts before apex releases |
| DM-08 | P7 | Trips Poach ownership including the isolated weak fade |
| DM-09 | P7 | Trips shallow #3 versus opposite releasing RB in both match families |
| DM-10 | P6 | Green dog launches on observed block and reacts to actual late release |
| DM-11 | P6 | Five-man edge pressure with real near/backside containment |
| DM-12 | P6 | Blocked stunt reaches its bounded fallback without collision shortcuts |
| DM-13 | P6 | Zone/simulated pressure have the correct live rush/drop counts |
| DM-14 | P5 | Ambiguous play action preserves deep responsibility with bounded box reaction |
| DM-15 | P5 | Outside run, cutback, and missed tackle preserve coordinated support |
| DM-16 | P4 | Reachable human handoff, cue, then recorded abandonment without auto-steering |
| DM-17 | P6 | Real 9/0 selection, two pressure flips, preview/snap parity, manual blitz |
| DM-18 | P2 | Hidden route/block metadata cannot affect defense before visible divergence |
| DM-19 | P7 | Goal-line leverage across all implemented coverage families and orientations |
| DM-20 | P9 | Actual CPU call distributions and stable package outcome/memory identity |

**Commands and evidence discipline**

Existing commands that can support phase gates, run from the repository root:

```powershell
dotnet build
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj
dotnet build -c Release
dotnet run -- --scenario defense-deep --headless --seed 101 --output artifacts/defense-redesign/p00/legacy-deep
dotnet run -- --scenario release-matches --headless --output artifacts/defense-redesign/p10/matches
dotnet run -- --scenario timed-layout --capture --output artifacts/defense-redesign/p10/layouts
```

These commands do not prove new rules by themselves. P0 added `defense-redesign-matrix --headless` and `--hz 30|60|120`. P1 added `defense-plan-matrix --headless` for the 6,000-case passive compiler/preview sweep. P2–P4 added `defense-runtime-matrix --headless --engine cover1|man|zone`, also accepting `--hz`. Defensive live scenarios accept `--engine legacy|cover1|man|zone`; all matrix scenarios accept `--seed` and `--output`. Exact commands and results are in the phase reports. Debug scenarios and release-build profiling are distinct paths; the normal Release application rejects development scenario arguments.

For each phase report, include: completed work-package IDs; implemented/verified rule IDs; full versus partial DM cases; exact commands and environment; counts and failure identities; artifact locations; design/tuning changes; and the next unmet gate. A later implementation session should be able to resume from that report and the repository state without relying on chat history.
