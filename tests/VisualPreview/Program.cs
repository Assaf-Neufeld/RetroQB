using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Data;
using RetroQB.Entities;
using RetroQB.Gameplay;
using RetroQB.Gameplay.Controllers;
using RetroQB.Gameplay.Replay;
using RetroQB.Input;
using RetroQB.Rendering;
using RetroQB.Stats;

bool playFeedback = args.Length > 0 && args[0] == "--play-feedback";
bool readmeScreenshot = args.Length > 0 && args[0] == "--readme";
bool pregameScreenshots = args.Length > 0 && args[0] == "--pregame";
bool teamScreenshots = args.Length > 0 && args[0] == "--teams";
bool fieldGoalScreenshots = args.Length > 0 && args[0] == "--field-goal";
string output = Path.GetFullPath(fieldGoalScreenshots ? (args.Length > 1 ? args[1] : "artifacts/field-goal") : playFeedback ? (args.Length > 1 ? args[1] : "artifacts/play-feedback") : teamScreenshots
    ? (args.Length > 1 ? args[1] : "artifacts/teams")
    : pregameScreenshots
    ? (args.Length > 1 ? args[1] : "artifacts/pregame")
    : readmeScreenshot
    ? (args.Length > 1 ? args[1] : "screenshots/gameplay.png")
    : (args.Length > 0 ? args[0] : "artifacts/visual-phase3"));
