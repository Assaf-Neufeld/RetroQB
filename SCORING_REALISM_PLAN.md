# Scoring Realism Implementation Plan

## Overview
Replace the current "every turnover = auto 7 points" system with realistic football scoring: field goals (3 pts), touchdowns (6 pts + PAT/2pt choice), punting, and simulated opponent drives. This is split into 4 phases, each independently shippable.

---

## Current State (Before Any Changes)

### How Scoring Works Now
- **Touchdown**: `DriveState.ResolveTouchdown()` adds `TouchdownPoints = 7` to `Score`
- **Interception**: `DriveState.ResolveInterception()` adds `DefensiveScorePoints = 7` to `AwayScore`
- **Turnover on downs**: `DriveState.CheckTurnoverOnDowns()` adds `DefensiveScorePoints = 7` to `AwayScore`
- **Win condition**: First to `WinningScore = 21` (in `GameSession`)

### Key Files
| File | Role |
|---|---|
| `RetroQB/Gameplay/DriveState.cs` | Tracks down/distance/LOS/score, resolves play outcomes |
| `RetroQB/Gameplay/GameSession.cs` | Orchestrates game flow, state transitions, `EndPlay()` |
| `RetroQB/Gameplay/PlayManager.cs` | Wraps DriveState, manages play selection, delegates scoring |
| `RetroQB/Core/GameState.cs` | Enum of all game states |
| `RetroQB/Core/GameStateManager.cs` | Manages current state + pause |
| `RetroQB/Rendering/BannerRenderer.cs` | Draws overlay banners (DriveOver, StageComplete, etc.) |
| `RetroQB/Rendering/SidePanelRenderer.cs` | Left panel: play calls, situation text |
| `RetroQB/Rendering/ScoreboardRenderer.cs` | Right panel: score, stats |
| `RetroQB/Rendering/HudRenderer.cs` | Thin coordinator delegating to other renderers |
| `RetroQB/Gameplay/Controllers/DrawingController.cs` | Main draw orchestrator |

### Game State Flow (Current)
```
MainMenu → PreSnap → PlayActive → PlayOver → PreSnap (next play)
                                 → DriveOver → PreSnap (new drive)
                                 → StageComplete → next stage
                                 → GameOver
```

### Key Code Locations
- `DriveState` constants: lines 11-15 (TouchdownPoints=7, DefensiveScorePoints=7)
- `DriveState.ResolveTouchdown()`: line 104
- `DriveState.ResolveInterception()`: line 113
- `DriveState.CheckTurnoverOnDowns()`: line 160
- `GameSession.WinningScore`: line 19 (const 21)
- `GameSession.EndPlay()`: ~line 620-730 — resolves play, transitions state
- `GameSession.HandlePlayOver()`: ~line 511 — auto-advances to PreSnap after timer
- `GameSession.HandleDriveOver()`: ~line 530 — waits for Enter, calls InitializeDrive()
- `PlayOutcome` enum: in `PlayManager.cs` lines 12-19 (Ongoing, Tackle, Incomplete, Touchdown, Interception, Turnover)

---

## Phase 1: Split TD into 6 + Extra Point / 2-Point Conversion

### Status: [ ] Not Started

### Goal
Touchdowns award 6 points. After a TD, the player chooses: automatic extra point (+1) or attempt a 2-point conversion (run a play from the 2-yard line; TD = +2, anything else = +0).

### Changes

#### 1.1 — DriveState.cs
- [ ] Change `TouchdownPoints` from `7` to `6`
- [ ] Add method `ResolveExtraPoint()` → adds 1 to `Score`
- [ ] Add method `ResolveTwoPointConversion(bool success)` → if success, adds 2 to `Score`
- [ ] Add property `bool IsExtraPointAttempt { get; set; }` to track when we're in PAT mode
- [ ] Add method `SetupTwoPointAttempt()` — sets LOS to 2 yards from goal (field Y = `FieldGeometry.OpponentGoalLine - 2`), Down=1, Distance=2

#### 1.2 — GameState.cs
- [ ] Add `ExtraPoint` to the `GameState` enum

