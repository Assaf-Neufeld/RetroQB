# Playable defense: engineering roadmap

Status: Phases 0 and 1 / M0–M2 verified. [Baseline results](phase0-verification.md) and [match rules and clock verification](phase1-verification.md). Phase 2 / M3–M5 is verified with automated gates passing and user acceptance of the test runs; see [Phase 2 report](phase2-verification.md). Phase 3 is implemented: M6 verified and M7 ready for hands-on checkpoint B; see [Phase 3 report](phase3-verification.md). M8–M11 have not started.

Product specification: [playable-defense-plan.md](playable-defense-plan.md). This roadmap turns that design into ordered work packages and evidence-based completion gates. It is the implementation tracker; the design remains the source for intended gameplay.

Confirmed scope: one fixed linebacker, defensive play calls, CPU offense, real alternating possessions, and timed quarters in the first release. Use the design's initial tuning defaults: four three-minute quarters, a 20-second play clock, ten defensive calls, abstracted special teams, and paired-possession overtime.

## How we will track progress

Use `Not started`, `In progress`, `Ready to verify`, or `Verified` for each milestone. A merged change, passing build, or screenshot alone does not make a milestone verified. Complete its automated gate and its demonstration, if listed, and record the evidence. Record gameplay judgments separately from correctness tests.

Each milestone can span several small changes, but every change must build and leave the previous working experience usable. During development, expose incomplete two-sided play only through a development scenario entry point. Do not ship two separate simulation engines or add a permanent legacy game mode.

| Phase | Milestones | Verifiable outcome | Status |
| --- | --- | --- | --- |
| 0. Baseline and verification support | M0 | Reproducible tests and scenario setup | Verified |
| 1. Match rules and time | M1–M2 | Correct team ownership, possession, scoring, and clocks without rendering | Verified |
| 2. First playable defensive drive | M3–M5 | You control a linebacker against a functioning CPU offense | Verified |
| 3. Defensive playcalling | M6–M7 | Ten real calls, readable assignments, and bounded CPU snap timing | Ready to verify |
| 4. Complete timed football | M8–M9 | Full regulation, special teams, clock strategy, halftime, and overtime | Not started |
| 5. Season integration and presentation | M10 | Correct stats, replays, saves, feedback, and three-stage progression | Not started |
| 6. Release verification | M11 | Validated playbook coverage and a tested default game experience | Not started |

Dependency order: `M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 -> M7 -> M8 -> M9 -> M10 -> M11`.

This deliberately puts a playable defensive drive before the complete defensive menu. The highest uncertainty is whether CPU offense and linebacker control produce good gameplay. Keep that checkpoint early.

## Verification conventions

- Names in unverified milestones describe work to implement. M0's currently supported scenarios and commands are documented in the [verification report](phase0-verification.md).
- New rules and AI tests run through the existing xUnit project without opening a Raylib window. Use injected RNG, scripted inputs, explicit starting states, and fixed simulation steps. New behavior should have testable public/internal boundaries instead of adding reflection access to `GameSession`.
- Test both user and opponent as possessing team for every rule. Different team IDs, colors, and attributes make accidental swaps visible.
- Use scenario IDs from this document in test names or test metadata. Each scenario stores seed, teams, field position, down/distance, clock state, calls, and expected invariants. A failure reports those inputs.
- Separate deterministic decision tests from statistical gameplay tests: a precise open-lane fixture can assert a decision; a stochastic drive should assert legal behavior and termination, not an arbitrary exact score.
- Run focused tests while implementing; run the complete regression suite at each integration gate. Do not weaken an assertion just to preserve obsolete synthetic scoring; replace it with the new rule and document the intentional change.
- Use isolated temporary save paths for tests and development scenarios. Verification must not write test seasons into the user's leaderboard.

Existing commands:

```powershell
dotnet build RetroQB.csproj
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj
dotnet run --project RetroQB.csproj
```

M0's scenario launcher now supports this reproducible manual baseline check:

```powershell
dotnet run --project RetroQB.csproj -- --scenario offense-pass --seed 101
```

The launcher is development-only, rejects unknown scenarios, and uses the production simulation and renderer. `defense-drive` will be added at M5. The launcher may construct starting states; it must not force an outcome in a demonstration of AI quality. Rule tests may inject terminal events to isolate scoring and transitions.

