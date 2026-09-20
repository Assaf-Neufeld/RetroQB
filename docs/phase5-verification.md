# Phase 5 verification

Phase 4 is committed as `be036f0`. Phase 5 implements M10 in the development timed-season path. The default menu remains on the legacy ruleset until Phase 6 release verification.

## Implemented

- Team defensive totals and per-defender attribution: tackles, sacks made, interceptions made, pass breakups, safeties, yards/points allowed, third/fourth-down stops. Controlled-linebacker tackles, sacks, interceptions, and pass breakups are separate from teammate contributions.
- Replay context records teams, possession, stage, period/clock, score, offensive call, defensive assignments, and controlled defender. Playback uses recorded context after possession changes and cannot apply match results or statistics.
- Version 2 saves retain legacy records, separate rankings by ruleset, and store each timed season's three-stage offensive and defensive snapshots. Season IDs prevent duplicate retries. Atomic replacement, backups, corrupt-save recovery, and refresh-before-save preserve both rulesets.
- Timed seasons progress through regular season, playoffs, and Super Bowl. Enter accepts a finished matchup; only accepted games enter the season summary. Restarting the current matchup preserves earlier games. The existing offensive dominance formula is unchanged.
- Pregame profiles/name entry, team-colored end zones, scoring celebrations/crowd reactions, possession feedback, recent-play/drive summaries, statistics overlay, and final timed leaderboard are connected to the new flow.

## Verification

- Debug and Release builds pass with zero warnings/errors. `git diff --check` is clean.
- 530 tests pass, including 15 new Phase 5 cases. Coverage includes defender attribution, replay twice after a possession change, clock suspension, legacy/timed record coexistence, backup recovery, failed-save retries, stale store instances, champion and each-stage elimination, saved-stat reload, and repeated current-match restarts.
- Seed 101 automated short season completes all three stages: regular season 14–0, playoff 7–0, Super Bowl 21–17. It saves one champion season with all three game snapshots (21,759 simulation ticks).
- Layout fixtures cover 1000×700, 1280×720, 1440×900, and 1920×1080: offense, defense, pregame, stats, halftime, overtime, CPU kick, final result, and replay after possession changes.
- Evidence is under `artifacts/phase5/layouts`, `artifacts/phase5/season`, and `artifacts/phase5/season-capture`. Development saves are isolated in each output directory.

Automated runs deliberately use scripted input and accelerated CPU ready signals. They establish flow and termination, not balance or human pacing. Playable checkpoint D is ready for hands-on review; no human playtest is claimed.

## Try it

```powershell
# Short quarters for checking the full season flow
dotnet run --no-restore --project RetroQB.csproj -- --scenario timed-season-short

# Default three-minute quarters
dotnet run --no-restore --project RetroQB.csproj -- --scenario timed-season
```

Type a name at pregame and press Enter. After a win, Enter accepts the result and advances; after elimination or the Super Bowl, Enter saves the season. On save failure, Enter retries. Before accepting a result, Z restarts only that matchup.

Space snaps/continues. Offense: numbers select passes, Q–P select runs, X flips; live 1–5 throws. Defense: numbers select defensive calls; WASD/arrows move the fixed linebacker. Shift sprints. K selects field goal, B selects a legal punt, V selects kneel. C timeout, Escape pause, F replay, Tab statistics, Page Up/Down history. Tab pauses the simulation independently of Escape. Close the window to leave.

For repeatable evidence:

```powershell
dotnet run --no-build --project RetroQB.csproj -- --scenario timed-season-short --headless --output artifacts/phase5/season
dotnet run --no-build --project RetroQB.csproj -- --scenario timed-layout --capture --output artifacts/phase5/layouts
```
