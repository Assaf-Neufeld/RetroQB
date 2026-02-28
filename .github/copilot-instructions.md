# RetroQB — Copilot Instructions

## Project Overview
RetroQB is a 2D retro-style American football quarterback simulation built with **C# / .NET 10.0** and **Raylib-cs 7.0.2**.
The player controls the QB, selects pass and run plays, reads the defense, throws to receivers, and drives the ball down the field.
A season progression system takes the player through Regular Season → Playoff → Super Bowl with escalating defensive difficulty.

## Build & Run
- **SDK**: .NET 10.0 (win-x64)
- **Build**: `dotnet build` from the repo root
- **Run**: `dotnet run` from the repo root
- **Publish**: `dotnet publish -c Release`
- **Output**: Self-contained single-file executable (`win-x64`)

## Architecture Principles
- **File-scoped namespaces** are used throughout (`namespace X;` syntax)
- **GlobalUsings.cs** provides project-wide imports: `RetroQB.Core`, `RetroQB.Data`, `RetroQB.Routes`, `RetroQB.Stats`
- **SOLID principles**: Each controller has a single responsibility
- **Data-driven formations**: `FormationFactory` uses a dictionary lookup table, not switch statements
- **Interface-first design**: Key systems expose interfaces (`IFormationFactory`, `IDefenseFactory`, `IThrowingMechanics`, `IStatisticsTracker`, `IBlitzDecisionStrategy`)
- **Composition over inheritance**: `GameSession` delegates to specialized controllers rather than handling logic directly
- **Immutable records** for data transfer (`DefensiveCallDecision`, `BlitzDecision`, `PlaySetupResult`, etc.)

---

## Folder Structure
```
RetroQB/
├── AI/               — Defensive intelligence (targeting, coverage, coordination, memory)
├── Core/             — Constants, GameState, Palette, FieldGeometry, Rules, SeasonStage
├── Data/             — TeamAttributes, Rosters (Offensive/Defensive), TeamPresets
├── Entities/         — QB, Receiver, Defender, Ball, Blocker, Entity (base class)
├── Gameplay/
│   ├── Controllers/  — PlayExecution, ReceiverUpdate, Ball, Tackle, Blocking, Drawing, Menu, etc.
│   ├── Factories/    — FormationFactory, DefenseFactory, BlitzDecisionStrategy
│   └── Replay/       — ReplayRecorder, ReplayPlayer, ReplayClipStore, frame types
├── Input/            — InputManager (centralized key handling)
├── Rendering/        — FieldRenderer, ScoreboardRenderer, SidePanelRenderer, MenuRenderer, BannerRenderer, HudRenderer, etc.
├── Routes/           — RouteType, RouteGeometry, RouteRunner, RouteAssigner, RouteVisualizer
└── Stats/            — QbStatLine, RushStatLine, SkillStatLine, StatisticsTracker, StatsSnapshot, SeasonSummary
```

---

## Game State Machine

`GameState` enum drives the main loop in `GameSession.Update()`:

```
MainMenu → PreSnap → PlayActive → PlayOver → PreSnap (next play)
                                 → DriveOver → PreSnap (new drive)
                                 → StageComplete → PreSnap (next stage)
                                 → GameOver → MainMenu
```

Any non-MainMenu state can transition to `Replay` (press F) and back.

| State | What Happens |
|---|---|
| `MainMenu` | Team selection (1-3 keys), Enter to confirm |
| `PreSnap` | Play selection visible, routes drawn, Space to snap |
| `PlayActive` | Full simulation: QB movement, route running, blocking, passing, tackling |
| `PlayOver` | Brief pause (1.25s normal, 2.6s touchdown), then auto-advance |
| `DriveOver` | TD/turnover/interception summary, Enter to start new drive |
| `StageComplete` | Won the stage, Enter to advance to next stage |
| `GameOver` | Season over (won championship or eliminated), Enter to return to menu |
| `Replay` | Frame-by-frame playback of previous play |

### Pause
`GameStateManager.TogglePause()` via Escape key. While paused, all updates are skipped.

---

## Entity Hierarchy

