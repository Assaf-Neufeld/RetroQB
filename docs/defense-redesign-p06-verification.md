# P6 verification — coordinated pressure and human controls

September 28, 2026. P6.1–P6.4 implemented and verified in development mode after P5 commit `7bf1241`. Plan/runtime versions: `defense-plan-p6-v1` / `defense-runtime-p6-v1`. Milestone B is complete within the base/nickel man and spot-zone scope; normal gameplay remains legacy until P10.

## P6.1 — fixed pressure packages (verified)

The compiler resolves Mike, edge and nickel variants of the five-man Cover 1 pressure budget, plus six-man Cover 0. Side changes recompile against the same offensive snapshot. Every added rusher is reserved before exact man matching; no runtime repair borrows a primary or deep helper. Nickel requires an actual nickel roster. Unsupported variants are filtered by compiling their complete geometry and budget.

Edge/nickel pressure puts the added actor outside with contain, compresses the adjacent end into a distinct opening, and retains the far edge. The run-fit compiler follows these named contain actors rather than assuming both ends always own force. All entries are distinct actual gaps. Interior counters name only an adjacent, initially unoccupied non-contain gap; live reservations prevent two actors taking the same alternate.

`PressureCoordinator` tracks approach, engage, counter, contain, close and released states. A 0.55-second lack of observed progress permits the declared counter or a bounded widening of the same outside contain path. Contact remains with production blocking/shed physics. Pressure boards distinguish called jobs from actual participation: an idle human rusher receives the cue without being counted as an active rusher or acquiring automatic movement. Throws and confirmed carriers end pressure participation. Immutable pressure boards are included in existing decision/replay snapshots.

Evidence under `artifacts/defense-redesign/p06/tests/`:

- `p06-fixed-pressure.trx`: 10 passing tests; 2,800 legal plans across every catalog formation, both offensive flips, both pressure sides and base/nickel; 400 invalid base-roster nickel cases rejected.
- 24 production-controller plays: four packages × two sides × 30/60/120 Hz, seed 101, Ballers/Scarlet Guard, `pass.mesh`; all resolve with ordinary whistles and bounded speed.
- Pressure flip twice restores serialized plans exactly, preserving offensive geometry and controlled slot. Stalled-lane tests verify reservations and no per-frame reversal; idle/moving/throwing manual tests verify actual participation and intact primaries.
- `p06-package1-regression.trx`: **770 passed / the same five P0 failures, 775 total**.
- `p06-first-regression.trx`: 99 earlier compiler, man, observation and run-fit tests passed before the new package tests.

The existing protected stationary-QB case can still remain live for 30 seconds; counters are physical attempts, not guaranteed sacks or forced whistles. The P6.4 section below records conditional pressure and full-phase requalification.

## P6.2 — human pressure workflow

Number-key input, including 9/0, now has explicit defensive input wiring through the keyboard and scripted contracts, game entry point, development launchers and both session controllers. X recompiles the pressure side against the unchanged offensive snapshot; Q/E cycles only variants that pass compilation for the current roster. Rejections retain the prior valid plan and report the reason. Calls, side changes and variants preserve offensive actor instances/call, defensive actor instances within the same roster, controlled slot, clocks and snap deadline. These pure recompilations consume no RNG.

Pre-snap controls are ignored while live, suspended, unfocused, presenting results or replaying. Manual blitzers receive a gold live path and retain zero AI velocity when idle; ordinary collision separation may physically displace them. Menus show personnel, pressure variant/side, called versus actual rush count and the controlled job. Replay renders its recorded plan/decision.

`PressureInputTests` passes five cases, including injected real keyboard 9/0/X/Q/E mappings, double-flip equality, invalid nickel-on-base rejection, legal variant cycling, preview/snap parity, both session controllers, clock/identity preservation, and idle manual participation. Existing defensive-session tests also pass. The hidden `defense-calls --engine zone --capture` run was inspected at 1440×900 for readable pressure selection, personnel/rush summary and X/Q/E controls.

P6.2 full regression: **775 passed / the same five P0 failures, 780 total** (p06-package2-regression.trx). The capture drive ended through a production touchdown with no harness failure.

## P6.3 — zone and simulated pressure

Cover 3 now offers five-man zone pressure and four-man simulated pressure on either side through Q/E. Both explicitly drop the near end and send the near linebacker with contain. Five-man zone pressure additionally sends the middle actor and allocates exactly three deep/three underneath jobs. Simulated pressure allocates three deep/four underneath jobs. The far end retains opposite contain.

The dropping end starts at its compiled physical alignment and travels to its curl/flat home. Preview and available-variant filtering check the actual roster's speed, turn/acceleration estimate and a 0.10-second margin against a declared 2.5-second arrival window. An incapable roster is rejected before preview; early flat exposure is a real cost of the drop. No defender is spawned at its live coverage home after snap. Existing spot-zone allocation, transfer and overload-concession rules apply to the actual six/seven coverage actors.

Production execution clears the drop's rusher flag before pass protection runs. The existing protection planner already consumes current IsRusher state; no duplicate protection implementation was added. Tests query its actual assignments throughout live plays to ensure the dropping end never remains a pass-rush target.

`p06-zone-pressure.trx`: six passing tests, including 1,600 catalog plans (100 plays × two offensive flips × two rosters × two sides × two variants), incompatible-call/slow-roster rejection, and 36 production plays (three passing plays × two sides × two variants × 30/60/120 Hz). All production plays end with ordinary whistles, preserved coverage boards and bounded speed. `p06-zone-integration.trx` requalifies 46 fixed-pressure/spot-zone tests.
P6.3 full regression: **781 passed / the same five P0 failures, 786 total** (`p06-package3-regression.trx`).

