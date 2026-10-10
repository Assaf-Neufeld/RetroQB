# Expanded playbook — phase 3

The production catalog contains **108 calls**: 57 authored passes, 49 authored runs,
and one generated wildcard per family. The original 100 calls keep their permanent
IDs. Eight screen/bootleg/rollout calls were added in offensive improvement Step 2.
Mirroring is an additional option and is not counted as another call.

## What players see

Before each snap, the game offers exactly **10 passes and 10 runs**. The call sheet is
sorted by concept category, formation, and concept name. Each row reads, for example,
`I Pro / PA Flood`; the selected call shows its category, actual personnel counts, and
brief guidance on the intended read or running lane. Drive history also retains the
formation and concept, along with the existing stable ID and flip state.

The existing `1–9, 0` pass keys and `Q–P` run keys remain. Press **X** before snapping to
mirror the entire call: players, routes, blockers, mesh point, and run direction.
The same players keep their targeting numbers. **Space** snaps the ball.

Press **L** before an offensive snap to browse all 108 calls. The field shows an independent preview of the highlighted call, with its routes, blockers, run lane or assisted QB path. **Enter** selects the preview by permanent ID and brings it into the quick sheet; selection and preview do not count as usage. Browsing or confirming a wildcard preserves its current generated assignments. The regular wildcard hotkey still generates a new call when selected again.

Browser keys: **Up/Down**, **Page Up/Down**, **Home/End** navigate; **T** cycles family, **]** formation, **C** category, **D** concept; **Backspace** resets filters. **X** mirrors the preview. **G** toggles a saved favorite and **F** shows only favorites. **L/Esc** closes the browser. Filters combine, so an empty view is possible. The timed match's play clock continues; delay penalties rebase preview geometry, and period/possession changes close the browser. Focus loss suspends the clock normally. Favorites never silently replace the quick sheet.

CPU calls and player recommendations share situational scoring of the resolved routes, protection, screens, run scheme, roster, field position and late-game clock. Eight recent snapped calls discourage immediate repetition separately from cumulative statistics. CPU offense remembers visible pressure and tight coverage from completed passing plays, without receiving defensive call IDs or assignments. Throwing windows project receiver and defender movement over estimated ball travel; hot reads retain authored development and fake gates. The existing bounded scramble/throwaway exits remain in place.

## Catalog structure

`ExpandedFormations` defines eight additional five-lineman alignments. Each supports five
passing concepts and five runs, adding 80 calls to the original 20 slots.

| Formation | Personnel | Passing concepts |
| --- | --- | --- |
| Gun Doubles | 3 WR, 1 TE, 1 RB (11) | Spacing, Drive, Sail, Scissors, PA Cross |
| Gun Trips | 3 WR, 1 TE, 1 RB (11) | Stick, Mesh Wheel, Levels, Mills, PA Flood |
| Pistol Twins | 2 WR, 2 TE, 1 RB (12) | Spacing, Drive, Sail, Scissors, PA Cross |
| Singleback Ace | 2 WR, 2 TE, 1 RB (12) | Stick, Mesh Wheel, Levels, Mills, PA Flood |
| Wing Tight | 2 WR, 2 TE, 1 RB (12) | Spacing, Drive, Sail, Scissors, PA Cross |
| I Pro | 2 WR, 1 TE, 1 FB, 1 RB (21) | Stick, Mesh Wheel, Levels, Mills, PA Flood |
| Strong I | 2 WR, 1 TE, 1 FB, 1 RB (21) | Spacing, Drive, Sail, Scissors, PA Cross |
| Split Backs | 3 WR, 2 RB (20) | Stick, Mesh Wheel, Levels, Mills, PA Flood |

Each formation has Dive (Lead Iso in the fullback sets), Power, Counter, Draw, and
Stretch or Sweep. Directional calls alternate authored sides between formations; X
makes the opposite side available without consuming another call-sheet slot. The
existing empty and six-lineman personnel remain in the catalog.

TE1 appears on either side in the authored formations, attached or off the line; TE2
can align opposite TE1 or as a wing. Shotgun, pistol, and under-center QB depths differ.

## Routes and blocking

Eight new route types share the existing stage executor and diagrams: **Hitch, Curl,
Comeback, Corner, Wheel, Angle, Drag, Seam**. Hitches, curls and comebacks settle; wheels
and angles change direction. Combinations pair crossings, vertical clear-outs, stops,
and layered sideline routes with backfield outlets.

Fullbacks have their own roster slot and FB glyph, use backfield coverage/receiving
behavior, and have a slower default speed than a halfback. On runs they lead through
a gap or kick out the edge on Power. The second back in Split Backs performs similar
support jobs. Other skill players stalk/drive or seal; Counter uses the existing line
pulling plans. On passes, supporting backs and selected TEs protect before releasing.

