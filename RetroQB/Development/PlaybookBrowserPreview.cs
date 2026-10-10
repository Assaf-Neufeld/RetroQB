using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Rendering;

namespace RetroQB.Development;

internal static class PlaybookBrowserPreview
{
    public static int Run(ScenarioLaunchOptions options)
    {
        if (!options.Capture) throw new ArgumentException("offense-browser-preview requires --capture.");
        string output = Path.GetFullPath(options.OutputDirectory ?? "artifacts/offensive-improvements/step-3/layouts");
        Directory.CreateDirectory(output); int count = 0;
        Raylib.SetConfigFlags(ConfigFlags.HiddenWindow | ConfigFlags.ResizableWindow);
        Raylib.InitWindow(1440, 900, "Playbook browser verification");
        try
        {
            foreach (var (width, height) in new[] { (1000, 700), (1280, 720), (1440, 900), (1920, 1080) })
            foreach (string state in new[] { "catalog", "screen", "bootleg-flipped", "favorites", "empty", "filters", "selected" })
            {
                Raylib.SetWindowSize(width, height);
                var user = TeamCatalog.Get("ballers"); var opponent = TeamCatalog.ForStage(SeasonStage.RegularSeason);
                string settings = Path.Combine(output, $"favorites-{width}-{state}.json");
                File.WriteAllText(settings, "{\"Version\":1,\"Favorites\":[\"pass.gun-doubles.rb-screen\",\"pass.ace.bootleg\"]}");
                var s = new FullMatchSession(new ScriptedInput(), options.Seed, new(user, opponent, user.Id, start: new(50)),
                    favorites: new(PlaybookBuilder.BuildCatalog(), settings));
                s.Update(0, new(Browser: new(Toggle: true)));
                string id = state == "bootleg-flipped" ? "pass.ace.bootleg" : "pass.gun-doubles.rb-screen";
                int index = s.Browser.Results.ToList().FindIndex(p => p.Id == id);
                s.Update(0, new(Browser: new(Move: index - s.Browser.Cursor)));
                if (state == "bootleg-flipped") s.Update(0, new(Browser: new(Flip: true)));
                if (state == "favorites") s.Update(0, new(Browser: new(Favorites: true)));
                if (state == "filters") s.Update(0, new(Browser: new(Category: true, Family: true)));
                if (state == "empty") s.Update(0, new(Browser: new(Category: true, Favorites: true)));
                if (state == "selected") s.Update(0, new(Browser: new(Select: true)));
                Capture(() => new FullMatchRenderer().Draw(s), $"{state}-{width}x{height}.png");

                // Also exercise the retained legacy renderer and input path.
                if (state == "catalog")
                {
                    var input = new ScriptedInput();
                    using var legacy = new GameSession(input, new(options.Seed), new PlayerRecordStore(Path.Combine(output, "records.json")));
                    legacy.StartScenario(new(50), "pass.gun-doubles.rb-screen", user.Offense, opponent.Defense);
                    input.Frame = new(Browser: new(Toggle: true)); legacy.Update(0);
                    Capture(legacy.Draw, $"legacy-{width}x{height}.png");
                }
                void Capture(Action draw, string filename)
                {
                    var texture = Raylib.LoadRenderTexture(width, height);
                    Raylib.BeginTextureMode(texture); draw(); Raylib.EndTextureMode();
                    var image = Raylib.LoadImageFromTexture(texture.Texture); Raylib.ImageFlipVertical(ref image);
                    if (!Raylib.ExportImage(image, Path.Combine(output, filename))) throw new IOException(filename);
                    Raylib.UnloadImage(image); Raylib.UnloadRenderTexture(texture); count++;
                }
            }
        }
        finally { Raylib.CloseWindow(); }
        Console.WriteLine($"Browser layouts: {count} captures; {output}"); return 0;
    }
}
