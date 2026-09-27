# P4 verification — coordinated spot zones

September 27, 2026. P4.1–P4.5 complete for base/nickel development-mode spot zones. Base revision `e3b2a4b36b70376cb31a163fc970add8230a3835` plus the uncommitted source hashes retained in `artifacts/defense-redesign/p04/verification-summary.json`. Zone plan version `defense-plan-p4-v1`; shared runtime `defense-runtime-p4-v1`. Normal Debug/Release gameplay remains on the complete legacy engine until P10.

## Delivered behavior

`SpotZoneCompiler` creates immutable area/home/neighbor responsibilities independently of legacy `ZoneCoverage` targeting. Cover 2 has two deep/five underneath, Cover 3 and Buzz three/four, and Cover 4 four/three. Four named rushers retain the P2 contain behavior. Buzz's SS starts above and inside its curl/flat home and physically rotates there; the other three deep roles stay intact. The existing `def.cover3-match` ID is retained for the menu's **Cover 3 Buzz spot-zone call**. This does not implement Cover 3 Match; P7 must introduce explicit authored match semantics and distinguish their call identity. `def.quarters-match` remains unavailable.

`CoverageCoordinator` consumes the same immutable observation as every other defender. It owns the coverage board and emits one decision per area actor, before any movement. A receiver has at most one primary; temporary/helper coverage is separately represented. Deep priorities, the local underneath windows, stable identities and nonnegative arrival urgency decide allocations. Ordinary changes require the 0.25-second commitment and 20% challenger gates; ties retain the current valid commitment. Posts and curls already inside the deep window remain deep threats after forward velocity disappears.

Every transfer records its transaction, ownership version, plan version, observation time, source, destination, receiver and stage. Offers use declared neighbors. Reachability includes actual distance/speed, turn time from current velocity, recognition delay and a 0.10-second margin. Human destinations use a 0.75-second horizon; AI destinations use at most 1.5 seconds. Occupied or unreachable destinations decline. Declines suppress identical evidence until 0.30 seconds expires; changed reachability/ownership can trigger earlier reevaluation. Competing offers reserve one incoming primary per actor. Complete chains/cycles can commit together; incomplete exchanges cannot steal a current primary. Sources retain their receivers until the atomic commit, then may help for up to 0.15 seconds unless another local job needs them.

Ordinary carries stop after 0.60 seconds outside the home area or five lateral yards, whichever comes first. A declined underneath carry records the receiver concession before recovery. A deep owner retains the threatening vertical and records its vacated home rather than silently losing the receiver. Cover 2's corner is the provisional outside-vertical carrier: when half-safety acceptance fails, its emergency priority keeps the fade and exposes the flat. The adjacent hook may explicitly replace that flat only within five lateral yards and actual reachability, recording the vacated hook. It does not take an occupied hook away from a more important seam.

Cover 2 distinguishes fade/seam conflict and flat/intermediate/deep flood allocation. Cover 3 keeps outside vertical owners and separate seam carriers; its middle safety cannot accept both seams. Cover 4 keeps outside/inside verticals distinct, restores deep ownership when an out turns upward, and retains recovery depth over shallow crossers. Overloads remain explicit concessions, not invented extra defenders or speed boosts.

Human transfer acceptance uses actual position and velocity. Later loss of reachability is recorded as abandonment while pass coverage is active. No decision or inactivity enables automatic human movement. Existing production physics still resolve blocking, catches, tackles and whistles. P5's coordinated carrier response is not implemented here; the labeled P2 pursuit fallback remains after confirmed possession/run.

The defensive and full-match renderers show the controlled player's blue home area separately from the gold live target. Assignment cues expose accepted/declined handoffs and abandonment. Immutable boards/events are included in replay frames; drawing/replay never advances decisions. Rendered capture inspected at 1440×900 includes pre-snap Cover 4, live Cover 2, menus, pause and replay.

## Evidence and limits

