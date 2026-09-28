# P6 verification — coordinated pressure and human controls

September 28, 2026. Work in progress after P5 commit `7bf1241`. Plan/runtime versions: `defense-plan-p6-v1` / `defense-runtime-p6-v1`. The complete P6 exit gate is still pending; normal gameplay remains legacy until P10.

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

P6.4 conditional pressure/screens remain outstanding. The existing protected stationary-QB case can still remain live for 30 seconds; counters are physical attempts, not guaranteed sacks or forced whistles. Full-phase acceptance must requalify broader production scenarios and remaining pressure interactions.

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