All game objects extend `Entity` (abstract):
```
Entity (abstract)
  ├── Quarterback   — Player-controlled, has TeamAttributes, physics-based movement
  ├── Receiver      — Route-running WR/TE/RB, has ReceiverSlot, Speed, Route, RouteProgress
  ├── Defender      — AI-controlled defensive player, has CoverageRole, DefensivePosition, star system
  ├── Blocker       — Offensive lineman, AI-controlled blocking
  └── Ball          — Tracks BallState (HeldByQB | HeldByReceiver | InAir | Dead)
```

### Key Entity Properties
- **Quarterback**: `HasBall`, `TeamAttributes`, physics via `ApplyInput(dir, sprint, aimMode, dt)`
- **Receiver**: `ReceiverSlot` (WR1-4, TE1-2, RB1-2), `OffensivePosition` (WR/TE/RB), `Route`, `IsBlocking`, `HasBall`, `Eligible`, `RouteSide`, `SlantInside`
- **Defender**: `DefensivePosition` (DL/DE/LB/DB), `DefenderSlot` (DT1-2, DE1-2, MLB, OLB1-2, CB1-2, FS, SS, NB), `IsRusher`, `CoverageReceiverIndex`, `ZoneRole`, `IsPressCoverage`, `ZoneJitterX`, `IsBeingBlocked`, `IsStarPlayer`
- **Ball**: `BallState`, `Holder`, `AirTime`, `ThrowStart`, `IntendedDistance`, `ArcApexHeight`, arc-based flight model

### Receiver Slots & Positions
```
ReceiverSlot: WR1, WR2, WR3, WR4, TE1, TE2, RB1, RB2
OffensivePosition: WR, TE, RB
```
- `IsRunningBackSlot()`, `IsTightEndSlot()`, `IsWideReceiverSlot()` extension methods
- Priority ordering: WR1(1) → WR2(2) → WR3(3) → WR4(4) → TE1(5) → TE2(6) → RB1(7) → RB2(8)

### Defender Slots
```
DefenderSlot: DT1, DT2, DE1, DE2, MLB, OLB1, OLB2, CB1, CB2, FS, SS, NB
DefensivePosition: DL, DE, LB, DB
```
- Slot-to-position mapping via `DefenderSlotExtensions.GetPositionType()`
- **Nickel package**: NB replaces MLB in zone coverages (Cover2Zone, Cover3Zone, Cover4Zone)

---

## Offensive System

### Play Definition
Each play has:
- `Name`, `Family` (Pass/Run), `Formation` (FormationType enum)
- `RunningBackRole` (Block/Route), `TightEndRole` (Block/Route)
- `Routes` dictionary: receiver index → RouteType
- `SlantDirections` dictionary: receiver index → bool (inside/outside)
- `RunningBackSide`: directional hint for run plays

### Formation Types (16 total)
**Base formations** (3 WR, 1 RB, 1 TE):
- BaseTripsRight, BaseTripsLeft, BaseSplit, BaseBunchRight, BaseBunchLeft

**Pass formations** (4 WR, 1 TE):
- PassSpread, PassBunchRight, PassBunchLeft, PassEmpty

**Run formations** (1 WR, 2 TE, 1 RB + extra OL):
- RunPowerRight, RunPowerLeft, RunIForm, RunSweepRight, RunSweepLeft, RunStretchRight, RunStretchLeft

### FormationFactory
- Dictionary-based lookup: `FormationType → FormationData`
- `FormationData` contains `ReceiverPlacement[]` and `ExtraLinemen` count
- Each `ReceiverPlacement` is `(XFraction, YOffset, ReceiverSlot)`
- Factory creates QB (7 yards behind LOS), 5 OL + extra linemen, and receivers per placement
- **Run formations add extra linemen** (1 extra for Power/IForm/Sweep, same for Stretch)

### Playbook
- **10 pass plays** (key 1 = wildcard, 2-9 + 0 = named plays)
- **10 run plays** (key Q = wildcard, W-P = named plays)
- Wildcard plays regenerate randomly from a pool at selection time
- Named plays: Mesh, Bunch Quick, Four Verts, Deep Ins, Flood, Smash, Slant Flat, PA Deep, Double Move
- Run plays: HB Power R/L, HB Dive, HB Toss R/L, HB Sweep R/L, HB Stretch R/L, HB Draw

