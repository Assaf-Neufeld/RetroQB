# Visual improvement phases

## Phase 1 — Ball, spectators, and sideline detail

Implemented:
- Shared live/replay football renderer: pointed pixel silhouette, leather shading,
  end stripes, and laces that rotate using recorded air time.
- Ground-position ball shadow drawn before actors; held football follows the
  player's arm side in screen pixels.
- Stable spectator skin tones, caps, hair, scarves, shirt details, and seated or
  standing poses, retaining team affiliation and crowd energy reactions.
- Team-colored reserves, segmented benches, coaches, coolers, camera operators,
  and an orange ball-position pole. Props are clipped to the sideline apron.

Validation: application build succeeded without warnings; all 34 existing
regression tests passed. Inspected direct renderer captures at 1280x720,
1920x1080, and 1000x700. These are staged render previews, not full gameplay or
interactive replay tests. Ball physics and collision rules were not changed.

The original phase-one captures are in the ignored `artifacts/visual-phase1`
directory. The preview harness now renders the latest stadium as described below.

## Phase 2 — Stadium architecture and lighting

Implemented:
- Continuous end-zone decks and stepped corner seating connect the side stands
  into a bowl; replaced the disconnected deck bands and elliptical extensions.
- Shaded outer tiers, lit field-facing seating, radial stair aisles, concourses,
  recessed entrance tunnels, safety rails, and numbered section plates.
- Warm amber regular-season lighting; cool blue-white playoff lighting; white
  championship floodlights with gold trim. End-deck signs identify each stage.
- Field layout reserves end-deck space and side clearances for the widest stage,
  including lights. The field keeps its aspect ratio and the same screen scale
  across stages at a given window size; narrow windows use a smaller field to
  keep the stadium clear of the fixed HUD columns.
- Stadium lighting is drawn before the field and players, preserving their
  colors and gameplay contrast.

The archived phase-two captures are in `artifacts/visual-phase2`. They include
all three stages at 1280x720, 1920x1080, and 1000x700, with masks matching the HUD
columns, several ball flight heights/spiral phases, and a held football.

Validation: build succeeded and all 34 regression tests passed. Reviewed all
stage/size combinations, corrected end-deck signage clipping at 1080p, and
regenerated the previews with apron-aware clearance. Rechecked representative
720p and 1080p captures after the correction.

## Phase 3 — Field finish and action animation

Implemented:
- Fixed cleat marks with subtle contrast near the pocket, flat painted end-zone
  lettering with sparse paint grain, and eight shaded orange end-zone pylons.
- Four-direction player facing, arm/leg stride, release follow-through, overhead
  catch-to-tuck motion, and a contact fall/ground pose. The held ball follows the
  catch and ground poses. Throws and collisions retain their existing timing.
- Player pose, facing, and animation clocks are recorded per actor. Replays
  preserve the original down and terminal contact/catch frame. Contact endings
  add 0.35 seconds of visual settling without moving the recorded spot or changing
  possession. Out-of-bounds endings do not trigger a contact fall.
- Crowd reactions spread across four sections by distance from the play, with a
  short delay between sides. Home/away supporters react to their team's outcome;
  neutral supporters react more mildly and some fans remain seated.
- Numbered down marker plus chain crew. The chain stays anchored to the series
  target and is parked for goal-to-go; the down marker remains visible.

Validation: application build succeeded without warnings; all 40 tests passed
(34 existing and six new cases covering contact versus out-of-bounds, pose/physics
separation, recorded down/pose preservation, visual-only replay settling, and
crowd-wave timing). Inspected direct field renders at three window sizes, the
action-frame sheet, and fourth-and-goal signage. No interactive gameplay test.

Regenerate the current previews with:

```powershell
dotnet run --project tests/VisualPreview/VisualPreview.csproj
```

The hidden Raylib renderer writes the nine stage/size PNGs, a 3x action-frame
sheet, and a fourth-and-goal preview to `artifacts/visual-phase3` (ignored by Git).

Keep field details subdued and preserve route, receiver-label, and ball clarity
throughout all phases. Review each phase before starting the next.
