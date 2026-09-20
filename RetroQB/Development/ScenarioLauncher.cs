using Raylib_cs;
using RetroQB.Core;

namespace RetroQB.Development;

internal static class ScenarioLauncher
{
    public static int Run(ScenarioLaunchOptions options)
    {
        if (options.Definition.Name.StartsWith("release-")) return ReleaseVerification.Run(options);
        if (options.Definition.FullMatch) return FullMatchScenarioLauncher.Run(options);
        if (options.Definition.Defending) return DefenseScenarioLauncher.Run(options);
        using var run = new ScenarioRun(options.Definition, options.Seed, options.OutputDirectory);
        if (options.Headless)
        {
            while (!run.Complete) run.Step();
        }
        else if (options.Capture)
        {
            Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
            Raylib.InitWindow(1440, 900, "RetroQB development capture");
            try
            {
                var captured = new HashSet<string>();
                Capture();
                while (!run.Complete) { run.Step(); Capture(); }

                void Capture()
                {
                    var state = run.Current;
                    string key = $"{state.State}-{state.BallState}-{state.KickPhase}-{state.Exchange}";
                    if (!captured.Add(key) && !run.Complete) return;
                    var target = Raylib.LoadRenderTexture(1440, 900);
                    try
                    {
                        Raylib.BeginTextureMode(target);
                        Raylib.ClearBackground(Palette.Background);
                        run.Session.Draw();
                        Raylib.EndTextureMode();
                        var image = Raylib.LoadImageFromTexture(target.Texture);
                        try
                        {
                            Raylib.ImageFlipVertical(ref image);
                            string path = Path.Combine(run.OutputDirectory, $"{run.Tick:D5}-{key.Replace(' ', '-')}.png");
                            if (!Raylib.ExportImage(image, path)) throw new IOException($"Could not export {path}");
                        }
                        finally { Raylib.UnloadImage(image); }
                    }
                    finally { Raylib.UnloadRenderTexture(target); }
                }
            }
            finally { Raylib.CloseWindow(); }
        }
        else
        {
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
            Raylib.InitWindow(1440, 900, $"RetroQB development: {options.Definition.Name} (seed {options.Seed})");
            Raylib.SetTargetFPS(Constants.TargetFps);
            try
            {
                double accumulator = 0;
                while (!Raylib.WindowShouldClose())
                {
                    accumulator += Math.Min(Raylib.GetFrameTime(), .25f);
                    while (accumulator >= ScenarioDefinition.FixedStep && !run.Complete)
                    {
                        run.Step();
                        accumulator -= ScenarioDefinition.FixedStep;
                    }
                    Raylib.BeginDrawing();
                    Raylib.ClearBackground(Palette.Background);
                    run.Session.Draw();
                    Raylib.DrawText($"SCRIPTED BASELINE | seed {options.Seed} | tick {run.Tick} | Esc: close", 12, 8, 14, Palette.White);
                    Raylib.EndDrawing();
                }
            }
            finally { Raylib.CloseWindow(); }
        }
        string report = run.WriteReport();
        Console.WriteLine($"{run.Definition.Name}: {run.Current.Result} | report: {report}");
        return run.Complete && run.Failure == null ? 0 : 1;
    }
}
