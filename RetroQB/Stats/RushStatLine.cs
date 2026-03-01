namespace RetroQB.Stats;

public sealed class RushStatLine
{
    public int Attempts { get; set; }
    public int Yards { get; set; }
    public int Tds { get; set; }

    public void Reset()
    {
        Attempts = 0;
        Yards = 0;
        Tds = 0;
    }
}
