# Phase 3 — Defensive playcalling

Status: **Implemented; M6 verified, M7 ready for hands-on checkpoint B**, September 19, 2026. Phase 2 was committed as `1393da0`. Phase 3 remains uncommitted.

## Play it

```powershell
dotnet run --project RetroQB.csproj -- --scenario defense-calls --seed 101
```

The defensive drive now has the ten-call panel, assignment preview, linebacker responsibility, score/possession display, quarter/game/play clocks, remaining timeouts, and a CPU snap countdown. All existing `defense-*` scenarios use this presentation and cadence.

- **1–9 / 0:** select the ten authored calls before the snap.
- **Space:** shorten the CPU cadence to at most 0.35 seconds; skip a replay during playback.
- **WASD/arrows:** move the fixed linebacker during the play.
- **C:** request one available dead-ball timeout.
- **P:** pause/resume. Losing focus also suspends simulation and discards catch-up time on return.
- **F:** replay the last completed play or leave replay. Playback freezes simulation, cadence, result timers, and match clocks.
- **Escape:** close the development scene.

The CPU chooses its offensive call once and a separate seeded 5–8 second cadence. Browsing retains the same offensive actors, call, field position, clocks, and deadline. Results automatically continue after 1.25 seconds unless possession changes or the match/drive ends. Defensive call counts increment only on a successful snap.

Normal startup still opens the existing offensive season. Full alternating-possession integration and special teams are Phase 4; this development scene ends when the defensive drive ends or period rules transfer possession to the user.

## Implemented boundaries

`DefensivePlaybook` supplies ten stable IDs: Cover 1, Cover 2 Zone, Cover 3, Cover 4, Cover 2 Man, Robber, Cover 3 Match, Quarters Match, Cover 1 Edge Pressure, and Cover 0 Pressure. Standard calls rush four, edge pressure rushes five, and Cover 0 pressure rushes six. Man packages explicitly allocate all five visible receiving threats before help assignments; Cover 2 Man keeps two deep helpers, edge pressure keeps one, and zero pressure keeps none.

`VisibleOffense` contains only QB position and receiver index/slot/position. Route, blocking assignment, changing eligibility, offensive call ID, and intended target cannot enter the preview resolver. `ResolvedDefensivePlay` freezes alignments, zone jitter, rush lanes, man targets, zone roles, star boosts, and preview targets. Repeated setup creates fresh actors from this immutable result without RNG draws. Mirroring reflects geometry and zone sides while preserving actor ownership, and double mirroring restores the original result exactly.

The automatic initial Cover 3 selection and manual defensive selections share the resolver/cache. `PlaySetupController` consumes the selected final call and no longer chooses random blitzers. The legacy offensive-season adapter chooses its existing automatic blitz package upstream in `GameSession`, retaining its baseline behavior until normal-match migration; it does not expose the unfinished defensive menu.

`DefensiveDrive` now uses the Phase 1 `TimedMatch` for snap legality, clock advancement, penalties, scoring, and period boundaries. `DefensivePlaySession` owns input/cadence/result presentation, without subtracting game time independently. A delay rebuilds the formation at the penalized spot with the same offensive call. Replay uses the existing recorder/player, retains the completed play's defensive call and controlled-defender index, and never mutates live actors or match state.

`DefensivePlayRenderer` renders the selected assignment data: red rush lines, blue zone cues, white man assignments, and the gold control ring. It has no offensive-call name, offensive route lines, receiver priorities, intended targets, or offensive coaching panel. Replay retains its original call and linebacker marker even after setup/browsing changes the next play.

## Verification

- **486 tests passed, zero failed/skipped**, including all earlier regression cases and 22 new cases. [Local TRX](../artifacts/phase3/phase3.trx).
- Debug test build and Release application build succeeded with zero warnings/errors.
- The full catalog is checked against every offensive catalog formation for all ten defensive calls, including both base and nickel personnel. These combinations execute inside the ten catalog test cases.
- Visually inspected Cover 3 preview and replay captures at 1440×900. Calls, assignments, control ring, clocks, and timeout allowances are readable; offensive secrets are absent.
- The final seed-101 capture scenario cycles calls, consumes a timeout, pauses/resumes, opens replay, and completes a real drive. Its report is byte-identical to the headless script: 3,056 ticks, four plays, two throws, both linebacker packages, and tackle/tackle/sack/touchdown results. The game clock finishes at about 2:25.25 in Q1.
- Local visual evidence: [Cover 3 preview](../artifacts/phase3/final-captures/00041-0-def.cover3-UntilSnap-None-HeldByQB-False.png), [paused timeout](../artifacts/phase3/final-captures/00141-0-def.cover3-match-Timeout-Pause-HeldByQB-False.png), [replay](../artifacts/phase3/final-captures/00522-1-def.cover2-zone-ResultPresentation-Replay-HeldByReceiver-False.png).

| Gate | Evidence |
| --- | --- |
| CALL-01/02 | `DefensivePlaybookTests`: eleven distinct active slots, exactly one primary job each, correct rush counts, five unique man matchups where applicable, required deep help, and valid targets for every catalog formation. |
| CALL-03 | Repeated production setup matches resolved positions, jitter, man targets, and rushers without further random draws. |
| CALL-04 | Double mirror restores the original result exactly; the controlled MLB/OLB1 policy survives reflection. |
| Visible-only resolution | Mutating hidden receiver route/blocking/eligibility fields leaves every call's preview unchanged for the same visible formation and seed. |
| UI-01/02 | Defense-only renderer plus inspected captures; preview endpoints and live actor assignments come from the same resolved data. |
| TIME-01 | Repeated browsing preserves CPU actors/call and snaps by the original deadline. Space only shortens cadence. |
| TIME-02 | Half-distance penalties preserve down/call, rebuild at the new spot, and stop the game clock; repeated delays cannot drain remaining regulation time. |
| TIME-03 | Duplicate timeout requests consume one allowance per dead-ball event. Pause, replay, and focus suspension preserve clocks/cadence; replay preserves live actors and series. |
| TIME-04 | Nonterminal results continue without Enter and resume the running clock. Expiry wins over a ready input on the same tick. Replay call/marker metadata survives subsequent defensive selection. |

The older first-down integration test depended on the former random defensive alignment. It now uses a controlled QB scramble/contact fixture, retaining assertions that the first down is earned and the next play starts at that spot. Full seeded-drive termination tests still exercise the complete defense.

Reproduce:

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-restore --nologo --logger "trx;LogFileName=phase3.trx" --results-directory artifacts/phase3
dotnet build RetroQB.csproj --no-restore --nologo -c Release
dotnet run --project RetroQB.csproj --no-build -- --scenario defense-calls --headless --seed 101 --output artifacts/phase3/final-headless
dotnet run --project RetroQB.csproj --no-build -- --scenario defense-calls --capture --seed 101 --output artifacts/phase3/final-captures
```

The automated scenario uses scripted linebacker pursuit. Visible runs use keyboard control. Reports and PNGs are ignored local artifacts, not leaderboard saves.

## Hands-on checkpoint B

Still to confirm: select Cover 3, defend a snap, change to pressure on the next snap, use C, and let the CPU snap while browsing. Check readability, assignment usefulness, and control feel. Correctness and captures do not establish tactical balance: the Phase 2 deep-read pressure and interior-run steering observations remain tuning considerations. This phase does not expand CPU offensive catalog coverage or implement late-game strategy, normal-season two-sided activation, or special teams.