## Phase 0 — Baseline and verification support

### M0 — Establish a trustworthy baseline

**Deliverables**

- Record the current revision, build result, full regression result, and any pre-existing failures before changing gameplay.
- Add a minimal reusable scenario fixture with seeded RNG, fixed-step updates, scripted input, explicit team/drive/clock state, and an event trace. Extend it as features arrive; do not build a separate simulation framework.
- Add a development scenario entry point in `Program.cs` with isolated persistence. Initially support an existing offensive scenario; add defensive scenarios in later milestones.
- Capture baseline offensive pass, run, play-action, field-goal, and replay behavior using current preview/test support.

**Primary code:** `RetroQB/Program.cs`, `tests/RetroQB.Tests`, `tests/VisualPreview/Program.cs`, construction seams in `GameSession`.

**Acceptance gate**

- [x] Build and regression results are recorded, including test counts. No baseline source/test failures were found.
- [x] Running the same scenario twice with the same seed and inputs reproduces its terminal event and rules state.
- [x] Scenario runs use a temporary record store and leave normal player records untouched.
- [x] Normal startup and current offensive controls pass the production-session scripted smoke and existing regression coverage.

**Evidence:** [Phase 0 verification report](phase0-verification.md): 346 tests before, 371 after; Debug/Release builds clean; five reproducible scenarios and production screenshots. No new defense is included yet.

## Phase 1 — Match rules and time

### M1 — Team identity and possession are independent of offense/defense

**Deliverables**

- Add complete `TeamDefinition` entries for selectable teams, secret team, and stage opponents; preserve existing names/colors and offensive/defensive ratings where already present.
- Add `MatchState`, an explicit start spot for `DriveState`, structured play-end events, and one possession/scoring resolver. Move match score ownership out of drive reset logic.
- Emit interception contact position and terminal reasons, including sideline exits, from ball/tackle resolution. Carry actor attribution forward for later defensive statistics.
- Add per-team offensive stat routing and a ruleset identifier now. Keep full defensive summaries and persistence migration for M10.
- Isolate adaptive defensive memory, call history, and opponent difficulty by team. Bridge existing call sites without activating the unfinished season flow.

**Primary code:** `Data/TeamAttributes.cs`, team presets/rosters, `Gameplay/DriveState.cs`, `PlayManager.cs`, `PlayRecord.cs`, `Controllers/BallController.cs`, `TackleController.cs`, `Stats/StatisticsTracker.cs`; new match/team/result types.

**Acceptance gate**

- [x] `RULE-01`: touchdown adds seven only to the possessing team and queues the other team at its own 20.
- [x] `RULE-02`: failed fourth down at the old offense's own 35 awards no points and starts the new offense 65 yards from its own goal.
- [x] `RULE-03`: interception at the old offense's 70 gives the new offense its own 30, regardless of the old line of scrimmage. A catch in the intercepting team's end zone becomes its own 20.
- [x] `RULE-04`: a safety credits two to the defending team and gives that team the next possession at its own 20.
- [x] `RULE-05`: first down inside the opponent's 10 creates correct goal-to-go distance; a short fourth-down gain preserves its gain/spot in the record while ending possession.
- [x] `RULE-06`: submitting the same play-end event twice does not change score, statistics, history, or possession a second time.
- [x] Team colors and ratings stay attached to identity across two possession changes; CPU offensive events do not enter user offensive totals.

**Evidence:** `MatchRulesTests`, `TeamDefinitionTests`, and stat-routing tests with the scenarios above. The synthetic field-goal/punt results can be tested now; live execution comes in M8.

### M2 — Clocks and period boundaries are correct in isolation

**Deliverables**

- Add `MatchClock` with quarter, game/play time, run/stop reason, timeout budgets, and pending period end.
- Add period/halftime/overtime rules independent of UI. Remove the 21-point terminal condition from this new rules path.
- Specify update ordering: apply eligible pre-snap timeout input, advance clocks, resolve expiry, then accept a snap only if its period remains open. A timeout with no remaining game time cannot revive an expired period.
- Record the opening receiver and opposite second-half receiver. Implement paired overtime attempts, alternating pair opener, one timeout per team per pair, and no overtime game clock.

**Primary code:** new match clock/period types in `Gameplay` or `Core`, `GameState.cs`, typed terminal event from M1. `GameSession` wiring is deferred to M7–M9.

