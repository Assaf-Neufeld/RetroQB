# RetroQB

Call both sides of the ball in a top-down arcade football season. Lead the offense, then control one linebacker while the CPU runs its own drives. Win the Regular Season, Playoff, and Super Bowl to become champion.

![Offensive playcalling](screenshots/gameplay.png)
![Defensive playcalling and linebacker assignment](screenshots/defense.png)
![Offense and defense statistics](screenshots/statistics.png)

## Play

Requires the .NET 10 SDK. Run from the repository root:

```powershell
dotnet run
```

Choose one of twelve teams with Up/Down or 1–9/0, then press Enter. G opens the secret-team password prompt. Press Enter on the matchup screen to play immediately. Enter your name on the original save-score screen after your season ends.

Teams have distinct offensive and defensive strengths, with permanent `*` roster stars and at least one defensive star per standard team. Selection shows both units' ratings and named stars; pregame scouting covers the opponent's offense and defense. See the [team balance guide](docs/team-balance.md) for identities, tradeoffs, and opponent tendencies.

Your team always attacks up-screen and defends the bottom end zone. The field stays fixed across possession changes, with screen-relative linebacker controls.

Each game has two two-minute halves (H1 and H2), a 20-second play clock, and three timeouts per half. Possession changes put you on the other side of the ball. Touchdowns score seven, field goals three, and safeties two, only for the scoring team. Reaching 21 does not end the game. Tied regulation uses paired possessions from the opponent's 25; both teams get an attempt before a winner is decided.

On offense, choose from ten situational pass calls and ten run calls drawn from the 108-call catalog. Route and blocking previews show the selected assignment, including run lanes, screen escorts, and moving QB paths. The CPU uses that catalog too. See the [playbook guide](docs/playbook-expansion.md).

On defense, choose one of ten coverage/pressure calls, including Cover 3 Match and Quarters Match. The highlighted player is always your linebacker: MLB in base or dime personnel, or OLB1 in nickel. Move and sprint to cover, contain, or pursue; contact handles tackles and pass interactions. The CPU snaps after its cadence, or Space signals that you are ready.

Normal timed games now use the compiled defense engine. Man coverage assigns receivers by formation geometry; zones coordinate ownership and handoffs; match coverage follows observed releases. CPU calling considers down, distance, visible formation, game situation, team strengths, and bounded history from completed plays. Humans and the CPU execute the same defensive plans.

H cycles Auto/Base/Nickel/Dime personnel and J cycles Even/Over/Under/Wide/Short Yardage fronts. Base keeps more linebackers; dime adds a sixth defensive back. Over/Under shift the interior gaps, Wide spreads the edge players, and Short Yardage compresses the front. Added and conditional pressure requires Even; unavailable combinations explain why before changing your selection.

A completed drive shows its outcome, play count, yards, points, and next possession. Press Space to continue; F replays the last play and Tab opens statistics.

At a final result, Enter accepts the game and advances after a win. After a loss or the Super Bowl, enter your name and press Enter to save the season; Enter then returns to team selection.

## Controls

| Action | Key |
| --- | --- |
| Move / sprint | WASD or arrows / Left Shift |
| Offensive pass call / defensive call before snap | 1–9, 0 |
| Offensive run call before snap | Q W E R T Y U I O P |
| Flip offensive call | X |
| Open full offensive playbook before snap | L |
| Flip defensive pressure before snap | X |
| Cycle defensive pressure variant | Q / E |
| Cycle defensive personnel / front before snap | H / J |
| Snap / ready / continue a drive or period summary | Space |
| Throw to receiver during a live offensive pass | 1–5 |
| Select field goal / punt / kneel | K / B / V |
| Timeout | C |
| Pause | Esc |
| Statistics / return | Tab |
| Scroll recent plays | Page Up / Page Down |
| Replay last play / skip replay | F / Space |
| Restart current matchup, preserving accepted earlier games | Z |
| Accept pregame or final result / return after saving | Enter |

Pause, statistics, replay, and loss of window focus suspend play. Restart is disabled after the completed season is saved.

