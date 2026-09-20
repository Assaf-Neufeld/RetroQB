using Raylib_cs;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.Development;

internal static class TimedLayoutCapture
{
    public static int Run(ScenarioLaunchOptions options)
    {
        string output = Path.GetFullPath(options.OutputDirectory ?? "artifacts/phase5/layouts");
        Directory.CreateDirectory(output);
        Raylib.SetConfigFlags(ConfigFlags.HiddenWindow | ConfigFlags.ResizableWindow);
        Raylib.InitWindow(1440, 900, "Timed match layout verification");
        try
        {
            foreach (var size in new[] { (1000, 700), (1280, 720), (1440, 900), (1920, 1080) })
            {
                Raylib.SetWindowSize(size.Item1, size.Item2);
                using var game = GameSession.CreateTimed(new ScriptedInput(), new Random(options.Seed), new PlayerRecordStore(Path.Combine(output, "unused-records.json"), MatchRuleset.TwoSidedTimed));
                void Capture(string name)
                {
                    var target = Raylib.LoadRenderTexture(size.Item1, size.Item2);
                    Raylib.BeginTextureMode(target); game.Draw(); Raylib.EndTextureMode();
                    var image = Raylib.LoadImageFromTexture(target.Texture); Raylib.ImageFlipVertical(ref image);
                    if (!Raylib.ExportImage(image, Path.Combine(output, $"{size.Item1}x{size.Item2}-{name}.png")))
                        throw new IOException("Layout export failed.");
                    Raylib.UnloadImage(image); Raylib.UnloadRenderTexture(target);
                }
                Capture("menu");
                game.StartTimedSeason(options.Seed, "Layout Review", new PlayerRecordStore(Path.Combine(output, "isolated-records.json"), MatchRuleset.TwoSidedTimed));
                Capture("pregame");
                game.TimedSeason!.Accept(); Capture("offense");
                var statsMatch = game.FullMatch!.Timed;
                var first = statsMatch.Advance(0, "layout.pass")!;
                statsMatch.Resolve(new(first.Id, first.OffenseId, PlayEndReason.Tackle, 26,
                    new(PassAttempt: true, Completion: true, Target: ReceiverSlot.WR1), DefenderSlot.MLB));
                statsMatch.Continue();
                statsMatch.Match.StartPossession(statsMatch.Match.Opponent.Definition.Id, new(20, 3, 10));
                var second = statsMatch.Advance(0, "layout.cpu-pass")!;
                statsMatch.Resolve(new(second.Id, second.OffenseId, PlayEndReason.Sack, 15,
                    Defender: DefenderSlot.MLB, ControlledDefender: DefenderSlot.MLB));
                game.FullMatch!.Update(0, new(Statistics: true)); Capture("statistics");
                game.StartTimedMatch(options.Seed, match: FullMatchScenarioLauncher.Create("late-tying-kick")); Capture("defense-kick");
                game.StartTimedMatch(options.Seed, match: FullMatchScenarioLauncher.Create("overtime-pairs")); Capture("overtime");
                var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
                var half = new TimedMatch(user, cpu, user.Id);
                half.Clock.StartPeriod(3); half.Match.StartPossession(cpu.Id, new());
                game.StartTimedMatch(options.Seed, match: half); Capture("halftime");
                game.FullMatch.Timed.Continue(); Capture("defense");
                game.FullMatch.Match.User.Score = 7; game.FullMatch.Clock.StartPeriod(4); game.FullMatch.Timed.Continue();
                game.FullMatch.Timed.Advance(0, "layout.final"); game.FullMatch.Timed.Advance(180);
                game.FullMatch.Drive.ResolveSpecial(PlayEndReason.Kneel, 19); Capture("final");
                game.StartTimedMatch(options.Seed, match: new TimedMatch(user, cpu, cpu.Id, start: new(99, 1, 1)));
                game.FullMatch!.Drive.Snap(); game.FullMatch.Drive.Actors.Qb.Position = new(25, 111);
                game.FullMatch.Drive.Update(.01f); game.FullMatch.Update(0, new(Ready: true));
                game.FullMatch.Update(0, new(Replay: true)); Capture("replay-after-possession");
            }
        }
        finally { Raylib.CloseWindow(); }
        return 0;
    }
}