**Acceptance gate**

- [x] `CLOCK-01`: advancing 0.5 seconds from 0:01 leaves 0:00.5; advancing past zero clamps to zero and emits one expiry event.
- [x] `CLOCK-02`: a live pass/run/kick at zero remains live; terminal resolution occurs before period transition. A snap on the tick that expires pre-snap time is rejected.
- [x] `CLOCK-03`: in-bounds tackles/first downs resume a running pre-snap game clock; incomplete, defended, out-of-bounds, scoring, and possession-change results wait until snap.
- [x] `CLOCK-04`: Q1/Q3 endings preserve the resulting series; halftime replaces any pending possession with the second-half receiver at its own 20 and resets timeout budgets.
- [x] `CLOCK-05`: 21–14 in Q2 does not end the match. A non-tied Q4 result does; a tied result starts overtime.
- [x] `CLOCK-06`: each timeout consumes exactly one allowance; exhausted allowances do nothing. Pause/replay suspend and restore all running clocks without changing their remaining time.
- [x] `CLOCK-07`: both overtime attempts complete before comparing scores; a tied pair repeats with reversed opening order. Turnovers end the attempt rather than starting a drive at the turnover spot.
- [x] Equivalent elapsed time split into 30/60/120 Hz steps produces equivalent clock state within one simulation tick and never duplicates transitions.

**Evidence:** `MatchClockTests` and `PeriodTransitionTests` (including overtime). All gates passed; see [Phase 1 verification](phase1-verification.md). GameSession activation remains deferred to M7–M9.

## Phase 2 — First playable defensive drive

### M3 — Human input controls exactly one linebacker

Status: Verified. Automated gates pass; user accepted the test runs. See the Phase 2 report for remaining tuning observations.

**Deliverables**

- Extract `ControlContext` and human/CPU offensive intents; expose a throw command independent of keyboard handling.
- Add the fixed linebacker policy: MLB in base personnel, OLB1 in nickel. Keep offensive ball-carrier assistance in `PossessionControl` separate.
- Separate movement decisions from shared contact/integration so manual control does not bypass blocks, collision, tackling, speed limits, or animation.
- Add a minimal linebacker ring and a scripted-offense development scenario. This is a control test, not yet a CPU demonstration.

**Primary code:** `PlayExecutionController.cs`, `ReceiverUpdateController.cs`, `BallController.cs`, `AI/DefenderTargeting.cs`, `Entities/Defender.cs`, `Input/InputManager.cs`, `DrawingController.cs`.

**Acceptance gate**

- [x] `CONTROL-01`: movement reaches only MLB in base and only OLB1 in nickel; other defenders still follow AI assignments.
- [x] `CONTROL-02`: handoff, pass flight, catch, and tackle do not transfer human control to the opponent's carrier.
- [x] `CONTROL-03`: each actor integrates once per update. Movement input cannot erase block slowdown or overwrite a tackle-break displacement.
- [x] `CONTROL-04`: releasing movement stops manual movement; the linebacker does not automatically pursue. Human contact can produce the same tackle/pass contest as AI contact.
- [x] Existing QB movement, throws, receiver takeover, and backfield regression tests pass through the new intent path.

**Demonstration:** in base and nickel scripted scenes, move away from the assignment, return to contact, and observe a block and tackle. Offensive keys must not make the CPU throw.

### M4 — CPU quarterback runs the passing game

Status: Verified. Automated gates and seeded CPU demonstrations recorded in the Phase 2 report; user accepted the test runs.

**Deliverables**

- Add `OffensiveCoordinator` and `QuarterbackAI`: seeded call selection, read progression, pressure response, checkdown, scramble, and legal throwaway.
- Use existing ball trajectories, lead calculations, pressure effects, and catching. Do not implement a CPU-only completion shortcut.
- Limit observations to current/public field state and the CPU's own play. Human defensive call IDs and input are not part of the AI observation contract.
- Support representative quick, deep, checkdown, and play-action passes before claiming complete catalog support.

**Primary code:** new `AI/OffensiveCoordinator.cs` and `QuarterbackAI.cs`, `PlaySuggestion.cs`, `SituationalCallSheet.cs`, `BackfieldController.cs`, shared throw command.

**Acceptance gate**

