namespace RetroQB.Gameplay;

public enum SimulatedDriveOutcome
{
    Touchdown,
    FieldGoal,
    TurnoverOnDowns,
    Interception
}

/// <summary>
/// A single play within a simulated opponent drive.
/// </summary>
/// <param name="Down">Current down (1-4).</param>
/// <param name="Distance">Yards to first down.</param>
/// <param name="YardLine">World-Y position of the ball after this play.</param>
/// <param name="Description">Human-readable play description.</param>
/// <param name="Gain">Net yards gained (positive = towards opponent goal).</param>
/// <param name="IsFirstDown">Whether this play resulted in a first down.</param>
public sealed record SimulatedPlay(
    int Down,
    float Distance,
    float YardLine,
    string Description,
    float Gain,
    bool IsFirstDown);

/// <summary>
/// Complete result of a simulated opponent drive.
/// All yard-line values are in world-Y coordinates.
/// </summary>
/// <param name="Plays">Ordered list of plays in the drive.</param>
/// <param name="Outcome">How the drive ended.</param>
/// <param name="PointsScored">Points scored by the opponent (0, 3, or 7).</param>
/// <param name="StartWorldY">World-Y where the opponent drive began.</param>
/// <param name="EndingWorldY">World-Y where the ball ended after the drive.</param>
/// <param name="PlayerNextStartWorldY">World-Y where the player's next drive starts.</param>
public sealed record SimulatedDriveResult(
    List<SimulatedPlay> Plays,
    SimulatedDriveOutcome Outcome,
    int PointsScored,
    float StartWorldY,
    float EndingWorldY,
    float PlayerNextStartWorldY);
