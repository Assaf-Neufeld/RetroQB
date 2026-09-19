# Phase 0 — Baseline and reproducible verification

M0 status: **Verified**, September 19, 2026. Later engineering milestones have not started.

Baseline source revision: `0d877c90d5c5e7682f1acd09182a1629127f8698`. Phase 0 changes are in the working tree relative to that revision. The design/roadmap documents were already untracked when implementation began.

## Results

| Check | Result | Local evidence |
| --- | --- | --- |
| Before-change Debug build | Passed; 0 warnings, 0 errors | [build log](../artifacts/phase0/baseline-build.log) |
| Before-change regression suite | 346 passed, 0 failed, 0 skipped | [test log](../artifacts/phase0/baseline-tests.log), [TRX](../artifacts/phase0/baseline.trx) |
| After-change Debug build | Passed; 0 warnings, 0 errors | [build log](../artifacts/phase0/final-build.log) |
| After-change regression suite | 371 passed, 0 failed, 0 skipped; 25 new cases | [test log](../artifacts/phase0/final-tests.log), [TRX](../artifacts/phase0/phase0-final.trx) |
| Release build | Passed; 0 warnings, 0 errors | [build log](../artifacts/phase0/release-build.log) |
| Release scenario access | Rejected with exit code 2 before opening a window | [rejection log](../artifacts/phase0/release-scenario-rejection.log) |
| Unknown scenario | Rejected with exit code 2; available names listed | [rejection log](../artifacts/phase0/unknown-scenario-rejection.log) |
| Documented Debug launch | Completed with exit code 0 | [launch log](../artifacts/phase0/launch-smoke.log) |
| Rendering independence | All five headless traces/final states matched their rendered runs | Reports in `artifacts/phase0/scenarios` and `artifacts/phase0/captures` |

The first sandboxed restore could not read the existing user NuGet configuration. Running the baseline command with the required filesystem access succeeded; this was an environment access issue, not a source/build failure. Subsequent checks used the restored packages with `--no-restore`.

No pre-existing test failures were found. All original 346 tests still pass. Normal startup and controls were checked through the production session with scripted menu confirmation, pregame, play selection, snap, QB movement, and pause; this is not a claim of a physical-keyboard playtest.

## What changed

- `IGameInput` allows a scripted input source alongside the unchanged keyboard mappings in `InputManager`. Menus, replay handling, and play execution use that same boundary.
- `GameSession` accepts an injected record store and uses the injected RNG for coverage, setup, play selection, throws, and tackles. Former independently created gameplay RNGs made a seed insufficient to reproduce a session.
- Layout updates moved to drawing, so fixed-step `GameSession.Update` runs without opening a Raylib window. Simulation behavior uses the same controllers as normal play.
- `DriveStart` permits a validated starting spot/down/distance without fabricating previous plays. Normal drives still start at the own 20, first-and-10.
- Internal scenario setup selects a catalog call, explicit team attributes and season stage, and captures detached observations without reflection access to session fields. Game-clock fields are intentionally absent until M2; reports state that the baseline ruleset has no game clock.
- A small fixed-step driver runs five offensive scenarios, writes a JSON trace, and optionally shows or captures the production renderer. No CPU offense, playable defense, or scoring-rule replacement is included in M0.

## Reproduce the scenarios

Run from the repository root. Scenario access is available in Debug builds only.

```powershell
# Watch the scripted scenario in a visible window; close with Escape.
dotnet run --project RetroQB.csproj -- --scenario offense-pass --seed 101

# Run the same simulation without a window and save its trace.
dotnet run --project RetroQB.csproj -- --scenario offense-pass --seed 101 --headless --output artifacts/phase0/recheck-pass

# Capture production-renderer states in a hidden window, then exit.
dotnet run --project RetroQB.csproj -- --scenario offense-replay --seed 101 --capture --output artifacts/phase0/recheck-replay

# Run just the Phase 0 checks, or the complete regression suite.
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --filter FullyQualifiedName~DevelopmentScenarioTests
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj
```

`--headless` and `--capture` are mutually exclusive. Unknown names/options, missing values, malformed seeds, and repeated options are rejected. `defense-drive` is not implemented yet and deliberately fails rather than silently opening a different scene.

Without `--output`, reports go to a unique temporary directory. The console output identifies `report.json`. A visible run holds its terminal screen until closed; closing early writes an incomplete report and returns exit code 1. A normal launch with no arguments still opens the ordinary team menu.