- [x] `CPU-01`: an open eligible primary target produces a physical throw after the configured reaction/readiness interval.
- [x] `CPU-02`: a defended primary lane plus open checkdown produces progression to the checkdown; a blocking/ineligible target is never selected.
- [x] `CPU-03`: play-action prevents an early throw; passing after crossing the line is rejected by shared legality checks.
- [x] `CPU-04`: with all reads covered, the QB reaches a legal scramble/throwaway/sack outcome; a timer alone never produces an incompletion.
- [x] Same observations/seed give the same intent regardless of hidden defensive call labels or unrelated human key inputs.

**Demonstration:** show a quick completion, a defended/incomplete attempt, a pressure response, and a play-action throw in seeded scenes. Record seeds and outcomes; these need not all occur in one drive.

### M5 — CPU carriers and one complete defensive drive

Status: Verified. Automated gates pass; user accepted checkpoint A after reviewing the test runs.

**Deliverables**

- Add `BallCarrierAI` with authored run entry, blocker/defender-aware lane choices, sideline handling, and bounded steering.
- Route handoffs and post-catch carrier movement to CPU intents when the opponent possesses the ball.
- Add a `defense-drive` scenario using production drive rules, basic defensive coverage, CPU call selection, and human linebacker control. End with a drive result; full normal-game integration remains M8.
- Add basic trace diagnostics for time without progress and repeated steering changes to diagnose stalls rather than hiding them with forced outcomes.

**Acceptance gate**

- [x] `CPU-05`: a run follows the exchange before entering the authored lane; catches transition from route running to carrying without human input.
- [x] `CPU-06`: controlled lane fixtures demonstrate avoiding a blocked lane and progressing toward the opponent's goal; carrier exits and touchdowns resolve correctly.
- [x] `DRIVE-01`: seeded multi-play scenarios exercise first downs and a drive-ending stop or score without resetting every play to the own 20.
- [x] Supported scenarios terminate within a 30-second simulated live-play test budget; reaching the budget fails with a trace. It does not force a production whistle.
- [x] Run representative fixtures at 30/60/120 Hz; require legal, finite, terminating behavior, not identical random outcomes between frame rates.

**Playable checkpoint A:** play one defensive drive against CPU-selected passes and runs. Verify that moving the linebacker affects coverage or tackling and that blocking feels credible. Record issues in control feel or AI competence before starting the menu expansion; passing unit tests alone does not close this checkpoint.

## Phase 3 — Defensive playcalling

### M6 — Defensive calls are executable data

Status: Verified. Catalog-wide assignment tests and deterministic preview/setup gates pass; see the Phase 3 report.

**Deliverables**

- Add `DefensivePlayDefinition`, catalog, resolver, and resolved assignments for the design's ten calls.
- Move random blitz selection out of `PlaySetupController`; factories consume a final call without rerolling it.
- Use the same resolution path for automatic and manual defense. Resolve from visible offensive personnel/alignment, never hidden routes.
- Validate both base and nickel packages, pressure coverage replacements, stable call IDs, and supported mirroring.

**Primary code:** `AI/DefensiveCoordinator.cs`, `Factories/DefenseFactory.cs`, `DefensivePersonnel.cs`, `BlitzDecisionStrategy.cs`, `Controllers/PlaySetupController.cs`, new defensive catalog/resolver.

**Acceptance gate**

- [x] `CALL-01`: all ten calls resolve against every existing offensive formation with exactly one valid primary assignment per active defensive slot.
- [x] `CALL-02`: pressure packages preserve required matchups/deep help or explicitly declare the intended coverage tradeoff; no absent slots or dangling receiver references.
- [x] `CALL-03`: repeated setup and previews of a resolved call retain alignments, jitter, rushers, and assignments without further RNG draws.
- [x] `CALL-04`: double mirroring restores original geometry/assignments, and mirroring does not change the controlled linebacker policy.
- [x] Equivalent visible formations with different hidden offensive routes produce the same defensive preview for the same call and seed.

**Evidence:** catalog-wide validation tests and assignment snapshots for a zone, man, and pressure call in both personnel packages.

### M7 — Playcalling UI, clocks, and CPU cadence work together

Status: Ready to verify. Automated timing gates and visual captures pass; hands-on checkpoint B remains open.

**Deliverables**

