# Phase 2 — First playable defensive drive

Status: **Verified and accepted**, September 19, 2026. M3–M5 automated gates pass. The user reviewed the test runs, reported "test runs look good," and approved the Phase 2 commit. This records user acceptance of checkpoint A; the automated evidence and known gameplay limitations remain documented below. Phase 1 was committed as `6363ec5`.

## Play it

From the repository root, in Debug:

```powershell
dotnet run --project RetroQB.csproj -- --scenario defense-drive --seed 101
```

Move with WASD/arrows. The gold ring identifies MLB in base or OLB1 in nickel. Release movement to stop; possession changes within the play never transfer control to the CPU carrier. CPU calls are selected once per snap, and the next play retains the earned spot/down/distance. Space starts the snap sooner; P pauses; Escape closes. The scenario ends on a score, interception, safety, or turnover on downs. Normal game startup remains the existing offensive season until the integration phases.

Additional scenarios:

- `defense-control-base`: fixed HB Dive, scripted exchange/carrier, human MLB.
- `defense-control-nickel`: fixed Mesh with a scripted one-second throw attempt, human OLB1.
- `defense-quick`, `defense-deep`, `defense-play-action`: fixed offensive concepts with CPU quarterback decisions rather than scripted throws.

Use `--headless --output artifacts/phase2/example` for a deterministic scripted linebacker pursuing the carrier, or `--capture` for hidden-window screenshots. Visible runs use the keyboard. Reports contain calls, positions, intents, exchange states, block contact, time without progress, steering changes, terminal events, and the complete drive history. These scenarios never instantiate the player's record store. The live-play budget fails a development run with a trace after 30 simulated seconds; it does not manufacture an on-field result.

## Verification

- **464 tests passed, zero failures/skips**: all 443 previous cases plus 21 new Phase 2 cases. [Local TRX](../artifacts/phase2/phase2.trx).
- Debug and Release application builds: zero warnings/errors.
- Existing offensive input, passing, receiver takeover, exchange, and seeded baseline tests remain passing through the shared intent/throw changes.
- Inspected the production-field capture with the linebacker ring, correct end-zone identities, readable controls, and no CPU route/call/target information. [Nickel screenshot](../artifacts/phase2/nickel/00121-0-InAir-True.png).
- Replaced desktop screenshots with render-texture captures after detecting a hidden-window DPI scaling problem.

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-restore --nologo --logger "trx;LogFileName=phase2.trx" --results-directory artifacts/phase2
dotnet build RetroQB.csproj --no-restore --nologo -c Release
```

| Gate | Evidence in `DefensiveDriveTests` |
| --- | --- |
| CONTROL-01 | Base MLB/nickel OLB1 receive movement; remaining defenders keep AI movement. |
| CONTROL-02/04 | Pass flight/catch preserve linebacker ownership; release stops movement; shared contact credits that defender. Offensive key presses do not change CPU throws or results. |
| CONTROL-03 | Controlled defender moves by exactly speed × timestep in isolation. Real blocking reduces its commanded velocity before its single integration; contact displacement is retained. |
| CPU-01/02 | Reaction interval, open primary, progression to an open outlet, and exclusion of blocking/ineligible receivers. Open-field pass fixtures physically throw, catch, carry, and score. |
| CPU-03/04 | Fake lockout, shared LOS/eligibility checks, covered-read scramble, physical legal throwaway with subsequent ball-flight incompletion. No timer manufactures a whistle. |
| CPU-05/06 | Authored exchange before carrying, post-catch movement without offensive input, congested-lane avoidance, sideline steering, and actual touchdowns. |
| DRIVE-01 | Earned first down survives setup of the next play; seeded full drives preserve series and end through match rules. |
| Budget/frame rates | Seeds 101/42/77/2026 exercise bounded, finite drives; seed 101 runs at 30/60/120 Hz with a 30-second budget per live play. |

Seed 101 headless observations (60 Hz, including one-second dead-ball pauses):

| Scenario | Plays / throws | Observed results |
| --- | --- | --- |
| `defense-drive` | 4 / 2 | Three tackles, then fourth-down sack; both pass completions and a run; 713 ticks. |
| `defense-quick` | 4 / 2 | Three tackles, interception; one first down; 842 ticks. |
| `defense-deep` | 4 / 0 | Four sacks; pressure prevents a throw; 936 ticks. Open-field deep fixture does throw and score. |
| `defense-play-action` | 4 / 4 | Four physical completions/tackles; drive ends on downs; 793 ticks. |
| `defense-control-base` | 4 / 0 | Four tackles, recorded block contact on the manual linebacker; 581 ticks. |
| `defense-control-nickel` | 4 / 4 | Incomplete, tackle, pass defended, interception; one first down; 684 ticks. |

Reports are in `artifacts/phase2/<scenario>-101/report.json`; control reports are in `base` and `nickel`. Artifacts are ignored local evidence; tests and reproduction commands are committed source material.

## Implementation and limits

`ControlContext` separates human ownership from possession. `OffensiveIntent` is consumed by the shared execution controller. Manual defender velocity is set before blocking, then integrated after blocker contact, with no second AI movement update. Existing overlap resolution and tackle-break handling remain authoritative.

`QuarterbackAI` receives only visible positions, its own read order/readiness, and throw eligibility. CPU throws use the existing lead, pressure, accuracy, trajectory, interception, and catch code. Throwaways require the QB outside the pocket and behind the line and still travel as physical balls. `BallCarrierAI` scores lanes against defenders/blockers, respects authored run entry, and commits around congested teammate walls rather than oscillating into them.

`DefensiveDrive` orchestrates the production setup/execution/ball/contact controllers and the Phase 1 `MatchState` resolver. Its `PlayManager` supplies resolved plays and formation coordinates; legacy terminal scoring is never used. `DefenseScenarioLauncher` provides a development view, not a second physics engine or a permanent game mode. M8 must fold this orchestration into the normal match flow.

The initial CPU set is Mesh, Slant Flat, Four Verts, Gun Doubles PA Cross, and HB Dive. Full catalog coverage is not claimed. Basic Cover 3 uses the existing personnel/blitz setup; the ten selectable defensive calls belong to M6. Game clocks, timeouts, replay controls, special teams, and season presentation are not activated in this one-drive checkpoint.

Known gameplay issues to review before menu expansion: deep reads can lose repeatedly to pressure (seed 101 above); run congestion recovery favors going around the blocking wall and can abandon an interior lane. Automated pursuit proves contact and termination, not satisfying human control feel. Further playtesting should assess moving away from an assignment and returning to contact in both packages, block resistance, stationary defense, and whether manual positioning materially changes the outcome.
