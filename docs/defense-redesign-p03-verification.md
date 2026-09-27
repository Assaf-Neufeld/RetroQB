# P3 verification — man coverage family

September 27, 2026. P3.1–P3.4 complete in Debug development mode. Base revision `e3b2a4b36b70376cb31a163fc970add8230a3835` plus retained working-tree changes. Evidence runtime version `defense-runtime-p3-v1`; adjusted plans use `defense-plan-p3-v1`. The [P4 report](defense-redesign-p04-verification.md) inventories the current source and requalifies this work. Normal gameplay remains legacy until P10.

Cover 0 protects its own top leverage, Cover 2 Man allocates two separate deep helpers, and Robber keeps a bounded low-middle jump with a reachable return. Primary identity survives crossings, help changes, physical obstruction and observed late releases. Fixed five/six-rusher fixtures execute coverage budgets, with `P3-FIXED-RUSH` diagnostic labels; the coordinated pressure packages still belong to P6.

Supported two-player bunch groups provisionally lock identities. Opposite observed inside/outside releases can commit one atomic switch during the 0.40-second window. Later crossings cannot switch again. Ambiguous/contact-obscured releases and three-player bunches keep explicit locks. Shadows constrain legal edges inside the exact assignment solver. Brackets consume declared low/robber help, never a reserved deep safety or another primary. These are pre-snap metadata/API adjustments through `SelectManAdjustments`, not a new UI variant editor.

Rules: ALIGN-04; MAN-01/04/05/06/07/08/09. Complete DM-01 and DM-03; the man portion of DM-02 and supported man portion of DM-19. Match-family parts remain P7.

Evidence:

- `ManDefensiveRuntimeTests`: atomic bunch switches at 30/60/120 Hz, later crossings, expiry and three-player fallback, valid/invalid shadow and bracket budgets, half-helper separation, robber reach/return, real mesh/twins plays, flipped goal-line plays, and physical blocking slowdown.
- 4,000 structural plans with explicit pair-switch metadata: 100 catalog plays × two flips × base/nickel × five man calls × LOS 30/109. The original 6,000 passive P1 plans also remain tested.
- Historical full-suite gate: **690 passed / the same five baseline failures**, 695 total, `artifacts/defense-redesign/p03/tests/phase03-final.trx`. P2/P3 runtime test classes contain 45 passing cases.
- Historical `p03/runtime-30`, `runtime-60`, `runtime-120`: 320 production cases per rate, zero failures. Eight plays × five calls × both defending roles × four input modes; seed 101, midfield, Ballers/Scarlet Guard. The 60-Hz artifact preceded the last helper refinements; P4's `man-requalification` reruns all 320 cases on the current runtime.
- `p03/capture/report.json`: rendered production drive reached its terminal result with no diagnostic failure. P4 additionally checks the rendered menus, live jobs and replay with the integrated zone extension.

Reproduce:

```powershell
dotnet test tests/RetroQB.Tests/RetroQB.Tests.csproj --filter FullyQualifiedName~ManDefensiveRuntimeTests
dotnet run --project RetroQB.csproj -- --scenario defense-runtime-matrix --headless --engine man --hz 60 --seed 101 --output artifacts/defense-redesign/p03/runtime-60
dotnet run --project RetroQB.csproj -- --scenario defense-quick --engine man
```

Milestone A is complete for base/nickel man coverage. This does not claim production cutover, advanced pressure, dime, zone/match behavior, or new CPU call strategy.