- Replace the offense panel while defending with ten calls, coaching text, assignment previews, and linebacker responsibility.
- Wire game/play clocks, timeout input, CPU snap deadlines, delay-of-game handling, pause, and replay suspension into pre-snap/active play.
- Lock the CPU offensive call and deadline once per play. Browsing only rebuilds the selected defensive setup, preserving clock state and offensive identity.
- Add automatic continuation for nonterminal result screens. Provide clock/timeout/possession HUD elements and a development visual-preview mode for these states.

**Primary code:** `GameSession.cs`, `InputManager.cs`, `SidePanelRenderer.cs`, `HudRenderer.cs`, `ScoreboardRenderer.cs`, `DrawingController.cs`, replay state handler.

**Acceptance gate**

- [x] `UI-01`: live defensive views never expose CPU route names, route lines, intended targets, offensive call names, or offensive coaching text.
- [x] `UI-02`: displayed defensive lines/zones/rushers match M6's resolved assignments. The controlled linebacker remains identifiable in base and nickel.
- [x] `TIME-01`: repeatedly change calls until the CPU deadline; the same offensive call still snaps on time. Space shortens, never extends, that deadline.
- [x] `TIME-02`: play-clock expiry gives five yards/half-distance, no lost down, and a stopped game clock until snap; repeated delays cannot consume game time.
- [x] `TIME-03`: one C press consumes one timeout; changing plays, pausing, or opening replay cannot reset either clock. Timeout return resumes the correct stopped-clock state.
- [x] `TIME-04`: in-bounds results advance without Enter and resume clock play; possession-change summaries can wait. Focus loss pauses without a catch-up clock jump.

**Playable checkpoint B:** call Cover 3, defend a snap, change to pressure next snap, use a timeout, and allow the CPU to snap while browsing. Show identical team identities and readable clock state throughout.

## Phase 4 — Complete timed football

### M8 — Regulation match and special teams are playable

**Deliverables**

- Integrate both possessions into `GameSession` with one rules resolver and exactly-once pending transitions.
- Add user punt selection and CPU fourth-down decisions. Preserve the field-goal timing game, remove synthetic away points, and add seeded CPU kick execution through shared kick results.
- Wire quarter breaks, halftime reception, final-play resolution, and regulation completion. Keep an unfinished tied-game path in development only until M9.
- Add user kneel selection and resolution. Basic team-aware scoring/possession feedback must work now, even if final presentation polish comes in M10.

**Acceptance gate**

- [x] `MATCH-01`: user touchdown -> opponent own-20 drive -> defensive stop -> user offense, preserving correct scores, identities, and spots.
- [x] `KICK-01`: made field goal adds only three to the kicker's team; missed kick adds none and gives the next offense the kick spot or own 20, whichever is farther upfield.
- [x] `KICK-02`: 40-yard net punt from own 30 yields opponent own 30; a punt reaching the end zone yields opponent own 20. No punt is offered except fourth down in regulation.
- [x] `KICK-03`: CPU kicks require no human meter input and are reproducible by seed/distance. The player's existing timing/accuracy rules still apply.
- [x] `MATCH-02`: a touchdown, interception, or missed field goal on the final play of Q2 records once, then halftime reception overrides the ordinary pending drive.
- [x] `MATCH-03`: a pass, run, or snapped field goal at Q4 0:00 finishes and can change the winner. A score of 21 during regulation never ends play early.
- [x] `MATCH-04`: a kneel consumes a down, applies its yardage/safety rules, and leaves the correct running-clock state.

**Playable checkpoint C:** finish a short-clock development match with both possessions, a quarter break, halftime, and a regulation winner. Also play through at least one default three-minute quarter to check pacing.

### M9 — Late-game strategy and overtime are complete

**Deliverables**

- Feed time, score, timeout counts, and overtime attempt state into CPU strategy.
- Add hurry-up/lead-protection snap timing, useful timeouts, sensible kneels, and situational punt/go/kick choices. Define the decision table in tests before tuning weights.
- Integrate overtime attempts and period UI; use the pure M2 rules without a second overtime-specific scoring implementation.

**Acceptance gate**

