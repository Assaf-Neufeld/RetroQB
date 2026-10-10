# Offensive improvements: three-step engineering plan

Date: October 10, 2026. Status: **Steps 1–3 implemented; final automated regression checks pass; human balance/playtesting acceptance remains pending**. See the [Step 1 verification report](offensive-improvements-step-1-verification.md), [Step 2 verification report](offensive-improvements-step-2-verification.md), [Step 3 verification report](offensive-improvements-step-3-verification.md), and [final review](offensive-improvements-final-review.md) for coverage, balance results, fixes, and remaining limits. The final review resolves the five failures recorded in the earlier reports.

Scope: sharper passing concepts, distinct run schemes, coordinated protection, RB/WR screens, designed rollouts, smarter CPU offense, favorites, and full-playbook browsing. **Pre-snap audibles and motion are deferred.** Browsing selects a complete existing call through normal play selection; it does not introduce an audible system.

Build on the existing `PlayDefinition` → `ResolvedPlay` → setup/execution/preview flow. Preserve permanent call IDs, roster-slot targeting numbers, whole-play mirroring, physical contact, team attributes, and human possession control. Extend the current model rather than introducing a separate offensive simulator. New types and fields below are proposed contracts.

| Step | Deliverable | Dependency |
| --- | --- | --- |
| 1 | Coordinated passing concepts and protection | Existing playbook/runtime |
| 2 | Distinct runs, screens, and bootlegs | Step 1 contracts and protection |
| 3 | CPU strategy, playbook access, and release balance | Steps 1–2 playable |

## 1. Make passing concepts and protection coherent

**Outcome:** existing pass calls create deliberate passing windows, with protection and readable progressions that match their intended concept.

- Capture a reproducible baseline through the current production scenario harness: representative teams, defenses, field positions, both orientations, and fixed seeds. Record completion/interception/sack rates, yards per attempt, time to throw, protection breakdowns, and trajectories. Treat existing documents' test counts as historical; establish the current build/test result.
- Extend authored play data with slot-based primary/secondary/checkdown reads, route-stage or timing gates, and a protection plan. Keep CPU read order separate from the targeting numbers shown to the user. Validate referenced slots, eligibility, timing, and mirrored geometry; keep execution state outside immutable definitions.
- Re-author Mesh/Mesh Wheel, Flood/Sail, Stick/Spacing, and Levels/Drive per formation with explicit route sides, depths, crossing separation, release timing, and clear-outs. Then audit the remaining named passing calls against the same standards. Replace the alternating generic route-array assignment where it produces an incoherent combination.
- Give Hitch/Curl intentional holds or settles where the concept needs them, using custom `RouteDefinition` data. Preserve deliberate continuous crossers. Replace tests that require all non-comeback routes to keep moving with tests for the authored finish policy; do not globally change every route bearing the same label.
- Coordinate the offensive line and eligible TE/back protectors through one assignment plan. Support balanced protection, a declared slide direction, and authored six/seven-player max protection. Allocate threats without accidental duplicate assignments; retain intentional help/double-teams. These are properties of the selected call, not new pre-snap controls.
- Replace unconditional timed check releases with a minimum protection duration, safe-release condition, and bounded decision timeout. A remaining threat can keep the protector in protection; the timeout must not force abandonment of an immediate threat. Use visible defender movement/contact to make live decisions.
- Update recommendation scoring and diagrams from the same resolved routes, release rules, and protection. Show the intended first read and outlet without changing receiver key bindings.

**Primary code:** `Gameplay/PlayDefinition.cs`, `ResolvedPlay.cs`, `ExpandedPlaybook.cs`, `PlaybookBuilder.cs`, `PlaySuggestion.cs`, `PassProtectionPlanner.cs`; `Routes/RouteDefinition.cs`, `RouteGeometry.cs`, `RouteRunner.cs`, `RouteVisualizer.cs`; `AI/OffensiveLinemanAI.cs`; receiver/blocking/setup controllers.

**Acceptance gate:** shared preview/execution geometry; correct holds and releases under contact; Mesh crossing separation and Flood depth layering; stable targeting numbers despite different read order; coordinated pickup against balanced, edge-overload, and delayed rushes; max protection trades eligible routes for blockers. Exercise both flips, shuffled entity order, red-zone geometry, and 30/60/120 Hz. Run affected tests and the full suite before Step 2.

## 2. Differentiate runs and add screens and bootlegs

**Outcome:** running calls create different lanes, and the offense gains functional answers to pressure through screens and moving pockets.

