namespace RetroQB.Gameplay.Controllers;

public sealed class SimulatedDriveController
{
    private const float IntroDurationSeconds = 0.45f;
    private const float AnimateDurationSeconds = 0.55f;
    private const float HoldDurationSeconds = 0.35f;
    private const float OutroDurationSeconds = 0.7f;

    private enum PlaybackPhase
    {
        Intro,
        Animate,
        Hold,
        Outro,
        Complete
    }

    private PlaybackPhase _phase = PlaybackPhase.Complete;
    private float _timer;
    private int _playIndex;
    private float _segmentStartY;

    public SimulatedDriveResult? ActiveDrive { get; private set; }
    public float BallWorldY { get; private set; }
    public float LineOfScrimmage => BallWorldY;
    public float FirstDownLine => MathF.Max(BallWorldY - 10f, FieldGeometry.EndZoneDepth);
    public string CurrentPlayText { get; private set; } = string.Empty;
    public string ResultBanner { get; private set; } = string.Empty;
    public List<string> PlayLog { get; } = new();
    public bool IsComplete => _phase == PlaybackPhase.Complete;

    public void Start(SimulatedDriveResult drive)
    {
        ActiveDrive = drive;
        BallWorldY = drive.StartWorldY;
        _segmentStartY = BallWorldY;
        _timer = 0f;
        _playIndex = 0;
        _phase = PlaybackPhase.Intro;
        CurrentPlayText = "OPPONENT DRIVE";
        PlayLog.Clear();
        ResultBanner = string.Empty;
    }

    public void SkipToEnd()
    {
        if (ActiveDrive == null)
        {
            return;
        }

        BallWorldY = ActiveDrive.EndingWorldY;
        _playIndex = ActiveDrive.Plays.Count;
        SetResultBanner();
        _phase = PlaybackPhase.Complete;
    }

    public void Update(float dt)
    {
        if (ActiveDrive == null || _phase == PlaybackPhase.Complete)
        {
            return;
        }

        _timer += dt;

        switch (_phase)
        {
            case PlaybackPhase.Intro:
                if (_timer >= IntroDurationSeconds)
                {
                    _timer = 0f;
                    if (ActiveDrive.Plays.Count == 0)
                    {
                        _phase = PlaybackPhase.Outro;
                        SetResultBanner();
                    }
                    else
                    {
                        _phase = PlaybackPhase.Animate;
                    }
                }
                break;

            case PlaybackPhase.Animate:
                if (_playIndex >= ActiveDrive.Plays.Count)
                {
                    _phase = PlaybackPhase.Outro;
                    _timer = 0f;
                    SetResultBanner();
                    break;
                }

                SimulatedPlay play = ActiveDrive.Plays[_playIndex];
                float t = MathF.Min(1f, _timer / AnimateDurationSeconds);
                BallWorldY = _segmentStartY + ((play.YardLine - _segmentStartY) * t);
                CurrentPlayText = FormatPlay(play);
                if (_timer >= AnimateDurationSeconds)
                {
                    BallWorldY = play.YardLine;
                    PlayLog.Add(CurrentPlayText);
                    _timer = 0f;
                    _phase = PlaybackPhase.Hold;
                }
                break;

            case PlaybackPhase.Hold:
                if (_timer >= HoldDurationSeconds)
                {
                    _timer = 0f;
                    _segmentStartY = BallWorldY;
                    _playIndex++;
                    _phase = PlaybackPhase.Animate;
                }
                break;

            case PlaybackPhase.Outro:
                if (_timer >= OutroDurationSeconds)
                {
                    _phase = PlaybackPhase.Complete;
                }
                break;
        }
    }

    private static string FormatPlay(SimulatedPlay play)
    {
        return $"{play.Down}&{MathF.Max(1f, play.Distance):F0} @ {FieldGeometry.GetYardLineDisplay(play.YardLine):F0}: {play.Description}";
    }

    private void SetResultBanner()
    {
        if (ActiveDrive == null)
        {
            return;
        }

        ResultBanner = ActiveDrive.Outcome switch
        {
            SimulatedDriveOutcome.Touchdown => "OPPONENT TOUCHDOWN (+7)",
            SimulatedDriveOutcome.FieldGoal => "OPPONENT FIELD GOAL (+3)",
            SimulatedDriveOutcome.TurnoverOnDowns => "TURNOVER ON DOWNS",
            SimulatedDriveOutcome.Interception => "OPPONENT INTERCEPTED",
            _ => "DRIVE COMPLETE"
        };

        CurrentPlayText = ResultBanner;
    }
}
