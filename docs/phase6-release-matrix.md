# Phase 6 frozen verification matrix

Frozen before executing the release matrix (candidate tuning inherited from Phase 5, commit 82535cd).

- Authored catalog: every call, own 20 / midfield / opponent 10, all three stages, seeds 101 / 202 / 303, 60 Hz.
- Generated calls: each wildcard, the same spots/stages, generation seeds 0 through 9.
- Frame rates: run, quick pass, deep pass, play action, and catalog tight/spread formations at 30 / 60 / 120 Hz using seeds 101 / 202 / 303.
- Full matches: 24 short-clock matches, seeds 101 through 124, stages cycling regular/playoff/Super Bowl, alternating opening receivers. Defensive calls and offensive run selections cycle through all ten hotkeys. No forced whistles or manufactured scores are used.
- Mechanical gates: finite/bounded actors, legal throws, stable linebacker ownership, one terminal result per snap, termination within 30 seconds per live play / 30 simulated minutes per short match, and scores matching terminal events.
- Metrics: CPU passing/completion/sack/turnover statistics, rushing efficiency, scoring per possession, simulated duration and execution wall time. Scripted runs accelerate ready signals; these are not human match-duration measurements.

No gameplay balance ranges are approved yet. The optional target question has not received an answer, so the fallback is measurement only. Human control responsiveness, defensive influence, call tradeoffs and default-length pacing require explicit playtest evidence before the gameplay release gate can be marked verified.

Any mechanical failure is fixed and the affected matrix rerun. Fixes and their evidence are recorded in phase6-verification.md; passing thresholds are not adjusted to fit observed output.

Default-length diagnostic extension, frozen before execution: seeds 301/302/303, regular/playoff/Super Bowl respectively, opening receivers user/CPU/user, three-minute quarters, normal CPU cadence, automated user ready signals, 60 simulated-minute budget. These are automated diagnostics, not the pending hands-on gameplay gate. Frame-rate tight/spread cases use FormationType identity rather than text matching after the initial sampler missed them.
