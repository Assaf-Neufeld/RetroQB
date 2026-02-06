# RetroQB — Copilot Instructions

## Project Overview
RetroQB is a 2D retro-style American football quarterback simulation built with C# / .NET 9.0 and Raylib-cs.

## Build & Run
- **SDK**: .NET 9.0 (win-x64)
- **Build**: `dotnet build` from the repo root
- **Run**: `dotnet run` from the repo root
- **Publish**: `dotnet publish -c Release`

## Architecture
- **File-scoped namespaces** are used throughout (`namespace X;` syntax)
- **GlobalUsings.cs** provides project-wide imports: `RetroQB.Core`, `RetroQB.Data`, `RetroQB.Routes`, `RetroQB.Stats`
- **SOLID principles**: Each controller has a single responsibility
- **Data-driven formations**: `FormationFactory` uses a dictionary lookup table, not switch statements

## Folder Structure
```
RetroQB/
├── AI/           — DefenderTargeting, OffensiveLinemanAI, ZoneCoverage
├── Core/         — Constants, GameState, Palette, FieldGeometry, Rules
├── Data/         — TeamAttributes, Rosters, TeamPresets
├── Entities/     — QB, Receiver, Defender, Ball, Blocker, Entity
├── Gameplay/
│   ├── Controllers/  — PlayExecution, ReceiverUpdate, Ball, Tackle, Blocking, Menu, Drawing, etc.
│   └── Factories/    — FormationFactory, DefenseFactory
├── Input/        — InputManager (centralized key handling)
├── Rendering/    — FieldRenderer, ScoreboardRenderer, SidePanelRenderer, MenuRenderer, BannerRenderer, HudRenderer
├── Routes/       — RouteType, RouteGeometry, RouteRunner, RouteAssigner, RouteVisualizer
└── Stats/        — QbStatLine, RushStatLine, SkillStatLine, StatisticsTracker, StatsSnapshot
```

## Key Conventions
- All input goes through `InputManager` — no direct `Raylib.IsKeyPressed` calls in game logic
- `BlockingUtils` holds shared blocking utilities (used by OffensiveLinemanAI and BlockingController)
- `HudRenderer` is a thin coordinator delegating to ScoreboardRenderer, SidePanelRenderer, MenuRenderer, BannerRenderer
- `PlayExecutionController` delegates receiver logic to `ReceiverUpdateController`
- Team data lives in `Data/` (rosters, presets, attributes)
- Route logic lives in `Routes/` (types, geometry, running, assignment, visualization)
