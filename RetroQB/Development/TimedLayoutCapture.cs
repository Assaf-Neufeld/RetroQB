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
            foreach (var size in new[] { (1000, 700), (1280, 720), (1440, 900), (1920, 1080), (2518, 1349) })
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
                game.TimedSeason!.Accept(); Capture("kickoff-formation");
                game.FullMatch!.Update(0, new(Ready: true));
                for (int tick = 0; tick < 80; tick++) game.FullMatch.Update(1f / 60);
                Capture("kickoff-flight");
                for (int tick = 0; game.FullMatch.SpecialTeams?.Phase != SpecialTeamsPhase.Return && game.FullMatch.Drive.Live && tick < 1800; tick++) game.FullMatch.Update(1f / 60);
                Capture("kickoff-return");
                if (game.FullMatch.SpecialTeams?.Phase == SpecialTeamsPhase.Return)
                {
                    game.FullMatch.SpecialTeams.Returner.Position = new(25, 9);
                    game.FullMatch.Update(1f / 60);
                    Capture("kickoff-touchdown");
                    game.FullMatch.Update(0, new(Ready: true));
                    Capture("kickoff-after-touchdown");
                    game.FullMatch.Update(0, new(Ready: true));
                }
                for (int tick = 0; game.FullMatch.Drive.Live && tick < 1800; tick++) game.FullMatch.Update(1f / 60);
                game.FullMatch.Update(0, new(Ready: true)); Capture("offense");
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
                var summaryUser = TeamCatalog.Get("ballers"); var summaryCpu = TeamCatalog.ForStage(SeasonStage.RegularSeason);
                game.StartTimedMatch(options.Seed, match: new TimedMatch(summaryUser, summaryCpu, summaryCpu.Id, start: new(35, 4, 10)));
                game.FullMatch!.Drive.Snap(); game.FullMatch.Drive.ResolveSpecial(PlayEndReason.Incomplete, 35); Capture("drive-result");
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
                game.StartTimedMatch(options.Seed, match: new TimedMatch(user, cpu, user.Id, start: new(30, 4, 10), quarterSeconds: 120, regulationPeriods: 2));
                game.FullMatch!.SelectAction(RetroQB.AI.MatchAction.Punt); Capture("punt-formation");
                game.FullMatch.Update(0, new(Ready: true));
                for (int tick = 0; tick < 85; tick++) game.FullMatch.Update(1f / 60);
                Capture("punt-flight");
                game.StartTimedSeason(options.Seed, "", new PlayerRecordStore(Path.Combine(output, "name-preview.json"), MatchRuleset.TwoSidedTimed));
                game.TimedSeason!.Accept(); var final = game.FullMatch!;
                final.Match.Opponent.Score = 7; final.Clock.StartPeriod(2); final.Timed.Continue();
                final.Timed.Advance(0, "layout.final"); final.Timed.Advance(120); final.Drive.ResolveSpecial(PlayEndReason.Kneel, 19);
                game.TimedSeason.Accept(); Capture("name-after-season");
                game.StartTimedSeason(options.Seed, "", new PlayerRecordStore(Path.Combine(output, "victory-preview.json"), MatchRuleset.TwoSidedTimed));
                for (int stage = 0; stage < 2; stage++)
                {
                    game.TimedSeason!.Accept();
                    var victory = game.FullMatch!;
                    victory.Match.User.Score = 14; victory.Match.Opponent.Score = 7;
                    victory.Clock.StartPeriod(2); victory.Timed.Continue();
                    victory.Timed.Advance(0, "layout.victory"); victory.Timed.Advance(120);
                    victory.Drive.ResolveSpecial(PlayEndReason.Kneel, 19);
                    Capture(stage == 0 ? "regular-season-victory" : "playoff-victory");
                    game.TimedSeason.Accept();
                    Capture(stage == 0 ? "playoff-pregame" : "superbowl-pregame");
                }
            }
        }
        finally { Raylib.CloseWindow(); }
        return 0;
    }
}
