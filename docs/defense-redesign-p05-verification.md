# P5 verification — run fits and pursuit

September 27, 2026. P5 implements conditional fits, observable play-action reads, coordinated pursuit, a budgeted spy and behind-LOS containment. Plan/runtime versions are `defense-plan-p5-v1` / `defense-runtime-p5-v1`. Normal gameplay remains legacy until P10. Base revision `e3b2a4b36b70376cb31a163fc970add8230a3835`; source and evidence hashes are retained in `artifacts/defense-redesign/p05/verification-summary.json`.

## Delivered behavior

`RunFitCompiler` assigns every actual offensive gap a distinct conditional owner, both force edges, two backside responders and one last support actor. Existing pass assignments remain authoritative until observed possession or a QB crossing the LOS confirms the run. Spy consumes an existing low/robber helper; it cannot consume a primary, deep safety, bracket or rusher.

`RunObservationBuilder` reads only visible motion and possession. A converging backfield player can trigger a mesh read for at most 0.35 seconds. Underneath defenders may take a 0.8-yard false step only when their normal coverage target remains reachable within 0.45 seconds. Deep coverage remains intact. Aborted exchanges, separation and throws end the read. A QB outside the pocket can still throw; crossing the LOS latches the run response while that QB holds possession. Catch and handoff reads use observed possession history.

`RunFitCoordinator` emits one intent per actor. Confirmed carriers replace active coverage/rush jobs with gap, force, cutback, last-support, tackle or complementary pursuit jobs. Intercepts use actual speed, acceleration and turning, with a 1.5-second AI / 0.75-second human horizon. Blocked or beaten first tacklers trigger reachable replacement; lateral reversals recognize the backside responder after 0.12 seconds. Force replacements reserve a free reachable actor. Missing support and vacated gaps are explicit concessions. Transfers retain offer/reserve/accept/commit or decline evidence.

Stationary runners exposed a real stalemate in the wider matrix. Edges now squeeze a stopped carrier while retaining outside leverage. When direct interior pursuit remains obstructed, one AI support actor takes an outside recovery path, retaining force, cutback and deepest support. After 0.75 seconds without progress, that actor widens the same committed path rather than reversing each frame. Carrier movement cancels the detour. Manual actors are never moved automatically. These changes do not alter collision, blocking, tackle resolution or speed limits.

The P2 carrier fallback is removed. Run boards, transfer evidence and live targets survive replay snapshots; renderers show the controlled player's run job. The captured 1440×900 drive was inspected through a completed tackle, including the gold live target and pursuit cue.

## Evidence

- `RunFitTests` exercises DM-14/15, blocked force, missed first tackles, cutbacks, catches, spy budgets, late throws, aborted fakes, confirmed QB runs, deterministic replays, manual ownership and stationary recovery. Frame-rate-sensitive cases run at 30/60/120 Hz.
- 7,200 valid compiled fit plans: 100 catalog plays × both offensive flips × base/nickel × nine calls × LOS 30/109. Existing man/spot compiler and behavioral regressions remain required.
- **1,728 production plays, zero failures:** 576 at each of 30/60/120 Hz. Nine calls × eight offensive plays × both team roles × four input modes. Seed 101; Ballers/Scarlet Guard; midfield. Production whistles must occur within 30 seconds; decisions, ownership and physical speed are checked throughout. Man/zone football behavior is separately tested rather than inferred from these lifecycle sweeps.
- The initial wider matrix found 44/64/60 stationary-runner failures at 30/60/120 Hz. Those failing traces remain under `runtime-*`; corrected evidence is under `final-runtime-*`. The regression now checks every call against four stationary human runs at all three rates.
- Final full suite: **760 passed / five unchanged baseline failures, 765 total**. P5 adds 34 passing cases. Counters and failure identities are recorded in `verification-summary.json` and `tests/phase05-final.trx`. Debug/Release builds and whitespace validation succeed.

Five unrelated baseline failures remain: `FieldGoalTests.OutOfRangeCannotSnap`, `TimedSeasonTests.REPLAY01_ReplayAfterPossessionChangeKeepsRecordedTeamClockAndLinebacker`, and `HalvesAndSpecialTeamsTests.CpuPuntsOnlyWhenPinnedBackWithLongYardage` for 20/10, 10/2 and 40/8. Their assertion sites/causes are unchanged from P0. None is suppressed.

Rules: DEC-01, RUN-01–08, PRESS-11. Behind-LOS escape here uses the existing four-man contain paths; P6 must requalify added pressure. A stationary protected QB can still be held indefinitely by the fixed rush entries; the test preserves pass coverage until an observed escape, then requires a physical resolution. P6 owns stalled-rusher counters. P5 does not invent a run or whistle from elapsed time. Match, additional fronts/personnel, CPU strategy, balance and production cutover remain pending.

## Reproduce

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --filter FullyQualifiedName~RunFitTests
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --logger "trx;LogFileName=phase05-final.trx" --results-directory artifacts/defense-redesign/p05/tests
dotnet build RetroQB.csproj -c Release
dotnet run --project RetroQB.csproj -- --scenario defense-fit-matrix --headless --engine zone --hz 30 --seed 101 --output artifacts/defense-redesign/p05/final-runtime-30
dotnet run --project RetroQB.csproj -- --scenario defense-fit-matrix --headless --engine zone --hz 60 --seed 101 --output artifacts/defense-redesign/p05/final-runtime-60
dotnet run --project RetroQB.csproj -- --scenario defense-fit-matrix --headless --engine zone --hz 120 --seed 101 --output artifacts/defense-redesign/p05/final-runtime-120
dotnet run --project RetroQB.csproj -- --scenario defense-control-base --engine zone --capture --seed 101 --output artifacts/defense-redesign/p05/capture
```
