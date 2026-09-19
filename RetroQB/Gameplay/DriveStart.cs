namespace RetroQB.Gameplay;

/// <summary>An explicit starting series, expressed in yards from the offense's own goal.</summary>
public sealed record DriveStart(float OwnYardLine = 20, int Down = 1, float Distance = 10)
{
    public void Validate()
    {
        if (!float.IsFinite(OwnYardLine) || OwnYardLine <= 0 || OwnYardLine >= 100)
            throw new ArgumentOutOfRangeException(nameof(OwnYardLine));
        if (Down is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(Down));
        if (!float.IsFinite(Distance) || Distance <= 0 || Distance > 100 - OwnYardLine)
            throw new ArgumentOutOfRangeException(nameof(Distance));
    }
}