- [x] `LATE-01`: trailing three in reachable field-goal range on fourth down with little time favors the tying kick; trailing four in the same fixture requires a touchdown attempt.
- [x] `LATE-02`: a trailing defense with remaining timeouts uses one after an in-bounds dead ball when the offense could otherwise exhaust the clock. It does not spend multiple timeouts on that same event.
- [x] `LATE-03`: a leading CPU kneels when the remaining downs/timeouts/play clocks guarantee clock exhaustion; it does not kneel when that calculation fails.
- [x] `OT-01`: tied regulation starts at the opponent's 25. Both attempts finish, punts are disabled, and an interception ends the attempt without creating a normal field-position drive.
- [x] `OT-02`: tied pair -> reversed opening order -> decisive pair produces exactly one match result. Overtime has play-clock enforcement and correctly replenished timeout allowances.
- [x] Restart from regulation, halftime, and overtime resets current-game score, Q1 time, timeouts, AI state, and pending events, preserving the original opening receiver and earlier season stages.

**Demonstration:** reproduce a tying-kick decision, a protected lead, and a tied overtime pair followed by a decisive pair. Use configured scenario states instead of waiting through multiple full games.

## Phase 5 — Season integration and presentation

### M10 — Statistics, saves, replays, and season flow are reliable

**Deliverables**

- Add defensive team and linebacker contribution totals with actor attribution from M1; separate sacks allowed from sacks made and interceptions thrown from interceptions made.
- Add team/possession/call/linebacker/clock metadata to replays. Playback is visual and cannot resolve rules or stats.
- Version saved records/rulesets and preserve old records and backups; use player name plus ruleset identity where needed to avoid overwriting a legacy season.
- Finish team-aware pregame profiles, drive summaries, crowd reactions, celebrations, scoreboard labels, and end-zone presentation.
- Connect timed match outcomes to all three season stages. Preserve the existing offensive rating formula within the new ruleset; do not invent a defense-weighted replacement in this milestone.

**Primary code:** `Stats/StatisticsTracker.cs`, `StatsSnapshot.cs`, `SeasonSummary.cs`, `PlayerLeaderboard.cs`, `PlayerRecordStore.cs`, `Gameplay/Replay`, `Rendering`, season/restart paths in `GameSession.cs`.

**Acceptance gate**

- [x] `STATS-01`: CPU passing/rushing totals and user defensive totals update from the same event without polluting user QB/rushing stats. Linebacker tackles and teammate tackles have distinct attribution.
- [x] `REPLAY-01`: replay after a possession change shows recorded teams, period/time, assignments, and controlled actor. Replaying or skipping twice leaves match state and stats unchanged.
- [x] `SAVE-01`: load a legacy fixture, save a two-sided season under the same name, reload, and retain both records with separate rankings. Backup recovery and failed-save retry remain correct.
- [x] `SEASON-01`: regular-season win -> playoff win -> Super Bowl win yields one champion record. A loss in each stage yields one correct elimination record.
- [x] `SEASON-02`: restarting the current matchup twice restores its pregame baseline without erasing earlier games or duplicating completed-game statistics.
- [x] Screenshots at 1000×700, 1280×720, 1440×900, and 1920×1080 show readable calls, linebacker assignment, both clocks, timeouts, possession, halftime, and overtime without clipping.

**Playable checkpoint D:** finish and save a three-stage season, inspect both units' statistics, replay a defensive highlight, restart a current matchup, and verify the earlier stage remains intact.

## Phase 6 — Release verification

### M11 — Validate coverage, tune, and make it the default

**Deliverables**

- Exercise the full CPU offensive catalog, generated-call samples, all defensive calls/personnel combinations, both possession sides, and each stage's difficulty profile.
- Freeze a seed set and tuning configuration before recording balance results. Record completion/sack/turnover rates, rushing efficiency, scoring per possession, and real match duration by stage.
- Review linebacker usefulness in zone, man, and blitz assignments. Tune from observed behavior; no test suite can establish fun on its own.
- Remove transitional legacy scoring adapters and development gating from normal season flow once ready. Keep useful test scenarios explicitly development-only.
- Update README, controls, game-flow descriptions, screenshots, and known simplifications.

**Automated release gate**