#### 1.3 — GameSession.cs
- [ ] In `EndPlay()`: when touchdown occurs, transition to `GameState.ExtraPoint` instead of `DriveOver`
- [ ] Add `HandleExtraPoint()` method:
  - Show banner: "EXTRA POINT (Enter) or 2PT CONVERSION (Space)"
  - If Enter pressed → `_playManager.ResolveExtraPoint()` → check win → `DriveOver`
  - If Space pressed → `_playManager.SetupTwoPointAttempt()` → `PreSnap` with a flag `_isTwoPointAttempt = true`
- [ ] Add field `bool _isTwoPointAttempt = false`
- [ ] In `EndPlay()`: if `_isTwoPointAttempt` is true, handle differently:
  - TD → `_playManager.ResolveTwoPointConversion(true)` → check win → `DriveOver` with "2PT GOOD!" message
  - Anything else → `_playManager.ResolveTwoPointConversion(false)` → `DriveOver` with "2PT NO GOOD" message
  - Reset `_isTwoPointAttempt = false`
- [ ] Update `_driveOverText` messages to show "TOUCHDOWN! +6" then after PAT: "+7 total" or "+8 total" or "+6 total"

#### 1.4 — BannerRenderer.cs
- [ ] Add rendering for `GameState.ExtraPoint` — show the PAT/2PT choice banner

#### 1.5 — DrawingController.cs
- [ ] Handle `GameState.ExtraPoint` in the draw method (show field + banner overlay)

#### 1.6 — Update Messages
- [ ] `DriveState.ResolveTouchdown()` message: "TOUCHDOWN! +6" (was "+7")
- [ ] DriveOver text after PAT: adjust to show total

#### 1.7 — copilot-instructions.md
- [ ] Update the state machine diagram to include `ExtraPoint`
- [ ] Update scoring rules section

### Testing
- Score a TD → see ExtraPoint screen → press Enter → verify +7 total
- Score a TD → press Space → play the 2pt conversion → score → verify +8 total
- Score a TD → press Space → fail 2pt → verify +6 total
- Win condition still works correctly at 21

---

## Phase 2: 4th-Down Decision (Field Goal / Punt / Go For It)

### Status: [ ] Not Started

### Goal
On 4th down, instead of auto-advancing to PreSnap, show a decision screen: Go For It, Field Goal (if in range), or Punt.

### Changes

#### 2.1 — GameState.cs
- [ ] Add `FourthDownDecision` to the enum
- [ ] Add `FieldGoal` to the enum

#### 2.2 — DriveState.cs
- [ ] Remove auto-scoring from `CheckTurnoverOnDowns()` — it should no longer add 7 to AwayScore
- [ ] Instead, return a `PlayOutcome.Turnover` result with no score change (scoring handled by simulated drive in Phase 3, or temporarily: no opponent score until Phase 3)
- [ ] Add `bool IsFieldGoalRange()` — returns true if LOS is close enough (within ~45 yards of goal, i.e. `LineOfScrimmage >= FieldGeometry.OpponentGoalLine - 45`)
- [ ] Add `float GetFieldGoalDistance()` — returns kick distance (LOS to goal + 7 yards for snap + hold)

#### 2.3 — GameSession.cs
- [ ] In `HandlePlayOver()`: when next down is 4th (`_playManager.Down == 4`), transition to `FourthDownDecision` instead of `PreSnap`
- [ ] Add `HandleFourthDownDecision()`:
  - Show banner with options
  - **Space** → Go for it → `PreSnap` (current 4th down behavior)
  - **F** → Field Goal (only if in range) → `GameState.FieldGoal`
  - **P** → Punt → calculate punt distance, set opponent start position, trigger opponent drive (Phase 3) or just `DriveOver` temporarily
- [ ] Add `HandleFieldGoal()`:
  - Run the FG minigame (see 2.5)
  - Made → +3 to Score, then `DriveOver` (or opponent drive in Phase 3)
  - Missed → `DriveOver` (opponent gets ball at kick spot)

