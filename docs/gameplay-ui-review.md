# Gameplay screen review and proposed fixes

Reviewed 11 October 2026. This records the pre-change findings and proposed design. The implementation and final checks are documented in [gameplay UI verification](gameplay-ui-verification.md).

## Recommendation

Keep the three-column screen and the pixel football aesthetic. Give each region a stable purpose:

| Region | Purpose | Contents, in priority order |
| --- | --- | --- |
| Left: YOUR PLAY | What you choose and control | Active call/controlled player; available calls before the snap; assignment or read guidance; contextual controls |
| Center: FIELD | What is happening | Players, ball, routes/assignments before the snap, controlled-player ring, essential live cues |
| Right: GAME STATUS | What is true about the match | Teams/score; period/game clock and preparation timers; possession/down/distance/spot/timeouts; last result/recent plays; compact optional stats |

Possession changes should update content inside those regions without moving information between them. The panels are persistent sidebars, rather than navigation tabs.

## Findings in the current screen

### 1. Timing is split across the screen

The right scoreboard always renders `PLAY 20` beside the game clock. Defensive scrimmage adds `CPU snap in 6.2s` on the left, below the linebacker assignment. These are different values: the play clock is the legal deadline, while the CPU countdown is its planned cadence. Moving one value without labeling both would still be confusing.

The left CPU countdown is gated by absence of replay, not by pre-snap state, so it can remain visible during live play and results. CPU kickoff preparation also has a countdown in the session, but the kickoff sidebar does not show it.

**Fix:** one clock area on the right, in the same place for offense and defense:

- Main row: `H1  01:42`, with an explicit stopped/paused state when relevant.
- Preparation row: `PLAY CLOCK 14`; defense also shows `CPU SNAP 6.2s` as a separate labeled value in that area.
- CPU kickoff setup: `CPU KICK 6.0s` when the CPU will start automatically. Do not imply a scrimmage play-clock deadline for kickoff setup.
- Live play/results: replace preparation countdowns with the phase label; do not show a stale `0.0s` countdown.
- Replay: use recorded score/clock/context and label them clearly. Any extra cadence value must also be recorded, otherwise omit it.
- Preserve the existing six-second defensive preparation and Space-to-ready behavior. This is a presentation change.

Sources: `FullMatchRenderer.cs:125`, `TimedScoreboardRenderer.cs:58`, `FullMatchSession.cs:53`, `FullMatchSession.cs:177`.

### 2. The panels have only a partial distinction

The left mostly contains actions and the right mostly contains match facts, which is a useful foundation. However, the left includes timing, while the right includes generic action prompts and repeated controls. Both kickoff sections repeat movement/readiness instructions. The right also alternates YOUR/CPU OFFENSE stats while always showing YOUR DEFENSE, so the ownership of its two stat sections is asymmetric.

**Fix:** put call selection, selected-play coaching, player responsibility, special-teams instructions, and action controls on the left. Put all timers, match status, results, and history on the right. Keep a single contextual controls block on the left. History may retain a small local paging hint. Prefer recent results above cumulative stats; full statistics already have a dedicated Tab screen. If compact stats stay visible, keep the same team ownership across possession changes, such as YOUR OFFENSE and YOUR DEFENSE, and label it explicitly.

On defense, clarify the ball spot with the possessing team's name or abbreviation rather than an unexplained `OWN`/`OPP`. On kickoffs, replace scrimmage spot/down information with the kick/return phase and relevant kick position.

### 3. The active gameplay renderer lost panel structure

The normal timed renderer draws left-side text directly over the background. The right uses a full framed surface and multiple boxed sections. Offense has two plain ten-row lists without PASS/RUN headings, row selection backgrounds, or formation distinctions. A selected name appears only farther down the panel. Calls such as Mesh Wheel, Counter Right, and Draw can appear more than once with no distinguishing information in the list.

**Fix:** use the same outer panel surface/header/dividers on both sides. Add PASS and RUN headers, consistent key badges, and a clear selected-row fill/marker. Distinguish repeated names with a compact formation label; show the full formation, concept, and flip state in the active-call card. Keep all existing quick-call shortcuts. Defense should use the same row component and show unavailable-call explanations in a readable detail area.