- Extend blocking plans with explicit combination blocks and second-level release conditions. Give each concept a called entry, approach path, and blocking sequence. Use stable actor identities to coordinate paired blockers and prevent both leaving a combination simultaneously.
- Author Power with down blocks, a kick-out, and a lead/wrap blocker; Counter with initial misdirection and coordinated kick-out/wrap paths; Stretch with lateral reach/seal leverage and backside cutback protection. Adapt the jobs to available personnel, including five/six-lineman and fullback/two-back sets. Retain Dive/Lead Iso, Sweep, and Draw with clearly differentiated jobs and timing.
- Replace the CPU runner's common `side * 6` entry with the resolved concept's entry and approach. Let CPU runners press the intended lane before choosing a visible cutback; keep movement player-controlled after a human handoff. Preview the intended lane and blocker paths.
- Add an initial RB screen and WR screen in appropriate formations, both mirrorable. Author the target's release, catch landmark, designated releasing linemen/skill blockers, protection-to-release transition, and escort path. Distinguish the RB screen's delayed line release from the WR screen's immediate perimeter blocking. A screen throw behind the line must be legal and available to the CPU.
- Add play-action bootlegs in suitable under-center/pistol formations and a shotgun rollout. Define the fake direction, QB path opposite the fake where appropriate, edge protection, and layered routes. Keep existing pocket PA calls available. Human movement overrides the assisted QB path; the CPU follows it until throwing or reacting to pressure. Preserve fake cancellation, throw gates, and timeout behavior.
- Give screens and bootlegs explicit runtime transitions and exits on a throw, catch, sack, scramble, turnover, or aborted fake. Keep one owner of movement and possession per actor per frame. Verify defensive reactions use observed movement rather than offensive call IDs or hidden assignments.
- Extend the catalog with new permanent IDs and categories. Update exact-count tests and documentation intentionally; retain the 10-pass/10-run quick sheet and all existing IDs. New calls should not be added solely to reach another round catalog count.

**Primary code:** `Gameplay/BlockingAssignment.cs`, `BlockingPlanner.cs`, `BackfieldSequence.cs`, `ResolvedPlay.cs`, `ExpandedPlaybook.cs`, `PlaybookInfo.cs`; blocking/backfield/execution/possession controllers; `AI/BallCarrierAI.cs`, `OffensiveLinemanAI.cs`; `Gameplay/DefensiveDrive.cs`; route and blocking previews.

**Acceptance gate:** Power, Counter, and Stretch produce their authored paths and coordinated transitions under live contact; second-level releases retain necessary first-level help. Screens release the designated blockers, permit behind-line targets, and create an escort after a catch. Bootlegs complete or abort safely, mirror correctly, and surrender movement to human input immediately. Exercise blocked meshes, pressure from either edge, near-own-goal/red-zone starts, both control roles, and 30/60/120 Hz. Run the expanded catalog sweep and full suite before Step 3.

## 3. Integrate CPU strategy, playbook access, and balance

**Outcome:** the CPU uses the improved concepts intelligently, and players can reliably find and reuse the calls they want.

- Replace uniform CPU selection within a run/pass family with seeded weighted selection over the catalog. Score down/distance, field position, score/time/timeouts, roster strengths, recent calls, and observed defensive tendencies. Preserve team identities and some variation; neither CPU strategy nor player recommendations may inspect a hidden defensive call.
- Build on Step 1's authored CPU progression and development gates with richer protection and throwing-lane observation. Add pressure-triggered hot reads, screen timing and behind-line eligibility, bootleg reads, and bounded scramble/throwaway exits. Step 1 already replaces the named calls' general two-yard depth gate and Four Verts special case with authored stage gates; retain that behavior. Account for receiver/defender movement and estimated ball travel when judging a window.
- Update `SituationalCallSheet` and `PlaySuggestion` to evaluate actual route depth/timing, protection, run scheme, screens, roster fit, and clock strategy. Use recent call history separately from cumulative usage; avoid treating every previously successful call as increasingly undesirable.
- Add a keyboard-friendly full-catalog browser with formation/concept/category filters, preview, read/lane guidance, and a direct selection path by permanent ID. Retain the existing quick-sheet hotkeys. Selecting a browser call must keep the active call visible and avoid rerolls or usage credit until the snap. Define browser behavior under the live play clock and prevent navigation keys from also throwing or snapping.
- Add favorites stored by permanent call ID, available in a dedicated browser view and surviving restart. Handle missing IDs and corrupt settings gracefully. Favorites do not silently replace the situational quick sheet.
- Extend the existing release harness to compare baseline and candidate with identical seeds, situations, teams, defenses, and scripted inputs. Measure completion/sack/interception rates, rushing efficiency, scoring per possession, new-call usage, runtime cost, and allocations. Separate mechanical gates from balance targets; record target ranges before tuning rather than choosing them after results are seen.
- Complete full-game simulations and hands-on playtesting for human offense and defense. Verify clocks, stats, replay, pause/restart, selection persistence, and browser layouts at supported window sizes. Tune bounded parameters, rerun affected comparisons, and update the playbook guide and controls documentation.

**Primary code:** `AI/OffensiveCoordinator.cs`, `QuarterbackAI.cs`, `BallCarrierAI.cs`; `Gameplay/DefensiveDrive.cs`, `PlayManager.cs`, `SituationalCallSheet.cs`, `PlaySuggestion.cs`; input interfaces, `Rendering/SidePanelRenderer.cs`, `FullMatchRenderer.cs`, legacy `GameSession.cs` where affected; development release harness and a proposed favorites store/browser model.

**Acceptance gate:** seeded CPU choices are reproducible, scenario-sensitive, and roster-sensitive; read order follows the selected concept while human target numbers stay stable. Screens and bootlegs are used successfully in full games. Every catalog call is directly selectable; favorites survive restart; browsing does not consume a snap or alter usage. Pass the full regression suite, catalog/frame-rate sweeps, complete-match simulations, visual checks, and recorded balance review.

Deliver a concise verification report after each step at `docs/offensive-improvements-step-N-verification.md`, with commands, revisions, fixtures, results, remaining limits, and balance observations. Keep generated traces/captures under the ignored `artifacts/offensive-improvements/step-N/` directory. No step is complete on compile success alone; each must pass its behavioral gate before dependent work proceeds.
