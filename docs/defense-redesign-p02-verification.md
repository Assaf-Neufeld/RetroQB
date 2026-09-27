# P2 verification — live Cover 1

September 27, 2026. P2.1–P2.5 complete in Debug development mode. Base revision `e3b2a4b36b70376cb31a163fc970add8230a3835` plus the retained working-tree changes. Historical evidence is under `artifacts/defense-redesign/p02/`; the current source inventory and subsequent regression gate are in the [P4 report](defense-redesign-p04-verification.md). Normal gameplay remains on the legacy engine until P10.

The new path runs a full production play through `DefensiveDrive`, `PlaySetupController` and `PlayExecutionController`. `DefensiveObservationBuilder` reads actual movement, ball state/height and physical contact, without routes, blocking assignments, eligibility, intended targets or landing coordinates. Its immutable common frame supplies `DefensiveRuntime`; `DefenderMotor` plans bounded velocity; existing blocking controllers can slow that velocity before one physical integration. The controlled linebacker continues to receive only human input.

Cover 1 has five persistent primary identities, middle help, a separate low helper and four named rush entries with both contain edges. Reachable help changes leverage; unavailable help cannot justify trailing a receiver. Recovery uses observed velocity and actual speed. Throw recognition and legal-height contest reachability precede ball contests; existing catch/interception physics resolve outcomes. Pause/focus loss, zero time, whistle, restart and replay preserve tactical lifecycle. Replays retain immutable decisions and plan/runtime versions; rendering does not run the decision engine.

Rules: OBS-01–08; DEC-00/02/03/05; MAN-02/03; MOVE-01/02/04/05; PRESS-01. Full DM-18 is covered; DM-14/19 are only their observation and supported-call portions. P5 still owns coordinated run fits; the current observed-carrier response is explicitly labeled `P2-CARRIER-FALLBACK` in diagnostic traces.

Evidence:

- `DefensiveRuntimeTests` exercises hidden metadata equivalence at 30/60/120 Hz, observed block/release, throw delay, unreachable contests/help, reachable safety help, hysteresis, speed/turn/arrival, back-line bounds, contain, contact integration and full lifecycle.
- Historical P2 gate: 667 passed / five known failures, 672 total, `p02/tests/phase02.trx`. Later P2/P3 additions bring their combined runtime tests to 45 passing cases at the P3 gate.
- `p02/runtime-60/manifest.json`: 64 production cases, zero failures. Eight plays × both defensive team roles × four input modes, seed 101, midfield, Ballers/Scarlet Guard. Subsequent man-family sweeps requalify Cover 1 at 30/60/120 Hz.
- The five baseline failures remain field-goal range, reopening an old replay, and three CPU punt-policy expectations; see P4 for exact identities.

Reproduce from the repository root:

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --filter FullyQualifiedName~DefensiveRuntimeTests
dotnet run --project RetroQB.csproj -- --scenario defense-runtime-matrix --headless --engine cover1 --hz 60 --seed 101 --output artifacts/defense-redesign/p02/runtime-60
dotnet run --project RetroQB.csproj -- --scenario defense-quick --engine cover1
```

P4 tightened the shared OBS-08 implementation to return zero urgency outside the specified 1.5-second horizon, and retain the current commitment on a zero-score tie. This corrects the earlier small residual score outside the horizon. The hysteresis test now exercises threats inside that horizon; the man family is requalified in P4.
