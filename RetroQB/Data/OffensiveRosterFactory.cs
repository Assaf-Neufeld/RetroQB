using System;
using System.Collections.Generic;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Data;

public static class OffensiveRosterFactory
{
    private static readonly float[] WideReceiverSpeedOffsets = [0.06f, 0.03f, 0f, -0.03f];
    private static readonly float[] WideReceiverSkillOffsets = [0.05f, 0.02f, 0f, -0.03f];
    private static readonly ReceiverSlot[] WideReceiverSlots = [ReceiverSlot.WR1, ReceiverSlot.WR2, ReceiverSlot.WR3, ReceiverSlot.WR4];

    public static OffensiveRoster Create(
        OffensiveTeamSkills skills,
        string qbName,
        IReadOnlyList<string> wideReceiverNames,
        string tightEndName,
        string runningBackName)
    {
        if (wideReceiverNames.Count < WideReceiverSlots.Length)
        {
            throw new ArgumentException("Four wide receiver names are required.", nameof(wideReceiverNames));
        }

        float rbPower = ClampSkill(skills.RbPower);
        float rbSpeed = ClampSkill(skills.RbSpeed);
        float qbThrowPower = ClampSkill(skills.QbThrowPower);
        float qbThrowAccuracy = ClampSkill(skills.QbThrowAccuracy);
        float wrSpeed = ClampSkill(skills.WrSpeed);
        float wrSkill = ClampSkill(skills.WrSkill);
        float olStrength = ClampSkill(skills.OlStrength);
        float qbMobility = ClampSkill(rbSpeed * 0.5f + wrSpeed * 0.2f + qbThrowAccuracy * 0.2f + (1f - qbThrowPower) * 0.1f);

        float wrBaseSpeedFactor = Lerp(0.74f, 1.28f, wrSpeed);
        float wrBaseCatch = Lerp(0.48f, 0.93f, wrSkill);
        float wrBaseCatchRadius = Lerp(0.78f, 1.18f, wrSkill);
        float wrBaseRouteSkill = Lerp(0.44f, 0.98f, wrSkill);

        var wideReceivers = new Dictionary<ReceiverSlot, WrProfile>(WideReceiverSlots.Length);
        for (int i = 0; i < WideReceiverSlots.Length; i++)
        {
            float speedFactor = Math.Clamp(wrBaseSpeedFactor + WideReceiverSpeedOffsets[i], 0.68f, 1.38f);
            float catchAbility = Math.Clamp(wrBaseCatch + WideReceiverSkillOffsets[i], 0.40f, 0.97f);
            float catchRadius = Math.Clamp(wrBaseCatchRadius + WideReceiverSkillOffsets[i] * 0.4f, 0.76f, 1.22f);
            float routeSkill = Math.Clamp(wrBaseRouteSkill + WideReceiverSkillOffsets[i], 0.40f, 0.99f);
            wideReceivers[WideReceiverSlots[i]] = new WrProfile
            {
                Name = wideReceiverNames[i],
                Speed = Constants.WrSpeed * speedFactor,
                CatchingAbility = catchAbility,
                CatchRadius = catchRadius,
                RouteSkill = routeSkill
            };
        }

        float teReceivingBlend = Math.Clamp(wrSkill * 0.7f + wrSpeed * 0.3f, 0f, 1f);
        float teSpeedFactor = Lerp(0.82f, 1.12f, wrSpeed);
        float teCatching = Lerp(0.50f, 0.86f, teReceivingBlend);
        float teCatchRadius = Lerp(0.84f, 1.12f, wrSkill);
        float teBlocking = Lerp(0.80f, 1.32f, olStrength);

        float rbSpeedFactor = Lerp(0.78f, 1.30f, rbSpeed);
        float rbCatching = Lerp(0.46f, 0.80f, Math.Clamp(rbSpeed * 0.55f + wrSkill * 0.45f, 0f, 1f));
        float rbCatchRadius = Lerp(0.82f, 1.10f, Math.Clamp(rbSpeed * 0.6f + wrSkill * 0.4f, 0f, 1f));
        float rbTackleBreak = Lerp(0.12f, 0.52f, rbPower);

        float olSpeedFactor = Lerp(0.78f, 1.18f, olStrength);
        float olBlocking = Lerp(0.70f, 1.42f, olStrength);

        return new OffensiveRoster
        {
            Quarterback = new QbProfile
            {
                Name = qbName,
                MaxSpeed = Constants.QbMaxSpeed * Lerp(0.82f, 1.14f, qbMobility),
                SprintSpeed = Constants.QbSprintSpeed * Lerp(0.84f, 1.16f, qbMobility),
                Acceleration = Constants.QbAcceleration * Lerp(0.82f, 1.18f, qbMobility),
                Friction = Constants.QbFriction,
                ArmStrength = Lerp(0.72f, 1.34f, qbThrowPower),
                Accuracy = Lerp(1.20f, 0.52f, qbThrowAccuracy),
                DeepAccuracyPenalty = Lerp(1.38f, 1.00f, qbThrowAccuracy)
            },
            WideReceivers = wideReceivers,
            TightEnds = new Dictionary<ReceiverSlot, TeProfile>
            {
                [ReceiverSlot.TE1] = new TeProfile
                {
                    Name = tightEndName,
                    Speed = Constants.TeSpeed * teSpeedFactor,
                    CatchingAbility = teCatching,
                    CatchRadius = teCatchRadius,
                    BlockingStrength = teBlocking
                }
            },
            RunningBacks = new Dictionary<ReceiverSlot, RbProfile>
            {
                [ReceiverSlot.RB1] = new RbProfile
                {
                    Name = runningBackName,
                    Speed = Constants.RbSpeed * rbSpeedFactor,
                    CatchingAbility = rbCatching,
                    CatchRadius = rbCatchRadius,
                    TackleBreakChance = rbTackleBreak
                }
            },
            OffensiveLine = new OLineProfile
            {
                Speed = Constants.OlSpeed * olSpeedFactor,
                BlockingStrength = olBlocking
            }
        };
    }

    public static float ComputeOverallRating(OffensiveTeamSkills skills)
    {
        return 0.84f + ClampSkill(skills.Average) * 0.34f;
    }

    private static float ClampSkill(float value) => Math.Clamp(value, 0f, 1f);

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * ClampSkill(t);
    }
}