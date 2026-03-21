# 🏈 RetroQB

**Call the play. Read the coverage. Survive the rush. Be the hero.**

RetroQB is a fast, retro-style 2D football game built around the best part of the sport: being the quarterback when everything is on the line. Pick your team, choose your play, scan the field, and try to stay cool while the defense starts doing very rude things.

It is part arcade football fantasy, part drive-management challenge, and part "please let my slot receiver win this route." 

---

## 📸 Screenshots

<p align="center">
  <img src="screenshots/gameplay.png" alt="Gameplay" width="600"/>
</p>

<p align="center">
  <em>Lead your receivers downfield and find the open man!</em>
</p>

<!-- Add more screenshots as needed:
<p align="center">
  <img src="screenshots/touchdown.png" alt="Touchdown!" width="600"/>
</p>
-->

---

## 🎮 What makes it fun

### Pick a play and commit
Every snap starts with a decision. Do you dial up a quick pass, attack deep, or call a run and trust the blocking? RetroQB keeps play selection simple and readable, so the fun is in the choice: take the safe yards, hunt the big one, or try to outsmart the defense before the ball is even snapped.

### Feel the game-day atmosphere
This is not just Xs and Os on a blank field. The stadium has lights, crowd detail, sideline flavor, replay moments, a subtle retro background score, and a scoreboard that keeps the whole drive feeling alive. When you hit a big play, it should feel like the building noticed.

### Watch the defense adjust
The defense is not there just to be decorative. Coverages change, blitz looks vary, and tougher stages bring smarter, nastier opponents. As you move from the Regular Season to the Playoff and then the Super Bowl, the defense starts feeling more prepared for what you want to do.

### Live in the pocket
Some plays are about rhythm. Some are about panic. RetroQB is built for both. Sometimes you plant, fire, and hit a clean route on time. Sometimes the edge collapses, your first read disappears, and you have to sprint out of danger and invent something.

### Build a season one drive at a time
You are trying to survive a full three-stage run:
- **Regular Season** — a good place to settle in and find your timing
- **Playoff** — stronger defenses, tighter windows, more pressure
- **Super Bowl** — the meanest version of the game, with everything on the line

---

## 🏈 Game flow

1. **Choose a team** with its own offensive personality.
2. **Pick a play** before the snap — pass or run, safe or bold.
3. **Read the defense** and identify your best option.
4. **Snap the ball** and react fast.
5. **Throw on time or take off running** if the pocket breaks down.
6. **Manage the drive** across four downs and chase the end zone.
7. **Advance through the season** by winning each stage.

If you like football games where the tension comes from decisions more than button combos, this is the idea.

---

## 🎯 A few reasons to come back for one more drive

- **Quick decision-making** — pre-snap choices matter
- **Readable receiver targeting** — eligible receivers are clearly labeled
- **Run and pass variety** — not every drive has to look the same
- **Replay support** — rewatch your best throw or your worst mistake
- **Team variety** — different rosters support different styles
- **Stage-based challenge** — the season arc gives every game a purpose

---

## 🎮 Controls

| Action | Key |
|--------|-----|
| Move | `WASD` or `Arrow Keys` |
| Sprint | `Left Shift` |
| Snap Ball | `Space` |
| Throw Pass | `1` `2` `3` `4` `5` |
| Replay Last Play (dead-ball) | `F` |
| Confirm / Next Drive / Continue | `Enter` |
| Pause | `Esc` |

### Pre-snap play selection

| Action | Key |
|--------|-----|
| Select pass play | `1`-`0` |
| Select run play | `Q`-`P` |

---

## 🧠 Quick tips

- **Do not wait forever** — the rush is coming
- **Throw where a receiver is going** — not where they are standing
- **Use the run game** — it helps when the defense starts selling out against the pass
- **Respect tight coverage** — forcing hero throws is how drives die
- **Scramble with purpose** — a smart run can be better than a bad pass

---

## ⚙️ Technical details

### Quick start

```bash
dotnet run
```

### Requirements

- .NET 10 SDK
- Windows x64 target for publish output

### Tech stack

- C# / .NET 10
- Raylib-cs 7.0.2
- Single-project desktop game structure

### Project structure

```
RetroQB.csproj    # Project file (run from repo root)
RetroQB/
├── AI/           # Coverage logic, defensive memory, targeting, line AI
├── Core/         # Constants, game state, rules, field geometry
├── Data/         # Offensive and defensive team presets and attributes
├── Entities/     # QB, receivers, defenders, blockers, ball
├── Gameplay/
│   ├── Controllers/   # Play execution, blocking, tackling, menus, drawing
│   └── Factories/     # Formations, defense creation, blitz decisions
├── Input/        # Centralized input handling
├── Rendering/    # Field, HUD, stadium, overlays, effects
├── Routes/       # Route types, geometry, assignment, visualization
└── Stats/        # Game and season stat tracking
```

### Build notes

- Build from the repository root.
- Open the `Football2DQB` folder in VS Code.
- The workspace includes VS Code settings to avoid showing nested Git repos unnecessarily.

### Created with

- Visual Studio Code
- GitHub Copilot

---

*The crowd is loud, the pocket is shrinking, and somebody has to make the read.*