#### 2.4 — New File: `RetroQB/Gameplay/Controllers/FieldGoalController.cs`
- [ ] Manages the field goal minigame state machine
- [ ] States: `PowerCharging`, `AccuracySweeping`, `ResultShowing`
- [ ] **Power meter**: A bar that fills 0→100→0 oscillating. Press Space to lock power. Ideal power depends on distance.
- [ ] **Accuracy meter**: A needle sweeping left↔right. Press Space to lock. Center = perfect, edges = wide left/right.
- [ ] **Result calculation**:
  - `powerScore` = how close to ideal power (0.0-1.0)
  - `accuracyScore` = how close to center (0.0-1.0)  
  - Distance penalty: longer kicks need tighter accuracy
  - Success if `accuracyScore >= threshold` (threshold increases with distance)
  - If power too low → "SHORT!", too high + off center → "WIDE LEFT/RIGHT!"
- [ ] **FG distances and difficulty**:
  - 20 yards: very easy (wide accuracy window)
  - 30 yards: easy
  - 40 yards: moderate
  - 45+ yards: hard (narrow accuracy window)
  - 50+: very hard

#### 2.5 — New File: `RetroQB/Rendering/FieldGoalRenderer.cs`
- [ ] Draws the FG minigame overlay on top of the field
- [ ] **Power bar**: Horizontal bar at bottom of field, fills left→right, green zone in the middle
- [ ] **Accuracy needle**: Vertical line sweeping across a horizontal track, center marked
- [ ] **Goalposts**: Simple `|---|` shape drawn at the top of the field (end zone)
- [ ] **Ball flight animation**: After both presses, show the ball arc toward the goalposts (reuse arc math from `Ball.cs`)
- [ ] **Result text**: "GOOD!" with green flash or "NO GOOD!" with red, then brief pause

#### 2.6 — BannerRenderer.cs
- [ ] Add rendering for `FourthDownDecision` state — show the 3 options with key hints
- [ ] Gray out "FIELD GOAL" option when not in range

#### 2.7 — InputManager.cs
- [ ] Add `IsFieldGoalPressed()` → F key (already used for Replay, but only in certain states — need to check for conflict)
- [ ] Add `IsPuntPressed()` → P key (check for conflict with run play P)
- [ ] Note: Input only active in `FourthDownDecision` state, so key conflicts are avoided since play selection keys are only active in `PreSnap`

#### 2.8 — Punt Logic (in GameSession or a small PuntController)
- [ ] Calculate punt distance: `35 + random(0-15)` yards
- [ ] New opponent starting position = `current LOS - punt distance` (clamped to 20 minimum)
- [ ] Touchback if punt goes into end zone → opponent starts at 25
- [ ] Temporarily (before Phase 3): just go to DriveOver with "PUNT" message, no opponent scoring
- [ ] After Phase 3: triggers simulated opponent drive from punt landing spot

### Testing
- Play to 4th down → see decision screen
- Choose "Go for it" → normal 4th down play
- Choose "Field Goal" within range → FG minigame → make it → +3 points
- Choose "Field Goal" → miss it → no points, drive over
- Choose "Punt" → drive over, no opponent points (temporary)
- Verify FG option is grayed out when out of range (e.g., own 20 yard line)

---

## Phase 3: Simulated Opponent Drives

### Status: [ ] Not Started

### Goal
Replace all instances of automatic opponent scoring with a simulated drive sequence. The opponent's drive is generated play-by-play and animated on the field with a ball marker moving between yard lines.

### When Opponent Drives Trigger
| Event | Opponent Starts At |
|---|---|
| After YOUR touchdown + PAT | Opponent's own 25 yard line |
| Interception | Spot of interception |
| Turnover on downs (4th down fail) | Current LOS |
| Your punt | Punt landing spot |
| Your missed field goal | Kick spot (LOS - 7) |

### Changes

#### 3.1 — GameState.cs
- [ ] Add `OpponentDrive` to the enum

#### 3.2 — New File: `RetroQB/Gameplay/SimulatedDrive.cs`

Data types and generation logic:

