# Offensive improvements — Step 3 verification

Historical implementation checkpoint. The subsequent [final review](offensive-improvements-final-review.md)
fixes integration bugs and resolves all five baseline regression failures described below.
The measurements in this report remain the original Step 3 comparison.

Date: October 10, 2026. CPU strategy, catalog browsing and favorites are implemented.
Automated verification is complete relative to the five established baseline failures;
human feel/pacing review remains pending. Audibles and motion remain deferred.

## Implementation

- CPU offense chooses seeded weighted catalog calls while retaining each team's
  run/pass preference. The shared scorer includes down/distance, field position,
  actual routes and protection, run scheme, screens, roster fit, score, time and
  timeouts. Player recommendations and situational sheets use the same scorer.
- Eight recent snapped calls are tracked separately from cumulative counts.
  Completed passing plays contribute bounded moving averages of visible pressure
  and tight coverage. Offensive strategy accepts no defensive call IDs or hidden
  assignments. Browsing and selecting calls do not add history or usage.
- Throwing windows project both receiver and defender motion over estimated travel
  time. Engaged defenders do not cause immediate pressure escapes. Hot/checkdown
  reads retain route-stage, fake and release gates; screens retain behind-line
  eligibility. Existing bounded scramble and legal throwaway exits remain.
- The L browser exposes all 108 calls with family/formation/category/concept
  filters, paging, a separate field preview, preview mirroring, stable-ID selection,
  read/protection/lane guidance, and a favorites view. The selected call is included
  in the 10-pass/10-run quick sheet. Cached wildcard geometry survives browsing,
  confirmation and delay penalties; ordinary wildcard hotkeys still regenerate.
- Browsing consumes the regular timed play clock. Navigation cannot also snap,
  throw, select a quick call, request a timeout or enter replay/statistics. Esc
  closes the browser. Focus loss suspends time; delay penalties rebase previews,
  and period/possession changes close browsing. CPU defensive timeouts remain
  available. Normal season Enter handling now reaches browser confirmation.
- Favorites persist by permanent ID in LocalAppData, survive restart, ignore
  removed calls and recover from corrupt/version-mismatched settings. Failed
  saves retain the current-session preference. Favorites do not replace the
  situational quick sheet. Both timed and retained legacy sessions are integrated.

## Reproducibility

Repository HEAD: `f82b187bb28083612cf576a4ad5bad029a773d42`; Steps 1–3 are uncommitted
working-tree changes. The Step 2 core was compiled and copied before CPU/browser
changes to `artifacts/offensive-improvements/step-3/baseline-runtime/`.
Its DLL SHA-256 is
`B5356F2552C9C0284AA73972FC1CA6DA863873C5D41C33AEB1FADEC1BA36510C`.
Final candidate DLL SHA-256 is
`A33138924841B3359A96CB3DCEF3EA223FF4680D95C25CA45D270405B0D3C1DE`.

The balance harness runs all 108 calls × two CPU rosters × own 20/opponent 10 ×
seeds 101/202/303 = **1,296 fixtures**, at 60 Hz, rotating ten supported production
defensive calls. Six complete two-minute-half games use seeds 401–406 and rotate
the three production opponents. Fixed calls, defense selection, seed, roster,
field position, ordering and scripted input policy are identical across builds.
Full games use identical hotkey policies; selected plays and subsequent inputs
can diverge as the new call sheets and match outcomes change.

The first baseline harness used match possession to steer the controlled actor
at transition boundaries. Two games exposed that fixture error. The corrected
harness uses the prepared execution control role in both builds. A standalone
friend-assembly runner executes that corrected harness against the untouched
Step 2 DLL; the invalid original run is excluded. The baseline has no Step 3 core
changes. Final cost runs are sequential, without concurrent tests, builds or
rendering. Cost is production scrimmage Update time/allocation; setup, validation,
rendering and full-game cost are excluded.

[Balance review bands](offensive-improvements-step-3-balance-targets.md) were
recorded before changing CPU strategy or throwing windows. After the initial
candidate's high completion/scoring results, the moving-window safety radius was
raised from 2.8 to 3.4 yards and lane clearance from 2.2 to 2.6 yards. No fabricated
whistles, catches or interceptions were introduced.

## Checks

Debug and Release builds: **zero warnings, zero errors**. Final regression suite:
**1,043 tests, 1,038 passed, the same five baseline failures**. Release-focused
browser/clock/input/drive checks: **131 passed**. Coverage includes every catalog
ID, preview independence, sheet capacity, snap-only usage, cached wildcard
selection, filters/pages, focus/pause, delay and period expiry, normal-season Enter,
legacy input, favorites reload/restart/corruption/save failure, seeded/context/roster
scoring, bounded recent history, observation deduplication and moving throwing lanes.

The existing seeded drive check was updated to require a throw when a pass was
called. Under weighted selection, seed 101 at 30 Hz can score on its opening
`run.pistol-twins.stretch`; a throw is not required for a valid all-run drive.
Finite-position, series, normal-whistle and terminal-result assertions remain.

The **6,876-case** catalog/control/flip/30–60–120 Hz production matrix passed with
**zero failures**: 5,832 CPU-offense cases plus 1,044 scripted human-offense cases.
It checks normal terminal resolution, legal throw releases, field bounds, stable
control ownership and designed transitions. All ten defensive selections are
checked for acceptance. Runtime was about 359 seconds.

**32 final production-renderer captures** cover 1000×700, 1280×720, 1440×900 and
1920×1080: catalog, screen, flipped bootleg, favorites, empty filters, filtered
results, selected quick sheet, plus the legacy browser. Representative captures
were visually inspected across all four sizes. Browser instructions, active/preview
identity, field diagrams and timed scoreboard remain visible.

