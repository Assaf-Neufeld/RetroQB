using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Defense;

namespace RetroQB.Development;

/// <summary>Identical fixtures for baseline/candidate builds; deadlines never manufacture a result.</summary>
internal static class OffensiveBalanceVerification
{
    private sealed record Row(string Call, string Team, int Seed, float Yard, string Defense,
        int Frames, double? FirstThrow, PlayResolution? Result, string? Failure);
    private sealed class Cost
    {
        private readonly List<double> _times = new(200000);
        private long _bytes;
        public void Measure(Action update)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            update();
            _bytes += GC.GetAllocatedBytesForCurrentThread() - bytes;
            _times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        public object Snapshot()
        {
            var times = _times.Order().ToArray();
            return new { Frames = times.Length, MeanMilliseconds = times.Average(),
                P95Milliseconds = times[(int)(times.Length * .95)], BytesPerFrame = _bytes / (double)times.Length };
        }
    }
    public static int Run(ScenarioLaunchOptions options)
    {
        string output = Path.GetFullPath(options.OutputDirectory ?? "artifacts/offensive-improvements/step-3/balance");
        Directory.CreateDirectory(output);
        var rows = new List<Row>(); var cost = new Cost(); var wall = Stopwatch.StartNew();
        var catalog = PlaybookBuilder.BuildCatalog(); var user = TeamCatalog.Get("ballers");
        foreach (var play in catalog.Plays)
        foreach (string team in new[] { "scarlet-guard", "crimson-rush" })
        foreach (float yard in new[] { 20f, 90f })
        foreach (int seed in new[] { 101, 202, 303 })
        {
            var input = new ScriptedInput(); var cpu = TeamCatalog.Get(team);
            var d = new DefensiveDrive(input, seed, fixedCall: play.Id,
                match: new(user, cpu, cpu.Id, start: new(yard)), engine: DefensiveEngineMode.Production);
            var call = d.Calls[rows.Count % 10].Id; string? failure = null; double? first = null; int frame = 0;
            try
            {
                if (!d.SelectDefense(call) || !d.Snap()) throw new InvalidOperationException("Fixture rejected.");
                while (d.Live && frame < 1800)
                {
                    var delta = (d.Actors.Ball.Holder?.Position ?? d.Actors.Ball.Position) - d.Linebacker.Position;
                    input.Frame = new(Movement: delta.LengthSquared() > .01f ? Vector2.Normalize(delta) : Vector2.Zero, Sprint: true);
                    cost.Measure(() => d.Update(1f / 60)); frame++;
                    if (d.Throws > 0) first ??= frame / 60.0;
                    if (d.Players.Any(p => !float.IsFinite(p.Position.X) || !float.IsFinite(p.Position.Y)))
                        throw new InvalidOperationException("Non-finite actor.");
                }
                if (d.Live || d.Match.History.Count != 1) throw new InvalidOperationException("Terminal deadline/ownership failure.");
            }
            catch (Exception e) { failure = e.Message; }
            rows.Add(new(play.Id, team, seed, yard, call, frame, first, d.LastResult, failure));
            if (rows.Count % 100 == 0) Console.WriteLine($"Balance: {rows.Count} cases; {rows.Count(r => r.Failure != null)} failures");
        }
        var matches = Matches();
        int failures = rows.Count(r => r.Failure != null) + matches.Count(m => m.Failure != null);
        var passes = rows.Where(r => r.Result?.Event.Stats?.PassAttempt == true).ToArray();
        var rushes = rows.Where(r => r.Result?.Event.Stats?.Rush == RushingRole.RunningBack).ToArray();
        var summary = new
        {
            Attempts = passes.Length, Completions = passes.Count(r => r.Result!.Event.Stats!.Completion),
            Sacks = rows.Count(r => r.Result?.Event.Reason == PlayEndReason.Sack),
            Interceptions = passes.Count(r => r.Result!.Event.Reason == PlayEndReason.Interception),
            YardsPerAttempt = passes.Average(r => (r.Result!.Event.Stats!.Completion ? r.Result.Gain : 0)),
            Rushes = rushes.Length, YardsPerRush = rushes.Average(r => r.Result!.Gain),
            FirstThrowSeconds = rows.Where(r => r.FirstThrow.HasValue).Average(r => r.FirstThrow!.Value)
        };
        File.WriteAllText(Path.Combine(output, "balance.json"), JsonSerializer.Serialize(new
        {
            Matrix = "offense-step3-balance-v1", Failures = failures, WallSeconds = wall.Elapsed.TotalSeconds,
            FixedCallSummary = summary, FrameCost = cost.Snapshot(), Matches = matches, Samples = rows
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Balance complete: {rows.Count} cases, {matches.Count} matches, {failures} failures");
        return failures == 0 ? 0 : 1;
    }
    private sealed record MatchRow(int Seed, string Opponent, string? Failure, int UserScore, int CpuScore,
        int CpuPlays, int CpuDrives, int CpuPoints, object CpuStats, IReadOnlyDictionary<string, int> Calls);
    private static List<MatchRow> Matches()
    {
        var rows = new List<MatchRow>();
        for (int i = 0; i < 6; i++)
        {
            int seed = 401 + i; var input = new ScriptedInput(); var user = TeamCatalog.Get("ballers");
            var cpu = TeamCatalog.ForStage((SeasonStage)(i % 3));
            var s = new FullMatchSession(input, seed, new(user, cpu, i % 2 == 0 ? user.Id : cpu.Id,
                quarterSeconds: 120, regulationPeriods: 2, specialTeams: true));
            string? failure = null; int frame = 0, selected = -1;
            try
            {
                while (!s.Timed.Finished && frame < 60 * 3600)
                {
                    var d = s.Drive;
                    var delta = d.Execution.Control.HumanOnDefense ? (d.Actors.Ball.Holder?.Position ?? d.Actors.Ball.Position) - d.Linebacker.Position
                        : d.Actors.Ball.State == BallState.HeldByReceiver ? Vector2.UnitY : Vector2.Zero;
                    if (s.SpecialTeams is { } st) delta = s.UserReceivingKick ? -Vector2.UnitY : st.Returner.Position - st.Coverage[st.ControlledCoverageIndex].Position;
                    int? call = null, run = null;
                    if (!s.HumanOnDefense && !d.Live && d.LastResult == null && selected != s.Match.History.Count)
                    {
                        selected = s.Match.History.Count;
                        if (selected % 2 == 0) call = selected % 10; else run = selected % 10;
                    }
                    input.Frame = new(Movement: delta.LengthSquared() > .01f ? Vector2.Normalize(delta) : Vector2.Zero,
                        Sprint: true, ThrowTarget: !s.HumanOnDefense && d.LiveSeconds > 1 ? 0 : null);
                    bool ready = !d.Live && (!s.HumanOnDefense || d.LastResult != null || s.Clock.Phase == ClockPhase.PeriodBreak);
                    if (d.Live && s.Kick is { } kick && !s.HumanOnDefense)
                        ready = kick.Phase == KickPhase.Ready || kick.Phase == KickPhase.Power && kick.Marker >= .495
                            || kick.Phase == KickPhase.Accuracy && kick.Marker <= .505;
                    s.Update(1f / 60, new(Ready: ready, Call: call, Run: run)); frame++;
                    if (d.LiveSeconds > 30) throw new InvalidOperationException("Live play deadline.");
                }
                if (!s.Timed.Finished || s.Match.History.Select(h => h.Event.PlayId).Distinct().Count() != s.Match.History.Count)
                    throw new InvalidOperationException("Match deadline/duplicate result.");
            }
            catch (Exception e) { failure = e.Message; }
            var plays = s.Match.History.Where(h => h.Event.OffenseId == cpu.Id).ToArray();
            var calls = plays.Select(h => s.Match.StartOf(h.Event.PlayId).CallId).Where(id => !id.StartsWith("special."))
                .GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
            rows.Add(new(seed, cpu.Id, failure, s.Match.User.Score, s.Match.Opponent.Score, calls.Values.Sum(),
                plays.Count(p => p.DriveEnded && p.Event.Reason != PlayEndReason.Kickoff),
                plays.Sum(p => p.ScoringTeamId == cpu.Id ? p.Points : 0), s.Match.Opponent.Stats, calls));
            Console.WriteLine($"Balance match {seed}: {s.Match.User.Score}-{s.Match.Opponent.Score}; {failure ?? "OK"}");
        }
        return rows;
    }
}
