using System.Text.Json;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Development;

internal static class DefenseFormationMatrix
{
    public static int Run(ScenarioLaunchOptions options)
    {
        if (!options.Headless || options.Engine != DefensiveEngineMode.MatchDevelopment)
            throw new ArgumentException("defense-formation-matrix requires --headless --engine match.");
        string output = Path.GetFullPath(options.OutputDirectory ?? $"artifacts/defense-redesign/p08/runtime-{options.Hz}");
        Directory.CreateDirectory(output); var rows = new List<object>(); int failures = 0;
        foreach (var package in Enum.GetValues<DefensePackage>())
        foreach (var front in Enum.GetValues<DefensiveFront>())
        foreach (var call in DefensiveCallDefinition.All.Where(c => front == DefensiveFront.Even || c.RushCount == 4))
        foreach (string play in new[] { "pass.mesh", "pass.gun-trips.mesh-wheel", "run.stretch-right" })
        foreach (bool human in new[] { false, true })
        foreach (bool flipped in new[] { false, true })
            Run(play,call.Id,package,front,human,flipped,null);
        // Additional dime pressure replacement/drop/conditional packages, both sides.
        foreach (var kind in new[] { PressureKind.Mike, PressureKind.Nickel, PressureKind.ZoneFive, PressureKind.Simulated, PressureKind.GreenDog, PressureKind.Delayed, PressureKind.Stunt })
        foreach (var side in new[] { FieldSide.Left,FieldSide.Right })
        foreach (bool human in new[] { false,true })
            Run("pass.gun-doubles.pa-cross",kind is PressureKind.ZoneFive or PressureKind.Simulated ? "def.cover3" : kind is PressureKind.Mike or PressureKind.Nickel ? "def.edge" : "def.cover1",DefensePackage.Dime,DefensiveFront.Even,human,false,new(kind,side));
        File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(new { DefensiveRuntime.Version, options.Seed, options.Hz, Cases=rows.Count, Failures=failures, Rows=rows },DefenseRedesignMatrix.Json));
        Console.WriteLine($"Formations: {rows.Count} cases; {failures} failures; {output}"); return failures==0?0:1;
        void Run(string play,string call,DefensePackage package,DefensiveFront front,bool human,bool flipped,PressureSelection? pressure)
        {
            var sample=DefenseRuntimeMatrix.Exercise(play,call,options.Seed,options.Hz,human,
                human?DefenseInputMode.AssignmentFollowing:DefenseInputMode.FullAI,options.Engine,flipped?98:50,flipped,pressure,package,front,advanceHumanRunner:true);
            if(sample.Failure!=null) failures++;
            if(sample.Failure!=null) File.WriteAllText(Path.Combine(output,$"{play}-{call}-{package}-{front}-{human}-{flipped}-{pressure?.Kind}-{pressure?.Side}.json"),JsonSerializer.Serialize(sample,DefenseRedesignMatrix.Json));
            rows.Add(new {sample.Play,sample.Call,Package=package,Front=front,Pressure=pressure,sample.HumanDefense,Flipped=flipped,sample.ResolvedCall,sample.Check,sample.Seconds,sample.Outcome,sample.Failure});
        }
    }
}
