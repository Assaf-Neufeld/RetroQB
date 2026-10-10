# Offensive improvements: final review

Date: October 10, 2026. Reviewed all three implementation steps, including authored
passing and protection, run blocking, screens and moving pockets, CPU decisions,
catalog selection, input arbitration, favorites, clocks, restart and replay.
Audibles and motion remain deferred.

## Fixes from review

- Filtering or favoriting a call now preserves the same resolved preview and its
  flip when that call survives the filter. The active call remains untouched until
  confirmation; navigating to a different call uses its cached resolution.
- Restart clears the play manager's cumulative usage and recommendation memory,
  alongside the match's team strategy state. Favorites survive restart.
- Replay remains available after continuing past a possession change. Replay key
  edges consume simultaneous continue/snap input; paused or unfocused playback
  cannot advance or be skipped. The offense controls now advertise F for replay.
- Untimed overtime no longer treats its zero remaining clock as late-game urgency
  in either CPU family selection or the shared recommendation scorer.

Added regression tests for these cases. The previous replay regression now passes.
Four old failures were stale special-teams expectations: the established kick limit
is 80 yards, and the documented CPU policy punts on ordinary fourth downs outside
useful kicking range. The range test now rejects an 81-yard attempt and accepts
61/80-yard attempts; punt tests retain fourth-and-inches and short midfield
exceptions. Kick mechanics and punt strategy were not changed.

## Verification

Final results and the balance rerun are recorded below. Generated output remains
under the ignored `artifacts/offensive-improvements/final-review/` directory.

- Debug and Release builds: no warnings or errors.
- Full final Debug suite: 1,054 tests passed.
- Production Release checks: 285 tests passed, covering offensive passing/design/
  integration, normal games, full matches, field goals, halves/special teams and
  timed seasons.
- Catalog matrix: 6,876 cases, zero failures. All 108 calls, three field starts,
  three opponents, both flips, and 30/60/120 Hz were exercised with CPU offense;
  all runs and new designed passes also ran through scripted human offense.
  Normal terminal resolution, legal throws, bounds, control ownership and designed
  transitions were checked. All ten defensive selections were checked for
  acceptance. Wall time: 426.5 seconds.
- Production renderer: 32 captures at 1000×700, 1280×720, 1440×900 and 1920×1080.
  Representative catalog, selected quick-sheet, flipped bootleg, favorites and
  empty-filter captures were visually inspected across all four sizes.

An exploratory whole-suite Release run passed 993 tests and failed 61. These are
development-defense tests that request engines deliberately disabled outside
Debug: 58 report the engine guard directly or through scenario failure, and three
hit an empty diagnostic collection after that guard. Their Debug equivalents pass.
The Release test list was then restricted to the production classes above; the
production guard and unrelated development tests were preserved.

The matrix completed before the final overtime scorer and controls-hint edits;
its fixed fixtures never use overtime and its controllers do not draw controls.
Final Debug/Release tests, balance games and renderer captures include those edits.

## Commands

```powershell
dotnet build tests/RetroQB.Tests/RetroQB.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1
dotnet build tests/RetroQB.Tests/RetroQB.Tests.csproj -c Release --no-restore -p:UseSharedCompilation=false -m:1
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=review-final-debug.trx' --results-directory artifacts/offensive-improvements/final-review/tests -p:UseSharedCompilation=false -m:1
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~OffensiveIntegrationTests|FullyQualifiedName~OffensivePassingTests|FullyQualifiedName~DesignedOffenseTests|FullyQualifiedName~NormalTimedGameTests|FullyQualifiedName~FullMatchTests|FullyQualifiedName~FieldGoalTests|FullyQualifiedName~HalvesAndSpecialTeamsTests|FullyQualifiedName~TimedSeasonTests' --logger 'trx;LogFileName=review-release-production.trx' --results-directory artifacts/offensive-improvements/final-review/tests -p:UseSharedCompilation=false -m:1
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario offense-designed-matrix --headless --seed 101 --output artifacts/offensive-improvements/final-review/matrix
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario offense-browser-preview --capture --seed 101 --output artifacts/offensive-improvements/final-review/layouts
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario offense-balance --headless --seed 101 --output artifacts/offensive-improvements/final-review/balance
dotnet .\artifacts\offensive-improvements\step-3\baseline-runner\bin\Debug\net10.0\RetroQB.Tests.dll artifacts/offensive-improvements/final-review/baseline
git diff --check
```

VSTest used its local loopback runner outside the filesystem sandbox. Builds use
the compiler without its shared server to avoid the local compiler-server issue.

## Balance and acceptance limits

The final balance rerun is compared against the frozen Step 2 runtime with
identical fixture keys, teams, defenses and seeds, as described in the
[Step 3 report](offensive-improvements-step-3-verification.md).

Both versions completed **1,296 fixed fixtures and six complete games with zero
mechanical failures**. The comparison asserts identical fixed-fixture keys and
game seeds/opponents before calculating metrics.

| Metric | Frozen Step 2 | Final reviewed changes |
| --- | ---: | ---: |
| Fixed-call completions / attempts | 645 / 690 (93.5%) | 601 / 687 (87.5%) |
| Screens completed / attempted | 48 / 48 (100%) | 43 / 48 (89.6%) |
| Sacks / attempts+sacks | 3 / 693 (0.43%) | 1 / 688 (0.15%) |
| Interceptions / attempts | 3 / 690 (0.43%) | 11 / 687 (1.60%) |
| Passing yards / attempt | 13.33 | 14.74 |
| RB yards / rush (600 rushes) | 5.88 | 5.88 |
| Mean first throw | 1.83 s | 2.15 s |
| Full-game CPU completion rate | 95.1% | 80.3% |
| CPU offensive points / completed drive | 4.65 | 5.24 |
| CPU scrimmage calls / distinct IDs | 119 / 70 | 116 / 69 |
| New-call snaps in completed games | 7 across 5 IDs | 10 across 6 IDs |
| Mean measured Update | 0.266 ms | 0.236 ms |
| P95 measured Update | 0.573 ms | 0.464 ms |
| Allocation / measured Update | 44,958 bytes | 45,632 bytes (+1.5%) |
| Measured fixed-fixture updates | 222,751 | 243,525 |

Final scores, user–CPU, for seeds 401–406: **21–17, 28–17, 14–21, 14–10, 21–28,
21–17**. The overtime fix changes seed 403 relative to the earlier Step 3 report;
the fixed-call outcomes are unchanged. CPU scoring remains above the pre-recorded
5.0-point ceiling at **5.24**. Review that result against human defense before
declaring balance acceptance complete. The remaining mechanical balance metrics
meet the recorded bands.

The current candidate initially appeared slower than the historical baseline, so
the frozen baseline was rerun under current machine conditions. Both runs were
slower than the earlier measurements; the refreshed comparison shows no measured
Update-time regression, and allocation remains below the 1.25× investigation /
1.5× hard regression gates. This is one sequential development measurement pair,
not a statistical performance improvement or a rendered-frame benchmark. Later
throws require 9.3% more simulated updates. Raw output and asserted comparison are
in `baseline/balance.json`, `balance/balance.json` and `comparison.json` under the
final-review artifact directory.

Native interactive control was unavailable. Scripted controllers and renderer
captures provide mechanical and layout evidence; human offense/defense feel,
difficulty and pacing still require hands-on playtesting. CPU scoring in the
small scripted sample remains a balance review item. Mechanical verification
does not close that acceptance gate.
