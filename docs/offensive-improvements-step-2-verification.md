# Offensive improvements — Step 2 verification

October 10, 2026. Status: **implemented and mechanically verified, with five unchanged baseline test failures**. Changes are uncommitted
on top of `f82b187bb28083612cf576a4ad5bad029a773d42`, including the completed Step 1.
Audibles and motion remain deferred; Step 3 strategy, browsing, favorites and balance
are not implemented in this step.

## Delivered behavior

- Every run has a resolved approach/entry track, used by the CPU and diagram. Power
  pairs down blocks with kick-out and wrap jobs; Counter adds initial misdirection;
  Stretch reaches/seals laterally and retains backside protection. Personnel changes
  select a fullback/second-back kick-out where available. Five- and six-lineman calls
  retain their IDs. Dive/Lead Iso, Sweep and Draw retain separate jobs and timing.
- Power/Counter/Stretch combine an anchor with one designated climbing lineman.
  Stable formation line slots survive actor enumeration and mirroring. A frame-wide
  position snapshot checks contact, leverage and penetration before releasing help.
  The released lineman cannot immediately reclaim the anchor's defender. Live local
  penetration can still interrupt the climb. Contact uses the existing physical engine.
- Added RB screens in Gun Doubles and Split Backs, WR screens in Gun Trips and Gun
  Doubles, PA bootlegs in Pistol Twins/Singleback Ace/I Pro, and a Gun Doubles rollout.
  The catalog contains **108 calls: 58 passes and 50 runs**, including two wildcards.
  All original IDs and the 10-pass/10-run quick sheet remain available.
- RB screens protect, delay the target, release two linemen plus a back/TE, and escort
  the actual catch. WR screens block the perimeter immediately. CPU progression and
  pressure outlets permit the authored behind-line screen target after its gate.
  Targeting numbers still follow roster order.
- Bootlegs fake left and move right after the fake; rollout moves immediately.
  Mirroring includes the fake, QB path, edge protector, routes and reads. Fresh human
  movement ends assistance immediately for the snap. CPU pressure escapes, throws,
  possession changes, scrambles and aborted fakes exit the appropriate sequence.
  The normal QB/receiver/line motors retain one movement integration per actor.
- Diagrams share resolved run tracks, screen escort landmarks, combination climbs
  and QB paths. Screen guidance shows escort count and release timing. Defenses
  continue observing positions, movement, contact and ball state through the existing
  sighting contract; offensive design data is not supplied to defensive decisions.

## Verification

| Check | Result |
| --- | --- |
| Build | Passed; zero warnings/errors |
| Step 1 baseline suite | 886 passed, 5 failed, 891 total |
| Final Step 2 suite | **914 passed, the same 5 failed, 919 total** |
| New designed-offense tests | **22 passed**; plus expanded scenario/catalog coverage |
| Production catalog/control sweep | **6,876 cases, zero mechanical failures**; 5,832 CPU offense + 1,044 human offense |
| Standard matches | **3 completed**, seeds 301–303, scores 21–7, 35–31, 14–7 |
| Production previews | **56 captures**: seven concepts, both flips, four window sizes |
| Diff whitespace check | Passed |

Failed test identities match the Step 1 evidence exactly: the existing out-of-range
field-goal state test, possession-change replay test, and the three punt-strategy cases
at yard/distance 20/10, 10/2 and 40/8. No new failure is present. The final TRX is
`tests/step-2-verified.trx` under the evidence directory.

Visual review checked the RB screen and mirrored Counter/bootleg at 1000×700 and the
WR screen at 1440×900. The route/block/QB diagrams, targeting labels and revised screen
guidance fit. Captures also cover 1280×720 and 1920×1080. The three standard matches
use two two-minute halves and normal special teams; scoring and terminal ownership
checks pass. They are scripted gameplay checks, not competitive balance measurements.

The focused tests exercise physical screen throws/catches behind the line, actual
releasing linemen, carrier input takeover, combination safety under penetration and
shuffled order, Counter press/cut behavior, mirrored moving pockets, blocked fakes,
timeouts, pressure/throw exits, other catches, dead balls and fresh-snap resets.
They run the production movement controllers at 30/60/120 Hz. A fixture regression
checks selectable production defensive IDs for both human control roles.

The release matrix uses seed 101, own 1/midfield/opponent 5, both flips, 30/60/120 Hz,
and all ten production defensive calls including the current match IDs. CPU offense
covers all 108 calls against regular/playoff/Super Bowl opponents (5,832 cases).
Scripted human offense covers all 50 runs and eight new passes against the regular
opponent (1,044 cases). It advances a human carrier and throws to the authored primary
when its gate permits. Each play must terminate through production physics within
30 seconds, with finite/in-bounds actors, a legal pass release, one terminal result
and correct offense ownership. No artificial whistle or completion is introduced.

An initial fixture incorrectly requested a manual linebacker for human offense.
The subsequent setup check exposed an older defensive ID absent from production.
Both were corrected in the harness, with role-aware ownership, actual production
call selection and explicit rejection checks; failed setup records remain in the
ignored integration evidence. The final matrix reruns every case with these checks.

## Observations for Step 3

The production sweep records 342 combination releases. All eight new calls produce
CPU throws; all four screens produce catches by their designated screen target.
The following are 54 CPU cases per call, with the scripted human linebacker chasing
the ball. Field position, opponent stage, orientation and frame rate vary, so these
are execution observations rather than competitive performance targets.

| Call | Attempts | Completions | Screen-target catches | Sacks |
| --- | ---: | ---: | ---: | ---: |
| `pass.ace.bootleg` | 54 | 53 | - | 0 |
| `pass.gun-doubles.rb-screen` | 53 | 53 | 47 | 1 |
| `pass.gun-doubles.rollout` | 48 | 45 | - | 3 |
| `pass.gun-doubles.wr-screen` | 54 | 53 | 52 | 0 |
| `pass.gun-trips.wr-screen` | 54 | 51 | 51 | 0 |
| `pass.i-pro.bootleg` | 54 | 45 | - | 0 |
| `pass.pistol-twins.bootleg` | 48 | 30 | - | 4 |
| `pass.split-backs.rb-screen` | 54 | 54 | 50 | 0 |

Screen completions are high in these scripted fixtures; Pistol Twins bootleg has more
incompletions and pressure exits than the under-center bootlegs. Step 3 should compare
these against fixed balance targets and human playtesting before tuning. The guide
and README now describe the 108-call catalog. Raw release data is
`release-matrix/designed.json`; observations are `new-call-observations.json`.
The final build and matrix include the corrected production defensive IDs and
role-aware setup. Earlier failed fixture records remain for diagnosis.

## Reproduction

```powershell
dotnet build tests/RetroQB.Tests/RetroQB.Tests.csproj --no-restore -p:UseSharedCompilation=false -m:1
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-build --no-restore
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario offense-designed-matrix --headless --output artifacts/offensive-improvements/step-2/release-matrix
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario offense-designed-controls --headless --output artifacts/offensive-improvements/step-2/control-recheck
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario release-default --headless --output artifacts/offensive-improvements/step-2/final-matches
dotnet .\bin\Debug\net10.0\win-x64\RetroQB.dll --scenario offense-designed-preview --capture --output artifacts/offensive-improvements/step-2/final-layouts
```

Evidence lives under ignored `artifacts/offensive-improvements/step-2/`. VSTest runs
use permitted local networking for its required socket. The command-line compiler
uses `UseSharedCompilation=false`. Generated captures use the production match renderer.

Balance, richer CPU calling, full-catalog access and favorites remain Step 3. These
scripted mechanical checks do not certify competitive balance or human play experience.