The five baseline failures remain: `FieldGoalTests.OutOfRangeCannotSnap`,
`TimedSeasonTests.REPLAY01_ReplayAfterPossessionChangeKeepsRecordedTeamClockAndLinebacker`,
and the three `HalvesAndSpecialTeamsTests.CpuPuntsOnlyWhenPinnedBackWithLongYardage`
expectations at yard/distance 20/10, 10/2 and 40/8. The suite is not fully green.

## Commands and artifacts

Run from the repository root:

```powershell
dotnet build --no-restore -p:UseSharedCompilation=false -m:1
dotnet build -c Release --no-restore -p:UseSharedCompilation=false -m:1
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=step3-verified.trx' --results-directory artifacts/offensive-improvements/step-3/tests -p:UseSharedCompilation=false -m:1
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario offense-designed-matrix --headless --seed 101 --output artifacts/offensive-improvements/step-3/release-matrix
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario offense-browser-preview --capture --seed 101 --output artifacts/offensive-improvements/step-3/layouts-final
dotnet .\artifacts\offensive-improvements\step-3\baseline-runner\bin\Debug\net10.0\RetroQB.Tests.dll artifacts/offensive-improvements/step-3/baseline-final
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario offense-balance --headless --seed 101 --output artifacts/offensive-improvements/step-3/candidate-final
```

VSTest required its loopback runner outside the filesystem sandbox. Generated
reports, fixture runner, frozen DLL, JSON traces and screenshots are retained under
the ignored `artifacts/offensive-improvements/step-3/` directory. The standard
balance scenario and browser preview are available in Debug builds.

## Balance results

Both builds completed **1,296 fixed fixtures and six full games with zero mechanical
failures**. `comparison.json` verifies identical fixed-fixture keys and game seeds/
opponents before calculating the results.

| Metric | Step 2 baseline | Final Step 3 |
| --- | ---: | ---: |
| Fixed-call completions / attempts | 645 / 690 (93.5%) | 601 / 687 (87.5%) |
| Screens completed / attempted | 48 / 48 (100%) | 43 / 48 (89.6%) |
| Sacks / attempts+sacks | 3 / 693 (0.43%) | 1 / 688 (0.15%) |
| Interceptions / attempts | 3 / 690 (0.43%) | 11 / 687 (1.60%) |
| Passing yards / attempt | 13.33 | 14.74 |
| RB yards / rush (600 rushes) | 5.88 | 5.88 |
| Mean first throw | 1.83 s | 2.15 s |
| Full-game CPU completion rate | 95.1% | 81.4% |
| CPU offensive points / completed drive | 4.65 | 5.52 |
| CPU scrimmage calls / distinct IDs | 119 / 70 | 132 / 76 |
| New-call snaps in completed games | 7 across 5 new IDs | 11 across 6 new IDs |
| Mean measured Update | 0.117 ms | 0.097 ms |
| P95 measured Update | 0.239 ms | 0.178 ms |
| Allocation / measured Update | 44,999 bytes | 45,645 bytes (+1.4%) |
| Measured fixed-fixture updates | 222,751 | 243,525 |

CPU use of new calls: Gun Trips WR screen **4**, Gun Doubles WR screen **2**,
Singleback Ace bootleg **2**, Gun Doubles RB screen **1**, Split Backs RB screen
**1**, and Gun Doubles rollout **1**. These counts come from normally resolved
plays in completed matches. The mechanical matrix also exercises the Pistol and
I Pro bootlegs; the six-game sample does not claim usage of every new call.

Final game scores, user–CPU: seeds 401–406 **21–17, 28–17, 42–49, 14–10, 21–28,
21–17**. Baseline scores: **21–7, 28–14, 14–21, 0–10, 42–38, 7–10**.
Both versions completed regulation/overtime through normal controllers and rules.

Completion, screen completion, sack/interception rates, rushing efficiency and
first-throw timing meet the pre-recorded review bands. The wider safety margins
reduced automatic-looking screen success without forcing incompletions. Passing
yards per attempt increased as the CPU waited for clearer, often deeper windows.
The bounded recent penalty discourages immediate repetition without banning calls.

**CPU scoring remains above the 5.0-point review ceiling at 5.52.** The fixed chasing
linebacker and hotkey policy are a small scripted sample, and new quick sheets also
change the human offense's selected calls and resulting field position. This is a
balance-review finding, not a passed target or proof of human difficulty. Do not
nerf the physical completion/contact system merely to fit six games. Review
scoring, deep-window pursuit, pressure influence and pacing against human defense
before declaring balance acceptance complete.

No timing regression was measured in this sequential pair, and allocations remain
well below the 1.25× investigation/1.5× hard regression gates. These are development
Update measurements, not a statistical performance improvement or total rendered
frame benchmark. The later throws produce about **9.3% more simulated updates**;
lower per-update time does not imply lower total gameplay work.

## Remaining acceptance limits

Native interactive control was unavailable in this session. Controller/input
walkthroughs, complete matches and production-renderer captures were exercised;
they are **not human hands-on playtesting**. Human offense/defense feel, difficulty,
scoring and pacing acceptance remains pending, alongside the five known baseline
test failures. The Step 3 implementation and automated gates are complete; the
human balance/signoff portion of the engineering plan remains open.

A short manual walkthrough: choose a team and finish the opening kickoff; press L,
filter to Screen, preview and flip a call, star it with G, view favorites with F,
confirm with Enter and snap with Space. Check receiver targeting, screen escort and
manual control after the catch. Repeat with a bootleg, then defend a CPU possession.
Check pause/focus loss, an intentional delay penalty, replay/statistics after a
result, and restart/relaunch favorite persistence. Use both small and large windows
and watch whether the CPU earns its scoring chances against deliberate coverage.
