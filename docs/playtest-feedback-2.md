# Playtest feedback 2: shorter games and special teams

Normal season play now uses two 120-second halves, displayed as H1/H2. Timeouts reset at halftime. Paired-possession overtime remains after a tied second half. The earlier four-quarter fixtures remain explicit development/rules regression configurations.

## Changes

- CPU QB establishes pocket depth and slides toward its current read even without immediate pressure. Pocket movement is capped at 3.2 yards/second, while pressured escapes and committed scrambles retain their running speed. It still plants to throw and obeys the line of scrimmage and play-action locks.
- Pregame requires no name. A completed season opens the original save-score/name dialog; blank names do not save or discard the result. Enter retries saving without duplicating the accepted games or record.
- CPU fourth-down strategy normally goes for it outside field-goal situations. Punting requires being inside its own 20 with more than five yards to gain, unless late-game urgency requires retaining possession.
- Punts have a long-snap formation, visible snap and flight, downfield coverage, blockers and a returner. B selects the formation on fourth down; Space snaps. Another offensive call cancels it before the snap.
- Kickoffs start each half and follow regulation scores. The scoring/period resolver queues the receiving team; halftime and final-play rules take priority. Space starts a human kick or signals readiness; CPU kickers start automatically. Kickoff flight does not consume the half clock, but returns do.
- On both kick types, the user controls the returner when receiving and one coverage linebacker when kicking. Movement uses screen directions and Shift sprints. Contact, sidelines, touchbacks, return touchdowns and return safeties resolve once through the match rules. Return play has no time-based forced tackle.
- Drive-result cards retain the outcome, return yards and incoming possession. Return touchdowns credit the receiving team; kicking events do not contaminate QB/RB passing or rushing statistics.

## Verification

- 556 tests pass, including name/save timing, normal startup, halftime reception/timeouts, second-half winner/overtime, punt decisions, playable punt flight/return, return touchdown/safety attribution, touchbacks, pause and restart.
- A 120-case special-teams matrix (ten seeds, two kick types, both user roles, three frame rates) terminates at 30/60/120 Hz with finite ball positions and legal receiving spots.
- All 2,880 offensive catalog/frame-rate cases pass with the additional QB movement.
- All 24 seeded short matches and three standard matches finish with the new two-half format and special teams enabled. Standard results: 42–14 / 28–21 / 28–31; simulated durations 7.78 / 7.16 / 8.10 minutes, including dead-ball sequences and automated acknowledgments. These are not human pacing measurements.
- Four-size captures include punt setup/flight, kickoff setup/flight/return, pregame without name entry, and the original name dialog after the season. Visual review checked 1000×700 and 1440×900 samples.

Commands remain in README. Outputs are in `artifacts/halves-special-teams/`. The release-matches/default commands now exercise the updated two-half rules; older Phase 6 output is historical evidence for the earlier four-quarter candidate.

## First-version limits

Kick distance/direction are automatic and seeded. Returns use simple coverage/blocking, with no onside kicks, fair-catch input, fumbles or blocked kicks. Punt/kickoff replays and separate cumulative return statistics are not yet implemented; scrimmage replay and detailed per-kick terminal results remain available. Return and overall match balance need hands-on playtesting.