```csharp
public enum SimulatedDriveOutcome 
{ 
    Touchdown,      // +7 (6+auto PAT for simplicity)
    FieldGoal,      // +3
    Punt,           // 0 — your next drive starts at punt landing
    TurnoverOnDowns,// 0
    Interception    // 0 — your next drive starts at INT spot
}

public record SimulatedPlay(
    int Down, 
    float Distance, 
    float YardLine,        // world Y position
    string Description,    // "HB Run +4", "Pass complete +12", "Incomplete"
    float Gain, 
    bool IsFirstDown);

public record SimulatedDriveResult(
    List<SimulatedPlay> Plays,
    SimulatedDriveOutcome Outcome,
    int PointsScored,
    float EndingYardLine,     // where opponent ended
    float PlayerStartYard);   // where the player's next drive starts
```

#### 3.3 — New File: `RetroQB/Gameplay/SimulatedDriveGenerator.cs`
- [ ] Method: `SimulatedDriveResult Generate(float startYardLine, DefensiveTeamAttributes defTeam, SeasonStage stage, Random rng)`
- [ ] **Play generation loop**:
  1. Start at `startYardLine`, Down=1, Distance=10
  2. Each play: roll play type (55% run, 45% pass)
  3. **Run plays**: gain = normal distribution centered at 3.5 yds, stdev 2.5, clamped [-3, 15]
  4. **Pass plays**: 35% incomplete (gain=0, advance down), 65% complete with gain = normal centered at 7 yds, stdev 5, clamped [-5, 30]. 2% chance of interception → drive ends.
  5. Check first down, update down/distance
  6. Check touchdown (crossed goal line)
  7. On **4th down**: 
     - If within FG range (≤42 yds from goal) and distance > 3 → attempt FG (75-90% make rate depending on distance)
     - If distance ≤ 3 and past midfield → go for it (50% conversion)
     - Otherwise → punt (drive ends, 0 points)
  8. Max plays: 12 (safety valve)
- [ ] **Difficulty scaling by stage**:
  - Regular Season: slightly below-average offense (center run gain 3.0, pass gain 6.0, higher incompletion rate 40%)
  - Playoff: average offense (center run gain 3.5, pass gain 7.0, 35% incompletion)
  - Super Bowl: above-average offense (center run gain 4.0, pass gain 8.0, 30% incompletion)
