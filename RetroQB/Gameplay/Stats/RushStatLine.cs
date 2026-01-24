namespace RetroQB.Gameplay.Stats;

public sealed class RushStatLine
{
    public int Yards { get; set; }
    public int Tds { get; set; }

    public void Reset()
    {
        Yards = 0;
        Tds = 0;
    }
}