### Route Types (10 types)
```
Go, Slant, OutShallow, OutDeep, InShallow, InDeep, PostShallow, PostDeep, DoubleMove, Flat
```
- `RouteGeometry` defines stem distances (shallow/deep vary by position: WR > TE > RB)
- `RouteRunner` executes movement: Y-distance-based progress (not time-based)
- `RouteAssigner` maps play definition routes to receivers, handles blocking assignments
- Outer WRs auto-adjust routes to avoid sideline collisions

### Throwing Mechanics
- Lead-time calculation using quadratic intercept formula
- Inaccuracy scales with: base → pressure factor → movement penalty (cross-body throws)
- Ball has arc-based flight: `ArcApexHeight` computed from distance, ball uncatchable above `PassCatchMaxHeight`
- Overthrow factor adds distance beyond the intended target

---

## Defensive System

### Defensive Coordinator (AI Decision Pipeline)
The `DefensiveCoordinator` is the **single pre-snap decision point**. Pipeline:
1. **Situational weights** (`CoverageSchemeSelector.GetSituationalWeights`): down, distance, field position, score differential
2. **Stage pool filter** (`ApplyStagePool`): Regular Season restricts to basic zones; Super Bowl unlocks full menu
3. **Memory multipliers** (`DefensiveMemory`): boosts successful schemes, penalizes burned ones
4. **Weighted random pick** → `CoverageScheme`
5. **Blitz decision** (`DefaultBlitzDecisionStrategy`): scheme-weighted blitz packages with memory feedback

### Coverage Schemes (6 schemes)
| Scheme | Description | Zone Flag | Nickel | Man Positions |
|---|---|---|---|---|
| Cover 0 | All man, no safety help, aggressive | false | No | All |
| Cover 1 | Man + 1 deep safety (FS deep middle zone) | true* | No | CBs, LBs |
| Cover 2 Zone | Two deep halves, CBs in flats, LBs in hooks | true | Yes (NB in hook) | None |
| Cover 3 Zone | Three deep thirds (2 CBs + FS), SS/NB in flats, LBs in flats | true | Yes (NB in flat) | None |
| Cover 4 Zone | Four deep quarters (2 CBs + 2 S), LBs in hooks | true | Yes (NB deep middle) | None |
| Cover 2 Man | Two deep safeties (zone), CBs and LBs man underneath | true* | No | CBs, LBs |

*Cover 1 and Cover 2 Man are hybrids: `IsZoneScheme` returns true so defenders with `ZoneRole != None` use zone logic.

### Stage-Specific Defense Pools
- **Regular Season**: Cover2Zone + Cover3Zone only (Cover0, Cover4, Cover2Man disabled/reduced)
- **Playoff**: All except Cover0
- **Super Bowl**: Full menu including Cover0

### Defense Factory Alignment
Standard 4-man front:
- **DL**: DE1 (left edge), DT1 (left interior), DT2 (right interior), DE2 (right edge)
- **LBs**: OLB1 (left), MLB (middle, replaced by NB in nickel), OLB2 (right)
- **DBs**: CB1 (left outside), CB2 (right outside), FS, SS, NB (nickel slot)

Positioning:
- DEs get circular edge rush paths (via `DefenderTargeting.GetEdgeRushTarget`)
- DTs rush straight with lane offsets
- DB positions determined per-scheme via `GetDbConfiguration()`: press/off depths, zone responsibilities
- Zone jitter (`ZoneJitterX`) shifts anchors per-play for variety (±1.8 world units max)

### Blitz Packages (8 packages)
```
None, WillPressure (OLB), MikePressure (MLB), StrongSafetyPressure (SS),
NickelCat (NB), DoubleLinebacker, DoubleEdge, ZeroPressure (Cover0-only)
```
- Base weights per scheme (zone coverages exclude multi-blitz packages)
- Modified by: `BlitzFrequency` team attribute, situational factors (clear pass down boosts blitz), memory feedback
- `BlitzSlotMultipliers` per team customize which positions blitz more frequently

