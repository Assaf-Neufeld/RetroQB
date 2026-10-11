# Gameplay UI implementation and verification

Implemented 11 October 2026 from the [screen review](gameplay-ui-review.md).

## Result

- The left **YOUR PLAY** panel owns call selection, the active call/controlled player, coaching, and contextual controls. The right **GAME STATUS** panel owns scores, clocks, possession/field position, match state, history, and compact user-team statistics.
- Play clock, snap cadence, and kickoff cadence share the right clock area, labeled **PLAY CLOCK**, **SNAP IN**, and **KICK IN**. Opponent possession uses the team name (for example, **SCARLET GUARD BALL v**); your possession remains **YOUR BALL ^**. Preparation countdowns disappear during live play, results, and replay; pausing preserves their frozen values. Replay shows recorded score/clock/field context and excludes current statistics/history.
- Offense and defense use the same panel shell, headings, row selection, key labels, font roles, spacing, and palette. Offense banks have PASS/RUN headings; repeated call names include formation details. The default pixel font is preserved. Essential body text is 14px or 16px; supporting text is 12px or 14px. Long rows use ellipses; coaching paragraphs wrap.
- Live play, results, replay, kneels, and special teams use contextual content and controls. Defensive run guidance takes precedence over coverage guidance. Empty guidance headings and repeated preparation instructions were removed.
- Full playbook browsing stays inside the left column, uses the same selected-row treatment, and shows formation/personnel instead of internal call IDs. The right clocks remain visible.
- Statistics, pause, and drive results use shared overlay chrome with opaque surfaces. Team identity uses colored strips; ordinary text and scores remain readable. Statistics headings use the team's name (for example, **BALLERS OFFENSE** and **BALLERS DEFENSE**, or **BALLERS STATS** in the compact summary). Compact statistics consistently describe the user's team; complete statistics remain available with Tab.
- A shared layout reserves matching columns for the field, stadium, sidebars, browser, and kick meter. Columns range from 240px at 1000px screen width to 320px at 1440px and above. The field retains its aspect ratio and orientation. Receiver key labels get dark backgrounds, stay inside the field, and choose alternate positions to reduce label collisions.

Clock rules, CPU cadence, key bindings, football physics, scoring, and replay data formats were not changed.

## Checks

- **1,063 tests passed**, zero failures/skips. Nine new cases cover preparation timer ownership, hiding timers during live play/results/replay, paused clock preservation, CPU/human kickoff timing, and matching panel bounds/readable text sizes at five window sizes.
- Debug and Release application builds succeeded. Release build: zero warnings/errors.
- **185 final captures** generated successfully: 37 states at 1000x700, 1280x720, 1440x900, 1920x1080, and 2518x1349.
- Inspected representative compact/normal/large captures of setup, live offense/defense, long call names, zone assignment, browser, low clocks, pause, statistics, replay, drive results, and human kick meters.
- `git diff --check` passed.

Follow-up wording check: all nine HUD tests passed after adding the football labels and possession assertions (including recorded replay ownership). The Release build also passed with zero warnings/errors, and all 185 captures were regenerated with the final wording.

The first test attempt had a Windows executable copy conflict while the capture process was running. After that process exited, the sandboxed test runner timed out connecting to its test host. The successful final suite ran outside the sandbox with normal local VSTest communication. These failures were environmental; the successful test report is `artifacts/gameplay-ui-test-results/gameplay-ui.trx`.

The capture fixture now explicitly creates offensive scrimmage instead of accidentally labeling kickoff setup as offense. It also captures browser, pause, live play, low clocks, zone defense, personnel/front changes, long calls, and human kick setup/power/accuracy. Main scrimmage and halftime/final fixtures use two-minute halves; isolated legacy overtime/replay scenarios still exercise the older quarter fixtures.

Final capture command:

```powershell
dotnet run -- --scenario timed-layout --capture --output artifacts/gameplay-ui-final
```

Evidence: `artifacts/gameplay-ui-final`, `artifacts/gameplay-ui-final.log`, `artifacts/gameplay-ui-release-build.log`, and the test results above. Artifacts are ignored local files. README gameplay/defense/statistics/punt images were refreshed from the final 1440x900 captures.

These checks verify rendering and regressions. A hands-on playtest remains useful for subjective readability and pacing.
