# P9 verification — CPU strategy and bounded memory

September 29, 2026. P9.1–P9.4 implemented after P8 commit `8ddb9fa`. Caller version `defense-caller-p9-v1` uses the unchanged P8 compiler/runtime contract. The `strategy` development engine connects it to the actual timed-match and timed-season paths. Default and Release gameplay remain legacy until P10.

## Selection and decision rules

`DefensivePlaycaller` first compiles legal personnel/front/call/pressure/spy candidates and validates dropping defenders against the defending team's actual attributes. Unsupported candidates carry rejection reasons. Named match fallbacks are deduplicated by their actual resolved package, so unsupported match labels cannot multiply the probability of the same man defense. Each candidate contains a complete ordinary `ResolvedDefensivePlan`; CPU selection grants no special actors, movement or observation privileges.

| Rule | Implemented score terms and consequence |
| --- | --- |
| CALL-01 | Short/heavy surfaces favor compact named fronts and base personnel, penalizing Wide and light dime. The selected shell remains intact. |
| CALL-02 | Long yardage retains deep coverage preference; prior slow releases add bounded pressure preference. The current route script is never an input. |
| CALL-03 | Detached threats favor nickel/dime and supported trips checks. Actual LB-to-detached man assignments carry a visible mismatch cost. |
| CALL-04 | Repeated observed pocket escapes increase Wide/contain and legal spy preference. A Cover 1 spy consumes SS's existing low-helper job; escape-side pressure risk is separately penalized. |
| CALL-05 | Past quick throws/screens favor coverage and reduce delayed/stunt pressure that arrives late. Live screen detection comes from the existing visible-motion pressure coordinator. |
| CALL-06 | A late lead favors deep and sideline coverage according to opponent timeouts, with explicit underneath concessions. |
| CALL-07 | A late tie/deficit or critical short conversion in field-goal range favors a bounded stop/pressure response. The latter overrides generic lead protection; Cover 0 is not forced. |
| CALL-08 | Exact package outcomes adjust later scores within a cap. Coverage result, actual pressure arrival and QB escape evidence are tracked separately; plain incompletions give no pressure bonus. |

Team coverage/tackling/rush/blitz attributes provide a bounded identity term. Scores become positive weighted choices; each coverage family's weights are normalized by its candidate count so authoring more variants does not itself make the family more frequent. Every candidate retains a nonzero weight. Terms, weights, chosen package and draw are recorded for diagnostics/replay.

A dedicated draw depends on the game seed, completed-play count and defending team ID. Preview browsing does not advance a random stream. Changed visible geometry can change the legal choices and scores, but returning to identical geometry returns the same choice. CPU and human choices use the same compiler and produce identical plans for equal inputs.

## Result-to-memory contract

`PlayEnded.DefenseOutcome` stores actual/requested call IDs, front ID, personnel, pressure ID, compiler version and exact package ID. It also stores the union of actual rush participants, first unblocked arrival within 2.5 yards of the QB, observed throw time, pocket escape side, visible screen evidence, bounded conditional-pressure transitions and human abandonment. These describe observed events, not proof that pressure caused a result.

The rules layer validates defending-team identity before mutating scores or memory. `MatchState` records once for the team that defended; replaying the same resolved event is idempotent. The new memory uses the same package key as the caller. It retains twelve recent observations and at most 256 package entries per adjustment map, ramps over six completed plays, clamps each coverage/pressure component to ±0.4 and their combined score contribution to ±0.6. Human-abandoned plays remain in observation history but do not penalize the AI package. Restart clears both teams' memory. Legacy package-name/blitzer-label learning remains isolated until P10 removes legacy callers.

## Verification

- Thirteen strategy tests cover each CALL row's directional/negative conditions, critical conversion versus generic prevent, real spy budgets, team identity, caps, early-sample limits, exact keys, duplicate results, side changes, restart, actual versus merely called rush participation, human departures, immutable observations, hidden route/blocking/eligibility mutations, selection variety and CPU/human plan parity. Live preview/recording tests run at 30/60/120 Hz.
- `p09-final-regression.trx`: **860 passed / five unchanged P0 failures, 865 total**, including all thirteen strategy tests. The final report and evidence hashes are in `artifacts/defense-redesign/p09/verification-summary.json`. The baseline failures remain field-goal snap state, replay after possession change and three CPU punt-choice assertions; none is suppressed.
- **1,200 CPU-selected production plays passed:** every one of the 100 offensive catalog plays, both flips, seeds 101/108, at 30/60/120 Hz. Unlike the earlier fixed-call matrices, this harness lets the live CPU select the defense. It checks ownership, physical bounds, ordinary whistles, exact outcome package identity and once-only learning by the correct team. Human offense advances its runner after a handoff/catch.
- Those two fixed first-play draws selected four resolved coverage calls and 25 exact packages at 60 Hz, spanning all three personnel packages and all five fronts. This is an offensive-catalog sweep, not a claim that these seeds selected every defensive call. The prior catalog gates and dedicated selection tests cover the remaining legal choices.
- Six complete scenario runs passed: timed-short seeds 101/108, late-protect-lead, late-tying-kick, overtime-pairs and timed-season-short (other seeds 101). Both team possessions were exercised; the season reached a second opponent. Retained selected-score traces include nonzero learned package terms in timed-short, overtime and season play. These are execution/contract checks, not competitive-balance judgments.
- Debug tests and Release compilation pass their applicable gates; final Release build has zero warnings/errors. The runtime matrix/full-match batches preceded the final immutable transition-list copy and diagnostic defending-team label; final regression/build checks cover those refinements.

Use a Debug launch with `--scenario timed-game --engine strategy` for interactive play, or add `--headless` for the scripted full-match harness. `--scenario defense-strategy-matrix --headless --engine strategy --hz 60` reproduces the CPU-selection sweep.

Milestone C: all **98/98 rules** now have owning-phase implementation and scoped verification. P10 remains required for broad balance, profiling, interactive football judgment, the three retained P8 stationary-runner stalls, baseline-failure accounting, legacy removal and production cutover. No P9 result establishes that every package is competitively balanced or that default gameplay already uses the new engine.
