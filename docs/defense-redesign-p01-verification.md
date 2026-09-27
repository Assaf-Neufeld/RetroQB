# P1 verification — shared defensive plan compiler

September 26, 2026. P1.1–P1.5 complete for **passive base/nickel man plans**. Base revision: `e3b2a4b36b70376cb31a163fc970add8230a3835`, plus the uncommitted implementation files recorded in `artifacts/defense-redesign/p01/verification-summary.json`. Rule version `defense-plan-p1-v1`; call definitions version 1. Normal gameplay continues to use the complete legacy engine. The new live Cover 1 slice begins in P2.

## Delivered

`Gameplay/Defense/` contains immutable visible snapshots, geometric surface labels, typed assignments, versioned calls/plans, an exact assignment solver, a validator, and a bounded per-preview cache. `PlaySetupController.SetupPreview` instantiates physical defenders from the validated plan without invoking `DefenseFactory`, the old resolver, uniqueness repair or star swaps.

The snapshot copies ball, QB, LOS, receiver ID/slot/position and lineman ID/position. It has no offensive play ID, route, eligibility, release timer, blocking job or star information. Receiver IDs are typed `ReceiverId` values, never list offsets. The current blockers lack persistent roster IDs, so the formation adapter gives them canonical pre-snap left-to-right IDs; explicit snapshots may provide their own IDs. QB/ball identities are implicit singletons. All snapshot/plan collections own their copies and expose read-only views.

Surface analysis provides left/right outside-in ranks, strength, attached TE/closed sides, backfield threats, close-release groups, and gaps between the actual linemen plus two outside entries. Caller-selected base/nickel remains authoritative. The default suggestion uses nickel for three or more detached threats. Heavy formations retain their actual four skill targets and six linemen; extra coverage players become explicit helpers. Bunch/trips behavior at this phase is explicitly `LockActorIds`/`LockAllPrimaries`, not an advertised switch or match check.

The compiler reserves the front, added rushers and mandatory helpers before matching. The bounded bitmask solver finds the global minimum over the remaining legal candidates. Travel, side locality, detached-threat suitability and back/TE suitability affect costs. Star status does not enter the compiler. Every primary, including an LB, aligns to its final target with inside leverage; spacing adjustments preserve that ownership. Rush entries reference real surface gaps and name the two contain edges as plan metadata.

| Compiled call | Rush | Mandatory help | Remaining jobs with five threats |
| --- | --- | --- | --- |
| Cover 1 | 4 | FS middle deep, SS low helper | Five man primaries |
| Cover 2 Man | 4 | FS left deep, SS right deep | Five man primaries |
| Robber | 4 | FS middle deep, SS robber | Five man primaries |
| Cover 1 Edge Pressure | 5 | FS middle deep | Five man primaries |
| Cover 0 Pressure | 6 | None | Five man primaries |

With fewer threats, surplus coverage actors receive explicit low help. Conditional run jobs are immutable intent metadata triggered by observed run commitment; no new run recognition, pursuit, blitz movement or pressure-side flip executes in P1. Unsupported calls/versions/packages fail explicitly.

Validation rejects wrong/missing/duplicate roster identities, bad receiver ownership, unsupported/missing pass jobs, invalid run metadata, bad gap IDs, incorrect rush/help/shell budgets, lost reserved safeties/contain, invalid control slots, overlap and non-finite/out-of-field alignment. The compiler validates before returning. Preview validates again and rejects offensive geometry different from the compiled snapshot.

`DefensivePlanPreview` is intentionally **not** `PlaySetupResult`. Typed jobs stay in `Plan`; passive entities do not populate legacy receiver-index/rush/zone fields. This prevents accidentally executing stable IDs through legacy list-index targeting. Future P2 integration must consume the typed plan directly. Entity movement cannot mutate the plan; roster modifiers are applied once by the existing defender constructor.

The one-entry `DefensivePlanCache` keys visible geometry, call/version, requested personnel and seed. It has no global state or RNG draws. A changed visible alignment requires a new resolution; repeated hidden-only changes reuse the plan. Two offensive flips reproduce the original assignments. P1 does not recompile during a live play or implement motion support.

## Evidence

- **6,000 valid plans, zero failures** in `defense-plan-matrix`: all 100 catalog plays × both offensive flips × base/nickel × five supported calls × LOS 11/60/109. Each plan has a preview/spawn check using Default and ScarletGuard defensive attributes.
- Representative full plans: `artifacts/defense-redesign/p01/pass.mesh.json`, `pass.pistol-twins.scissors.json`, and `run.hb-dive.json`. The manifest includes a SHA-256 assignment digest for every combination.
- DM-01's structural gate: five distinct primary owners, both reserved safeties, an explicit legal LB/WR mismatch, and target-based LB alignment. In the representative plan OLB1 aligns 0.5 yards inside the local receiver ID 1; it no longer starts 18.122 yards across the field from ID 2. Deep helpers stay FS/SS.
- DM-02's structural gate: closed-side TE and twins labels/ownership in both orientations. DM-18's pre-snap gate: changing routes, blocking and eligibility cannot change compilation. These are partial DM gates; live portions remain owned by P2/P3/P7.
- Tests include noncontiguous/shuffled receiver IDs, immutable input/output copies, balanced local assignments, trips weak/back owners, five detached threats with explicit base override, six-lineman gaps, repeated preview/spawn and ratings, cache invalidation, unsupported calls and corrupted-plan rejection.
- Exact-solver evidence includes a greedy counterexample and independent exhaustive enumeration of 30 five-target/seven-candidate cost matrices.