## P6.4 — conditional pressure, stunts and screen response

Cover 1 exposes three additional authored variants through the same Q/E/X input path:

- **Green dog:** the compiled owner of the nearest visible back retains man responsibility until 0.35 seconds of observed blocking engagement. Launch uses a reserved interior entry and explicitly removes live primary coverage of that back; its identity remains the release key. An observed late release restores the same primary and a physical recovery intent, including while engaged. Empty formations without a back cannot select this variant. A late release can beat the recovering defender.
- **Delayed:** the otherwise uncommitted low helper stages without giving other man defenders false help credit, then enters after 0.45 seconds with a visible pass read. All receiving targets retain separate owners. A spy/bracket that consumes this helper makes the variant unavailable. Observed throws and confirmed runs cancel the launch.
- **Stunt:** two interior linemen receive distinct penetrator/looper entries. The looper waits for observed clearance, including visible offensive linemen. After at most 0.50 seconds it uses its declared free fallback lane. Both contain actors retain their jobs. Throw/run transitions end the exchange; ordinary motor and blocking physics remain authoritative.

The sighting boundary now includes lineman identities, positions, velocities and radii. It excludes offensive blocking assignments and play metadata. Screen response requires a lineman advancing at least 1.5 yards from his original position with forward velocity over 1 yard/second for 0.12 seconds, together with a nearby moving outlet behind him. One available interior fixed rusher occupies the rally lane. Contains, the outlet's existing owner and conditional release keys are preserved. A moving outlet or advancing lineman alone cannot trigger it.

Fixed rushers cannot counter into a conditional actor's reserved entry. Pressure boards record staging, engagement, fallback, screen response, called jobs and actual participation; idle humans remain manual and do not generate replacement rushers. Pause and replay do not advance tactical timers. The menu shows pressure side and conditional/active counts; its expanded text was inspected at 1440×900 in `conditional-controls-capture`. That production drive ended with a tackle and no harness failure.

`ConditionalPressureTests` covers DM-10/11/12 plus screen positive/negative cases, cancellation, roster filtering, all catalog surfaces/flips/base-nickel/sides, manual participation, production lineman contact and bounded motor recovery. Frame-sensitive cases run at 30/60/120 Hz. `p06-contact-recovery.trx` passes all 64 conditional-pressure and run-fit cases. Existing fixed/zone/simulated pressure, keyboard, blocking, pass-protection and session tests remain in the full-suite gate.

The final full suite passes **811 tests with the same five P0 failures, 816 total** (`p06-final-regression.trx`). P6 adds 51 passing cases, including 30 in `ConditionalPressureTests`. A direct comparison of failed test identities against `p00/tests/baseline.trx` finds no additions or removals. The five failures remain the unrelated field-goal range test, replay reopening after possession change, and three CPU punt expectations. Debug compilation, Release build (zero warnings/errors), and whitespace checks succeed.

The broader production matrix exposed four equivalent failures at 60 Hz: right-side simulated pressure against `run.hb-dive` with a stationary human runner. Contact repeatedly reset force ownership to its original actor, stealing and restarting the actor's outside recovery path. Force now retains its committed owner until a reachable replacement accepts, and replacement selection respects the active recovery reservation. A focused regression verifies ordinary resolution at all three rates. Initial traces remain in `final-runtime-60`; corrected sweeps use `verified-runtime-*`. No forced whistle or movement-speed increase was introduced.

Final production gate: **2,736 plays, zero failures** — 912 each at 30/60/120 Hz, seed 101, Ballers/Scarlet Guard in both possession orientations, four control modes, both pressure sides, six offensive calls and every legal pressure kind. Unsupported empty-back green dogs and base-roster nickel variants are explicitly excluded. Every play must end through a production whistle within 30 seconds with eleven finite intents, unique receiving ownership (only the declared green-dog release key may be conceded), consistent live-rusher counts and bounded physical speed. Frame-level football decisions are also checked by the targeted tests rather than inferred from play completion. Source/evidence hashes and failure identities are retained in `artifacts/defense-redesign/p06/verification-summary.json`.

Rules ALIGN-08, MOVE-03, PRESS-02–10/12 and HUMAN-01/02 are verified; PRESS-01/11 and the P5 carrier-response rules are requalified with added pressure. This is 74 of 98 rules across P1–P6. Authored match coverage (P7), additional fronts/dime (P8), strategic CPU calling (P9), balance and production cutover (P10) remain pending. These tests do not promise a sack against every protected stationary QB or establish final competitive balance.

## Reproduce P6

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --logger "trx;LogFileName=p06-final-regression.trx" --results-directory artifacts/defense-redesign/p06/tests
dotnet build RetroQB.csproj -c Release
dotnet run --project RetroQB.csproj -- --scenario defense-pressure-matrix --headless --engine zone --hz 30 --seed 101 --output artifacts/defense-redesign/p06/verified-runtime-30
dotnet run --project RetroQB.csproj -- --scenario defense-pressure-matrix --headless --engine zone --hz 60 --seed 101 --output artifacts/defense-redesign/p06/verified-runtime-60
dotnet run --project RetroQB.csproj -- --scenario defense-pressure-matrix --headless --engine zone --hz 120 --seed 101 --output artifacts/defense-redesign/p06/verified-runtime-120
dotnet run --project RetroQB.csproj -- --scenario defense-pressure-controls --engine zone --capture --seed 101 --output artifacts/defense-redesign/p06/conditional-controls-capture
```