- `SpotZoneCoverageTests`: **36 passing cases** covering positive, refused, expired/changed-evidence and fallback transfers; atomic cycles; destination contention; frame-rate-specific post overlap; deep horizontal cuts and return crossings; human reach/velocity/contact/abandonment; sustained Cover 2 fade and reachable/unreachable flat replacement; flood, quarters wheel and shallow-cross behavior; Buzz arrival; hidden metadata equivalence; determinism; pause/whistle/replay immutability; stationary human input; and real goal-line plays in both team roles.
- **3,200 valid zone plans:** all 100 catalog plays × two flips × base/nickel × four zone calls × LOS 30/109. Existing 6,000 passive P1 and 4,000 P3 switched-man plan checks continue passing.
- **768 production zone plays, zero failures:** 256 each at 30/60/120 Hz, seed 101, midfield, Ballers/Scarlet Guard. Eight offensive plays × four zone calls × both defending roles × assignment-following, abandonment, stationary and full-AI input. The actual CPU offense runs when the user defends; the attacking-user fixture throws to its first target after one second. The harness requires a production whistle within 30 seconds and asserts unique primary ownership, complete finite decisions and bounded physical speed. These are lifecycle/invariant sweeps; the targeted tests establish football-rule correctness.
- Eight additional flipped goal-line runs (own yard 98), seed 202, 120 Hz, both team roles, are part of the test suite. Repeated-seed production board/decision JSON matches exactly.
- **320 man-family production cases, zero failures**, requalified at 60 Hz under P4 after the shared urgency correction. Historical 30/120-Hz man results remain linked in the P3 report.
- Final full suite: **726 passed / five known failures**, 731 total. P4 adds 36 passing cases to P3's 695. Debug and Release builds succeed. `phase04-final.trx` and source/evidence hashes are retained under the P4 artifacts directory.

The unchanged baseline failures, compared by identity and assertion site:

| Test | Existing mismatch |
| --- | --- |
| `FieldGoalTests.OutOfRangeCannotSnap` | Expected Setup, actual Ready at line 24; test range differs from the current 80-yard maximum. |
| `TimedSeasonTests.REPLAY01_ReplayAfterPossessionChangeKeepsRecordedTeamClockAndLinebacker` | Expected prior clip, actual null at line 104; old replay reopening lifecycle. |
| `HalvesAndSpecialTeamsTests.CpuPuntsOnlyWhenPinnedBackWithLongYardage` (20/10, 10/2, 40/8) | Expected Scrimmage, actual Punt at line 189; current CPU punt policy differs. |

The P1 unsupported-call assertion now checks the still-unsupported `def.quarters-match`; `def.cover3` is intentionally supported by P4. No baseline failing test is suppressed or marked expected.

Rules verified in this phase: DEC-04; Z2-01–03; Z3-01–04; Z4-01–03; ZONE-01; XFER-01–04; HUMAN-03. DM-04/05/06/16 are covered; DM-19 is covered only for supported spot-zone/man families, not P7 match families. The ledger now separates these rules from future pressure, fits, match and CPU strategy work.

Design clarifications: the ordinary carry limit has an explicit Z2-01/02 exception for the corner's provisional vertical duty, with a reachable adjacent-hook flat replacement; shared OBS-08 urgency is zero outside the specified 1.5-second window, and zero-score ties retain the current commitment. Human abandonment cues apply while coverage is active, not after a confirmed run/catch.

## Reproduce

Run from the repository root with .NET SDK 10.0.112 on Windows:

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --filter FullyQualifiedName~SpotZoneCoverageTests
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --logger "trx;LogFileName=phase04-final.trx" --results-directory artifacts/defense-redesign/p04/tests
dotnet build RetroQB.csproj -c Release
dotnet run --project RetroQB.csproj -- --scenario defense-runtime-matrix --headless --engine zone --hz 30 --seed 101 --output artifacts/defense-redesign/p04/runtime-30
dotnet run --project RetroQB.csproj -- --scenario defense-runtime-matrix --headless --engine zone --hz 60 --seed 101 --output artifacts/defense-redesign/p04/runtime-60
dotnet run --project RetroQB.csproj -- --scenario defense-runtime-matrix --headless --engine zone --hz 120 --seed 101 --output artifacts/defense-redesign/p04/runtime-120
dotnet run --project RetroQB.csproj -- --scenario defense-runtime-matrix --headless --engine man --hz 60 --seed 101 --output artifacts/defense-redesign/p04/man-requalification
dotnet run --project RetroQB.csproj -- --scenario defense-calls --engine zone --capture --seed 101 --output artifacts/defense-redesign/p04/capture
dotnet run --project RetroQB.csproj -- --scenario defense-deep --engine zone
```

In the interactive development scenario, keys 2/3/4/7 select Cover 2, Cover 3, Cover 4 and Buzz. Normal launch remains legacy. Next gate: P5 run fits, play-action discipline, pursuit and coordinated pass-to-run transitions. P6 pressure/human blitz controls, P7 match, P8 formations, P9 strategy and P10 balance/cutover remain outstanding. The retained scenarios do not establish exhaustive balance or production performance.
