# Phase 6 release verification

Engineering candidate complete; gameplay release gate remains open. Phase 5 is committed as `82535cd`. Phase 6 changes are in the working tree.

## Candidate changes

- Normal startup now opens the complete timed, two-sided season, including team/name selection, all three stages, saving, and return to the menu. Synthetic opponent points and first-to-21 termination remain only in isolated legacy regression paths.
- CPU selection now includes the full 100-call catalog; human offense receives situational call sheets. Defensive control remains one linebacker.
- Normal input freezes on focus loss and consumes edge actions once while advancing physics in bounded steps. Slow-frame catch-up is capped at 100 ms.
- Offensive route/blocking previews are restored in the timed renderer. README controls, rules, screenshots, and simplifications match the new default.
- Debug release scenarios emit reproducible JSON diagnostics. No balance constants were tuned to fit the results.

## Automated evidence

| Check | Result |
| --- | --- |
| Debug and Release builds | Both pass, zero warnings/errors; diff whitespace check passes |
| Full regression suite | 534 passed, zero failures |
| Catalog and frame-rate sweep | 2,880 cases, zero failures; 100 calls, three field positions, all stages |
| Short full matches | 24/24 completed, seeds 101–124, alternating opening receivers, all stages |
| Default-length diagnostics | 3/3 completed, seeds 301–303, three-minute quarters, normal CPU cadence |
| Layout capture | 40 PNGs: ten states at 1000×700, 1280×720, 1440×900, 1920×1080 |
| Normal-launch regressions | Team/name selection, genuine possession after TD, no synthetic points or 21-point ending, focus suspension, save/menu/new-season cycle |

The [frozen matrix](phase6-release-matrix.md) specifies seeds and mechanical limits. `release-catalog` covers 2,826 catalog cases plus 54 frame-rate cases: HB Dive, Bunch Quick, Four Verts, PA Cross, Wing Tight, and Mesh/spread at 30/60/120 Hz. Defensive calls cycle across cases (all ten, both personnel packages); this is not every possible offense/defense Cartesian pairing.

The initial formation sampler missed named tight/spread calls. A revised selector incorrectly demanded a third PassSpread call; the catalog has Mesh and Four Verts. It was corrected to use Bunch Quick as the quick-pass representative and Mesh as spread. The full corrected sweep passed. The interrupted build encountered a locked executable from the failed harness process; after ending that process, the normal build succeeded.

Existing overtime/final-play, restart, replay isolation, season progression, save migration/backup/retry tests are included in the 534-test suite. Detailed earlier evidence remains in [Phase 4](phase4-verification.md) and [Phase 5](phase5-verification.md). New captures were generated with the normal timed factory. Menu, offensive routes, defense, and nonzero statistics were visually inspected; screenshots committed with this change show the actual renderer, with constructed statistics fixtures.

Reproduce using the commands in README. JSON outputs are under `artifacts/phase6/candidate/{catalog,matches,default-matches}.json`; layout captures are under `artifacts/phase6/layouts`. Artifacts are ignored generated files; this report and README screenshots retain the reviewable evidence.

## Default-length diagnostic metrics

These are one automated game per stage, using scripted offense and direct linebacker pursuit. They measure mechanics and expose balance questions, not human skill or real elapsed playtime. CPU possessions below count ended drives; interception percentage is zero in all three samples. Turnovers include interceptions and turnover on downs. Rushing efficiency uses running-back attempts; quarterback rushing remains separate in JSON.

| Stage / seed | User–CPU | Simulated minutes | CPU completion | Sack rate | RB yards/rush | Points/ended possession | Turnovers/ended possession |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Regular / 301 | 107–14 | 20.26 | 100% | 7.32% | 0.87 | 0.74 | 26.32% |
| Playoff / 302 | 77–13 | 20.30 | 97.22% | 2.70% | 1.97 | 0.68 | 15.79% |
| Super Bowl / 303 | 44–37 | 20.07 | 93.48% | 4.17% | 2.64 | 1.95 | 15.79% |

The first two profiles lose heavily to the scripted offense, while CPU completion rates are very high and rushing efficiency low. This is a material balance concern to investigate in playtesting, not evidence of acceptable difficulty. No approved balance ranges exist; no passing ranges have been inferred from these results. Wall-clock execution time is stored separately in JSON and is not match duration.

## Remaining gameplay gate

- Play a complete default-length match against each stage, covering both opening receivers. Record actual elapsed time, result, completion/sack/interception rates, rushing efficiency, and points per possession.
- Before judging those samples, agree acceptable balance ranges. Keep the current tuning and seeds documented when comparing later changes.
- Exercise zone, man, and pressure calls in both personnel packages. Record control responsiveness, ability to affect tackles/coverage, CPU decision quality, meaningful call tradeoffs, and clock pacing.
- Assign severity to observed issues. No mechanical blocker was observed in the automated sample; balance and fun remain unverified. A game-breaking issue blocks release.

Normal launch is enabled as the candidate to playtest. Automated default-length runs do not check off the hands-on gate, and this report does not claim release approval.