### Defensive Memory (Adaptive AI)
- Tracks per-scheme and per-blitz-package weight multipliers
- **Learning ramp**: nearly zero adjustments early, full strength after ~30 plays
- **Scheme learning**: big gains/TDs penalize scheme weight; short gains boost it
- **Blitz learning**: sacks/incompletions boost blitz package; big gains penalize it
- Multipliers clamped to [0.35, 1.85] — no option fully eliminated

### Star Player System
- Playoff: 2 star players (FS, DE1)
- Super Bowl: 5 star players (DE1, DE2, MLB, CB1, FS)
- Star boost varies by position: DEs get speed/block-shed; DBs get interception; LBs get tackling

### Defender Targeting (In-Play AI)
Priority chain for each defender:
1. **QB running past LOS** → all converge on QB
2. **Ball held by receiver** → all converge on ball carrier
3. **Ball in air** → break on ball with position-specific timing (DBs break earlier)
4. **Rusher** → DEs take arc rush, DTs go straight
5. **Coverage** → zone target from `ZoneCoverage` or man-coverage receiver tracking

### Zone Coverage Mechanics
- `ZoneCoverage.GetZoneTarget()`: anchor position → zone match → match target calculation
- Zone bounds per role: `GetZoneBounds()` returns XMin/XMax/YMin/YMax
- Deep zones maintain cushion above deepest receiver in zone
- Receivers matched by zone proximity scoring (depth priority + distance)
- Hook/flat zones match by X-bounds; deep zones match and carry receivers vertically

---

## Blocking System

### Offensive Line AI (`OffensiveLinemanAI`)
- **Run plays**: Blockers pull to running lane, engage defenders near anchor positions
- **Pass plays**: Blockers form pocket, engage closest rushers with priority on DEs for tackles
- Behavior modes: `RunContext` (isRun, runSide, isSweep, isStretch, lateralPush, targetY)
- `RunProfile` per blocker: backside sealing, lane shifts based on OL position
- Anchor computation considers formation type and play direction
- **Double-team effectiveness**: 2.4x when multiple blockers on one defender

### Blocking Contact
- `BlockingUtils.ResetDefenderBlockingState()` called each frame before blocking updates
- `RegisterBlockContact()` marks `IsBeingBlocked` and increments `ActiveBlockersCount`
- Blocked defenders have 15% tackle chance (vs 100% normally)
- `TackleShedBoost`: defenders closer to ball carrier shed blocks more easily

### Receiver Blocking
- **RB blocking**: protects edges in pass plays, engages nearest rusher in run plays
- **TE blocking**: run plays drive in run direction, pass plays protect pocket
- **WR auto-blocking**: after catch or during run play, WRs auto-switch to blocking mode

---

## Scoring & Drive System

### Drive State
- 4 downs, 10 yards for first down
- Start at own 20-yard line each drive
- TD = 7 points (includes PAT), Interception/Turnover = opponent scores 7
- `DifficultyMultiplier` increases with player score (defenders get faster as you score more)

### Win Condition
- First to 21 points wins the game
- Win a game → advance to next stage
- Lose → eliminated, season over

### Season Progression
```
SeasonStage: RegularSeason (1.0x) → Playoff (1.06x) → SuperBowl (1.13x)
```
- Each stage has a `DifficultyMultiplier` scaling defensive attributes
- `PassRushMultiplier` scales at half rate (1.0 → 1.03 → 1.06) so QB isn't instantly sacked
- Each stage faces a different defensive team preset (ScarletGuard → CrimsonRush → BloodlineBastion)

---

## Team Data System

### Offensive Team Attributes
- `OffensiveTeamAttributes` wraps `OffensiveRoster` with per-player profiles
- Profiles: `QbProfile` (speed, accuracy, inaccuracy), `WrProfile` (speed, catching, catch radius), `TeProfile` (+ blocking), `RbProfile` (+ tackle break), `OLineProfile` (speed, blocking strength)
- 3 preset teams: **Ballers** (balanced), **Lightning** (speed/passing, weak OL), **Ironclad** (power running)
- Colors: `PrimaryColor` (QB, UI), `SecondaryColor` (receivers, OL)