Builds: Debug and Release succeed with zero warnings/errors. Full-suite baseline was 617 passed / five failed; the final expanded suite is **645 passed / the same five failed** (650 total), including **28 new passing tests**. The failure names and assertion sites match the baseline; results are retained in `tests/phase01-final.trx` and `verification-summary.json`. No baseline failure was hidden, removed or converted into an expected failure.

Commands from the repository root:

```powershell
dotnet build RetroQB.csproj --no-restore --verbosity minimal
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-restore --logger 'trx;LogFileName=phase01-final.trx' --results-directory artifacts/defense-redesign/p01/tests --verbosity minimal
dotnet run --project RetroQB.csproj --no-build -- --scenario defense-plan-matrix --headless --seed 101 --output artifacts/defense-redesign/p01
dotnet build RetroQB.csproj -c Release --no-restore --verbosity minimal
```

`defense-plan-matrix` is a Debug-only passive diagnostic, not a new playable engine menu. It accepts existing seed/output flags and requires headless operation. There is no `--engine` flag. Generated traces/plans remain under the ignored artifacts directory.

## Initial tuning and design refinements

All values below are initial deterministic compilation choices, not runtime balance claims. Backs are at least three yards behind LOS; attached TE positions are within three lateral yards of the visible line surface; close-release groups use 3.5 lateral / 2.5 depth yards. Primary inside leverage is 0.5 yards, ordinary DB/LB depth 4.5/3.5, deep-helper depth 14; existing field-depth scaling compresses these near the goal line. Minimum defender spacing is 2.25 yards, with a bounded search preferring extra depth over lateral displacement.

Soft pairing costs are lateral travel, +14 for a non-DB on a detached threat, +10 for a DB on a back, +4 for a DB on an attached threat, and +4 for a cross-side pairing. These permit necessary mismatches while preferring local suitable actors. Remaining candidates always exclude reserved shell/rush actors. Exact ties use stable slot/receiver ID order; the seed is preserved as part of resolution identity without adding arbitrary randomness.

P1 uses a separate preview result type instead of overloading the legacy live setup return type. This is a deliberate migration safeguard because `DefenderTargeting` interprets legacy `CoverageReceiverIndex` as a list subscript. Bunch groups are detected but locked; zone definitions and advanced pressure variants are rejected rather than approximated by man plans.

## Caller inventory and migration paths

| Existing boundary/caller | Current behavior | Named migration / regression gate |
| --- | --- | --- |
| `DefensiveDrive.PrepareActors` | Resolves the ten legacy calls and invokes resolved legacy setup | P2 development Cover 1 selection; `DefensiveDriveTests`, possession/contact/replay tests; P10 default cutover |
| `DefensiveDrive.SelectDefense` | Reinstantiates selected legacy preview while retaining offense/clocks | P2 typed plan selection; P6 input/variant handling; `DefensivePlaybookTests`, control/deadline tests |
| `DefensivePlayResolver.Resolve` → `DefenseFactory.CreateDefense` | Legacy construction and subsequent repair/star swaps | Leave intact for the old engine; never call from the new compiler. Retire after P10 call coverage |
| `PlaySetupController.SetupPlay(context, call, ...)` → factory | Legacy coordinator/factory setup | `GameSession` migration below; current coverage/factory tests remain old-engine regressions |
| `PlaySetupController.SetupPlay(..., ResolvedDefensivePlay, ...)` → `CreateDefense` | Existing resolved live setup | P2 adds a separate typed runtime integration; preview/spawn parity required |
| Legacy `GameSession` constructors and `SetupPlay` | Inject `DefenseFactory` and use coordinator setup; timed games delegate to `FullMatchSession` | P10 migrate or remove explicitly after full new-engine lifecycle coverage; keep `DevelopmentScenarioTests` and session/replay/special-team suites |
| `FullMatchSession`, timed season and full-match launchers | Execute through `DefensiveDrive`; no separate factory implementation | P2 development integration, P9 CPU policy, P10 normal rollout; `TimedSeasonTests` and timed/full-match regressions |
| `DefenseScenarioLauncher` and `ReleaseVerification` | Legacy production-controller scenarios | Keep as baseline comparators; later compare the same frozen fixture inputs against the new runtime |
| New `SetupPreview` / `DefensePlanMatrix` | Validated passive compiled entities only | Ready for P2 to consume `ResolvedDefensivePlan`; no implicit legacy runtime conversion |

The [98-row rule ledger](defense-redesign-rule-ledger.md) marks only six ALIGN rules verified in P1's supported scope. The next gate is **P2's complete live Cover 1 slice**: observations, runtime state, motor, execution integration and full-play evidence. Zone reactions, run fits, smarter active pressure and human pressure controls remain intentionally unimplemented until their assigned phases.
