# P10 playable checkpoint

September 30, 2026, after P9 commit `2ba0c38`. This checkpoint enables the completed defense in normal timed matches and seasons so the user can assess gameplay. It does not declare the entire P10 release gate complete.

Normal `GameSession`, `FullMatchSession`, and `TimedSeason` defaults now use `DefensiveEngineMode.Production`, the same complete engine previously exposed as StrategyDevelopment. Release permits this engine. Explicit legacy/partial development fixtures remain available for comparison; legacy removal and the full profiling matrix remain pending.

The three P8 stationary-runner failures now have dedicated regressions. When a tackler makes no progress toward a stationary carrier, the run coordinator rotates the pursuit assignment after 1.25 seconds of observed stagnation. Goal-side support may close after two seconds of observed stationary possession behind the line. Movement, blocking, tackle breaks and contact still determine the outcome; no deadline awards a tackle. Candidates remain unique when support is already within contact range.

Verification:

- The three exact retained cases pass through ordinary tackle contact: Cover 1/dime/Wide at 30 Hz; Cover 2 Man/nickel/Short Yardage at 60 Hz; flipped Robber/base/Over at 120 Hz, all seed 101 and Stretch Right.
- `p10-playable.trx`: 868 total, 863 passing, the five unchanged P0 failures. These are the field-goal range assertion, replay reopening after possession changes, and three CPU punt-policy assertions. None is suppressed. This full run preceded the final candidate deduplication guard and default-engine assertions; the targeted final run requalifies those refinements.
- The complete `timed-short` strategy match, seed 101, reaches a normal final result. Report: `artifacts/defense-redesign/p10/timed-short-101/report.json`.
- Release builds with zero warnings and zero errors. The new default has normal-path regression coverage in `NormalTimedGameTests`.
- `p10-final-targeted.trx`: all 54 run-fit, strategy and normal-game tests pass after the final candidate guard and startup assertions. Final Release compilation also succeeds with zero warnings/errors.

README documents Q/E pressure variants, X pressure flip, H personnel and J front selection. Start with `dotnet run -c Release --no-build`, or open `bin/Release/net10.0/win-x64/RetroQB.exe`. The normal menu starts the new engine; no scenario flags are needed.

Remaining P10 work: broad matchup and failure-variant sweeps, quantitative football quality comparisons, warmed Release latency/allocation/memory profiling, legacy engine retirement, and hands-on review of both sides and window sizes. This is a playable assessment checkpoint, not milestone D. Earlier evidence and the 98-rule ledger retain their scoped meaning.