All twelve named PA calls, including the corrected original **PA Deep**, perform a real
fake while the QB retains possession. The original PA Deep now has 11 personnel and
a backfield participant. Throws unlock after the fake or its safety timeout.
TE2, RB2 and FB receiving statistics appear once those players have recorded stats.

## Situational selection

`SituationalCallSheet` evaluates route depth, protection, run concept, down, distance,
and field position using the shared recommendation logic. Quick concepts receive a
short-yardage boost; vertical concepts are reduced in the red zone. Formation/concept
repetition and prior call usage lower selection weight.

Each bank contains six strongest fits (with diversity penalties) plus four weighted
alternatives. Every call has a nonzero opportunity to appear. These are recommendations,
not hard exclusions of all suboptimal calls. The sheet refreshes on a new situation or
completed play, never while browsing or flipping. The automatic selected-play recommendation
then operates only within those 20 available calls.

## Verification

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --no-restore
dotnet run --project tests/VisualPreview/VisualPreview.csproj --no-restore -- --readme artifacts/phase3/playbook-720.png 1280 720
```

The original phase 3 checkpoint had 102 tests covering catalog counts and personnel, stable 10/10 sheets, reproducible selection,
whole-catalog reachability across situations/seeds, usage tracking, new route execution,
new-player receiving stats, and successful play-action fakes in both orientations.
At that checkpoint, the execution sweep explicitly installed each catalog call before simulating
it, covering all 100 calls in both orientations and checking every run's designated
handoff. Existing formation/defense/mirroring tests also run against the larger catalog.
These checks verify execution and integration; competitive balance still benefits from
human playtesting.

## Offensive improvement step 1 — October 2026

Step 1 preserved the 100-call catalog and permanent IDs. Named passing calls use
formation-specific authored geometry and slot-based read progressions. Mesh crossers
have separate shallow depths, Flood/Sail routes layer along one sideline, and
Stick/Spacing hitches and selected curls settle intentionally. Shared field-depth
compression retains the layers near the end zone. Default route types and generated
wildcards retain their existing finish behavior.

CPU reads follow the selected concept's progression and wait for its time/route-stage
gates. Receiver targeting keys retain roster-slot order. The offense panel shows the
first read, outlet, protector count, and protection direction. Stopping routes end in
a circle in the preview; continuing routes retain arrows.

Linemen and protecting backs/TEs share a live threat allocation. Flood/Sail calls use
authored protection slides. Mills/Scissors use six or seven protectors according to
personnel, trading receiving targets for protection; a declared helper can double a
covered threat after uncovered threats receive priority. Supporting backs and selected
PA tight ends check threats before releasing after their minimum block interval. An
immediate uncovered threat keeps the protector blocking even past the decision timeout.
PA Cross retains its fake and wheel outlet.

See the [engineering plan](offensive-improvements-engineering-plan.md) and
[Step 1 verification report](offensive-improvements-step-1-verification.md). Screens,
bootlegs, run-scheme changes, CPU call strategy, favorites, and full-playbook browsing
remain in Steps 2–3. Audibles and motion remain deferred.


## Offensive improvement step 2 — October 2026

Power uses down blocks, an edge kick-out and a wrapping guard. Counter begins away
from the called side, then follows the kick-out and wrap. Stretch reaches laterally
with backside cutback protection. Paired first-level blocks retain an anchor while
one lineman climbs after securing contact; penetration delays that release. Dive,
Lead Iso, Sweep and Draw retain separate tracks and exchange timing. CPU runners
press the previewed track before choosing a cut from visible defenders. Fresh human
movement controls the carrier immediately after a handoff.

| Formation | Added calls |
| --- | --- |
| Gun Doubles | RB Screen, WR Screen, Rollout Right |
| Gun Trips | WR Screen |
| Split Backs | RB Screen |
| Pistol Twins | PA Bootleg Right |
| Singleback Ace | PA Bootleg Right |
| I Pro | PA Bootleg Right |

RB screens hold the target briefly while the line protects, then release two linemen
and a back/TE to escort a catch behind the line. WR screens use immediate perimeter
blocks. CPU reads can target these behind-line receivers after their release gates.
Escorts follow the intended catch area before the catch, then the actual carrier.

Bootlegs fake left and move the QB right after the fake, with sideline routes at
separate depths and edge protection. Rollout starts its QB path immediately. X mirrors
the complete concept. Movement input cancels the assisted QB path immediately and
keeps it canceled for that snap; CPU pressure escapes and throws also end assistance.
Existing pocket PA calls remain available. Blocked fakes retain a bounded timeout.

Gold block paths show the screen escorts and combination climbs. Run paths show the
mesh followed by the called approach and entry; dotted QB paths show the moving pocket.
The two new categories are Screen and Moving pocket. Targeting keys remain in roster
order and each situational sheet still contains ten passes and ten runs. Audibles and
motion remain deferred.

See the [Step 2 verification report](offensive-improvements-step-2-verification.md).
