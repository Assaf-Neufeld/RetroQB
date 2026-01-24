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

public sealed class FormationFactory : IFormationFactory
{
    private static readonly float[] BaseLineX = { 0.42f, 0.46f, 0.50f, 0.54f, 0.58f };
    
    private OffensiveTeamAttributes? _currentTeamAttributes;

    public FormationResult CreateFormation(PlayDefinition play, float lineOfScrimmage, OffensiveTeamAttributes? teamAttributes = null)
    {
        _currentTeamAttributes = teamAttributes ?? OffensiveTeamAttributes.Default;
        
        var receivers = new List<Receiver>();
        var blockers = new List<Blocker>();

        var qb = new Quarterback(new Vector2(Constants.FieldWidth / 2f, ClampFormationY(lineOfScrimmage, 1.6f)), _currentTeamAttributes);
        var ball = new Ball(qb.Position);
        ball.SetHeld(qb, BallState.HeldByQB);

        if (play.Family == PlayType.QbRunFocus)
        {
            AddRunFormation(receivers, blockers, lineOfScrimmage, play.RunningBackSide);
        }
        else
        {
            AddFormation(receivers, blockers, play.Formation, lineOfScrimmage);
        }

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

    private void AddFormation(List<Receiver> receivers, List<Blocker> blockers, FormationType formation, float los)
    {
        switch (formation)
        {
            case FormationType.BaseTripsRight:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.12f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.72f, ClampFormationY(los, 1.0f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.88f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.38f, ClampFormationY(los, 0.05f)), isTightEnd: true);
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.50f, ClampFormationY(los, 5.0f)), isRunningBack: true);
                AddBaseLine(blockers, los, extraCount: 0);
                break;

            case FormationType.BaseTripsLeft:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.12f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.28f, ClampFormationY(los, 1.0f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.88f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.62f, ClampFormationY(los, 0.05f)), isTightEnd: true);
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.50f, ClampFormationY(los, 5.0f)), isRunningBack: true);
                AddBaseLine(blockers, los, extraCount: 0);
                break;

            case FormationType.BaseSplit:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.10f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.26f, ClampFormationY(los, 1.0f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.90f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.64f, ClampFormationY(los, 0.05f)), isTightEnd: true);
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.50f, ClampFormationY(los, 5.0f)), isRunningBack: true);
                AddBaseLine(blockers, los, extraCount: 0);
                break;

            case FormationType.PassSpread:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.08f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.26f, ClampFormationY(los, 1.0f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.74f, ClampFormationY(los, 1.0f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.92f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.62f, ClampFormationY(los, 0.05f)), isTightEnd: true);
                AddBaseLine(blockers, los, extraCount: 0);
                break;

            case FormationType.PassBunch:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.10f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.72f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.78f, ClampFormationY(los, 1.2f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.84f, ClampFormationY(los, 0.6f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.36f, ClampFormationY(los, 0.05f)), isTightEnd: true);
                AddBaseLine(blockers, los, extraCount: 0);
                break;

            case FormationType.PassEmpty:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.06f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.24f, ClampFormationY(los, 1.0f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.76f, ClampFormationY(los, 1.0f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.94f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.50f, ClampFormationY(los, 1.5f)), isTightEnd: true);
                AddBaseLine(blockers, los, extraCount: 0);
                break;

            case FormationType.RunPowerRight:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.10f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.55f, ClampFormationY(los, 4.0f)), isRunningBack: true);
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.66f, ClampFormationY(los, 0.05f)), isTightEnd: true);
                AddBaseLine(blockers, los, extraCount: 2);
                break;

            case FormationType.RunPowerLeft:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.90f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.45f, ClampFormationY(los, 4.0f)), isRunningBack: true);
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.34f, ClampFormationY(los, 0.05f)), isTightEnd: true);
                AddBaseLine(blockers, los, extraCount: 2);
                break;

            case FormationType.RunIForm:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.12f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.50f, ClampFormationY(los, 3.5f)), isRunningBack: true);
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.64f, ClampFormationY(los, 0.05f)), isTightEnd: true);
                AddBaseLine(blockers, los, extraCount: 2);
                break;

            default:
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.12f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.72f, ClampFormationY(los, 1.0f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.88f, ClampFormationY(los, 0.3f)));
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.38f, ClampFormationY(los, 0.05f)), isTightEnd: true);
                AddReceiver(receivers, new Vector2(Constants.FieldWidth * 0.50f, ClampFormationY(los, 5.0f)), isRunningBack: true);
                AddBaseLine(blockers, los, extraCount: 0);
                break;
        }
    }

    private void AddRunFormation(List<Receiver> receivers, List<Blocker> blockers, float los, int runningBackSide)
    {
        var formation = runningBackSide switch
        {
            1 => FormationType.RunPowerRight,
            -1 => FormationType.RunPowerLeft,
            _ => FormationType.RunIForm
        };
        AddFormation(receivers, blockers, formation, los);
    }

    private void AddReceiver(List<Receiver> receivers, Vector2 position, bool isRunningBack = false, bool isTightEnd = false)
    {
        receivers.Add(new Receiver(receivers.Count, position, isRunningBack, isTightEnd, _currentTeamAttributes));
    }

    private void AddBaseLine(List<Blocker> blockers, float los, int extraCount)
    {
        foreach (float x in BaseLineX)
        {
            blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * x, los - 0.1f), _currentTeamAttributes));
        }

        if (extraCount >= 1)
        {
            blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.36f, los - 0.1f), _currentTeamAttributes));
        }
        if (extraCount >= 2)
        {
            blockers.Add(new Blocker(new Vector2(Constants.FieldWidth * 0.64f, los - 0.1f), _currentTeamAttributes));
        }
    }
}
