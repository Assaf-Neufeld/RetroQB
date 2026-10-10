# Step 3 balance targets

Recorded before CPU/throwing changes, October 10, 2026. These are arcade-game review
bands for the fixed scripted fixtures, not claims about real football.

The identical baseline/candidate balance scenario uses every catalog call, two CPU
rosters, own 20/opponent 10, seeds 101/202/303, all ten production defenses at 60 Hz;
then six standard two-minute-half games, seeds 401–406, rotating three opponent teams.
Keep scripted inputs, scenario ordering, teams and seeds fixed across builds.

| Diagnostic | Review band |
| --- | --- |
| Completion rate | 50–90% overall; investigate screens above 95% |
| Sack rate (sacks / attempts+sacks) | 0–15% |
| Interception rate | 0–15% of attempts |
| Running-back yards per rush | 1–12 yards |
| First throw | Mean 0.6–3.8 seconds |
| CPU points per completed drive | 0.5–5.0 |
| CPU variety | Screens and moving pockets used; avoid consecutive repeated calls |
| Frame runtime and allocations | Investigate candidate >1.25× baseline; hard regression gate >1.5× |

Mechanical gates remain hard: every live play resolves through normal physics within
30 seconds, games complete within the harness budget, no duplicate results or illegal
pass release, finite positions, correct possession/stat ownership, all 108 calls
reachable and stable, and no new regression failures relative to Step 2's five.

Tune only bounded parameters justified by these observations. Outcomes outside a
review band must be explained; do not manufacture whistles, catches, interceptions
or an artificially favorable fixture to make a band pass. Runtime measurements
include production updates only, excluding fixture construction and validation.
Run cost comparisons without concurrent rendering/test jobs.
