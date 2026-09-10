# Playbook foundation — phase 1

Phase 1 preserves the current 18 named calls and two wildcard slots. New route mechanics,
lead blocking, new formations/personnel, the situational 100-play catalog, and the new
playbook interface remain for phases 2 and 3.

## Data flow

`PlayCatalog` → `PlayCallSheet` → `ResolvedPlay` → formation/assignment setup → execution and diagrams

- `PlayCatalog` owns immutable authored `PlayDefinition` entries indexed by permanent ID.
  Display names and keyboard positions are not identities.
- `PlayCallSheet` holds exactly ten distinct pass IDs and ten distinct run IDs from its catalog.
  The current default retains the original hotkey order. Phase 3 can supply a situational
  sheet through `PlayManager.SetCallSheet` between snaps.
- `PlayManager` preserves selection and usage by ID when a sheet changes. Actual snaps,
  including manually chosen calls, update usage; browsing does not. Play records retain
  the ID, display name, and flip state.
- `PersonnelPackage` defines active roster slots and the lineman count. `FormationAlignment`
  defines geometry and QB depth. `FormationDefinition.AlignmentSlots` binds the package's
  players to alignment locations independently of runtime receiver indices.
- Each `PlayDefinition` supplies one `PlayerAssignment` per active skill player. Roles are
  route, block, or designated ball carrier. A run must have exactly one RB carrier.
- `PlayResolver` produces immutable positions and explicit directions once. Flipping
  mirrors QB, skill players, linemen, route sides, and run direction without renumbering
  players. Flipping twice reconstructs the original geometry exactly.
- `FormationFactory` instantiates that geometry; `RouteAssigner` applies those assignments
  by roster slot. Neither chooses or substitutes formations, routes, or directions.
- Recommendations consume the same resolved route/protection assignments as setup.
  Diagrams and route movement read the resulting receiver assignment fields. Multi-stage
  route geometry remains phase 2 work.

## Authoring rules

1. Give every play an explicit, unique ID that remains unchanged when its name changes.
2. Choose a personnel package and matching alignment, then bind every skill slot once.
3. Supply every assignment explicitly. Missing routes do not fall back to random routes.
4. Use individual block assignments for each TE/back. Identify the carrier through the
   ball-carrier role rather than list position or proximity.
5. Resolve once and pass the resulting object through setup and recommendation paths.
   Use `ResolvedPlay.Flip()` for mirroring; do not hand-edit positions or directions.

Construction rejects invalid player counts, duplicate/unknown slots, missing/extra
assignments, invalid directions, inconsistent run carriers, non-finite/out-of-field
alignment positions, collapsed positions, and mismatched personnel/alignment sizes.
Legacy close QB/center and bunch spacing is retained; this is not a full football
formation-legality validator. Existing near-own-goal Y clamping is also retained.

## Deliberate migration details

- Existing effective named routes are now authored explicitly. For example, Bunch Quick's
  outside WR runs the same inside route previously produced by an implicit correction.
  New authored outside-WR out routes and RB vertical routes are no longer rewritten.
- Named RB pass outlets have a stable right-side direction instead of randomly choosing
  a side on every setup. Flipping mirrors that direction. Wildcards still randomize.
- Wildcards have complete generated definitions and retain the IDs `pass.wildcard` and
  `run.wildcard`. They reroll when selected, remain stable during setup/rendering, and never
  mutate catalog templates. Automatic selection generates candidates before scoring and
  activates the exact candidate scored, without another reroll.
- Original QB depths, six-lineman heavy sets, and formation-specific player ordering are
  preserved. Existing TE separation is normalized in the formation catalog before play
  resolution. Different legacy left/right definitions remain distinct; flipping an
  individual call is exact even where those historical definitions are not symmetric.
- Recommendations now inspect effective routes and actual protecting players. A generated
  wildcard can therefore receive a different score than the old empty placeholder.
- No new flip hotkey or call-sheet browsing UI is added in phase 1.

## Verification

Run `dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj`.

`PlaybookRegressionTests` captures named pass assignments, original hotkey order, personnel
counts, and run handoffs. `PlaybookFoundationTests` covers a test-only 100-play catalog,
reordered hotkeys/receiver indices, independent TE assignments, RB2 handoff and mesh
movement, immutable definitions, validation, wildcard resolution, shared personnel,
authored QB depth, mirrored routes/diagrams, and defensive setup across every current
formation near both ends of the field and at midfield.

The production renderer smoke check can be captured with:

```powershell
dotnet run --project tests/VisualPreview/VisualPreview.csproj -- --readme artifacts/phase1/playbook-presnap.png
```