Directory.CreateDirectory(readmeScreenshot ? Path.GetDirectoryName(output)! : output);
Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
Raylib.InitWindow(1280, 720, "RetroQB visual preview");
try
{
    if (fieldGoalScreenshots)
    {
        foreach (var (width, height) in new[] { (1000, 700), (1280, 720), (1920, 1080), (2560, 1440) })
        foreach (string scenario in new[] { "setup", "snap", "ready", "range", "power", "power-close", "power-long", "accuracy", "long", "close", "flight", "good", "short", "left", "right", "paused", "presnap" })
        {
            Raylib.SetWindowSize(width, height);
            Constants.UpdateFieldRect();
            var kick = new FieldGoalAttempt(FieldGeometry.OpponentGoalLine - (scenario switch { "range" => 80, "long" or "power-long" => 43, "close" or "power-close" => 1, _ => 30 }));
            if (scenario is not "setup" and not "range")
            {
                kick.PressSpace();
                kick.Update(FieldGoalAttempt.SnapDuration * (scenario == "snap" ? 0.5f : 1));
                if (scenario is not "snap" and not "ready")
                {
                    kick.PressSpace();
                    kick.Update((scenario == "short" ? 0.1f : 0.5f) / 0.65f);
                    if (!scenario.StartsWith("power", StringComparison.Ordinal))
                    {
                        kick.PressSpace();
                        float accuracy = scenario == "right" ? 0.9f : scenario == "left" ? 0.1f : 0.5f;
                        kick.Update((1 - accuracy) / (0.55f + kick.Power * 0.25f));
                        if (scenario is "good" or "short" or "left" or "right" or "flight")
                        {
                            kick.PressSpace();
                            kick.Update(FieldGoalAttempt.FlightDuration * (scenario == "flight" ? 0.55f : 1));
                        }
                    }
                }
            }
            var target = Raylib.LoadRenderTexture(width, height);
            Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Palette.Background);
            {
                using var session = new GameSession();
                var state = (GameStateManager)typeof(GameSession).GetField("_stateManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
                var plays = (PlayManager)typeof(GameSession).GetField("_playManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
                if (scenario != "presnap")
                {
                    plays.ResolvePlay(FieldGeometry.OpponentGoalLine - kick.Distance + 17, false, false, false, false);
                    typeof(GameSession).GetField("_fieldGoal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(session, kick);
                }
                state.SetState(scenario == "presnap" ? GameState.PreSnap : GameState.FieldGoal);
                if (scenario == "paused") state.TogglePause();
                session.Draw();
            }
            Raylib.EndTextureMode();
            var capture = Raylib.LoadImageFromTexture(target.Texture);
            Raylib.ImageFlipVertical(ref capture);
            string path = Path.Combine(output, $"{scenario}-{width}.png");
            if (!Raylib.ExportImage(capture, path)) throw new IOException(path);
            Raylib.UnloadImage(capture);
            Raylib.UnloadRenderTexture(target);
        }
        return;
    }
    if (playFeedback)
    {
        foreach (var (width, height) in new[] { (1000, 700), (1440, 900) })
        foreach (string scenario in new[] { "pass", "run", "play-action", "catch", "handoff", "fake", "ready" })
            RenderPlayFeedback(Path.Combine(output, $"{scenario}-{width}.png"), width, height, scenario);
        return;
    }
    if (teamScreenshots)
    {
        foreach (var (width, height) in new[] { (1000, 700), (1280, 720), (1920, 1080) })
        foreach (int selected in new[] { 0, 5, 9, 10 })
        {
            Raylib.SetWindowSize(width, height);
            var target = Raylib.LoadRenderTexture(width, height);
            Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Palette.Background);
            new MenuRenderer().Draw(selected, OffensiveTeamPresets.GetMenuTeams(selected == 10),
                LeaderboardSummary.Empty, false, false, "", "");
            Raylib.EndTextureMode();
            var capture = Raylib.LoadImageFromTexture(target.Texture);
            Raylib.ImageFlipVertical(ref capture);
            string path = Path.Combine(output, $"teams-{width}-{selected}.png");
            if (!Raylib.ExportImage(capture, path)) throw new IOException($"Could not export {path}");
            Raylib.UnloadImage(capture);
            Raylib.UnloadRenderTexture(target);
        }
        RenderTeamUniforms(output);
        return;
    }

    if (pregameScreenshots)
    {
        foreach (var (width, height) in new[] { (1000, 700), (1280, 720), (1920, 1080) })
        foreach (var stage in Enum.GetValues<SeasonStage>())
        {
            Raylib.SetWindowSize(width, height);
            Constants.UpdateFieldRect();
            var offense = OffensiveTeamPresets.Ballers;
            var defense = DefensiveTeamPresets.All[stage.GetStageNumber() - 1];
            var target = Raylib.LoadRenderTexture(width, height);
            Raylib.BeginTextureMode(target);
            Raylib.ClearBackground(Palette.Background);
            new FieldRenderer().DrawField(40, 50, offense.Name, offense.PrimaryColor,
                defense.Name, defense.PrimaryColor, stage, default);
            PregameRenderer.Draw(offense, defense, stage);
            RetroScreenOverlay.Draw();
            Raylib.EndTextureMode();
            var capture = Raylib.LoadImageFromTexture(target.Texture);
            Raylib.ImageFlipVertical(ref capture);
            string path = Path.Combine(output, $"pregame-{width}-{stage}.png");
            if (!Raylib.ExportImage(capture, path)) throw new IOException($"Could not export {path}");
            Raylib.UnloadImage(capture);
            Raylib.UnloadRenderTexture(target);
            Console.WriteLine(path);
        }
        return;
    }

    if (readmeScreenshot)
    {
        RenderReadmeScreenshot(output, args.Length > 2 ? int.Parse(args[2]) : 1440, args.Length > 3 ? int.Parse(args[3]) : 900);
        return;
    }

    foreach (var (width, height) in new[] {
        (1280, 720), (1920, 1080),
        (1000, 700) })
    foreach (var stage in Enum.GetValues<SeasonStage>())
    {
        Raylib.SetWindowSize(width, height);
        Constants.UpdateFieldRect();
        var target = Raylib.LoadRenderTexture(width, height);
        Raylib.BeginTextureMode(target);
        Raylib.ClearBackground(Palette.Background);
        new FieldRenderer().DrawField(40f, 50f, "LIGHTNING", new Color(114, 68, 185, 255),
            "SCARLET GUARD", new Color(197, 57, 62, 255), stage,
            new CrowdBackdropState(0.5f, 0.3f, 0.4f, "", 0f, 0f, 0f, 0.7f, 0.8f, 0.7f, true, 1.25f), down: 3);
        var heldPosition = new Vector2(25, 35);
        PixelPlayerRenderer.Draw(Constants.WorldToScreen(heldPosition), Vector2.Zero, "QB", new Color(114, 68, 185, 255));
        FootballRenderer.Draw(heldPosition, Vector2.Zero, BallState.HeldByQB, 0f, 0f, heldPosition, Vector2.Zero);
        for (int i = 0; i < 4; i++)
        {
            var position = new Vector2(12 + i * 9, 56 + i * 9);
            FootballRenderer.DrawGroundShadow(position, BallState.InAir, i * 0.9f);
            FootballRenderer.Draw(position, new Vector2(i - 2, 3), BallState.InAir, i * 0.9f, i * 0.13f);
        }
        var receiverPosition = new Vector2(37, 80);
        var catchPose = new PlayerVisualFrame(PlayerPose.Catching, 0.06f, 0.3f, Vector2.UnitY);
        PixelPlayerRenderer.Draw(Constants.WorldToScreen(receiverPosition), Vector2.UnitY * 4, "WR", new Color(114, 68, 185, 255), catchPose);
        FootballRenderer.Draw(receiverPosition, Vector2.Zero, BallState.HeldByReceiver, 0, 0, receiverPosition, Vector2.UnitY * 4, catchPose);
        PixelPlayerRenderer.Draw(Constants.WorldToScreen(new Vector2(19, 55)), Vector2.Zero, "DB", new Color(197, 57, 62, 255),
            new PlayerVisualFrame(PlayerPose.Tackled, 0.22f, 0.3f, -Vector2.UnitX));
        // Reserve the actual HUD columns to reveal stadium/panel overlap.
        Raylib.DrawRectangle(10, 0, 320, height, new Color(14, 19, 27, 255));
        Raylib.DrawRectangle(width - 330, 0, 320, height, new Color(14, 19, 27, 255));
        Raylib.DrawText("PHASE 3 / RENDER PREVIEW", 20, 24, 16, Palette.White);
        Raylib.DrawText(stage.ToString(), 20, 76, 16, Palette.White);
        Raylib.DrawText($"{width} x {height}", 20, 52, 16, Palette.White);
        Raylib.EndTextureMode();
        var capture = Raylib.LoadImageFromTexture(target.Texture);
        Raylib.ImageFlipVertical(ref capture);
        string path = Path.Combine(output, $"phase3-{width}-{stage}.png");
        Raylib.ExportImage(capture, path);
        Raylib.UnloadImage(capture);
        Raylib.UnloadRenderTexture(target);
        Console.WriteLine(path);
    }
    RenderActionSheet(output);
    RenderGoalLinePreview(output);
}
finally { Raylib.CloseWindow(); }

static void RenderTeamUniforms(string output)
{
    Raylib.SetWindowSize(1280, 900);
    Constants.UpdateFieldRect();
    var target = Raylib.LoadRenderTexture(1280, 900);
    Raylib.BeginTextureMode(target);
    Raylib.ClearBackground(Palette.Background);
    Raylib.DrawText("TEAM UNIFORMS / ALL MATCHUPS", 30, 24, 26, Palette.White);
    string[] labels = ["QB", "WR / OL", "SCARLET GUARD", "CRIMSON RUSH", "BLOODLINE BASTION"];
    int[] columns = [330, 480, 670, 900, 1140];
    for (int i = 0; i < labels.Length; i++)
        Raylib.DrawText(labels[i], columns[i] - Raylib.MeasureText(labels[i], 14) / 2, 83, 14, Palette.White);
    int row = 0;
    foreach (var team in OffensiveTeamPresets.GetMenuTeams(true))
    {
        int y = 136 + row++ * 65;
        Raylib.DrawRectangle(24, y - 22, 1232, 60, Palette.Field);
        Raylib.DrawText(team.Name, 40, y - 5, 18, Palette.White);
        Color[] colors = [team.PrimaryColor, team.GetUniformColor(),
            DefensiveTeamPresets.ScarletGuard.PrimaryColor, DefensiveTeamPresets.CrimsonRush.PrimaryColor,
            DefensiveTeamPresets.BloodlineBastion.PrimaryColor];
        for (int col = 0; col < colors.Length; col++)
        {
            Raylib.BeginMode2D(new Camera2D { Target = Vector2.Zero, Offset = new Vector2(columns[col], y), Zoom = 1.7f });
            PixelPlayerRenderer.Draw(Vector2.Zero, Vector2.Zero, col == 0 ? "QB" : col == 1 ? "WR" : "DB", colors[col]);
            Raylib.EndMode2D();
        }
    }
    Raylib.EndTextureMode();
    var capture = Raylib.LoadImageFromTexture(target.Texture);
    Raylib.ImageFlipVertical(ref capture);
    string path = Path.Combine(output, "uniforms.png");
    if (!Raylib.ExportImage(capture, path)) throw new IOException($"Could not export {path}");
    Raylib.UnloadImage(capture);
    Raylib.UnloadRenderTexture(target);
}

static void RenderReadmeScreenshot(string path, int width, int height)
{


    Raylib.SetWindowSize(width, height);
    Constants.UpdateFieldRect();
    var state = new GameStateManager();
    using var session = new GameSession(state, new PlayManager(), new InputManager(),
        new FieldRenderer(), new HudRenderer(), new FireworksEffect(), new Random(42),
        new FormationFactory(), new DefenseFactory(), new StatisticsTracker(),
        new ThrowingMechanics(), new ReplayRecorder(), new ReplayClipStore(),
        new ReplayPlayer(), new ReplayStateHandler());
    session.SetOffensiveTeam(OffensiveTeamPresets.Ballers);
    session.SetDefensiveTeam(DefensiveTeamPresets.ScarletGuard);
    state.SetState(GameState.PreSnap);
    // Let the normal pre-snap logic select a play and build both teams.
    session.Update(1f / 60f);

    var target = Raylib.LoadRenderTexture(width, height);
    Raylib.BeginTextureMode(target);
    Raylib.ClearBackground(Palette.Background);
    session.Draw();
    Raylib.EndTextureMode();
    var capture = Raylib.LoadImageFromTexture(target.Texture);
    Raylib.ImageFlipVertical(ref capture);
    if (!Raylib.ExportImage(capture, path))
        throw new IOException($"Could not export screenshot to {path}");
    Raylib.UnloadImage(capture);
    Raylib.UnloadRenderTexture(target);
    Console.WriteLine(path);
}

static void RenderActionSheet(string output)
{
    Raylib.SetWindowSize(1280, 720);
    Constants.UpdateFieldRect();
    var target = Raylib.LoadRenderTexture(1280, 720);
    Raylib.BeginTextureMode(target);
    Raylib.ClearBackground(new Color(14, 19, 27, 255));
    Raylib.DrawText("PHASE 3 / PLAYER ACTION FRAMES", 24, 22, 24, Palette.White);
    Raylib.DrawText("3x detail view - recorded pose time drives gameplay and replay", 24, 56, 16, Palette.White);
    float[] times = [0f, 0.06f, 0.14f, 0.22f, 0.34f];
    string[] labels = ["RUN / FACING", "THROW / RELEASE", "CATCH / TUCK", "CONTACT / FALL"];
    string[] facingLabels = ["UPFIELD", "LEFT", "RIGHT", "DOWNFIELD", "STOPPED"];
    Vector2[] directions = [Vector2.UnitY, -Vector2.UnitX, Vector2.UnitX, -Vector2.UnitY, Vector2.UnitY];
    PlayerPose[] poses = [PlayerPose.Normal, PlayerPose.Throwing, PlayerPose.Catching, PlayerPose.Tackled];
    Vector2 position = new(25, 40);
    Vector2 screen = Constants.WorldToScreen(position);
    for (int row = 0; row < 4; row++)
    {
        int rowY = 120 + row * 145;
        Raylib.DrawText(labels[row], 24, rowY + 45, 14, Palette.Gold);
        for (int col = 0; col < 5; col++)
        {
            int x = 220 + col * 205;
            Raylib.DrawRectangle(x, rowY, 194, 130, new Color(13, 68, 35, 255));
            Raylib.DrawText(row == 0 ? facingLabels[col] : $"{times[col]:0.00}s", x + 12, rowY + 9, 14, Palette.White);
            Vector2 direction = row == 0 ? directions[col] : Vector2.UnitX;
            Vector2 velocity = row == 0 && col < 4 ? direction * 5 : Vector2.Zero;
            var visual = PlayerAnimation.Advance(new PlayerVisualFrame(poses[row], 0f, 0.10f, direction), times[col], velocity);
            Raylib.BeginMode2D(new Camera2D { Target = screen, Offset = new Vector2(x + 97, rowY + 78), Zoom = 3f });
            PixelPlayerRenderer.Draw(screen, velocity, row == 2 ? "WR" : "QB", new Color(114, 68, 185, 255), visual);
            if (row >= 2)
                FootballRenderer.Draw(position, Vector2.Zero, BallState.HeldByReceiver, 0, 0, position, velocity, visual);
            Raylib.EndMode2D();
        }
    }
    Raylib.EndTextureMode();
    var capture = Raylib.LoadImageFromTexture(target.Texture);
    Raylib.ImageFlipVertical(ref capture);
    Raylib.ExportImage(capture, Path.Combine(output, "phase3-action-frames.png"));
    Raylib.UnloadImage(capture);
    Raylib.UnloadRenderTexture(target);
}

static void RenderGoalLinePreview(string output)
{
    Raylib.SetWindowSize(1280, 720);
    Constants.UpdateFieldRect();
    var target = Raylib.LoadRenderTexture(1280, 720);
    Raylib.BeginTextureMode(target);
    new FieldRenderer().DrawField(104f, 110f, "LIGHTNING", new Color(114, 68, 185, 255),
        "SCARLET GUARD", new Color(197, 57, 62, 255), SeasonStage.SuperBowl,
        new CrowdBackdropState(0.6f, 0.5f, 0.7f, "", 0, 0, 0, AnimationTime: 0.4f), down: 4);
    Raylib.DrawRectangle(10, 0, 320, 720, new Color(14, 19, 27, 255));
    Raylib.DrawRectangle(950, 0, 320, 720, new Color(14, 19, 27, 255));
    Raylib.DrawText("FOURTH AND GOAL", 24, 30, 20, Palette.White);
    Raylib.DrawText("Down marker: 4", 24, 70, 16, Palette.White);
    Raylib.DrawText("Ten-yard chain parked", 24, 98, 16, Palette.White);
    Raylib.EndTextureMode();
    var capture = Raylib.LoadImageFromTexture(target.Texture);
    Raylib.ImageFlipVertical(ref capture);
    Raylib.ExportImage(capture, Path.Combine(output, "phase3-goal-line.png"));
    Raylib.UnloadImage(capture);
    Raylib.UnloadRenderTexture(target);
}

static void RenderPlayFeedback(string path, int width, int height, string scenario)
{
    Raylib.SetWindowSize(width, height);
    Constants.UpdateFieldRect();
    var manager = new PlayManager();
    bool run = scenario is "run" or "handoff";
    if (run) manager.SelectRunPlay(1, new Random(1));
    if (scenario is "play-action" or "fake" or "ready")
    {
        const string id = "pass.gun-doubles.pa-cross";
        manager.SetCallSheet(new PlayCallSheet(manager.Catalog, manager.CallSheet.PassIds.Take(9).Append(id), manager.CallSheet.RunIds));
        manager.SelectPassPlay(9, new Random(1));
    }
    var field = new FormationFactory().CreateFormation(manager.SelectedPlay, manager.LineOfScrimmage);
    RetroQB.Routes.RouteAssigner.AssignRoutes(field.Receivers, manager.SelectedPlay);
    var priority = new ReceiverPriorityManager();
    priority.AssignPriorities(field.Receivers);
    var drawing = new DrawingController(new FieldRenderer(), new HudRenderer(), new FireworksEffect(), priority);
    var execution = new PlayExecutionController(new PreviewMovement(), new BlockingController());
    bool active = scenario is "catch" or "handoff" or "fake" or "ready";
    if (scenario is "fake" or "ready")
    {
        var rb = field.Receivers.Single(r => r.Slot == manager.SelectedPlay.Backfield.Participant);
        rb.Position = manager.SelectedPlay.Backfield.GetMeshPoint(field.Qb.Position);
        int frames = scenario == "fake" ? 2 : 26;
        for (int i = 0; i < frames; i++)
            execution.UpdatePlay(field.Qb, field.Ball, field.Receivers, [], [], manager, false, false, false, _ => { }, 1f / 60);
    }
    if (scenario is "catch" or "handoff")
    {
        var carrier = run ? field.Receivers.Single(r => r.Slot == manager.SelectedPlay.BallCarrierSlot) : field.Receivers[0];
        carrier.HasBall = true;
        field.Qb.HasBall = false;
        if (!run) carrier.Position = new Vector2(8, manager.LineOfScrimmage + 12);
        field.Ball.SetHeld(carrier, BallState.HeldByReceiver);
        execution.ObservePossession(field.Ball, manager);
    }
    var target = Raylib.LoadRenderTexture(width, height);
    Raylib.BeginTextureMode(target);
    Raylib.ClearBackground(Palette.Background);
    drawing.Draw(manager, field.Qb, field.Ball, field.Receivers, field.Blockers, [],
        OffensiveTeamPresets.GetMenuTeams(false), active ? GameState.PlayActive : GameState.PreSnap,
        "", "", "", "", 0, OffensiveTeamPresets.Ballers, DefensiveTeamPresets.ScarletGuard,
        0, "", "", "", "", false, LeaderboardSummary.Empty, false, false, "", "", false,
        SeasonStage.RegularSeason, new SeasonSummary(), false, default, execution);
    Raylib.EndTextureMode();
    var capture = Raylib.LoadImageFromTexture(target.Texture);
    Raylib.ImageFlipVertical(ref capture);
    if (!Raylib.ExportImage(capture, path)) throw new IOException(path);
    Raylib.UnloadImage(capture);
    Raylib.UnloadRenderTexture(target);
}

sealed class PreviewMovement : IPlayerMovementInput
{
    public Vector2 GetMovementDirection() => Vector2.Zero;
    public bool IsSprintHeld() => false;
}
