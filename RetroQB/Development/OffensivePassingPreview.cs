using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Rendering;

namespace RetroQB.Development;

internal static class OffensivePassingPreview
{
    public static int Run(ScenarioLaunchOptions options)
    {
        if (!options.Capture) throw new ArgumentException("offense-passing-preview requires --capture.");
        string output = Path.GetFullPath(options.OutputDirectory ?? "artifacts/offensive-improvements/step-1/layouts");
        Directory.CreateDirectory(output);
        Raylib.SetConfigFlags(ConfigFlags.HiddenWindow | ConfigFlags.ResizableWindow);
        Raylib.InitWindow(1440, 900, "Passing concept verification");
        try
        {
            foreach (var (width, height) in new[] { (1000, 700), (1280, 720), (1440, 900), (1920, 1080) })
            foreach (string call in options.Definition.Name == "offense-designed-preview"
                ? new[] { "run.gun-doubles.power", "run.gun-doubles.counter", "run.gun-doubles.stretch", "pass.gun-doubles.rb-screen", "pass.gun-trips.wr-screen", "pass.ace.bootleg", "pass.gun-doubles.rollout" }
                : new[] { "pass.gun-trips.mesh-wheel", "pass.gun-doubles.spacing", "pass.gun-doubles.sail", "pass.i-pro.mills" })
            foreach (bool flipped in new[] { false, true })
            {
                Raylib.SetWindowSize(width, height);
                var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
                var session = new FullMatchSession(input, options.Seed,
                    new(user, cpu, user.Id, start: new(50), quarterSeconds: 120, regulationPeriods: 2));
                var drive = session.Drive;
                bool run = drive.Plays.Catalog[call].Family == PlayType.Run;
                drive.Plays.SetCallSheet(new(drive.Plays.Catalog,
                    run ? drive.Plays.CallSheet.PassIds : new[] { call }.Concat(drive.Plays.CallSheet.PassIds.Where(id => id != call)).Take(10),
                    run ? new[] { call }.Concat(drive.Plays.CallSheet.RunIds.Where(id => id != call)).Take(10) : drive.Plays.CallSheet.RunIds));
                drive.SelectOffense(0, run: run);
                if (flipped) drive.SelectOffense(0, run: run, flip: true);
                var target = Raylib.LoadRenderTexture(width, height);
                Raylib.BeginTextureMode(target); new FullMatchRenderer().Draw(session); Raylib.EndTextureMode();
                var image = Raylib.LoadImageFromTexture(target.Texture); Raylib.ImageFlipVertical(ref image);
                string path = Path.Combine(output, $"{call}-{width}x{height}{(flipped ? "-flipped" : "")}.png");
                if (!Raylib.ExportImage(image, path)) throw new IOException(path);
                Raylib.UnloadImage(image); Raylib.UnloadRenderTexture(target);
            }
        }
        finally { Raylib.CloseWindow(); }
        Console.WriteLine($"Offensive previews complete; {output}");
        return 0;
    }
}
