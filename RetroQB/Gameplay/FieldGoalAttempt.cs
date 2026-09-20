using System.Numerics;
using RetroQB.Core;

namespace RetroQB.Gameplay;

public enum KickPhase { Setup, Snap, Ready, Power, Accuracy, Flight, Result }

/// <summary>Deterministic, frame-time based kicking; no rendering or keyboard dependencies.</summary>
public sealed class FieldGoalAttempt
{
    public const float MaxDistance = 60f;
    public const float AccuracyTarget = 0.5f;
    public const float PowerTarget = 0.5f;
    public const float FlightDuration = 1.6f;
    public const float SnapDuration = 0.45f;
    public float LineOfScrimmage { get; }
    public Vector2 SnapStart => new(Constants.FieldWidth / 2, LineOfScrimmage);
    public Vector2 HolderSpot => SnapStart - new Vector2(0, 7);
    public Vector2 KickerStart => HolderSpot + new Vector2(-3, -4);
    public Vector2 SnapBallPosition => Vector2.Lerp(SnapStart, HolderSpot, SnapProgress);
    public float SnapProgress { get; private set; }
    public bool TimingActive => Phase is KickPhase.Ready or KickPhase.Power or KickPhase.Accuracy;
    public bool ShowMeter => TimingActive || Phase == KickPhase.Result;
    public float Distance { get; }
    public bool InRange => Distance <= MaxDistance;
    public string Difficulty => Distance < 30 ? "FORGIVING" : Distance <= 45 ? "MODERATE" : Distance <= 55 ? "HARD" : "VERY HARD";
    public KickPhase Phase { get; private set; }
    public float Marker { get; private set; }
    public float Power { get; private set; }
    public float MinimumPower => Distance / MaxDistance;
    public float PowerHalfWidth => Math.Clamp(0.30f - Distance * 0.004f, 0.06f, 0.23f);
    public float AccuracyHalfWidth => Math.Clamp(0.15f - Distance * 0.0015f - Power * 0.025f, 0.025f, 0.13f);
    public float FlightProgress { get; private set; }
    public float LateralError { get; private set; }
    public bool IsGood { get; private set; }
    public int OpponentPoints => IsGood ? 3 : 7;
    public string Result { get; private set; } = "";
    public string ScoringResult => $"{Result} | AWAY +{OpponentPoints}";
    internal bool ResultRecorded { get; set; }

    public FieldGoalAttempt(float lineOfScrimmage)
    {
        LineOfScrimmage = lineOfScrimmage;
        Distance = DistanceFrom(lineOfScrimmage);
    }
    public static float DistanceFrom(float lineOfScrimmage) => FieldGeometry.OpponentGoalLine - lineOfScrimmage + 17f;

    internal void BeginCpuFlight(bool good)
    {
        if (Phase != KickPhase.Setup) throw new InvalidOperationException("CPU kick already started.");
        Power = 1; SnapProgress = 1; LateralError = good ? 0 : 2;
        IsGood = good; Result = good ? "FIELD GOAL GOOD! +3" : "WIDE RIGHT";
        Phase = KickPhase.Flight;
    }

    public void PressSpace()
    {
        switch (Phase)
        {
            case KickPhase.Setup when InRange:
                Phase = KickPhase.Snap;
                break;
            case KickPhase.Ready:
                Phase = KickPhase.Power;
                break;
            case KickPhase.Power:
                LockPower();
                break;
            case KickPhase.Accuracy:
                Kick();
                break;
        }
    }

    public void Update(float dt)
    {
        dt = Math.Max(0, dt);
        if (Phase == KickPhase.Snap)
        {
            SnapProgress = Math.Min(1, SnapProgress + dt / SnapDuration);
            // Freeze at the catch, even after a long frame. A fresh press starts timing.
            if (SnapProgress >= 1) Phase = KickPhase.Ready;
        }
        else if (Phase == KickPhase.Power)
        {
            Marker = Math.Min(1, Marker + dt * 0.65f);
            // Missing the center window commits a weak kick; neither sweep can be held.
            if (Marker >= 1) LockPower();
        }
        else if (Phase == KickPhase.Accuracy)
        {
            Marker = Math.Max(0, Marker - dt * (0.55f + Power * 0.25f));
            if (Marker <= 0) Kick();
        }
        else if (Phase == KickPhase.Flight)
        {
            FlightProgress = Math.Min(1, FlightProgress + dt / FlightDuration);
            if (FlightProgress >= 1) Phase = KickPhase.Result;
        }
    }

    private void LockPower()
    {
        // The middle is ideal power. Both green edges provide just enough distance;
        // timing outside either edge falls short, matching the visible success zone.
        float timingQuality = 1 - MathF.Abs(Marker - PowerTarget) / PowerHalfWidth;
        Power = Math.Clamp((Distance + timingQuality * 8) / MaxDistance, 0, 1);
        Marker = 1;
        Phase = KickPhase.Accuracy;
    }

    private void Kick()
    {
        LateralError = (Marker - AccuracyTarget) / AccuracyHalfWidth;
        bool shortKick = Power * MaxDistance + 0.001f < Distance;
        IsGood = !shortKick && MathF.Abs(LateralError) <= 1.0001f;
        Result = shortKick ? "SHORT" : IsGood ? "FIELD GOAL GOOD! +3" : LateralError > 0 ? "WIDE RIGHT" : "WIDE LEFT";
        Phase = KickPhase.Flight;
    }
}