- [x] Build and full regression suite pass; obsolete synthetic scoring and first-to-21 expectations have intentional replacements.
- [x] Catalog sweep: every offensive call × own 20 / midfield / opponent 10 × three stages × three fixed seeds. Generated calls additionally use ten fixed generation seeds. Validate legal actions, finite positions, terminating plays, correct result attribution, and valid field bounds.
- [x] A representative run, quick pass, deep pass, play-action, tight formation, and spread set run at 30/60/120 Hz with no stalls, illegal throws, duplicate events, or lost control ownership.
- [x] Run at least 20 seeded short-clock full matches covering both opening receivers and all stage profiles, plus explicit overtime and final-play fixtures. Zero crashes, invalid states, duplicate scoring, or scenario time-budget failures.
- [x] Save compatibility/recovery, restart, replay suspension, and UI evidence from M10 are complete for the release candidate.

**Gameplay release gate**

- [ ] Play at least one full default-length match per stage profile, including user opening on offense and on defense across the sample.
- [ ] Record actual match duration and rate metrics. Agree and write the acceptable balance ranges before calling this gate verified; do not derive passing thresholds from the candidate's own output after the fact.
- [ ] Record an explicit gameplay judgment for control responsiveness, ability to affect the play, CPU competence, defensive-call tradeoffs, and clock pacing. List unresolved issues with severity; any game-breaking issue blocks release.
- [x] Normal launch starts the complete timed two-sided game; no development setup is necessary to play defense or complete a season.

**Evidence:** release test summary, scenario/seed matrix, screenshots, balance report, manual playtest notes, and final known limitations. No extra mechanics are added under the label of polish.

## Milestone evidence log

Fill one row per milestone as implementation proceeds. Store the detailed command output and scenario notes with the implementation change; use links here instead of copying entire logs.

| Milestone | Status | Revision/change | Tests and result | Demo/evidence | Remaining issues |
| --- | --- | --- | --- | --- | --- |
| M0 | Verified | Working tree based on `0d877c9` | 346 baseline / 371 final passed; Debug and Release clean | [Verification report](phase0-verification.md) | No M0 blocker; clock/defense remain later work |
| M1 | Automated gates verified | `6363ec5` | See phase report | [Phase 1](phase1-verification.md) | Hands-on release review tracked in M11 |
| M2 | Automated gates verified | `6363ec5` | See phase report | [Phase 1](phase1-verification.md) | Hands-on release review tracked in M11 |
| M3 | Automated gates verified | `1393da0` | See phase report | [Phase 2](phase2-verification.md) | Hands-on release review tracked in M11 |
| M4 | Automated gates verified | `1393da0` | See phase report | [Phase 2](phase2-verification.md) | Hands-on release review tracked in M11 |
| M5 | Automated gates verified | `1393da0` | See phase report | [Phase 2](phase2-verification.md) | Hands-on release review tracked in M11 |
| M6 | Automated gates verified | `a7e6a06` | See phase report | [Phase 3](phase3-verification.md) | Hands-on release review tracked in M11 |
| M7 | Automated gates verified | `a7e6a06` | See phase report | [Phase 3](phase3-verification.md) | Hands-on release review tracked in M11 |
| M8 | Automated gates verified | `be036f0` | See phase report | [Phase 4](phase4-verification.md) | Hands-on release review tracked in M11 |
| M9 | Automated gates verified | `be036f0` | See phase report | [Phase 4](phase4-verification.md) | Hands-on release review tracked in M11 |
| M10 | Automated gates verified | `82535cd` | See phase report | [Phase 5](phase5-verification.md) | Hands-on release review tracked in M11 |
| M11 | Engineering verified; gameplay pending | Phase 6 working tree | 534 tests; 2,880 catalog cases; 27 matches | [Release report](phase6-verification.md) | Balance ranges and hands-on review pending |

## Scope boundaries

Do not add defender switching, arbitrary player selection, manual tackle/swats, returns, fumbles, advanced audibles, new offensive playbooks, a permanent legacy mode, or full league-rule simulation to these milestones. If a milestone reveals that a proposed simplification is inadequate, record the concrete issue and update the design and its affected acceptance gates before implementing the replacement.

Phases 0–5 are implemented. Phase 6 enables the normal timed game and passes the automated release matrix; the remaining action is the documented hands-on balance and pacing review.

Post-release playtesting explicitly expands the original scope: two-minute halves and playable punt/kickoff returns are authorized by the user. See [feedback 2](playtest-feedback-2.md). Earlier no-return and four-quarter requirements describe the original milestone scope, not the current normal game.
