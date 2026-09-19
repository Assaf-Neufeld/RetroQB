# Phase 1 — Match rules and time

M1 and M2: **Verified**, September 19, 2026. Phase 0 is committed as `b2fe65a`; Phase 1 is the subsequent working-tree change.

## Results

- Complete xUnit suite: **443 passed, 0 failed, 0 skipped**; 72 cases added to the 371-case Phase 0 baseline. Local evidence: [TRX](../artifacts/phase1/phase1.trx).
- Debug test build and Release application build succeeded. Release: zero warnings/errors.
- Existing deterministic offensive pass, run, play-action, field-goal, replay, restart, and persistence-isolation regression tests remain passing.
- No new rendering or keyboard demonstration is claimed: these milestones explicitly verify rules independently of rendering.

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-restore --nologo --logger "trx;LogFileName=phase1.trx" --results-directory artifacts/phase1
dotnet build RetroQB.csproj --no-restore --nologo -c Release
```

## Implemented boundaries

`TeamCatalog` supplies stable IDs and both units for all ten standard teams, Golden Legion, three stage opponents, and Lockdown. Existing unit profiles retain their ratings and colors. Complementary units use provisional balanced ratings: selectable defenses derive strength from offensive skill average; stage-opponent offensive skill defaults are .50/.65/.80. These are starting values, not playtested balance claims.

`TeamMatchState` owns score, offensive statistics, call counts, and adaptive defensive memory. Opponent-only defensive stage scaling shares the exact helper used by the current session; CPU read intervals also scale with stage. Human team attributes remain unchanged across role switches.

`MatchRules` is the pure scoring/series resolver for the timed ruleset. `MatchState` applies its result exactly once and retains the original event, gain, contact spot, and actor. Possession starts are explicit, offense-relative yards. Duplicate events cannot repeat scores, statistics, history, or transitions; conflicting/stale/invalid events are rejected.

`BallController` and `TackleController` expose immutable terminal observations while retaining their current caller interfaces and RNG behavior. Observations distinguish interceptions, pass breakups, incompletions, sideline exits, sacks, tackles, touchdowns, and safeties. `PlayContact.ToEvent` preserves the actual ball/carrier position and defender slot for later integration.

`MatchClock` owns simulation-time countdowns, stop reasons, timeout budgets, and independent pause/replay/focus suspensions. `TimedMatch` orders timeout input, clock advancement, expiry, snap, result application, and period transitions. Defaults are four 180-second quarters and a 20-second play clock. Large pre-snap time steps stop at the first clock boundary; they cannot skip a delay penalty and end the quarter instead.

## Acceptance evidence

| Gates | Evidence |
| --- | --- |
| RULE-01–04 | `MatchRulesTests`: touchdowns, fourth-down changes, interceptions/contact spots/touchbacks, and safeties in both team directions. Only the entitled team scores. |
| RULE-05–06 | Goal-to-go, retained fourth-down gain, duplicate results before/after possession continuation, conflicting events, and stale events after restart. |
| Team identity and routing | `TeamDefinitionTests`, `MatchRulesTests`: catalog completeness, colors/ratings, two possession changes, per-team rushing/passing stats, call counts, memory, and stage scaling. |
| Contact observations | `BallControllerTests`, `PlayContactTests`: actual contact spot rather than old LOS, defender slot, sack/sideline/safety/TD reasons, reset clearing. |
| CLOCK-01–02 | `MatchClockTests`, `PeriodTransitionTests`: half-second subtraction, clamping, one expiry, live final plays resolving before transition, expiry-tick snap rejection. |
| CLOCK-03 | Result presentation freezes; tackles/first downs resume running pre-snap; incomplete, defended, sideline, score, and interception results stop until snap, for either team. |
| CLOCK-04–05 | Q1/Q3 preserve resulting series; halftime overrides pending possession and replenishes timeouts; 21–14 does not end an early quarter; final TD/FG/safety determines winner; tied regulation enters overtime. |
| CLOCK-06 | Timeout budgets, duplicate requests, timeout-before-clock ordering, expired-period rejection, nested pause/replay restoration, invalid delta atomicity. |
| CLOCK-07 | Both overtime attempts precede comparison, tied pairs reverse opener, fixed opponent-25 starting spots, no game clock/punts, active play clock, per-pair timeout reset, restart. |
| Frame independence | Equivalent elapsed time at 30/60/120 Hz yields one expiry and the same remaining time. |

Additional rule decisions: an interception caught in the throwing team's own end zone is already a defensive touchdown (seven points, no return simulation). Interceptions in the intercepting team's end zone are touchbacks. Punts require fourth down and use 40 net yards with own-20 touchbacks; missed field goals transfer possession at the supplied kick spot with an own-20 minimum. A delay penalty moves five yards or half the distance without losing a down and stops the game clock. These special-team results are synthetic rule inputs until M8.

## Integration remaining

Normal `GameSession` continues through its existing `DriveState` adapter. It does not yet activate timed quarters, CPU possessions, or linebacker control. This preserves the usable game while M3–M9 build the shared on-field control and orchestration; it is not a permanent second game mode. M7–M9 must consume terminal observations through `TimedMatch`, route live offensive stats to the correct team, and retire synthetic legacy possession scoring. M10 supplies full defensive summaries, persistence migration, and season integration.

Phase 1 changes are not committed yet. The next planned milestone is M3: fixed-linebacker control and shared movement/contact integration.