- [ ] **Play descriptions**: Generate flavor text — "HB Run up the middle +4", "Pass complete to WR +12", "Incomplete", "Sacked -3", "INTERCEPTION!"
- [ ] **Player next drive start**:
  - After opponent TD/FG: player starts at own 25
  - After opponent punt: player starts at punt return spot (opponent's end yard - punt distance)
  - After opponent turnover on downs: player starts at that spot
  - After opponent interception: player starts at own 25

#### 3.4 — New File: `RetroQB/Gameplay/Controllers/SimulatedDriveController.cs`
- [ ] Manages the animation state machine for playing back a `SimulatedDriveResult`
- [ ] States: `Starting`, `AnimatingPlay`, `ShowingResult`, `DriveComplete`
- [ ] Tracks: current play index, animation timer, ball world position
- [ ] **Timing**:
  - 0.5s pause at start ("OPPONENT DRIVE" banner)
  - Per play: 0.4s ball slide animation + 0.3s result text display = ~0.7s per play
  - 1.0s pause at end for drive result banner
  - Total: ~4-10 seconds depending on drive length
- [ ] Method: `void Start(SimulatedDriveResult drive)`
- [ ] Method: `void Update(float dt)` → advances animation, returns when complete
- [ ] Method: `bool IsComplete { get; }`
- [ ] Method: `float BallWorldY { get; }` — current animated ball position
- [ ] Method: `float LineOfScrimmage { get; }` — for field markings
- [ ] Method: `float FirstDownLine { get; }` — for field markings
- [ ] Method: `SimulatedPlay? CurrentPlay { get; }` — for text display
- [ ] Method: `string? ResultBanner { get; }` — "OPPONENT TOUCHDOWN!", "FIELD GOAL - 3 PTS", "OPPONENT PUNTS"
- [ ] Allow **Space to skip** — jumps to end result immediately

#### 3.5 — New File: `RetroQB/Rendering/SimulatedDriveRenderer.cs`
- [ ] Draws on top of the normal field (field still visible with yard lines)
- [ ] **Ball marker**: Pulsing circle (opponent team color or red) at `BallWorldY`, centered on field X
- [ ] **LOS line**: Red line at current scrimmage (reuse `FieldMarkingsRenderer` style)
- [ ] **First down line**: Yellow line (reuse existing)
- [ ] **Header overlay**: "OPPONENT DRIVE" text at top of field, semi-transparent background
- [ ] **Play log**: In the side panel area, show scrolling list of completed plays:
  ```
  1st&10 OPP 25 — HB Run +4
  2nd&6  OPP 29 — Pass +12, 1ST DOWN
  1st&10 OPP 41 — Incomplete
  2nd&10 OPP 41 — HB Run +3
  ...
  ```
- [ ] **Result banner**: Large centered text for outcome, styled like existing `BannerRenderer`
- [ ] **Screen effects**: Reuse `ScreenEffects` for opponent TD (red flash) and FG (minor shake)

#### 3.6 — DriveState.cs Modifications
- [ ] Remove `AwayScore += DefensiveScorePoints` from `ResolveInterception()` — just return the interception result
- [ ] Remove `AwayScore += DefensiveScorePoints` from `CheckTurnoverOnDowns()` — just return turnover result
- [ ] Add method `AddOpponentScore(int points)` — called after simulated drive completes
- [ ] Add method `float GetInterceptionSpot()` or pass it through the play result

#### 3.7 — GameSession.cs Modifications
- [ ] Add fields: `SimulatedDriveGenerator _simDriveGenerator`, `SimulatedDriveController _simDriveController`, `SimulatedDriveResult? _currentSimDrive`
- [ ] Add `float _opponentDriveStartYard` field
- [ ] Modify `EndPlay()`:
  - On interception: store INT spot, transition to `OpponentDrive` (not DriveOver)
  - On TD + after PAT resolution: transition to `OpponentDrive` 
- [ ] Modify flow after Phase 2's 4th-down outcomes:
  - On punt: calculate landing, transition to `OpponentDrive`
  - On missed FG: transition to `OpponentDrive` from kick spot
  - On turnover on downs: transition to `OpponentDrive` from LOS
- [ ] Add `HandleOpponentDrive()`:
  ```
  if not started yet:
      generate drive via SimulatedDriveGenerator
      start SimulatedDriveController animation
  
  update SimulatedDriveController(dt)
  
  if controller.IsComplete:
      apply score: _playManager.AddOpponentScore(result.PointsScored)
      check win condition
      set up next player drive at result.PlayerStartYard
      transition to DriveOver (or StageComplete/GameOver if score threshold met)
  ```

#### 3.8 — DrawingController.cs
- [ ] Handle `GameState.OpponentDrive` — render field + SimulatedDriveRenderer overlay
- [ ] Pass SimulatedDriveController state to the renderer

#### 3.9 — HudRenderer.cs / SidePanelRenderer.cs
- [ ] During `OpponentDrive` state: show play log in side panel instead of normal play selection info
- [ ] Scoreboard continues to show as normal (score updates in real time as opponent scores)

### Testing
- Throw an interception → see opponent drive animate → opponent may score 0/3/7
- Turnover on downs → opponent drive from that spot
- Punt on 4th down → opponent drive from landing spot
- Score a TD → PAT → opponent drive from their 25
- Opponent scores a TD → verify AwayScore increases by 7
- Opponent kicks FG → verify AwayScore increases by 3
- Opponent punts → verify your next drive starts at the right spot
- Space to skip → jumps to result
- Win condition still works (either side reaching 21)

---

## Phase 4: Win Condition Tuning

### Status: [ ] Not Started

### Goal
With variable scoring, tune the win condition so games feel right.

### Options (Choose one during implementation)

**Option A — Keep "First to 21" (Recommended to start)**
- No code change needed
- Games may take longer with 3-point possessions — that's fine, adds drama
- Both sides can reach 21 naturally through 3s and 7s

**Option B — Possession-Cycle Check**
- Only check win condition after a full cycle (your drive + opponent drive)
- Prevents "opponent scores to 21 during your post-TD drive" feeling abrupt
- Small change in `HandleOpponentDrive()`: defer win check until cycle completes

**Option C — Fixed Drive Count**
- 16 total possessions (8 per "half"), highest score wins
- Bigger restructure — save for a future version

### Changes (if Option A)
- [ ] Possibly raise `WinningScore` from 21 to 24 or 28 if playtesting shows games are too short
- [ ] Consider: if tied at 21+, play until someone leads after a full possession cycle (overtime feel)

---

## Implementation Dependency Graph

```
Phase 1 (PAT/2pt)
    └── no dependencies, start here

Phase 2 (4th Down / FG / Punt)  
    └── depends on Phase 1 being done (TD scoring must be 6+PAT already)
    └── Punt temporarily just ends the drive (no opponent scoring yet)

Phase 3 (Simulated Opponent Drives)
    └── depends on Phase 2 (punt/FG/turnover triggers are defined there)
    └── Wires up all the trigger points from Phase 1 and 2

Phase 4 (Win Condition)
    └── depends on Phase 3 (variable scoring must be working)
    └── Tuning/playtesting pass
```

---

## New Files Summary

| File | Phase | Purpose |
|---|---|---|
| `RetroQB/Gameplay/Controllers/FieldGoalController.cs` | 2 | FG minigame state machine |
| `RetroQB/Rendering/FieldGoalRenderer.cs` | 2 | FG minigame visuals |
| `RetroQB/Gameplay/SimulatedDrive.cs` | 3 | Data types (records/enums) |
| `RetroQB/Gameplay/SimulatedDriveGenerator.cs` | 3 | Play-by-play drive generation |
| `RetroQB/Gameplay/Controllers/SimulatedDriveController.cs` | 3 | Animation state machine |
| `RetroQB/Rendering/SimulatedDriveRenderer.cs` | 3 | Drive playback visuals |

## Modified Files Summary

| File | Phase(s) | Changes |
|---|---|---|
| `RetroQB/Core/GameState.cs` | 1, 2, 3 | Add ExtraPoint, FourthDownDecision, FieldGoal, OpponentDrive |
| `RetroQB/Gameplay/DriveState.cs` | 1, 2, 3 | Split TD scoring, remove auto-7, add PAT/FG/punt methods |
| `RetroQB/Gameplay/PlayManager.cs` | 1, 2 | Expose new DriveState methods |
| `RetroQB/Gameplay/GameSession.cs` | 1, 2, 3 | New state handlers, modified EndPlay flow |
| `RetroQB/Rendering/BannerRenderer.cs` | 1, 2 | PAT choice banner, 4th down decision banner |
| `RetroQB/Rendering/HudRenderer.cs` | 3 | Delegate to SimulatedDriveRenderer during opponent drives |
| `RetroQB/Rendering/SidePanelRenderer.cs` | 3 | Show play log during opponent drives |
| `RetroQB/Gameplay/Controllers/DrawingController.cs` | 1, 2, 3 | Handle new GameStates in draw |
| `RetroQB/Input/InputManager.cs` | 2 | Add FG/Punt key checks |
| `RetroQB/.github/copilot-instructions.md` | 1, 2, 3, 4 | Update documentation |

---

## Progress Tracker

## Checkpoint Log

- **Checkpoint 1 (2026-02-28)**
  - Phase 1 implementation completed (except commit step).
  - Added `GameState.ExtraPoint`, TD scoring changed to +6, PAT (+1) and 2pt conversion (+2) flow implemented.
  - Extra point choice UI added via `BannerRenderer` and hooked through `HudRenderer`/`DrawingController`.
  - Build status: `dotnet build` passing.

- **Checkpoint 2 (2026-02-28)**
  - Phase 2 baseline implemented: `FourthDownDecision` and `FieldGoal` states are live.
  - 4th-down choices added: go-for-it (`Space`), field goal (`F`, range-checked), punt (`P`).
  - Basic field-goal resolution implemented (distance-based make chance); full meter minigame still pending.
  - Turnover on downs no longer auto-awards 7 points.
  - Build status: `dotnet build` passing.

- **Checkpoint 3 (2026-02-28)**
  - Added full field-goal meter minigame with `FieldGoalController` (power lock + accuracy lock).
  - Added `FieldGoalRenderer` overlay with live power/accuracy bars and stateful prompts.
  - Integrated minigame into `GameSession` + `DrawingController` in `GameState.FieldGoal`.
  - Build status: `dotnet build` passing.

- **Checkpoint 4 (2026-02-28)**
  - Implemented `OpponentDrive` flow with generated and animated simulated possessions.
  - Added `SimulatedDrive`, `SimulatedDriveGenerator`, `SimulatedDriveController`, and `SimulatedDriveRenderer`.
  - Wired triggers from interception, turnover on downs, punt, made FG, missed FG, and post-TD conversion flow.
  - Added custom next-drive start support and opponent score application after simulation result.
  - Build status: `dotnet build` passing.

- **Checkpoint 5 (2026-02-28)**
  - Completed Phase 4 tuning using a centralized win target: `Rules.WinningScore = 24`.
  - Updated gameplay and UI references to the centralized score goal (`GameSession`, `MenuRenderer`, `SidePanelRenderer`).
  - Updated GameOver winner banner condition to compare `Score` vs `AwayScore` (no hardcoded threshold in rendering).
  - Build status: `dotnet build` passing.

- **Checkpoint 6 (2026-02-28)**
  - Review-driven gameplay fixes applied:
    - Touchdowns now always transition to `ExtraPoint` before any win-condition check.
    - Punt decisions now compute opponent start spot using punt distance (35-50 yards, clamped to own 20).
    - Missed field goals now start opponent drives from the kick spot (`LOS - 7`, clamped).
  - Build status: `dotnet build` passing.

### Phase 1 Checklist
- [x] 1.1 DriveState: TD=6, add PAT/2pt methods
- [x] 1.2 GameState: add ExtraPoint
- [x] 1.3 GameSession: ExtraPoint handler, 2pt attempt flow
- [x] 1.4 BannerRenderer: PAT choice banner
- [x] 1.5 DrawingController: handle ExtraPoint state
- [x] 1.6 Update scoring messages
- [x] 1.7 Build & test Phase 1
- [x] 1.8 Update copilot-instructions.md
- [ ] 1.9 Commit Phase 1

### Phase 2 Checklist
- [x] 2.1 GameState: add FourthDownDecision, FieldGoal
- [x] 2.2 DriveState: remove auto-7 from turnovers, add FG helpers
- [x] 2.3 GameSession: 4th down decision handler, FG/punt flow
- [x] 2.4 Create FieldGoalController.cs
- [x] 2.5 Create FieldGoalRenderer.cs
- [x] 2.6 BannerRenderer: 4th down decision banner
- [x] 2.7 InputManager: FG/Punt keys
- [x] 2.8 Punt logic
- [x] 2.9 Build & test Phase 2
- [x] 2.10 Update copilot-instructions.md
- [ ] 2.11 Commit Phase 2

### Phase 3 Checklist
- [x] 3.1 GameState: add OpponentDrive
- [x] 3.2 Create SimulatedDrive.cs (data types)
- [x] 3.3 Create SimulatedDriveGenerator.cs
- [x] 3.4 Create SimulatedDriveController.cs
- [x] 3.5 Create SimulatedDriveRenderer.cs
- [x] 3.6 DriveState: remove all auto-scoring, add AddOpponentScore
- [x] 3.7 GameSession: wire up OpponentDrive triggers and handler
- [x] 3.8 DrawingController: handle OpponentDrive state
- [x] 3.9 HudRenderer/SidePanelRenderer: play log during opponent drive
- [x] 3.10 Build & test Phase 3
- [x] 3.11 Update copilot-instructions.md
- [ ] 3.12 Commit Phase 3

### Phase 4 Checklist
- [x] 4.1 Playtest and decide on win condition approach
- [x] 4.2 Implement chosen option
- [x] 4.3 Build & test Phase 4
- [x] 4.4 Update copilot-instructions.md
- [ ] 4.5 Commit Phase 4
