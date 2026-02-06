using System.Numerics;
using RetroQB.Core;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

public interface IFormationFactory
{
    FormationResult CreateFormation(PlayDefinition play, float lineOfScrimmage, OffensiveTeamAttributes? teamAttributes = null);
}

public sealed class FormationResult
{
    public Quarterback Qb { get; init; } = null!;
    public Ball Ball { get; init; } = null!;
    public List<Receiver> Receivers { get; init; } = new();
    public List<Blocker> Blockers { get; init; } = new();
}

/// <summary>
/// Describes a single receiver slot within a formation: its X position (as a fraction of field width),
/// its Y offset behind the line of scrimmage, and which slot label it occupies.
/// </summary>
internal readonly record struct ReceiverPlacement(float XFraction, float YOffset, ReceiverSlot Slot);

/// <summary>
/// A complete formation definition: which receivers are placed where and
/// how many extra linemen are added beyond the base five.
/// </summary>
internal readonly record struct FormationData(ReceiverPlacement[] Receivers, int ExtraLinemen);

public sealed class FormationFactory : IFormationFactory
{
    private static readonly float[] BaseLineX = { 0.42f, 0.46f, 0.50f, 0.54f, 0.58f };

    // ── Formation look-up table ──────────────────────────────────────────
    private static readonly Dictionary<FormationType, FormationData> Formations = new()
    {
        [FormationType.BaseTripsRight] = new(new ReceiverPlacement[]
        {
            new(0.12f, 0.3f, ReceiverSlot.WR1),
            new(0.72f, 1.0f, ReceiverSlot.WR2),
            new(0.88f, 0.3f, ReceiverSlot.WR3),
            new(0.38f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.BaseTripsLeft] = new(new ReceiverPlacement[]
        {
            new(0.12f, 0.3f, ReceiverSlot.WR1),
            new(0.28f, 1.0f, ReceiverSlot.WR2),
            new(0.88f, 0.3f, ReceiverSlot.WR3),
            new(0.62f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.BaseSplit] = new(new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.26f, 1.0f, ReceiverSlot.WR2),
            new(0.90f, 0.3f, ReceiverSlot.WR3),
            new(0.64f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.BaseBunchRight] = new(new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.74f, 0.3f, ReceiverSlot.WR2),
            new(0.80f, 1.2f, ReceiverSlot.WR3),
            new(0.36f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.BaseBunchLeft] = new(new ReceiverPlacement[]
        {
            new(0.20f, 1.2f, ReceiverSlot.WR1),
            new(0.26f, 0.3f, ReceiverSlot.WR2),
            new(0.90f, 0.3f, ReceiverSlot.WR3),
            new(0.64f, 0.05f, ReceiverSlot.TE1),
            new(0.50f, 5.0f, ReceiverSlot.RB1),
        }, ExtraLinemen: 0),

        [FormationType.PassSpread] = new(new ReceiverPlacement[]
        {
            new(0.08f, 0.3f, ReceiverSlot.WR1),
            new(0.26f, 1.0f, ReceiverSlot.WR2),
            new(0.74f, 1.0f, ReceiverSlot.WR3),
            new(0.92f, 0.3f, ReceiverSlot.WR4),
            new(0.62f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),

        [FormationType.PassBunchRight] = new(new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.72f, 0.3f, ReceiverSlot.WR2),
            new(0.78f, 1.2f, ReceiverSlot.WR3),
            new(0.84f, 0.6f, ReceiverSlot.WR4),
            new(0.36f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),

        [FormationType.PassBunchLeft] = new(new ReceiverPlacement[]
        {
            new(0.16f, 0.6f, ReceiverSlot.WR1),
            new(0.22f, 1.2f, ReceiverSlot.WR2),
            new(0.28f, 0.3f, ReceiverSlot.WR3),
            new(0.90f, 0.3f, ReceiverSlot.WR4),
            new(0.64f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),

        [FormationType.PassEmpty] = new(new ReceiverPlacement[]
        {
            new(0.06f, 0.3f, ReceiverSlot.WR1),
            new(0.24f, 1.0f, ReceiverSlot.WR2),
            new(0.76f, 1.0f, ReceiverSlot.WR3),
            new(0.94f, 0.3f, ReceiverSlot.WR4),
            new(0.64f, 0.6f, ReceiverSlot.TE1),
        }, ExtraLinemen: 0),

        [FormationType.RunPowerRight] = new(new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.55f, 4.0f, ReceiverSlot.RB1),
            new(0.66f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 2),

        [FormationType.RunPowerLeft] = new(new ReceiverPlacement[]
        {
            new(0.90f, 0.3f, ReceiverSlot.WR1),
            new(0.45f, 4.0f, ReceiverSlot.RB1),
            new(0.34f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 2),

        [FormationType.RunIForm] = new(new ReceiverPlacement[]
        {
            new(0.12f, 0.3f, ReceiverSlot.WR1),
            new(0.50f, 3.5f, ReceiverSlot.RB1),
            new(0.64f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 2),

        [FormationType.RunSweepRight] = new(new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.60f, 4.6f, ReceiverSlot.RB1),
            new(0.72f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 2),

        [FormationType.RunSweepLeft] = new(new ReceiverPlacement[]
        {
            new(0.90f, 0.3f, ReceiverSlot.WR1),
            new(0.40f, 4.6f, ReceiverSlot.RB1),
            new(0.28f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 2),

        [FormationType.RunStretchRight] = new(new ReceiverPlacement[]
        {
            new(0.10f, 0.3f, ReceiverSlot.WR1),
            new(0.56f, 3.2f, ReceiverSlot.RB1),
            new(0.66f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 2),

        [FormationType.RunStretchLeft] = new(new ReceiverPlacement[]
        {
            new(0.90f, 0.3f, ReceiverSlot.WR1),
            new(0.44f, 3.2f, ReceiverSlot.RB1),
            new(0.34f, 0.05f, ReceiverSlot.TE1),
        }, ExtraLinemen: 2),
    };

    // Default formation used when the requested type is not in the table.
    private static readonly FormationData DefaultFormation = Formations[FormationType.BaseTripsRight];

    private OffensiveTeamAttributes? _currentTeamAttributes;

    public FormationResult CreateFormation(PlayDefinition play, float lineOfScrimmage, OffensiveTeamAttributes? teamAttributes = null)
    {
        _currentTeamAttributes = teamAttributes ?? OffensiveTeamAttributes.Default;

        var receivers = new List<Receiver>();
        var blockers = new List<Blocker>();

        var qb = new Quarterback(new Vector2(Constants.FieldWidth / 2f, ClampFormationY(lineOfScrimmage, 1.6f)), _currentTeamAttributes);
        var ball = new Ball(qb.Position);
        ball.SetHeld(qb, BallState.HeldByQB);

        FormationType formation = play.Formation;
        if (play.Family == PlayType.Run)
        {
            formation = ResolveRunFormation(formation, play.RunningBackSide);
        }

        ApplyFormation(receivers, blockers, formation, lineOfScrimmage);

        return new FormationResult
        {
            Qb = qb,
            Ball = ball,
            Receivers = receivers,
            Blockers = blockers
        };
    }

    private static float ClampFormationY(float los, float offset)
    {
        float y = los - offset;
        return MathF.Max(0.6f, y);
    }

    private void ApplyFormation(List<Receiver> receivers, List<Blocker> blockers, FormationType formation, float los)
    {
        var data = Formations.GetValueOrDefault(formation, DefaultFormation);

        foreach (var p in data.Receivers)
        {
            var position = new Vector2(Constants.FieldWidth * p.XFraction, ClampFormationY(los, p.YOffset));
            receivers.Add(new Receiver(receivers.Count, p.Slot, position, _currentTeamAttributes));
        }

        AddBaseLine(blockers, los, data.ExtraLinemen);
    }

    private static FormationType ResolveRunFormation(FormationType formation, int runningBackSide)
    {
        if (formation is FormationType.RunPowerRight
            or FormationType.RunPowerLeft
            or FormationType.RunIForm
            or FormationType.RunSweepRight
            or FormationType.RunSweepLeft
            or FormationType.RunStretchRight
            or FormationType.RunStretchLeft)
        {
            return formation;
        }

        return runningBackSide switch
        {
            1 => FormationType.RunPowerRight,
            -1 => FormationType.RunPowerLeft,
            _ => FormationType.RunIForm
        };
    }

    private static void AddBaseLine(List<Blocker> blockers, float los, int extraCount, OffensiveTeamAttributes? team = null)
    {
        foreach (float x in BaseLineX)
        {
            blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * x, los - 0.1f), team));
        }

        if (extraCount >= 1)
        {
            blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.36f, los - 0.1f), team));
        }
        if (extraCount >= 2)
        {
            blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.64f, los - 0.1f), team));
        }
    }
}