| Scenario | Starting series | Script | Seed 101 result | Ticks at 60 Hz |
| --- | --- | --- | --- | --- |
| `offense-pass` | Own 20, 1st & 10 | Mesh; throw to priority 1 on active tick 45; run forward after catch | 80-yard TD, 7–0, one completion/attempt | 525 |
| `offense-run` | Own 20, 1st & 10 | HB Dive; forward movement through exchange and carry | Tackle, gain 4, 0–0 | 106 |
| `offense-play-action` | Own 20, 1st & 10 | Gun Doubles PA Cross; throw on active tick 90; run after catch | 80-yard TD, 7–0; fake and pass-ready phases observed | 522 |
| `offense-field-goal` | Opponent 30, 4th & 5 | 47-yard kick; press at the center of each meter | Made kick, **3–3 under current legacy rules** | 213 |
| `offense-replay` | Own 20, 1st & 10 | Same pass, then F; allow actual replay to finish | Same TD/stats, 280 recorded frames, no duplicate score | 1325 |

These expected results were frozen after the input/RNG seams were installed. The old session was not fully seedable, so they are not claims of matching a particular old random defensive alignment. They are a reproducible baseline for subsequent phases. Scoring tests that expect synthetic away points must change intentionally when real possession rules activate.

## Trace and persistence guarantees

Each report contains schema version, scenario/play IDs, seed, explicit teams and series, stage, fixed timestep, tick count, completion/failure, isolated save path, terminal rules/stat state, and a trace of inputs/state transitions plus periodic snapshots. Actor and ball positions are included. A neutral frame is the default between recorded commands; sustained nonneutral commands are recorded every tick.

All scenarios inject a fresh store under `%TEMP%/RetroQB/scenario-saves/<unique-id>/player-records.json`. Even a supplied report-output directory cannot change this save location. Tests save an actual scenario record there and verify that an existing `player-records.json` sentinel alongside reports remains untouched. No scenario loads the ordinary `%LOCALAPPDATA%/RetroQB/player-records.json` store.

Repeated runs have different temporary save paths. Compare `Trace` and `Final`, not whole report bytes. Replay creation timestamps and cosmetic particle/camera randomness are outside the determinism contract; gameplay traces are deterministic. Captures are visual QA artifacts, not byte-identical pixel golden files.

The driver fails with a trace on non-finite ball/actor state or after 90 simulated seconds without completion. It never fabricates a terminal result to make a scenario pass. Temporary report/save directories are retained for diagnosis; there is no recursive cleanup of user files.

## Visual evidence

Before changing code, the existing `VisualPreview` tool captured 14 offensive-feedback images and 68 field-goal images:

```powershell
dotnet run --project tests/VisualPreview/VisualPreview.csproj -- --play-feedback artifacts/phase0/baseline-feedback
dotnet run --project tests/VisualPreview/VisualPreview.csproj -- --field-goal artifacts/phase0/baseline-kicks
```

After adding the verification seams, the new scenario launcher captured 32 images across actual plays and replay playback. Inspected production captures include:

- [Mesh pre-snap, call selection and scoreboard](../artifacts/phase0/captures/offense-pass/00000-PreSnap-HeldByQB--.png)
- [Play-action fake](../artifacts/phase0/captures/offense-play-action/00002-PlayActive-HeldByQB--FAKE.png)
- [Field-goal accuracy meter](../artifacts/phase0/captures/offense-field-goal/00078-FieldGoal-HeldByQB-Accuracy-.png)
- [Production replay overlay](../artifacts/phase0/captures/offense-replay/00526-Replay-HeldByReceiver--.png)

The snapshots show readable field/HUD rendering at 1440×900. This phase does not claim the full multi-resolution defensive UI audit scheduled for M10. Existing preview captures are staged visual fixtures; new scenario captures come from the actual simulation, without forced catches or scoring outcomes.

Artifacts are under the repository's existing ignored `artifacts/` directory. They are available in this workspace and can be regenerated with the commands above; a fresh checkout will need to regenerate them. This checked-in report and the scenario tests retain the revision, expectations, and reproduction procedure.

## M0 acceptance closure

- [x] Baseline build/test counts recorded; no source failures to carry forward.
- [x] Same seed/input reproduces all five terminal states and traces.
- [x] Save isolation tested with a real temporary write and a protected sentinel.
- [x] Normal startup/menu/confirmation/offensive movement/pause smoke passes; existing offensive regression suite remains green.
- [x] Pass, run, play-action, field-goal, and replay captures and behavior records retained.

Next: M1's team identity, possession, and scoring foundations. M0 does not implement those rules.
