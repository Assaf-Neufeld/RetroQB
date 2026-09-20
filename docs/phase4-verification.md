# Phase 4 verification

Phase 3 committed as `a7e6a06`. Phase 4 implements M8/M9 in the development timed-match path through GameSession.

- Both possessions use the same simulation and TimedMatch resolver. Possession and period summaries wait for Space; ordinary results continue after 1.25 seconds.
- User kicks retain the existing meter. CPU kicks use seeded distance odds. Punts use 40 net yards; kneels consume one yard and a down.
- CPU strategy covers fourth downs, hurry-up, lead protection, defensive timeouts and conservative kneel guarantees.
- Paired overtime uses own 75, reverses the opener after tied pairs, and resets timeouts per pair.
- 515 tests pass (29 new Phase 4 cases). The tests cover MATCH-01 through MATCH-04, KICK-01 through KICK-03, LATE-01 through LATE-03, overtime and restarts.
- Seed 101 short game: 6,429 simulation ticks, all four quarters, both possessions, Ballers 14–0.
- Seed 101 default three-minute quarters: 71,162 ticks, all four quarters, Ballers 161–35. Automated offense repeatedly chooses the same run and accelerates CPU cadence; this is termination evidence, not balance or human pacing approval.

Playable checkpoint C is ready for hands-on review. No human playtest is claimed. Default season activation remains in Phase 6; season/save integration belongs to Phase 5.

Run from the repository root:

```powershell
dotnet run --project RetroQB.csproj -- --scenario timed-short
dotnet run --project RetroQB.csproj -- --scenario timed-game
dotnet run --project RetroQB.csproj -- --scenario late-tying-kick
dotnet run --project RetroQB.csproj -- --scenario late-protect-lead
dotnet run --project RetroQB.csproj -- --scenario overtime-pairs
```

Controls: Space snap/ready/continue; offense number keys select passes, Q–P select runs, X flips; live number keys throw; WASD/arrows move, Shift sprints. K selects field goal, B selects a legal punt, V selects kneel. Defense number keys select calls. C timeout, Escape pause, F replay, Z restart; close the window to leave. Escape replaces P for pause in the full match because P is an existing run-call key.

Evidence: `artifacts/phase4/short`, `default`, `captures`, `overtime`, and `tying`. Reports are isolated from normal player records.
