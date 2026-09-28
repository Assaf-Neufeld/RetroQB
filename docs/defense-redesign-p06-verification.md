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

P6.2 input workflow, P6.3 zone/simulated pressure and P6.4 conditional pressure/screens remain outstanding. The existing protected stationary-QB case can still remain live for 30 seconds; counters are physical attempts, not guaranteed sacks or forced whistles. Full-phase acceptance must requalify broader production scenarios and remaining pressure interactions.
