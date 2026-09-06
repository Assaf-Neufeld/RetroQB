using System.Numerics;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Entities;
using RetroQB.Rendering;

string output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/visual-phase3");
Directory.CreateDirectory(output);
Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
Raylib.InitWindow(1280, 720, "RetroQB visual preview");
try
{
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