The retained legacy `SidePanelRenderer` has more of this structure, but normal timed play routes through `FullMatchRenderer`. Updating the legacy panel alone will not fix the current game. Reuse its successful hierarchy, not its obsolete score-to-advance wording.

Sources: `GameSession.cs:1049`, `FullMatchRenderer.cs:92`, `FullMatchRenderer.cs:143`, `SidePanelRenderer.cs`.

### 4. Text hierarchy and sizing are inconsistent

The screens already share Raylib's default pixel font; the main problem is inconsistent sizes and fitting. The left uses height/900 scaling with an 11px floor. The right uses a different height/860 calculation and can shrink fitted text to 9px. The field-goal HUD and statistics use separate fixed coordinates/sizes. At 1280x720, offensive call rows and coaching are about 11px; the right footer is about 10px. At large sizes, text stops growing while the stadium grows.

**Fix:** centralize font roles and text layout. Starting targets in screen pixels:

| Role | Target |
| --- | --- |
| Score and primary clock | 26–28px |
| Panel heading | 22px |
| Call rows and body text | 16px; 14px compact minimum |
| Section labels, hints, secondary detail | 14px; 12px compact minimum |

Use consistent line height and the same fitting policy. Wrap coaching text; ellipsize long row titles and show the full title in the selected-call card. Measure numeric columns instead of aligning a proportional pixel font with spaces. Do not reduce essential text below its minimum to preserve optional content. Pixel-font rasterization should be checked at the chosen sizes; these targets need visual validation.

### 5. Color and borders compete for attention

Gold currently signals headings, clock, selected calls, player control, status, and many hints. Cyan borders surround green down/distance text; team colors also become full row borders and score text. The left has no equivalent framing. Pregame uses the richer shared overlay shell, while drive summaries and statistics draw their own surfaces. Statistics allow underlying text to remain visible through the panel.

**Fix:** use `Palette` as the single source of UI colors, with semantic roles:

- Dark navy panel and slightly lighter raised cards; one shared fill/opacity policy.
- White primary text, muted gray secondary text.
- Gold for selection, the controlled player, and the primary actionable cue.
- Cyan for informational accents and diagram explanations, applied sparingly.
- Amber/red for time pressure and errors; pair color with a label.
- Team colors for identity strips/helmets and small score accents. Maintain readable score text for every team.

Use a 2px outer shell, 1px inner dividers, consistent padding (approximately 16px), and a 4/8/16px spacing rhythm. Most sections need a heading and divider, not another bright box. Extend `OverlayChromeRenderer` for shared modal chrome; use its restrained variant for statistics and drive results. Preserve the more celebratory pregame/victory treatment.

### 6. Content does not fully follow the phase of play

The code keeps call lists and CALL OFFENSE/CALL DEFENSE headings during live scrimmage. Defensive replay still shows a call-selection list. Defensive responsibility can be described twice. Coverage and run guidance share one hard-coded text position, so their rendering should explicitly select the relevant cue. Kickoff status can still say SELECT YOUR CALL.

**Fix:** use an explicit presentation state:

| State | Left panel | Right panel |
| --- | --- | --- |
| Offensive setup | Pass/run banks, active call, read/protection, snap/flip/special-teams choices | Match facts and play clock |
| Defensive setup | Defensive bank, personnel/front/pressure, YOUR MLB/OLB assignment, ready action | Match facts, play clock, CPU snap countdown |
| Live scrimmage | Active call, controlled player, one concise read/assignment cue, live controls | Match facts and LIVE state |
| Kick/punt/return | Role, phase, relevant instructions; kick timing meter when active | Match facts and special-teams phase/countdown |
| Play result | Active call and next available action | Outcome, gain, updated series, recent plays |
| Replay | Recorded call/controlled player and exit controls | Recorded score/clock/series, REPLAY label |
| Pause/statistics/drive result | Shared overlay treatment | Underlying HUD subdued; timers suspended as today |

Receiver key labels should remain distinguishable from player-role glyphs and routes. Check overlaps at compact sizes, keep labels inside the playing surface, and retain the gold controlled-player ring. Keep the kick meter close to the kicker while avoiding the ball and both HUD panels.

