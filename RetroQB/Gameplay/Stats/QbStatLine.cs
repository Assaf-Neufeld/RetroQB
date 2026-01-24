namespace RetroQB.Gameplay.Stats;

public sealed class QbStatLine
{
    public int Completions { get; set; }
    public int Attempts { get; set; }
    public int PassYards { get; set; }
    public int PassTds { get; set; }
    public int Interceptions { get; set; }
    public int RushYards { get; set; }
    public int RushTds { get; set; }

    public void Reset()
    {
        Completions = 0;
        Attempts = 0;
        PassYards = 0;
        PassTds = 0;
        Interceptions = 0;
        RushYards = 0;
        RushTds = 0;
    }
}