### Defensive Team Attributes
- `DefensiveTeamAttributes` wraps `DefensiveRoster` with per-player `DefenderProfile`
- Attributes: `SpeedMultiplier`, `InterceptionAbility`, `TackleAbility`, `CoverageTightness`, `PassRushAbility`, `BlitzFrequency`
- Per-slot `BlitzSlotMultipliers` customize blitz tendencies
- Position speeds: `DlSpeed`, `DeSpeed`, `LbSpeed`, `DbSpeed` (override Constants defaults)
- 3 preset teams per stage: **Scarlet Guard** (Regular Season), **Crimson Rush** (Playoff, blitz-heavy), **Bloodline Bastion** (Super Bowl, elite coverage)
- `PositionBaselineAttributes` allow per-team position-group tuning (e.g., CrimsonRush has enhanced LB tackle/block-shed but weaker DB coverage)

---

## Input System

All input flows through `InputManager` — **never** call `Raylib.IsKeyPressed` directly in game logic.

| Action | Keys | State |
|---|---|---|
| Move QB | WASD / Arrow keys | PlayActive |
| Sprint | Left Shift (hold) | PlayActive |
| Snap ball | Space | PreSnap |
| Select pass play | 1-0 (number row) | PreSnap |
| Select run play | Q-P (letter row) | PreSnap |
| Throw to receiver | 1-5 (by priority) | PlayActive |
| Pause | Escape | Any |
| Confirm/advance | Enter | Menu/DriveOver/StageComplete/GameOver |
| Replay | F | PlayOver/DriveOver/StageComplete/GameOver/PreSnap |
| Skip replay | Space | Replay |

---

## Rendering Pipeline

### Three-Column Layout
```
[Side Panel (320px)] [Field (dynamic)] [Scoreboard (320px)]
```
- Field dimensions maintain 53.3:120 aspect ratio, centered in available space
- `Constants.UpdateFieldRect()` recalculates each frame for window resizing
- World-to-screen: `Constants.WorldToScreen(worldPos)`

### HudRenderer (Coordinator)
Delegates to:
- `ScoreboardRenderer` — score, down/distance, play result, QB stats, receiver stats, RB stats
- `SidePanelRenderer` — play call info, receiver labels, situation text
- `MenuRenderer` — team selection, pause overlay
- `BannerRenderer` — stage complete, champion, elimination, touchdown popups

### DrawingController
- Called by `GameSession.Draw()`
- Manages field rendering, entity drawing, route overlays, fireworks, screen effects
- Receiver priority labels (1-5) drawn next to eligible receivers
- Screen effects: camera shake, color flash (touchdown, interception, big hit)

### FieldRenderer
Delegates to:
- `FieldSurfaceRenderer` — grass, yard lines, end zones
- `FieldMarkingsRenderer` — LOS, first down line, hash marks
- `SidelineRenderer` — sideline boundaries
- `StadiumBackdropRenderer` — stadium lights, background

---

## Statistics & Recording

### StatisticsTracker
Tracks per-game: QB stats (comp/att/yards/TDs/INTs/sacks/rush), receiver stats per slot (targets/receptions/yards/TDs), RB stats (yards/TDs)

### SeasonSummary
Aggregates across games: cumulative QB stats, game results per stage, NFL passer rating calculation (with sack penalty house rule)

### PlayRecord
Per-play record: pre-snap situation (down, distance, yard line), offensive play name, defensive scheme/coverage, blitzers, outcome, gain, catcher info

### Replay System
- `ReplayRecorder` captures entity positions each frame during `PlayActive`
- `ReplayClipStore` holds the most recent clip
- `ReplayPlayer` plays back frames at adjustable speed
- `ReplayOverlayRenderer` draws the replay with "REPLAY" overlay
- `ReplayStateHandler` manages replay→game state transitions

---

## Key Conventions & Rules

