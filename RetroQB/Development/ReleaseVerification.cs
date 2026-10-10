using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using RetroQB.Gameplay;
using RetroQB.Entities;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Development;

/// <summary>Fixed release matrix. Failures are reported, never converted into artificial whistles.</summary>
internal static class ReleaseVerification
{
    private static readonly int[] Seeds = [101, 202, 303];
    private sealed record Sample(string Call, float Yard, SeasonStage Stage, int Seed, int Hz, string Defense,
        double Seconds, string? Failure, PlayResolution? Result, int Throws, string Controlled,
        bool Flipped = false, double? FirstThrowSeconds = null, int ProtectionBreakdowns = 0, bool HumanDefense = true, bool ScreenReleased = false, bool ScreenCaught = false,
        int CombinationReleases = 0, double QuarterbackTravel = 0);

    public static int Run(ScenarioLaunchOptions options)
    {
        string output = Path.GetFullPath(options.OutputDirectory ?? "artifacts/phase6"); Directory.CreateDirectory(output);
        var timer = Stopwatch.StartNew();
        var samples = new List<Sample>();
        var catalog = new PlayManager().Catalog.Plays;
        if (options.Definition.Name is "offense-designed-matrix" or "offense-designed-controls")
        {
            if (options.Definition.Name == "offense-designed-matrix")
            foreach (var play in catalog)
            foreach (float yard in new[] { 1f, 50f, 95f })
            foreach (var stage in Enum.GetValues<SeasonStage>())
            foreach (bool flipped in new[] { false, true })
            foreach (int hz in new[] { 30, 60, 120 })
                Add(play.Id, yard, stage, hz, flipped, true);
            foreach (var play in catalog.Where(p => p.Family == PlayType.Run || p.Screen != null || p.QuarterbackPath != null))
            foreach (float yard in new[] { 1f, 50f, 95f })
            foreach (bool flipped in new[] { false, true })
            foreach (int hz in new[] { 30, 60, 120 })
                Add(play.Id, yard, SeasonStage.RegularSeason, hz, flipped, false);
            void Add(string id, float yard, SeasonStage stage, int hz, bool flip, bool humanDefense)
            {
                samples.Add(Exercise(id, yard, stage, options.Seed, hz, samples.Count % 10, flip, DefensiveEngineMode.Production, humanDefense));
                if (samples.Count % 100 == 0) Console.WriteLine($"Designed offense: {samples.Count} cases, {samples.Count(s => s.Failure != null)} failures");
            }
        }
        else if (options.Definition.Name == "offense-passing-matrix")
        {
            foreach (var play in catalog.Where(p => p.Family == PlayType.Pass && !p.IsWildcard))
            foreach (float yard in new[] { 1f, 50f, 95f })
            foreach (var stage in Enum.GetValues<SeasonStage>())
            foreach (bool flipped in new[] { false, true })
            foreach (int hz in new[] { 30, 60, 120 })
            {
                samples.Add(Exercise(play.Id, yard, stage, options.Seed, hz, samples.Count % 10, flipped, DefensiveEngineMode.Production));
                if (samples.Count % 100 == 0) Console.WriteLine($"Passing: {samples.Count} plays, {samples.Count(s => s.Failure != null)} failures");
            }
        }
        else if (options.Definition.Name == "release-catalog")
        {
            foreach (var play in catalog)
            foreach (float yard in new[] { 20f, 50f, 90f })
            foreach (var stage in Enum.GetValues<SeasonStage>())
            foreach (int seed in play.IsWildcard ? Enumerable.Range(0, 10) : Seeds)
            {
                samples.Add(Exercise(play.Id, yard, stage, seed, 60, samples.Count % 10));
                if (samples.Count % 100 == 0) Console.WriteLine($"Catalog: {samples.Count} plays, {samples.Count(s => s.Failure != null)} failures");
            }
            // Formation families are sampled explicitly, independently of the catalog's current row order.
            foreach (var id in new[] { "run.hb-dive", "pass.bunch-quick", "pass.four-verts", "pass.gun-doubles.pa-cross" }
                .Concat(new[] { catalog.First(p => p.Formation.Type == FormationType.WingTight).Id,
                    catalog.First(p => p.Formation.Type == FormationType.PassSpread).Id }).Distinct())
            foreach (int hz in new[] { 30, 60, 120 })
            foreach (int seed in Seeds) samples.Add(Exercise(id, 50, SeasonStage.Playoff, seed, hz, seed % 10));
        }
        else return RunMatches(output, options.Definition.Name == "release-default");
        var report = new { Matrix = options.Definition.Name is "offense-designed-matrix" or "offense-designed-controls" ? "offense-step2-v1" : options.Definition.Name == "offense-passing-matrix" ? "offense-step1-v1" : "phase6-v1",
            Seeds = options.Definition.Name is "offense-passing-matrix" or "offense-designed-matrix" or "offense-designed-controls" ? new[] { options.Seed } : Seeds,
            FramesPerSecond = new[] { 30, 60, 120 }, CatalogCalls = catalog.Count,
            WallSeconds = timer.Elapsed.TotalSeconds, Failures = samples.Count(s => s.Failure != null), Samples = samples };
        File.WriteAllText(Path.Combine(output, options.Definition.Name == "offense-designed-controls" ? "controls.json" : options.Definition.Name == "offense-designed-matrix" ? "designed.json" : options.Definition.Name == "offense-passing-matrix" ? "passing.json" : "catalog.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Catalog complete: {samples.Count} cases; {report.Failures} failures; {timer.Elapsed.TotalSeconds:0.0}s wall time");
        return report.Failures == 0 ? 0 : 1;
    }

    private static Sample Exercise(string call, float yard, SeasonStage stage, int seed, int hz, int defense, bool flipped = false,
        DefensiveEngineMode engine = DefensiveEngineMode.Legacy, bool humanDefense = true)
    {
        DefensiveDrive? drive = null; string? failure = null; int ticks = 0;
        double? firstThrow = null; bool screenReleased = false, screenCaught = false; int combos = 0; double qbTravel = 0;
        var breakdowns = new HashSet<DefenderSlot>();
        string defenseId = DefensivePlaybook.ForEngine(engine)[defense].Id;
        try
        {
            var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(stage);
            drive = new(input, seed, fixedCall: call, match: new(user, cpu, humanDefense ? cpu.Id : user.Id, stage, new(yard, Distance: MathF.Min(10, 100 - yard))), engine: engine);
            if (flipped) drive.FlipOffensiveFormationForDevelopment();
            if (humanDefense ? !drive.SelectDefense(defenseId) : !drive.SelectCompiledDefenseForDevelopment(defenseId))
                throw new InvalidOperationException("Development defense selection rejected.");
            var controlled = humanDefense ? drive.Linebacker : null;
            var qbStart = drive.Actors.Qb.Position;
            if (!drive.Snap()) throw new InvalidOperationException("Snap rejected.");
            while (drive.Live && ticks < hz * 30)
            {
                var delta = controlled == null ? Vector2.Zero
                    : (drive.Actors.Ball.Holder?.Position ?? drive.Actors.Ball.Position) - controlled.Position;
                int? throwKey = null;
                var primary = drive.Plays.SelectedPlay.Reads.FirstOrDefault();
                if (!humanDefense && drive.Plays.SelectedPlay.Family == PlayType.Pass && primary != null
                    && drive.LiveSeconds >= primary.EarliestSeconds
                    && drive.Actors.Receivers.Single(r => r.Slot == primary.Slot).RouteState.StepIndex >= primary.AfterStep)
                    throwKey = drive.Actors.Receivers.Where(r => r.Eligible).OrderBy(r => r.Slot.GetPriorityOrder())
                        .Select(r => r.Slot).ToList().IndexOf(primary.Slot);
                input.Frame = new(Movement: humanDefense ? delta.LengthSquared() > .01f ? Vector2.Normalize(delta) : Vector2.Zero
                    : drive.Actors.Ball.State == BallState.HeldByReceiver ? Vector2.UnitY : Vector2.Zero, Sprint: true, ThrowTarget: throwKey);
                drive.Update(1f / hz); ticks++;
                if (drive.Throws > 0) firstThrow ??= ticks / (double)hz;
                if (drive.Actors.Ball.State == BallState.HeldByQB)
                    foreach (var threat in drive.Actors.Defenders.Where(d => d.IsRusher && !d.IsBeingBlocked
                        && Vector2.Distance(d.Position, drive.Actors.Qb.Position) < 3)) breakdowns.Add(threat.Slot);
                screenReleased |= drive.Execution.Design.ScreenReleased;
                screenCaught |= drive.Actors.Ball.Holder is Receiver caught && caught.Slot == drive.Plays.SelectedPlay.Screen?.Target;
                combos = Math.Max(combos, drive.Actors.Blockers.Count(b => b.CombinationReleased));
                qbTravel = Math.Max(qbTravel, Vector2.Distance(qbStart, drive.Actors.Qb.Position));
                if (humanDefense && !ReferenceEquals(controlled, drive.Execution.Control.ControlledDefender(drive.Actors.Defenders))) throw new InvalidOperationException("Controlled actor changed.");
                Validate(drive, hz);
            }
            if (drive.Live) throw new InvalidOperationException($"No terminal event in 30s; ball={drive.Actors.Ball.State}, holder={drive.Actors.Ball.Holder?.Position}, exchange={drive.Execution.Backfield.Phase}, stalled={drive.SecondsWithoutProgress}");
            if (drive.Match.History.Count != 1) throw new InvalidOperationException("Missing/duplicate terminal result.");
            if (drive.LastResult!.Event.OffenseId != (humanDefense ? cpu.Id : user.Id)) throw new InvalidOperationException("Wrong result owner.");
        }
        catch (Exception error) { failure = error.Message; }
        return new(call, yard, stage, seed, hz, drive?.SelectedDefense.Definition.Id ?? defenseId, ticks / (double)hz,
            failure, drive?.LastResult, drive?.Throws ?? 0, humanDefense ? drive?.SelectedDefense.ControlledSlot.ToString() ?? "" : "Offense",
            flipped, firstThrow, breakdowns.Count, humanDefense, screenReleased, screenCaught, combos, qbTravel);
    }

    private static void Validate(DefensiveDrive d, int hz)
    {
        foreach (var p in d.Players)
        {
            if (!float.IsFinite(p.Position.X) || !float.IsFinite(p.Position.Y)) throw new InvalidOperationException("Non-finite actor.");
            // A sideline whistle can occur one movement step beyond the boundary.
            if (p.Position.Y < 0 || p.Position.Y > Constants.FieldLength || p.Position.X < -20f / hz || p.Position.X > Constants.FieldWidth + 20f / hz)
                throw new InvalidOperationException($"Actor outside bounds: {p.Glyph} {p.Position}.");
        }
        if (d.Actors.Ball.State == BallState.InAir && d.Actors.Ball.ThrowStart.Y > d.Plays.LineOfScrimmage + .1f)
            throw new InvalidOperationException("Pass released beyond line of scrimmage.");
    }

    private static int RunMatches(string output, bool defaultLength)
    {
        var rows = new List<object>(); int failures = 0;
        int count = defaultLength ? 3 : 24;
        for (int index = 0; index < count; index++)
        {
            int seed = (defaultLength ? 301 : 101) + index; var stage = (SeasonStage)(index % 3);
            var user = TeamCatalog.Get("ballers"); var cpu = TeamCatalog.ForStage(stage);
            bool userOpens = index % 2 == 0;
            var input = new ScriptedInput();
            var s = new FullMatchSession(input, seed, new(user, cpu, userOpens ? user.Id : cpu.Id, stage, quarterSeconds: defaultLength ? 120 : 15, regulationPeriods: 2, specialTeams: true));
            int ticks = 0; string? failure = null; var clock = Stopwatch.StartNew();
            try
            {
                while (!s.Timed.Finished && ticks < 60 * (defaultLength ? 3600 : 1800))
                {
                    var d = s.Drive;
                    var delta = d.Execution.Control.HumanOnDefense ? (d.Actors.Ball.Holder?.Position ?? d.Actors.Ball.Position) - d.Linebacker.Position : Vector2.UnitY;
                    if (s.SpecialTeams is { } special) delta = s.UserReceivingKick ? -Vector2.UnitY : special.Returner.Position - special.Coverage[special.ControlledCoverageIndex].Position;
                    input.Frame = new(Movement: delta.LengthSquared() > .01f ? Vector2.Normalize(delta) : Vector2.Zero, Sprint: true,
                        ThrowTarget: !s.HumanOnDefense && d.LiveSeconds > 1 ? d.Match.History.Count % 5 : null);
                    bool ready = !d.Live && (!defaultLength || !s.HumanOnDefense || d.LastResult != null || s.Clock.Phase == ClockPhase.PeriodBreak);
                    if (d.Live && s.Kick is { } kick && !s.HumanOnDefense)
                        ready = kick.Phase == KickPhase.Ready || kick.Phase == KickPhase.Power && kick.Marker >= .495
                            || kick.Phase == KickPhase.Accuracy && kick.Marker <= .505;
                    s.Update(1f / 60, new(Ready: ready, Call: !d.Live && s.HumanOnDefense ? index % 10 : null,
                        Run: !d.Live && !s.HumanOnDefense ? index % 10 : null));
                    ticks++; if (d.LiveSeconds > 30) throw new InvalidOperationException("Live play exceeds 30s.");
                    Validate(d, 60);
                }
                if (!s.Timed.Finished) throw new InvalidOperationException("Match exceeds simulation time budget.");
                if (s.Match.History.Select(h => h.Event.PlayId).Distinct().Count() != s.Match.History.Count) throw new InvalidOperationException("Duplicate result.");
                if (s.Match.History.Sum(h => h.ScoringTeamId == user.Id ? h.Points : 0) != s.Match.User.Score
                    || s.Match.History.Sum(h => h.ScoringTeamId == cpu.Id ? h.Points : 0) != s.Match.Opponent.Score) throw new InvalidOperationException("Score attribution mismatch.");
            }
            catch (Exception error) { failure = error.Message; failures++; }
            var stats = s.Match.Opponent.Stats; int cpuDrives = s.Match.History.Count(h => h.Event.OffenseId == cpu.Id && h.DriveEnded && h.Event.Reason != PlayEndReason.Kickoff);
            rows.Add(new { Seed = seed, Stage = stage, UserOpens = userOpens, Failure = failure, SimulatedSeconds = ticks / 60.0,
                WallSeconds = clock.Elapsed.TotalSeconds, s.Timed.WinnerId, s.Timed.OvertimePair, UserScore = s.Match.User.Score, CpuScore = s.Match.Opponent.Score,
                CpuStats = stats, UserDefense = s.Match.User.DefenseStats, CpuDrives = cpuDrives, Plays = s.Match.History.Count,
                CompletionRate = stats.Qb.Attempts == 0 ? (double?)null : stats.Qb.Completions / (double)stats.Qb.Attempts,
                SackRate = stats.Qb.Attempts + stats.Qb.Sacks == 0 ? (double?)null : stats.Qb.Sacks / (double)(stats.Qb.Attempts + stats.Qb.Sacks),
                InterceptionRate = stats.Qb.Attempts == 0 ? (double?)null : stats.Qb.Interceptions / (double)stats.Qb.Attempts,
                TurnoversPerPossession = cpuDrives == 0 ? (double?)null : s.Match.History.Count(h => h.Event.OffenseId == cpu.Id && (h.TurnoverOnDowns || h.Event.Reason == PlayEndReason.Interception)) / (double)cpuDrives,
                YardsPerRush = stats.Rb.Attempts == 0 ? (double?)null : stats.Rb.Yards / (double)stats.Rb.Attempts,
                PointsPerPossession = cpuDrives == 0 ? (double?)null : s.Match.History.Where(h => h.Event.OffenseId == cpu.Id && h.ScoringTeamId == cpu.Id).Sum(h => h.Points) / (double)cpuDrives });
            Console.WriteLine($"Match {index + 1}/{count}: {stage}, seed {seed}, {s.Match.User.Score}-{s.Match.Opponent.Score}, {failure ?? "OK"}");
        }
        File.WriteAllText(Path.Combine(output, defaultLength ? "default-matches.json" : "matches.json"), JsonSerializer.Serialize(new { Matrix = "phase6-v1", Failures = failures, Samples = rows }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
