# Defensive playtest fixes

September 30, 2026, after playable checkpoint `69efa2a`.

The last defensive back could retreat six to ten yards ahead of a confirmed ball carrier while its support role excluded it from tackle selection until almost-contact. The new regression reproduced that behavior at 30/60/120 Hz. Open-field support now joins the tackle candidates when it can intercept within 1.25 seconds; if no ordinary candidate can reach, any reachable support can commit within the existing pursuit horizon. Existing speed, turn, observation, manual-input and contact rules still apply. The nearest viable candidate wins, with the existing commitment rules. This also applies to force/cutback support after a catch or once the runner reaches open field. RUN-07 now states the concrete transition.

The side panel's general controls were drawn over the defensive pressure/personnel hints. The renderer now draws one controls block for the current role. Before a defensive snap it shows 1–9/0 call selection, Q/E pressure variants, X pressure side, H personnel, J front and Space ready. Live play, replay and special teams show their own controls.

Kickoffs and punts were drawing the current scrimmage plan's coverage box over the special-teams players. Scrimmage coverage is now gated to the scrimmage action or a recorded scrimmage replay. Special teams retain their controlled-player ring and return/coverage controls.

Verification:

- 40 run-fit tests pass, including the new DB regression producing ordinary tackle contact at all three stepping rates, the three retained stationary-runner cases, blocked/unreachable support, cutbacks and manual ownership.
- 41 display/input/full-match tests pass, including special teams versus scrimmage overlay policy and role-specific switching instructions.
- Full regression: **867 passed / five unchanged P0 failures, 872 total**, in `artifacts/defense-redesign/playtest3/tests/playtest3-regression.trx`. The failures remain the field-goal range assertion, replay reopening after possession change, and three CPU punt-policy assertions.
- Release compilation: zero warnings/errors. A separate build is in `artifacts/defense-redesign/playtest3/game/`, preserving the user's running older game.
- The layout harness exports five window sizes. Inspected 1000×700 defense/kickoff captures and 1440×900 defense capture: switching controls are legible without overlap and the kickoff scrimmage box is absent. Captures remain in `artifacts/defense-redesign/playtest3/layouts/`.

Close the previous game window before running `dotnet run -c Release` to load the updated default build. The separate Release executable can also be opened directly. Interactive competitive balance remains a playtest judgment.
