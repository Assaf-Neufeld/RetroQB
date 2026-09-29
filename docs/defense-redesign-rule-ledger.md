# Defensive redesign rule implementation ledger

Updated September 29, 2026. Current runtime `defense-runtime-p7-v1`; plan `defense-plan-p7-v1`.

`verified` means the owning phase's supported scope passed its behavioral gate. P1–P7 verify 90 of 98 rules for base/nickel man, spot zones and authored match checks in Debug development mode. Normal gameplay remains legacy until P10. P6 verifies fixed, zone, simulated and conditional pressure with coordinated carrier response. P7 verifies declared Cover 3 Match/Quarters Match checks and explicit unsupported-geometry fallbacks. Dime/front expansion and CPU strategy remain in their roadmap phases.

Evidence: [P0](defense-redesign-p00-verification.md), [P1](defense-redesign-p01-verification.md), [P2](defense-redesign-p02-verification.md), [P3](defense-redesign-p03-verification.md), [P4](defense-redesign-p04-verification.md), [P5](defense-redesign-p05-verification.md), [P6](defense-redesign-p06-verification.md), and [P7](defense-redesign-p07-verification.md) reports. The phase reports state exact fixture, scenario and implementation limits. The 20 DM definitions alone do not verify new behavior.

| Rule | Owner | Status | Evidence / remaining work |
| --- | --- | --- | --- |
| OBS-01 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| OBS-02 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| OBS-03 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| OBS-04 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| OBS-05 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| OBS-06 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| OBS-07 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| OBS-08 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| DEC-00 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| DEC-01 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| DEC-02 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| DEC-03 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| DEC-04 | P4 | verified | SpotZoneCoverageTests.DEC04_ChainedExchangeCommitsAtomicallyWithoutDuplicateOwnership |
| DEC-05 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| ALIGN-01 | P1 | verified | ALIGN01_BalancedAceKeepsCornersAndUnderneathOwnersLocal; catalog sweep |
| ALIGN-02 | P1 | verified | ALIGN02_TripsRanksReserveWeakReceiverAndBackWithExplicitLockCheck |
| ALIGN-03 | P1 | verified | ALIGN03_DM02_TwinsAndClosedTeAreBoundToVisibleGeometry |
| ALIGN-04 | P3 | verified | ManDefensiveRuntimeTests; P3 report and P4 man requalification. |
| ALIGN-05 | P1 | verified | DM01_FivePrimariesTwoSafetiesAndExplicitLegalLinebackerMismatch; ALIGN05_FiveDetachedThreatsSuggestNickelButRetainExplicitBase |
| ALIGN-06 | P1 | verified | ALIGN06_HeavyUsesActualLineGapsAndAssignsSurplusHelper |
| ALIGN-07 | P1 | verified | ALIGN07_FlipRecompilesAndTwoFlipsRestoreWithoutRandomDraws; ALIGN07_CacheUsesVisibleGeometryAndEverySupportedAdjustment |
| ALIGN-08 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| MAN-01 | P3 | verified | ManDefensiveRuntimeTests; P3 report and P4 man requalification. |
| MAN-02 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| MAN-03 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| MAN-04 | P3 | verified | ManDefensiveRuntimeTests; P3 report and P4 man requalification. |
| MAN-05 | P3 | verified | ManDefensiveRuntimeTests; P3 report and P4 man requalification. |
| MAN-06 | P3 | verified | ManDefensiveRuntimeTests; P3 report and P4 man requalification. |
| MAN-07 | P3 | verified | ManDefensiveRuntimeTests; P3 report and P4 man requalification. |
| MAN-08 | P3 | verified | ManDefensiveRuntimeTests; P3 report and P4 man requalification. |
| MAN-09 | P3 | verified | ManDefensiveRuntimeTests; P3 report and P4 man requalification. |
| Z2-01 | P4 | verified | SpotZoneCoverageTests.Z201_CornerKeepsDeclinedFadeBeyondOrdinaryUnderneathLimitAndOnlyReachableHookReplacesFlat |
| Z2-02 | P4 | verified | SpotZoneCoverageTests.DM05_Cover2SafetyTakesSeamWhileCornerRetainsFadeAndFlatIsExplicitlyConceded |
| Z2-03 | P4 | verified | SpotZoneCoverageTests.FloodKeepsDeepPrimaryIntermediateContestAndDeclaredShortConcession |
| Z3-01 | P4 | verified | SpotZoneCoverageTests.DM04_FourVerticalsKeepOutsideOwnersAndBothSeamCarriersUntilOneSafetyAccepts (30/60/120 Hz) |
| Z3-02 | P4 | verified | SpotZoneCoverageTests.FloodKeepsDeepPrimaryIntermediateContestAndDeclaredShortConcession |
| Z3-03 | P4 | verified | SpotZoneCoverageTests.DM06_PostTransferCommitsOnceThenOverlapExpiresAndCornerRecovers; PostThatTurnsHorizontalStillNeedsAcceptedTransferAndCanCrossBackWithoutDuplicateOwners |
| Z3-04 | P4 | verified | SpotZoneCoverageTests.Z304_BuzzRequiresPhysicalRotationAndKeepsThreeDeepActors |
| Z4-01 | P4 | verified | SpotZoneCoverageTests.Z4_OutsideVerticalAndInsideSeamRemainSeparateWhileShallowOutStaysUnderneath |
| Z4-02 | P4 | verified | SpotZoneCoverageTests.Z4_OutsideVerticalAndInsideSeamRemainSeparateWhileShallowOutStaysUnderneath (observed wheel restoration) |
| Z4-03 | P4 | verified | SpotZoneCoverageTests.Z403_ShallowCrossersNeverCollapseEmptyQuarterStructure |
| ZONE-01 | P4 | verified | SpotZoneCoverageTests.ZONE01_DeclinedUnderneathCarryEndsAtTimeOrLateralLimitWithExplicitConcession; DM06_UnreachablePostDestinationDeclinesWithoutDroppingDeepOwnerOrSpammingOffers |
| M3-01 | P7 | verified | AuthoredMatchCoverageTests; Outside vertical locks, apex seam ownership; cuts/stops retained. P7 report. |
| M3-02 | P7 | verified | AuthoredMatchCoverageTests; Short out/wheel retains apex until a declared reachable transfer. P7 report. |
| M3-03 | P7 | verified | AuthoredMatchCoverageTests; Shallow carry and occupied-back refusal beyond home. P7 report. |
| M3-04 | P7 | verified | AuthoredMatchCoverageTests; Complete two-receiver exchange; failed chain retains both owners. P7 report. |
| M3-05 | P7 | verified | AuthoredMatchCoverageTests; Trips five owners/two helpers; inside deep help priority. P7 report. |
| M3-06 | P7 | verified | AuthoredMatchCoverageTests; Trips shallow #3 to available weak apex; opposite back retained. P7 report. |
| M3-07 | P7 | verified | AuthoredMatchCoverageTests; Closed-side TE numbering and corner ownership across flips. P7 report. |
| QM-01 | P7 | verified | AuthoredMatchCoverageTests; Safeties accept vertical #2 at 30/60/120 Hz; later cuts retain owner. P7 report. |
| QM-02 | P7 | verified | AuthoredMatchCoverageTests; Wheel waits for actual safety acceptance; existing help retained meanwhile. P7 report. |
| QM-03 | P7 | verified | AuthoredMatchCoverageTests; Shallow handoff cannot consume the occupied back owner. P7 report. |
| QM-04 | P7 | verified | AuthoredMatchCoverageTests; Shallow outside exchange requires an available destination; vertical #2 retained. P7 report. |
| QM-05 | P7 | verified | AuthoredMatchCoverageTests; Trips distinct #2/#3/back owners; reachable poach acceptance. P7 report. |
| QM-06 | P7 | verified | AuthoredMatchCoverageTests; Shallow #3 exchange and later wheel acceptance; inside-only safety help. P7 report. |
| QM-07 | P7 | verified | AuthoredMatchCoverageTests; Isolated weak corner retains primary; no promised poach-safety help. P7 report. |
| QM-08 | P7 | verified | AuthoredMatchCoverageTests; Catalog fallback checks preserve requested/actual IDs before preview. P7 report. |
| QM-09 | P7 | verified | AuthoredMatchCoverageTests; Closed-side TE ownership and local deep help roles. P7 report. |
| XFER-01 | P4 | verified | SpotZoneCoverageTests.DM06_PostTransferCommitsOnceThenOverlapExpiresAndCornerRecovers (30/60/120 Hz) |
| XFER-02 | P4 | verified | SpotZoneCoverageTests.DeclinedOfferReevaluatesChangedReachabilityAndOccupiedDestinationCannotDropItsPrimary |
| XFER-03 | P4 | verified | SpotZoneCoverageTests.DM04_FourVerticalsKeepOutsideOwnersAndBothSeamCarriersUntilOneSafetyAccepts; DEC04_ChainedExchangeCommitsAtomicallyWithoutDuplicateOwnership |
| XFER-04 | P4 | verified | SpotZoneCoverageTests.XFER04_DM16_HumanReachabilityUsesVelocityAndRecordsLaterAbandonment; XFER04_AwayVelocityAndContactCanInvalidateOtherwiseNearbyAcceptance |
| MOVE-01 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| MOVE-02 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| MOVE-03 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| MOVE-04 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| MOVE-05 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| PRESS-01 | P2 | verified | DefensiveRuntimeTests; P2 report and P4 man requalification. |
| PRESS-02 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| PRESS-03 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| PRESS-04 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| PRESS-05 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| PRESS-06 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| PRESS-07 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| PRESS-08 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| PRESS-09 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| PRESS-10 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| PRESS-11 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| PRESS-12 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| RUN-01 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| RUN-02 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| RUN-03 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| RUN-04 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| RUN-05 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| RUN-06 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| RUN-07 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| RUN-08 | P5 | verified | RunFitTests; P5 report and 1,728 production fit cases. |
| CALL-01 | P9 | planned | Await owning phase's live behavioral tests. |
| CALL-02 | P9 | planned | Await owning phase's live behavioral tests. |
| CALL-03 | P9 | planned | Await owning phase's live behavioral tests. |
| CALL-04 | P9 | planned | Await owning phase's live behavioral tests. |
| CALL-05 | P9 | planned | Await owning phase's live behavioral tests. |
| CALL-06 | P9 | planned | Await owning phase's live behavioral tests. |
| CALL-07 | P9 | planned | Await owning phase's live behavioral tests. |
| CALL-08 | P9 | planned | Await owning phase's live behavioral tests. |
| HUMAN-01 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| HUMAN-02 | P6 | verified | PressureTests, PressureInputTests, ZonePressureTests, ConditionalPressureTests; P6 report and 2,736 production plays. |
| HUMAN-03 | P4 | verified | SpotZoneCoverageTests.XFER04_DM16_HumanReachabilityUsesVelocityAndRecordsLaterAbandonment; ZoneRuntimeKeepsHumanIdleAndFreezesBoardForPauseWhistleAndReplay |
