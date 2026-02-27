namespace RetroQB.Stats;

public sealed class SkillStatLine
{
    public int Targets { get; set; }
    public int Receptions { get; set; }
    public int Yards { get; set; }
    public int Tds { get; set; }

    public void Reset()
    {
        Targets = 0;
        Receptions = 0;
        Yards = 0;
        Tds = 0;
    }
}
