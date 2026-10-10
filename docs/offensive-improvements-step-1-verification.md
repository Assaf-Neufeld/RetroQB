# Offensive improvements — Step 1 verification

October 10, 2026. Status: **implemented; new offensive behavior verified, with five unchanged baseline test failures**. Source changes are uncommitted on top of `f82b187bb28083612cf576a4ad5bad029a773d42`. Steps 2–3, audibles, and motion have not been implemented.

## Delivered behavior

- All 49 named passing calls use formation-specific authored routes and immutable slot-based read progressions. Mesh has separated shallow crossing depths; Flood/Sail has coordinated clear-out, corner, and flat levels; Stick/Spacing and selected curls have deliberate stops. Remaining receivers receive purposeful clear-outs/outlets. The 100-call catalog, permanent IDs, formations, targeting order, and wildcard generators remain intact.
- Shared route geometry compresses upfield depths against one common 28-yard budget near the back line, retaining concept layers rather than independently clamping every route to the same endpoint. Previews and movement consume that geometry. Stops show endpoint circles; continuing routes show arrows.
- A joint protection allocator covers the line and protecting backs/TEs, maximizes reachable threat coverage, preserves ordered line lanes, and discourages assignment churn. Nearby pocket protectors pick up penetration. Authored slide direction changes set landmarks and allocation cost. Six/seven-player max protection trades routes for blocking; only a declared helper can deliberately double an already-covered threat after free threats take priority.
- Check releases honor minimum blocking time, visible threats, nearby help, and a decision timeout. An immediate uncovered threat retains the protector past the timeout; a throw/catch exits the opening block. Existing explicitly timed fixture assignments remain supported.
- CPU reads consume the authored order and time/stage gates, independently of human target keys. Pressure outlets also respect development gates. Named routes use their authored development gates in place of the previous generic depth threshold and Four Verts special case. CPU call selection and more advanced anticipation remain Step 3 work.
- The offense panel shows the first read, outlet, protection count, and slide/max-protect label. Eligible players retain visible targeting labels while check-protecting. PA Cross retains its fake, cancellation behavior, and wheel outlet.

## Validation

| Check | Result |
| --- | --- |
| Baseline full test suite | 867 passed, 5 failed, 872 total |
| Targeted offense/feedback suite before the final identity regression | 315 passed, 0 failed |
| Final full test suite, including the identity regression | 886 passed, the same 5 failed, 891 total |
| Existing full-catalog comparison sweep | Baseline and final: 2,880 cases each, 0 mechanical failures |
| Final production passing sweep | 2,646 cases, 0 mechanical failures |
| Final standard matches | Seeds 301–303 complete: 7–10, 21–7, 21–17 |
| Additional integration matches | 24 short matches completed before the final read-gate refinement |
| Final render captures | 32 images: four calls, both flips, four window sizes |
| Build and whitespace | Build succeeds; `git diff --check` passes |

The 19 new test cases cover complete/valid read data, invalid gates and release contracts, live Mesh separation at 30/60/120 Hz, same-sideline Flood layering at midfield and near the goal line, stable human targeting versus concept read order, undeveloped reads under pressure, six/seven-player protection, shuffled actor enumeration, immediate threats surviving the release timeout, and live overload/delayed-rush contact without accidental double assignments. A display-name regression confirms that renaming a call/concept does not change its routes, protection, or reads: authoring uses the permanent ID. Existing route, blocking, exchange, fake-cancellation, contact, defense, clock, stats, replay, and restart tests provide broader integration coverage.

The final passing matrix runs every named pass through the production defensive engine at own 1, midfield, and opponent 5, against regular/playoff/Super Bowl opponent profiles, both offensive orientations, and 30/60/120 Hz, seed 101. Calls rotate through all ten defensive calls. Like the baseline harness, the scripted human linebacker chases the ball. These are reproducible mechanical checks, not human skill or competitive-balance measurements.

Visual review checked the smallest 1000×700 layout and a flipped seven-protector formation at 1440×900. Guidance fits without colliding with controls or the scoreboard, targeting labels remain visible, and route/block previews mirror with the selected call. Other captured sizes are 1280×720 and 1920×1080.

The older offense panel was also rendered at 1280×720. Its read/protection guidance uses the existing two-line coaching area so it does not overlap the stage card or controls; evidence is `legacy-preview.png`.

The five baseline failures remain:

1. `FieldGoalTests.OutOfRangeCannotSnap` — expected Setup, actual Ready.
2. `TimedSeasonTests.REPLAY01_ReplayAfterPossessionChangeKeepsRecordedTeamClockAndLinebacker` — expected replay instance, actual null.
3. `HalvesAndSpecialTeamsTests.CpuPuntsOnlyWhenPinnedBackWithLongYardage` — the existing failures at yard/distance 20/10, 10/2, and 40/8.

An exploratory expanded legacy-defense sweep also exposed two near-own-goal defender-boundary cases. The final expanded sweep explicitly uses the production engine used by normal timed games; all its cases pass. The original legacy sweep retains its original field positions for a like-for-like baseline comparison. Older legacy adapters therefore are not being certified across the expanded goal-line matrix.

## Baseline comparison and limits

The existing legacy catalog sweep uses the same calls, positions, stages, seeds, defensive-call rotation, and scripted input before and after the changes. Across its 1,458 pass-call samples:

| Observation | Baseline | Final |
| --- | ---: | ---: |
| Pass attempts | 1,454 | 1,410 |
| Completions | 1,101 | 1,192 |
| Completion rate | 75.7% | 84.5% |
| Sacks | 4 | 42 |
| Interceptions | 99 | 59 |
| Passing yards per attempt | 14.19 | 14.74 |
| Mean play duration | 2.58 s | 3.34 s |

Final mean first-throw time is 1.87 s; the original baseline did not capture that timestamp, so no baseline value is claimed. The longer development gates materially change sack exposure and passing outcomes. Balance tuning and hands-on offense/defense playtesting remain Step 3 work. Short-match fixtures can enter many paired-overtime possessions and produce inflated scores, so their completion is only a mechanical result. Harness wall times were collected during concurrent validation and are not controlled frame-cost/allocation benchmarks.

## Reproduction and evidence

Build first, then run the development scenarios in Debug:

```powershell
dotnet build --no-restore -p:UseSharedCompilation=false -m:1
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-build --no-restore
dotnet run --no-build -- --scenario release-catalog --headless --output artifacts/offensive-improvements/step-1/final-catalog
dotnet run --no-build -- --scenario offense-passing-matrix --headless --output artifacts/offensive-improvements/step-1/final-passing
dotnet run --no-build -- --scenario release-default --headless --output artifacts/offensive-improvements/step-1/final-matches
dotnet run --no-build -- --scenario offense-passing-preview --capture --output artifacts/offensive-improvements/step-1/final-layouts
dotnet run --project tests/VisualPreview/VisualPreview.csproj --no-restore -p:UseSharedCompilation=false -- --readme artifacts/offensive-improvements/step-1/legacy-preview.png 1280 720
```

Evidence is retained under ignored `artifacts/offensive-improvements/step-1/`: `baseline/catalog.json`, `final-catalog/catalog.json`, `final-passing/passing.json`, `final-matches/default-matches.json`, `matches/matches.json`, `comparison.json`, `tests/step-1-final.trx`, and `final-layouts/*.png`. Sandbox VSTest runs failed to establish their required local socket connection; completed test runs used permitted local networking. The shared compiler also canceled inside the sandbox; the command-line compiler succeeded with `UseSharedCompilation=false`.
