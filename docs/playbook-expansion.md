# Expanded playbook — phase 3

Phase 2 is committed as `4312a3b`. Phase 3 expands the production catalog to **100 calls**:
49 authored passes, 49 authored runs, and one generated wildcard per family. Mirroring
is an additional option and is not counted toward the 100.

## What players see

Before each snap, the game offers exactly **10 passes and 10 runs**. The call sheet is
sorted by concept category, formation, and concept name. Each row reads, for example,
`I Pro / PA Flood`; the selected call shows its category, actual personnel counts, and
brief guidance on the intended read or running lane. Drive history also retains the
formation and concept, along with the existing stable ID and flip state.

The existing `1–9, 0` pass keys and `Q–P` run keys remain. Press **X** before snapping to
mirror the entire call: players, routes, blockers, mesh point, and run direction.
The same players keep their targeting numbers. **Space** snaps the ball.

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

All nine named PA calls, including the corrected original **PA Deep**, perform a real
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

102 tests cover catalog counts and personnel, stable 10/10 sheets, reproducible selection,
whole-catalog reachability across situations/seeds, usage tracking, new route execution,
new-player receiving stats, and successful play-action fakes in both orientations.
The existing execution sweep now explicitly installs each catalog call before simulating
it, covering all 100 calls in both orientations and checking every run's designated
handoff. Existing formation/defense/mirroring tests also run against the larger catalog.
These checks verify execution and integration; competitive balance still benefits from
human playtesting.
