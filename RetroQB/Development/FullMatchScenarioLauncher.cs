using System.Numerics;
using System.Text.Json;
using Raylib_cs;
using RetroQB.Gameplay;
using RetroQB.Input;
using RetroQB.AI;

namespace RetroQB.Development;

internal static class FullMatchScenarioLauncher
{
    internal static TimedMatch Create(string name)
    {
        var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
        var match = new TimedMatch(user, cpu, user.Id, quarterSeconds: name == "timed-game" ? 180 : 15);
        if (name.StartsWith("late-"))
        {
            match.Clock.StartPeriod(4);
            match.Match.StartPossession(cpu.Id, name == "late-tying-kick" ? new(70, 4, 5) : new());
            match.Match.User.Score = name == "late-tying-kick" ? 3 : 0;
            match.Match.Opponent.Score = name == "late-protect-lead" ? 7 : 0;
            match.Continue();
        }
        if (name == "overtime-pairs")
        {
            match.Clock.StartPeriod(4); match.Continue();
            var play = match.Advance(0, "fixture.regulation"); match.Advance(15);
            match.Resolve(new(play!.Id, play.OffenseId, PlayEndReason.Kneel, 19)); match.Continue();
        }
        return match;
    }

    public static int Run(ScenarioLaunchOptions options)
    {
        string output = Path.GetFullPath(options.OutputDirectory ?? Path.Combine("artifacts", options.Definition.Name));
        Directory.CreateDirectory(output);
        bool automated = options.Headless || options.Capture;
        var script = new ScriptedInput(); var keyboard = new InputManager();
        using var game = new GameSession(automated ? script : keyboard, new Random(options.Seed), new PlayerRecordStore(Path.Combine(output, "isolated-records.json")));
        game.StartTimedMatch(options.Seed, match: Create(options.Definition.Name));
        var session = game.FullMatch!;
        int ticks = 0; string? failure = null;
        var periods = new HashSet<int>(); var possessions = new HashSet<string>();
        void Step(MatchInput? input = null)
        {
            var drive = session.Drive;
            if (automated)
            {
                Vector2 delta = drive.Execution.Control.HumanOnDefense ? (drive.Actors.Ball.Holder?.Position ?? drive.Actors.Ball.Position) - drive.Linebacker.Position : Vector2.UnitY;
                script.Frame = new(Movement: delta.LengthSquared() > .01f ? Vector2.Normalize(delta) : Vector2.Zero, Sprint: true,
                    ThrowTarget: !session.HumanOnDefense && drive.LiveSeconds > 1 ? 0 : null);
                bool ready = !drive.Live;
                if (drive.Live && session.Kick is { } kick && !session.HumanOnDefense)
                    ready = kick.Phase == KickPhase.Ready || kick.Phase == KickPhase.Power && kick.Marker >= .495
                        || kick.Phase == KickPhase.Accuracy && kick.Marker <= .505;
                input = new(Ready: ready, Run: !drive.Live && !session.HumanOnDefense ? 1 : null);
            }
            try { game.UpdateTimedMatch(ScenarioDefinition.FixedStep, input ?? new()); }
            catch (Exception error) { failure = $"Tick {ticks}: {error}"; }
            ticks++;
            periods.Add(session.Clock.Quarter); possessions.Add(session.Match.PossessionId);
            if (ticks > 60 * 3600) failure = "Exceeded one simulated hour.";
            if (drive.LiveSeconds > 40) failure = "Scrimmage play exceeded 40 seconds.";
            if (drive.Players.Any(a => !float.IsFinite(a.Position.X) || !float.IsFinite(a.Position.Y))) failure = "Non-finite actor position.";
        }
        bool Done() => session.Timed.Finished || failure != null;
        if (options.Headless) { while (!Done()) Step(); }
        else
        {
            Raylib.SetConfigFlags(options.Capture ? ConfigFlags.HiddenWindow : ConfigFlags.ResizableWindow);
            Raylib.InitWindow(1440, 900, $"RetroQB: {options.Definition.Name}");
            Raylib.SetExitKey(KeyboardKey.Null); Raylib.SetTargetFPS(60);
            try
            {
                var captures = new HashSet<string>();
                while (!Raylib.WindowShouldClose())
                {
                    if (options.Capture) { if (!Done()) Step(); }
                    else
                    {
                        // One input edge per frame, bounded substeps, no focus-loss catch-up.
                        var input = new MatchInput(Call: keyboard.GetPassPlaySelection(), Run: keyboard.GetRunPlaySelection(),
                            Ready: keyboard.IsSpacePressed(), Kick: keyboard.IsFieldGoalPressed(), Punt: keyboard.IsPuntPressed(),
                            Kneel: keyboard.IsKneelPressed(), Flip: keyboard.IsFlipPlayPressed(), Timeout: keyboard.IsTimeoutPressed(),
                            Pause: keyboard.IsEscapePressed(), Replay: keyboard.IsReplayPressed(), Restart: keyboard.IsRestartPressed(), Focused: Raylib.IsWindowFocused());
                        float remaining = Math.Min(Raylib.GetFrameTime(), .1f);
                        do
                        {
                            float dt = Math.Min(remaining, ScenarioDefinition.FixedStep);
                            game.UpdateTimedMatch(dt, input); remaining -= dt;
                            input = new(Focused: input.Focused);
                        } while (remaining > .00001f);
                    }
                    if (options.Capture)
                    {
                        string key = $"{session.Clock.Quarter}-{session.Match.PossessionId}-{session.Action}-{session.Clock.Phase}";
                        if (captures.Add(key) || Done())
                        {
                            var target = Raylib.LoadRenderTexture(1440, 900);
                            Raylib.BeginTextureMode(target); game.Draw(); Raylib.EndTextureMode();
                            var image = Raylib.LoadImageFromTexture(target.Texture); Raylib.ImageFlipVertical(ref image);
                            Raylib.ExportImage(image, Path.Combine(output, $"{ticks:D6}-{key}.png"));
                            Raylib.UnloadImage(image); Raylib.UnloadRenderTexture(target);
                        }
                        if (Done()) break;
                    }
                    else { Raylib.BeginDrawing(); game.Draw(); Raylib.EndDrawing(); }
                }
            }
            finally { Raylib.CloseWindow(); }
        }
        string report = Path.Combine(output, "report.json");
        File.WriteAllText(report, JsonSerializer.Serialize(new { options.Definition.Name, options.Seed, Ticks = ticks, Failure = failure,
            session.Timed.WinnerId, session.Timed.OvertimePair, Periods = periods.Order().ToArray(), Possessions = possessions.Order().ToArray(),
            UserScore = session.Match.User.Score, CpuScore = session.Match.Opponent.Score, Clock = session.Clock.Snapshot(), Plays = session.Match.History },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{options.Definition.Name}: {session.Status} {failure} | {report}");
        return failure == null ? 0 : 1;
    }
}