In the offensive playbook, use Up/Down to browse, Page Up/Down to page, and Home/End to jump. T cycles pass/run, ] cycles formation, C cycles category, and D cycles concept. F shows favorites; G stars or unstars the previewed call. X flips the preview, Enter selects it, and L/Esc closes the browser. Backspace clears filters. Browsing keeps the normal play clock running; navigation does not snap or change the active call. The selected call appears in the usual 10-pass/10-run quick sheet.

Favorites use permanent call IDs and are saved to `%LOCALAPPDATA%\RetroQB\playbook-favorites.json`. They survive matchup restart and relaunch. Missing calls are ignored, and unreadable preferences start an empty favorites list. If saving fails, the choice remains available for the current session.

For a field goal, press K before the snap, then Space to snap. Once the holder is ready, use Space to start the meter, lock power, and lock accuracy in the green zones. Distance includes the end zone and snap; attempts beyond 80 yards are unavailable, with kicks beyond 65 yards rated extreme difficulty. A miss changes possession without awarding points. Select another offensive call before snapping to leave kick setup. Punts are available only on fourth down in regulation. B selects the punt formation; Space snaps to the punter, who kicks downfield. On fourth down, the CPU considers field-goal distance, yards to gain, field position, and game urgency; it normally punts when backed up or facing a longer gap. Extreme-range attempts are reserved for game-changing situations.

Kickoffs start each half and follow scores in regulation. Space starts your kick or readies your receiving team; CPU kickers also start automatically. The kicking side controls a coverage linebacker, while the receiving side controls the highlighted returner after the catch. WASD/arrows move in screen directions and Shift sprints. Coverage and blockers move on the field; tackles, sidelines, touchbacks, return touchdowns and return safeties determine the next possession. Kickoff flight does not consume the half clock; the clock starts on the return.

## Records and current limitations

Records live at `%LOCALAPPDATA%\RetroQB\player-records.json`, with a `.bak` backup. Version 2 preserves legacy records and ranks timed seasons separately. Detailed game results include both units' statistics and controlled-linebacker contributions. The season rating retains the existing offensive formula. Failed saves can be retried with Enter.

This is an arcade ruleset: no fumbles, extra-point attempts, defender switching, or manual tackle/swats. Punt and kickoff direction/distance are automatic and seeded; there are no onside kicks, fair-catch controls, or blocked kicks yet. Scrimmage replays remain available; punt/kickoff replay is not yet recorded. CPU field goals use seeded distance-based outcomes; human kicks use the timing meter. Delay of game is enforced, but the full football penalty rulebook is not simulated.

Phase 6 automated verification is documented in the [release report](docs/phase6-verification.md). Hands-on balance, defensive influence, and pacing review remain pending; automated simulations do not establish those qualities.

The offensive improvements now include coordinated passing/protection, distinct run schemes, screens/bootlegs, shared CPU/recommendation scoring, full-playbook browsing, and favorites. [Step 3 verification](docs/offensive-improvements-step-3-verification.md) records the before/after balance results; the [final review](docs/offensive-improvements-final-review.md) records bug fixes and final regression results. Human balance acceptance remains pending; audibles and motion are deferred.

## Development

C# / .NET 10, Raylib-cs 7.0.2; publish target Windows x64.

```powershell
dotnet build
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj
dotnet build -c Release
```

Debug-only reproducible verification:

```powershell
dotnet run -- --scenario release-catalog --headless --output artifacts/phase6/candidate
dotnet run -- --scenario release-matches --headless --output artifacts/phase6/candidate
dotnet run -- --scenario release-default --headless --output artifacts/phase6/candidate
dotnet run -- --scenario timed-layout --capture --output artifacts/phase6/layouts
```

The release matrix freezes its seeds internally. Layout capture exports menu, pregame, offense, defense, statistics, kicking, halftime, overtime, final, replay, punt/kickoff sequences, and the end-of-season name screen at four sizes. README images use its 1440×900 captures.

Other fixtures include `timed-short`, `late-tying-kick`, `late-protect-lead`, `overtime-pairs`, and `timed-season-short`. Original `offense-*` fixtures retain isolated legacy regression adapters. Normal startup always uses timed two-sided seasons; Release builds reject scenario arguments.

The latest [playtest changes and verification](docs/playtest-feedback-2.md) cover two-minute halves, end-of-season name entry, QB movement, and playable punts/kickoffs.
