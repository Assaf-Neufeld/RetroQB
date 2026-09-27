# P0 verification — reproducible defense baseline

September 26, 2026. P0.1–P0.5 complete. Base revision: `e3b2a4b36b70376cb31a163fc970add8230a3835`; implementation is an uncommitted working-tree change. See the P1 evidence directory for the source hash inventory. Engine: **legacy**, manifest version `defense-baseline-v1`. Windows x64, SDK 10.0.112, runtime .NET 10.0.12.

## Baseline and triage

Before implementation: **622 tests, 617 passed, five failed**. Baseline TRX: `artifacts/defense-redesign/p00/tests/baseline.trx`. None of these failures blocks a single defensive-play trace, and no special-teams policy or replay-lifecycle fix is included in this phase.

| Existing failure | Observed cause / ownership |
| --- | --- |
| `FieldGoalTests.OutOfRangeCannotSnap` | The test treats a 61-yard kick as out of range. `FieldGoalAttempt.MaxDistance` is currently 80, so the kick reaches `Ready` instead of staying in `Setup`. Resolve the test/policy disagreement separately. |
| `TimedSeasonTests.REPLAY01_ReplayAfterPossessionChangeKeepsRecordedTeamClockAndLinebacker` | The recorded clip exists, but after continuing through possession change, `FullMatchSession.Update` only opens replay while `Drive.LastResult?.DriveEnded` is true. The test expects the previous clip to remain reopenable; actual `Replay.Clip` is null. Replay presentation/lifecycle issue, independent of defensive assignment. |
| `HalvesAndSpecialTeamsTests.CpuPuntsOnlyWhenPinnedBackWithLongYardage`, yard 20 / distance 10 | `MatchStrategy.Choose` now punts on ordinary fourth downs at yard <=35 unless distance <=0.5. Test expects Scrimmage. |
| Same test, yard 10 / distance 2 | Same current fourth-down rule; test expects Scrimmage. |
| Same test, yard 40 / distance 8 | Current fallback punts when distance >2; test expects Scrimmage. |

The post-implementation suite retains these same five names and assertion sites. See [P1 results](defense-redesign-p01-verification.md).

## Implemented harness

`DefenseRedesignMatrix` defines DM-01 through DM-20 with a catalog call, visible/acceptance geometry, defensive call, teams, start down/distance/yard, six-second observation window, desired transitions and required capability. The manifest records seed, stepping rate, orientation and input mode. DM-19 starts at yard 98 with two yards to go. DM-02 runs both offensive orientations.

Every baseline uses production `DefensiveDrive`, setup, play execution, ball, blocking, overlap, tackle, clock and replay controllers. No alternative simulator, forced incompletion or artificial whistle is used. A live play at the six-second cutoff is reported as `observation-window-ended-without-forced-whistle`, not an engine error or successful acceptance.

The four input modes are:

- `AssignmentFollowing`: the controlled linebacker follows the current legacy targeting function, rather than chasing the ball on every play.
- `Abandonment`: follows that assignment for one second, then deliberately moves toward the offense's backfield.
- `Stationary`: zero human movement input. Contact can still displace the player.
- `FullAI`: an internal development override removes manual defensive ownership while retaining CPU offense. All eleven defenders use the production AI update. Replay/result ownership correctly records no manual defender.

All frames contain QB, receiver, lineman and defender IDs; positions and velocities; ball state/holder; human input/ownership; legacy target points; man IDs and zones; legacy rush flags; blocking contacts; significant target-point changes; and terminal contact/ball outcomes. The legacy rush flag expresses assignment state, not proof that a stationary human participated in pressure. Target/ball/contact labels are explicitly legacy facts, never invented matrix-rule decisions. Setup failures go into the manifest; step exceptions retain the preceding frames, and non-finite values can be serialized for inspection.

Advanced fixtures name acceptance geometry and the closest current catalog baseline separately. P0 does **not** claim that a legacy straight rush exercises a future stunt, or that a legacy zone executes Quarters Match. Exact authored transitions, opponent motion variants and unavailable-helper versions are required when their owning phases implement them. All such results remain `unresolved` here.

## Reproductions and limitations

| Initial case | Measured result |
| --- | --- |
| DM-01 | Four repeated input-mode cases reproduce an alignment defect: nickel Cover 2 Man gives OLB1 receiver ID 2, **18.122 yards laterally away**. The frozen diagnostic threshold is >5 lateral yards. This is an alignment defect despite a later `PassDefended` outcome. |
| DM-02, normal/flipped | No >5-yard lateral defect at seed 101. Preserve as unresolved; do not infer that all twins/closed-side behavior is correct. |
| DM-04 and DM-06 | Trajectories and targets captured. The old engine has no team ownership/transfer events; these runs do not isolate a deep-seam ownership failure. Remain unresolved until the coverage-board cases can assess the intended transitions. |
| DM-17 | Production `MatchInput.Call` indices 8 and 9 select `def.edge` and `def.zero`. `InputManager` maps keyboard 9/0 to those indices. A defensive flip changes nothing; two ignored flips trivially restore the same assignments. Reported as unsupported, never as a passing flip test. Offense ID and snap deadline remain stable. Native physical-key injection was not performed. |

At seed 101 / 60 Hz: **84 traces**, four alignment-defect results and 80 unresolved desired-behavior results; no harness failures. All **84 trace files matched SHA-256 hashes** in the repeat run. The same 84-case matrix completed at **30 and 120 Hz**. Identical fixed-step runs are deterministic; different stepping rates are not required to produce identical outcomes.

Performance baseline, warmed 60-Hz repeat: **17,064 measured production steps**, weighted **61.19 microseconds/step**, **9,742.19 allocated bytes/step**. Timing covers `Drive.Update` and its existing replay allocations; trace construction/serialization is outside the timer. These are local Debug measurements, not a release performance budget or cross-machine guarantee. Performance JSON is separate from deterministic traces.

Hidden offensive reads remaining in the legacy runtime include `RouteGeometry.HasCompletedBreak`/`RouteSide` in `DefenderTargeting`, and eligibility, blocking state and `RouteStart` in `ZoneCoverage`. P1 excludes those inputs from compilation; P2/P4 replace their runtime use.

## Reproduce

Run from the repository root. Scenario commands require Debug and `--headless`. `--hz` accepts only 30, 60 or 120 and applies only to this matrix; other scenarios reject it.

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-restore --logger 'trx;LogFileName=baseline.trx' --results-directory artifacts/defense-redesign/p00/tests --verbosity minimal
dotnet run --project RetroQB.csproj -- --scenario defense-redesign-matrix --headless --seed 101 --output artifacts/defense-redesign/p00/seed101
dotnet run --project RetroQB.csproj --no-build -- --scenario defense-redesign-matrix --headless --seed 101 --output artifacts/defense-redesign/p00/seed101-repeat
dotnet run --project RetroQB.csproj --no-build -- --scenario defense-redesign-matrix --headless --seed 101 --hz 30 --output artifacts/defense-redesign/p00/seed101-hz30
dotnet run --project RetroQB.csproj --no-build -- --scenario defense-redesign-matrix --headless --seed 101 --hz 120 --output artifacts/defense-redesign/p00/seed101-hz120
```

The first command now runs the expanded current suite; retain the original baseline TRX rather than overwriting it when comparing future phases. Generated artifacts are ignored by Git; the source registry, tests and these reports are retained. `DefenseRedesignMatrixTests` checks fixture resolution, argument validation, identity completeness, input modes and deterministic traces at all three rates. P0 supports later gates without marking future gameplay rules passed.
