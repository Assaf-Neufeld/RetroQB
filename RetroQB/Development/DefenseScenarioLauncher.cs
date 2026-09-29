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
            string.IsNullOrEmpty(options.Definition.PlayId) ? null : options.Definition.PlayId, options.Definition.ScriptedOffense, engine: options.Engine);
        string output = Path.GetFullPath(options.OutputDirectory ?? Path.Combine(Path.GetTempPath(), "RetroQB", "defense", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        var trace = new List<object>();
        int tick = 0;
        string? failure = null;
        var renderer = new DefensivePlayRenderer();
        var session = new DefensivePlaySession(drive, options.Seed);
        bool replayShown = false;
        bool done() => drive.Complete || failure != null;
        void step(DefensiveInput? frame = null)
        {
            if (failure != null) return;
            if (automated)
            {
                var target = drive.Actors.Ball.Holder?.Position ?? drive.Actors.Ball.Position;
                Vector2 delta = target - drive.Linebacker.Position;
                script.Frame = new(Movement: delta.LengthSquared() > .1f ? Vector2.Normalize(delta) : Vector2.Zero);
            }
            if (automated && options.Definition.Name == "defense-calls")
            {
                bool showReplay = drive.LastReplay != null && !replayShown;
                replayShown |= showReplay;
                frame = new(Call: !drive.Live && tick % 20 == 0 ? tick / 20 % 10 : null, Timeout: tick == 100,
                    Pause: tick is 140 or 170, Replay: showReplay);
            }
            if (automated && options.Definition.Name == "defense-pressure-controls")
                frame = new(Call: tick == 0 ? 0 : null, VariantStep: tick is 20 or 40 or 60 ? 1 : tick == 80 ? -1 : 0,
                    Flip: tick is 100 or 120);
            if (automated && options.Definition.Name is "defense-match-controls" or "defense-match-fallback")
            {
                bool showReplay = drive.LastReplay != null && !replayShown;
                replayShown |= showReplay;
                frame = new(Call: tick == 0 ? 6 : tick == 20 ? 7 : null, Ready: tick == 40, Replay: showReplay);
            }
            session.Update(ScenarioDefinition.FixedStep, frame); tick++;
            if (drive.LiveSeconds > 30) failure = "Live play exceeded 30 seconds; no production whistle forced.";
            if (tick > 60 * 600) failure = "Drive exceeded ten simulated minutes.";
            if (drive.Players.Any(a => !float.IsFinite(a.Position.X) || !float.IsFinite(a.Position.Y))) failure = "Non-finite actor.";
            if (tick % 60 == 0 || drive.LastResult != null || done()) trace.Add(new
            {
                Tick = tick, drive.LiveSeconds, drive.SecondsWithoutProgress, drive.SteeringChanges,
                Call = drive.Plays.SelectedPlay.Id, Exchange = drive.Execution.Backfield.Phase, Intent = drive.Execution.CpuIntent,
                Defense = drive.SelectedDefense.Definition.Id, Clock = session.Clock.Snapshot(), session.SnapRemaining,
                drive.Match.Series, Ball = drive.Actors.Ball.State, drive.Actors.Ball.Position,
                Controlled = drive.Linebacker.Slot, Linebacker = drive.Linebacker.Position,
                Blocked = drive.Linebacker.IsBeingBlocked, Result = drive.LastResult,
                Actors = drive.Players.Select(a => new { a.Glyph, a.Position, a.Velocity }).ToArray()
                , Decision = drive.Execution.DefenseRuntime?.Current
            });
        }
        void draw() => renderer.Draw(session, failure);
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
                var pending = new DefensiveInput();
                bool wasFocused = true;
                while (!Raylib.WindowShouldClose())
                {
                    if (options.Capture) step();
                    else
                    {
                        bool focused = Raylib.IsWindowFocused();
                        pending = new(keyboard.GetDefensivePlaySelection() ?? pending.Call,
                            pending.Ready || keyboard.IsSpacePressed(), pending.Timeout || keyboard.IsTimeoutPressed(),
                            pending.Pause || keyboard.IsPausePressed(), pending.Replay || keyboard.IsReplayPressed(), focused,
                            pending.Flip || keyboard.IsFlipPlayPressed(), keyboard.GetDefensiveVariantStep() is var variant && variant != 0 ? variant : pending.VariantStep);
                        if (focused && !wasFocused) accumulator = 0;
                        else accumulator += Math.Min(Raylib.GetFrameTime(), .1f);
                        wasFocused = focused;
                        while (accumulator >= ScenarioDefinition.FixedStep)
                        { step(pending); pending = new(Focused: focused); accumulator -= ScenarioDefinition.FixedStep; }
                    }
                    Raylib.BeginDrawing(); draw(); Raylib.EndDrawing();
                    if (options.Capture)
                    {
                        string key = $"{drive.Match.History.Count}-{drive.SelectedDefense.Definition.Id}-{drive.CompiledPlan?.Pressure.Selection.Kind}-{drive.CompiledPlan?.Pressure.Selection.Side}-{session.Clock.StopReason}-{session.Clock.Suspension}-{drive.Actors.Ball.State}-{drive.Live}";
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