### 7. Window layout needs one shared calculation

Both side columns reserve 320px regardless of window width. At 1000x700 the stadium/field shrinks to a small central area while large vertical spaces remain in the sidebars. Independent hard-coded Y coordinates make multiline content and controls fragile. The browser extends its left surface to the field edge, covering more of the stadium surround, and abruptly changes the left panel's width. It exposes `preview.Id` and clips text by removing characters without an ellipsis.

**Fix:** introduce one layout calculation used by the field, both panels, browser, and kick meter. Begin with roughly 320px columns at normal/large widths and 240–280px columns at compact widths, validated against text measurements and stadium clearance. Bottom-anchor controls, lay sections out from measured heights, and reduce secondary stats/history density before shrinking essential text. Keep the field's aspect ratio and camera orientation stable. Use the left panel shell for the browser; replace internal call IDs with player-facing formation/personnel/concept information.

Sources: `Constants.cs:18`, `Constants.cs:34`, `PlaybookBrowserRenderer.cs:10`, `PlaybookBrowserRenderer.cs:38`.

## Implementation order

1. **Panel roles and timing:** move CPU cadence to the right clock group; gate timers and labels by phase; deduplicate controls/coaching; add bank headings and selected rows. Keep clock/input/simulation behavior intact.
2. **Shared presentation:** extract common layout, typography, panels, rows, key hints, and status rendering. Apply them to offense, defense, full playbook, and special teams. Prioritize game facts and recent results over stats.
3. **Overlays and compact layouts:** unify statistics/drive-result chrome, fix numeric alignment and text fitting, improve the compact field allocation, and check on-field labels and kick-meter clearance.

## Verification and evidence

Fresh capture runs completed successfully:

```powershell
dotnet run -- --scenario timed-layout --capture --output artifacts/gameplay-ui-review
dotnet run --no-build -- --scenario offense-browser-preview --capture --output artifacts/gameplay-ui-review/browser
dotnet run --no-build -- --scenario offense-passing-preview --capture --output artifacts/gameplay-ui-review/passing
```

Inspected representative offense, defense, browser, replay, statistics, and pregame renders at 1000x700, 1280x720, and 1440x900. The timed capture generated five sizes through 2518x1349; the two offense preview fixtures generated four through 1920x1080. These are staged render captures, not a hands-on live playtest. Phase-dependent findings above also come from renderer code inspection.

**Fixture limitation:** the timed harness's image named `offense` currently captures kickoff setup. The added kickoff sequence changes possession before that capture. Actual offensive scrimmage was reviewed with `offense-passing-preview` instead. Some isolated timed fixtures use the older four-quarter settings; use normal two-half fixtures for future acceptance. Fix the capture sequence and add explicit live offense/defense, low-clock, pause, human field-goal meter, and long/unavailable-call captures before accepting the UI implementation.

Acceptance: inspect every gameplay state at all five sizes; no clipped/overlapping text; readable essential labels; clear selected calls and team ownership; timers stay in one place and reflect the active phase; full playbook keeps clocks visible; field actors/ball remain clear. Cover phase/timer presentation with focused tests, then run the existing gameplay regression suite to confirm clock, input, replay, and special-teams behavior are unchanged.

Representative current captures:

- [Offensive scrimmage, 1440x900](../artifacts/gameplay-ui-review/passing/pass.gun-trips.mesh-wheel-1440x900.png)
- [Offensive scrimmage, 1280x720](../artifacts/gameplay-ui-review/passing/pass.gun-trips.mesh-wheel-1280x720.png)
- [Defense, 1440x900](../artifacts/gameplay-ui-review/1440x900-defense.png)
- [Defense, 1000x700](../artifacts/gameplay-ui-review/1000x700-defense.png)
- [Full playbook, 1440x900](../artifacts/gameplay-ui-review/browser/catalog-1440x900.png)
- [Defensive replay](../artifacts/gameplay-ui-review/1440x900-replay-after-possession.png)
- [Statistics](../artifacts/gameplay-ui-review/1440x900-statistics.png)

Capture artifacts are ignored local files; regenerate them with the commands above.
