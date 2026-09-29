using System.Text.Json;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Development;

internal static class DefenseStrategyMatrix
{
    public static int Run(ScenarioLaunchOptions options)
    {
        if(!options.Headless||options.Engine!=DefensiveEngineMode.StrategyDevelopment)
            throw new ArgumentException("defense-strategy-matrix requires --headless --engine strategy.");
        string output=Path.GetFullPath(options.OutputDirectory??$"artifacts/defense-redesign/p09/runtime-{options.Hz}");
        Directory.CreateDirectory(output);var rows=new List<object>();int failures=0;
        foreach(var play in new PlayManager().Catalog.Plays)
        foreach(bool flipped in new[]{false,true})
        foreach(int seed in new[]{options.Seed,options.Seed+7})
        {
            var s=DefenseRuntimeMatrix.Exercise(play.Id,"cpu.auto",seed,options.Hz,false,DefenseInputMode.FullAI,
                options.Engine,flipped?98:50,flipped,advanceHumanRunner:true,selectCpuDefense:true);
            if(s.Failure!=null)
            {
                failures++;
                File.WriteAllText(Path.Combine(output,$"{play.Id}-{seed}-{flipped}.json"),JsonSerializer.Serialize(s,DefenseRedesignMatrix.Json));
            }
            rows.Add(new {s.Play,s.Seed,Flipped=flipped,s.ResolvedCall,s.Check,s.Seconds,s.Outcome,s.Failure,s.StrategyScore,s.OutcomeMetadata});
        }
        File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(new {Version="defense-caller-p9-v1",options.Hz,Cases=rows.Count,Failures=failures,Rows=rows},DefenseRedesignMatrix.Json));
        Console.WriteLine($"Strategy: {rows.Count} cases; {failures} failures; {output}");return failures==0?0:1;
    }
}
