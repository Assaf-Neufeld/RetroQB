# Two-sided team balance

The 12 standard teams now span 70–91 overall. Ratings average offense and defense: Sharks are the underdog at 70, while Vipers lead at 91. Golden Legion remains an unlockable secret team rated 90, strong but no longer a perfect all-star roster. Ratings describe roster design, not guaranteed match outcomes.

Standard ratings: Sharks 70, Firebirds 73, Mustangs 76, Cyclones 77, Ballers and Phantoms 78, Bulldozers 80, Sentinels 82, Lightning 84, Ironclad 86, Bombers 87, and Vipers 91.

Each standard team has two permanent roster stars. Stars stay with their actual positions: a benched MLB does not turn the nickel back into a star. The gold `*` appears on both offensive and defensive players and survives replay. Team selection lists the selected team's named stars and OFF/DEF ratings, with bars for passing, receiver speed, rushing, protection, coverage, pass rush, and tackling.

| Team | Strengths | Weaknesses | Offensive stars | Defensive stars |
| --- | --- | --- | --- | --- |
| Ballers | Versatile offense and balanced defense | No dominant unit | QB Ace | MLB Captain |
| Lightning | Fast spread receivers, accurate passing | Weak blocking and tackling | QB Spark, WR1 Flash | — |
| Bulldozers | Power back, heavy protection, stout front | Slow perimeter and limited passing | RB1 Pound | DT1 Quarry |
| Phantoms | Coverage and takeaways, accurate short passing | Light run defense, limited deep arm | — | CB1 Specter, FS Mirage |
| Cyclones | Receiver/back speed, fast edge pursuit | Interior power and blocking | WR2 Gale | OLB1 Tempest |
| Ironclad | Tackling, front-seven strength, protection | Slow receivers and modest passing | — | DT1 Crucible, MLB Bastion |
| Firebirds | Receiving technique, featured tight end | Leaky defense and modest running | WR1 Flare, TE1 Torch | — |
| Mustangs | Fast rushing attack and linebacker pursuit | Modest passing and coverage | RB1 Gallop | MLB Wrangler |
| Bombers | Accurate deep passing, quick receivers, and strong pressure | Less explosive arm and blitzing than before; otherwise solid across the roster | QB Cannon | DE1 Warhead |
| Sentinels | Coverage, protection, reliable hands | Slow offense and limited pass rush | — | CB1 Warden, OLB1 Vigil |
| Sharks | Accurate possession passing and takeaways | Weak offensive line and defensive front | — | CB1 Mako, FS Finback |
| Vipers | Elite passing, receivers, and pressure | Patient power rushing can attack the defense | QB Venom, WR1 Fang | — |

Golden Legion has QB Crown, WR1 Solar, RB1 Inferno, MLB Caesar, DE1 Aureus, and CB1 Midas. Its ratings are 90 overall; the Vipers are the strongest standard team at 91.

## Opponents

| Opponent | Offensive identity | Defensive identity | Base pass / run | Stars |
| --- | --- | --- | --- | --- |
| Scarlet Guard | Short passes and a featured TE; limited deep speed | Sure tackling; modest rush and deep coverage | 46 / 54 | TE1 Herald, MLB Core |
| Crimson Rush | Explosive passing; weak protection | Aggressive edge pressure; exploitable coverage | 72 / 28 | QB Flint, WR1 Flame, DE1 Fury |
| Bloodline Bastion | Power running and TE play; slow wideouts | Dominant front; slower secondary | 36 / 64 | RB1 Reign, TE1 Monarch, DT1 Wall, MLB Fortress, OLB1 Bulwark |
| Lockdown | Accurate possession passing; little power | Elite secondary; lighter run front | 62 / 38 | QB Cipher, CB1 Island, FS Hawk |

Down and distance still adjust the base run/pass preferences. CPU read intervals differ by opponent, and later rounds retain their existing faster read progression. Pregame scouting describes both units and identifies their stars. Lockdown remains an alternate catalog opponent; the three-round season order is unchanged.

## Gameplay changes behind the ratings

- Defender profiles now affect actual speed, tackling, interception ability, and block shedding. Previously their individual multipliers were unused.
- Pass-rush and coverage ratings now affect movement during the corresponding assignment while the QB holds the ball. Their square-root scaling keeps the effect modest; they do not increase pursuit speed after the QB becomes a runner or the ball is caught.
- Defensive stars have explicit individual profiles. Offensive stars receive small position-specific advantages in throwing, catching, speed, blocking, or tackle breaks. RB2 and TE2 do not inherit their starter's star bonuses.
- Resolved and mirrored defensive assignments restore their captured modifiers exactly once, avoiding compounded profile boosts.
- Playoff/Super Bowl no longer inject generic star players into whichever defense is on the field. Later-round defensive speed applies the general multiplier once to linebackers and defensive backs, preserving slow-secondary weaknesses.
- CPU playcalling uses each team's stated base pass preference, without the former universal extra 15 percentage points.

## Verification

Automated coverage includes ratings and star allocations, real profile effects, resolution/mirroring across formations and rounds, situational CPU calling, replay markers, and live pass/run cases across all 12 standard teams plus Golden Legion, both sides of the ball, and all three rounds. The existing match matrix also exercises 24 short games and three full-length games. Menu and scouting captures are generated at 1000, 1280, and 1920 pixel widths under `artifacts/team-balance/`.

These checks establish integration and match completion. Human playtesting is still needed to assess difficulty, fun, and win-rate balance across different player styles.
