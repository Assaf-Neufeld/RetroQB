# First playable-defense feedback fixes

Based on user playtesting of `dac4097`.

1. **CPU passing:** normal regular-season pass-call probability increases from 50% to 65%, with more passing on third-and-long and more running in short yardage. Receivers must develop positive depth instead of taking instant behind-the-line checkdowns. Early pressure causes a pocket escape that preserves the passing window; the QB waits longer before running forward. Deep reads can clear underneath coverage, while a defender at the catch point still closes the read. Throws, catches, interceptions and tackles use the existing physics.
2. **Field orientation:** user end zone stays at the bottom; user offense moves up and CPU offense down. Rendering reflects offense-relative play coordinates, including ball, coverage assignments, chain markers, kicks and recorded replays. Stadium/end-zone layout stays fixed. Normal game input reverses defensive world Y so Up always moves up-screen; scripted physics fixtures retain world-relative input.
3. **Scoreboard:** restored bordered stat-board styling, colored team stripes/scores, period/game/play clocks, possession, down/distance, timeouts, offensive stats, user defense and recent plays. Team labels use high-contrast text. Full statistics remain available with Tab.
4. **Drive transition:** centered result card shows the offense, outcome (including turnover on downs), plays, net yards, points and next possession/period. The existing Space acknowledgment now has a prominent result to read. Replay and statistics remain available before continuing.

## Verification

- 539 regression tests pass, including new tests for CPU call mix, route development, pocket movement, deep coverage, defensive input direction and a four-play CPU stop that waits for acknowledgment without consuming the clock.
- All 2,880 catalog/frame-rate cases pass after AI changes.
- All 24 short matches and three default-length automated matches complete without mechanical failures.
- Default-length seeded comparison: CPU passing yards changed from 106 to 891 (seed 301), 60 to 446 (302), and 286 to 588 (303). This demonstrates changed behavior, not final balance approval; early-stage scoring can still be very high.
- Four-size layout capture now includes a drive-result fixture. During verification, the new direction exposed the stadium's upward-only screen bounds; stadium/turf drawing was separated from play-coordinate orientation and the captures reran successfully.
- Visual review includes defensive orientation at 1440×900, the drive result and CPU kicking at 1000×700. Outputs: `artifacts/playtest-fixes/`; README screenshots refreshed from the current renderer.

Please retest CPU passing, defensive movement, scoreboard readability and the result shown after a defensive stop. Balance still needs hands-on judgment.
