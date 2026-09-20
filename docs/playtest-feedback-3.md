# Playtest feedback: kickoff returns and presentation

- Restored the original centered pregame matchup card, team helmets, colors, and scouting panel. The footer reflects two 2-minute halves and name entry after the season.
- Kickoff landing range changed from world Y 103–115 to 90–113: approximately 87% now land in the field of play, with occasional deep touchbacks. Both teams use the same distribution.
- Kickoffs now field 10 coverage players plus the kicker against 10 blockers plus the returner. Receiving blockers align in two rows of five. Punt personnel is unchanged.
- Reproduced the return-touchdown crash in the real rendering harness: the crowd chant calculated seating bounds with the reversed play orientation, causing an invalid Math.Clamp range. The chant overlay now uses the fixed stadium orientation and restores the play orientation afterward.

Verification:
- 569 tests pass, including a 100-seed kickoff roster/returnability check and a live return touchdown followed by the next kickoff.
- Existing special-teams contact/goal checks cover both control roles and 30/60/120 Hz.
- The rendering harness now captures a kickoff return touchdown and the following kickoff; all five window sizes pass (1000×700 through 2518×1349).
- Three full two-half match simulations pass, seeds 301–303.
- Isolated build succeeds with zero warnings/errors; git diff --check passes.

Evidence: artifacts/kickoff-fix-layouts and artifacts/kickoff-fix-matches. The original running game was left open; restart it to load these changes.

## Follow-up: preparation time, kick contact, and touchback frequency

- CPU snaps now allow at least six seconds to choose a defense, including hurry-up situations. Space readies the defense immediately. CPU kickoff setup also waits six seconds unless readied.
- Kickoff touchbacks use an explicit one-in-five random roll (20% probability per kick). Returnable kicks land between the receiving 5 and 20. This is a probability, not a guarantee of exactly one touchback in each group of five.
- Kickoff and punt coverage players, blockers, and kickers now physically separate on contact. Small simulation steps prevent crossing through a block on slow frames; players can slide around contact.
- Regression coverage includes hurry-up readiness, early Space, contact during kickoff/punt flight, all five touchback roll outcomes, return scoring, and the existing seed/frame-rate sweep.
- Three full match simulations pass after these changes; seeds 301–303, evidence in artifacts/kick-contact-matches.

## Follow-up: eleven-player punt formations

Punts now field 11 players on each team. The kicking side has seven on the line (five interior players and two wide gunners), three protectors, and the punter. The receiving side has seven near the line, three deeper blockers, and one returner. Existing contact and return controls apply to the expanded personnel.

Verification: 577 tests pass, including roster counts, distinct in-bounds positions at own 1/15/50, the snap-to-flight transition, and the existing return/contact/frame-rate tests. Build passes with zero warnings/errors. Layout capture passes at five sizes; the 1440x900 punt formation was visually reviewed. Evidence: artifacts/punt-eleven-layouts; updated screenshots/punt.png.

## Follow-up: kicker and punter join coverage

After releasing the ball, the kicker/punter now pursues the landing spot and then the returner using the same movement, blocking, and tackling rules as the coverage team. CPU returners evade them, and return blockers recognize them as threats. Setup and kick release remain unchanged, and both teams still field 11 players.

Verification: 581 tests pass, including kicker/punter movement after release and isolated tackles with either control role. Three full match simulations pass (seeds 301–303), recorded in artifacts/kicker-coverage-matches. Isolated build succeeds with zero warnings/errors.