1. **All input goes through `InputManager`** — no direct `Raylib.IsKeyPressed` calls in game logic
2. **BlockingUtils holds shared blocking utilities** — used by `OffensiveLinemanAI` and `BlockingController`
3. **HudRenderer is a thin coordinator** — delegates to ScoreboardRenderer, SidePanelRenderer, MenuRenderer, BannerRenderer
4. **PlayExecutionController delegates receiver logic** to `ReceiverUpdateController`
5. **Team data lives in `Data/`** — rosters, presets, attributes
6. **Route logic lives in `Routes/`** — types, geometry, running, assignment, visualization
7. **Single defensive decision point**: `DefensiveCoordinator.Decide()` → `DefenseFactory.CreateDefense()` (never call factory directly with ad-hoc scheme choices)
8. **Defense factory maps receivers by spatial order** (leftmost → rightmost) for man-coverage assignments
9. **Nickel package replaces MLB with NB** in zone coverages only
10. **Star players are stage-dependent** — don't add star boosts during Regular Season
11. **Zone jitter is per-defender per-play** — assigned in `DefenseFactory`, used in `ZoneCoverage`
12. **Ball flight uses arc model** — `GetArcHeight()` and `GetFlightProgress()` determine catchability
13. **Route progress is Y-distance-based** (not time-based) — ensures routes break at correct field positions even when blocked
14. **Tackle break** only applies to running backs, chance modified by both offensive and defensive attributes
15. **PlayRecord must be started (`StartPlayRecord`) before snap and finalized (`FinalizePlayRecord`) after play resolution** — memory observes the record post-finalization
16. **Screen effects (shake/flash) are triggered in `EndPlay()`** based on outcome — touchdown, interception, sack, big play

## Coordinate System
- World coordinates: X = 0 (left sideline) to 53.3 (right sideline), Y = 0 (own end zone back) to 120 (opponent end zone back)
- `FieldGeometry.EndZoneDepth = 10` — end zones are yards 0-10 and 110-120
- `FieldGeometry.OpponentGoalLine = 110` — touchdown when Y >= 110
- LOS and first-down line are in world Y coordinates
- Screen Y is inverted (Y increases downward) — handled by `WorldToScreen()`

## Adding New Features — Quick Reference

### New Coverage Scheme
1. Add enum value to `CoverageScheme`
2. Add DB configuration in `DefenseFactory.GetDbConfiguration()`
3. Add LB zone roles in `GetLbZoneRoles()`
4. Add man/zone classification in `IsManForPosition()`, `IsZoneScheme()`
5. Add nickel decision in `UsesNickelPackage()`
6. Add base blitz weights in `BlitzDecisionStrategy.GetBasePackageWeights()`
7. Add situational weights in `CoverageSchemeSelector`
8. Add stage-pool filter rules in `ApplyStagePool()`

### New Route Type
1. Add enum value to `RouteType`
2. Add geometry in `RouteGeometry` (stem distances, break directions)
3. Add movement calculation in `RouteRunner.CalculateRouteDirection()`
4. Add visualization in `RouteVisualizer`

### New Formation
1. Add enum value to `FormationType`
2. Add `ReceiverPlacement[]` entry in `FormationFactory.Formations` dictionary
3. If run formation: consider extra linemen and `BlockingUtils` sweep/stretch classification

### New Play
1. Add `PlayDefinition` in `PlaybookBuilder.BuildPassPlays()` or `BuildRunPlays()`
2. Define formation, routes per receiver index, RB/TE roles, slant directions

### New Defensive Team
1. Create static property in `DefensiveTeamPresets`
2. Define roster with `DefenderProfile` per slot
3. Set team-level attributes (speed, interception, tackle, coverage, blitz multipliers)
4. Optionally customize `PositionBaselineAttributes`
5. Wire into `GameSession.SetDefensiveTeamForStage()` if stage-specific

### New Offensive Team
1. Create static property in `OffensiveTeamPresets`
2. Define roster with QB, WR, TE, RB, OLine profiles
3. Add to `OffensiveTeamPresets.All` list
4. Colors: ensure `PrimaryColor` is not reddish (or QB auto-falls back to default cyan)
