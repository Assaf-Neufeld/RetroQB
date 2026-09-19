using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Input;
using RetroQB.Rendering;

namespace RetroQB.Development;

internal static class DefenseScenarioLauncher
{
    public static int Run(ScenarioLaunchOptions options)
    {
        var script = new ScriptedInput();
        var keyboard = new InputManager();
        bool automated = options.Headless || options.Capture;
        var drive = new DefensiveDrive(automated ? script : keyboard, options.Seed, options.Definition.Start,
            string.IsNullOrEmpty(options.Definition.PlayId) ? null : options.Definition.PlayId, options.Definition.ScriptedOffense);
        string output = Path.GetFullPath(options.OutputDirectory ?? Path.Combine(Path.GetTempPath(), "RetroQB", "defense", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        var trace = new List<object>();
        int tick = 0; float deadTime = 0;
        string? failure = null;
        var field = new FieldRenderer();
        bool paused = false;
        bool done() => drive.Complete || failure != null;
        void step()
        {
            if (paused || done()) return;
            if (!drive.Live)
            {
                deadTime += ScenarioDefinition.FixedStep;
                if (deadTime >= 1 || !automated && keyboard.IsSpacePressed())
                { drive.Continue(); drive.Snap(); deadTime = 0; }
            }
            if (automated)
            {
                var target = drive.Actors.Ball.Holder?.Position ?? drive.Actors.Ball.Position;
                Vector2 delta = target - drive.Linebacker.Position;
                script.Frame = new(Movement: delta.LengthSquared() > .1f ? Vector2.Normalize(delta) : Vector2.Zero);
            }
            drive.Update(ScenarioDefinition.FixedStep); tick++;
            if (drive.LiveSeconds > 30) failure = "Live play exceeded 30 seconds; no production whistle forced.";
            if (tick > 60 * 600) failure = "Drive exceeded ten simulated minutes.";
            if (drive.Players.Any(a => !float.IsFinite(a.Position.X) || !float.IsFinite(a.Position.Y))) failure = "Non-finite actor.";
            if (tick % 60 == 0 || drive.LastResult != null || done()) trace.Add(new
            {
                Tick = tick, drive.LiveSeconds, drive.SecondsWithoutProgress, drive.SteeringChanges,
                Call = drive.Plays.SelectedPlay.Id, Exchange = drive.Execution.Backfield.Phase, Intent = drive.Execution.CpuIntent,
                drive.Match.Series, Ball = drive.Actors.Ball.State, drive.Actors.Ball.Position,
                Controlled = drive.Linebacker.Slot, Linebacker = drive.Linebacker.Position,
                Blocked = drive.Linebacker.IsBeingBlocked, Result = drive.LastResult,
                Actors = drive.Players.Select(a => new { a.Glyph, a.Position, a.Velocity }).ToArray()
            });
        }
        void draw()
        {
            Constants.UpdateFieldRect();
            Raylib.ClearBackground(Palette.Background);
            field.DrawField(drive.Plays.LineOfScrimmage, drive.Plays.FirstDownLine,
                drive.Match.Opponent.Definition.Name, drive.Match.Opponent.Definition.PrimaryColor,
                drive.Match.User.Definition.Name, drive.Match.User.Definition.PrimaryColor,
                SeasonStage.RegularSeason, default, drive.Match.Series.Down);
            foreach (var actor in drive.Players) actor.Draw();
            drive.Actors.Ball.Draw();
            var center = Constants.WorldToScreen(drive.Linebacker.Position);
            Raylib.DrawCircleLines((int)center.X, (int)center.Y, 13, Palette.Gold);
            Raylib.DrawText("PLAY DEFENSE", 24, 70, 26, Palette.Gold);
            Raylib.DrawText($"You: {drive.Match.User.Definition.Name}\nControl: {drive.Linebacker.Slot}\n\nWASD / arrows: move\nSpace: snap sooner\nP: pause\nEsc: close", 24, 120, 20, Palette.White);
            Raylib.DrawText($"Down {drive.Match.Series.Down}  |  {drive.Match.Series.Distance:0.#} to go\nCPU own {drive.Match.Series.OwnYardLine:0.#}\nPlays: {drive.Match.History.Count}", 24, 340, 20, Palette.White);
            string status = failure ?? (drive.Complete ? $"DRIVE OVER: {drive.LastResult?.Event.Reason}" : paused ? "PAUSED" : drive.Live ? "LIVE" : "GET READY");
            Raylib.DrawText(status, 24, 450, 19, Palette.Gold);
            Raylib.DrawText($"Seed {options.Seed} | Cover 3 | Development drive", 24, 25, 16, Palette.White);
        }
        if (options.Headless) { while (!done()) step(); }
        else
        {
            if (options.Capture) Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
            Raylib.InitWindow(1440, 900, "RetroQB: Play defense");
            Raylib.SetTargetFPS(60);
            try
            {
                var captured = new HashSet<string>();
                double accumulator = 0;
                while (!Raylib.WindowShouldClose())
                {
                    if (!automated && Raylib.IsKeyPressed(KeyboardKey.P)) paused = !paused;
                    if (options.Capture) step();
                    else
                    {
                        accumulator += Math.Min(Raylib.GetFrameTime(), .1f);
                        while (accumulator >= ScenarioDefinition.FixedStep) { step(); accumulator -= ScenarioDefinition.FixedStep; }
                    }
                    Raylib.BeginDrawing(); draw(); Raylib.EndDrawing();
                    if (options.Capture)
                    {
                        string key = $"{drive.Match.History.Count}-{drive.Actors.Ball.State}-{drive.Live}";
                        if (captured.Add(key))
                        {
                            var texture = Raylib.LoadRenderTexture(1440, 900);
                            try
                            {
                                Raylib.BeginTextureMode(texture); draw(); Raylib.EndTextureMode();
                                var image = Raylib.LoadImageFromTexture(texture.Texture);
                                try
                                {
                                    Raylib.ImageFlipVertical(ref image);
                                    if (!Raylib.ExportImage(image, Path.Combine(output, $"{tick:D5}-{key}.png")))
                                        throw new IOException("Could not export defensive drive capture.");
                                }
                                finally { Raylib.UnloadImage(image); }
                            }
                            finally { Raylib.UnloadRenderTexture(texture); }
                        }
                        if (done()) break;
                    }
                }
            }
            finally { Raylib.CloseWindow(); }
        }
        string report = Path.Combine(output, "report.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new { options.Seed, Complete = drive.Complete, Failure = failure,
            Tick = tick, drive.Throws, drive.Match.History, Trace = trace }, ScenarioRun.JsonOptions));
        Console.WriteLine($"Defense drive: {drive.LastResult?.Event.Reason}; failure: {failure ?? "none"}; report: {report}");
        return drive.Complete && failure == null ? 0 : 1;
    }
}
