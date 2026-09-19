using System.Reflection;
using System.Text;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;
using RetroQB.Development;

try
{
#if DEBUG
    const bool developmentBuild = true;
#else
    const bool developmentBuild = false;
#endif
    if (ScenarioLaunchOptions.Parse(args, developmentBuild) is { } scenario)
    {
        Environment.ExitCode = ScenarioLauncher.Run(scenario);
        return;
    }
}
catch (ArgumentException error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 2;
    return;
}

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(Constants.ScreenWidth, Constants.ScreenHeight, "RetroQB");
// Escape belongs to pause/menu handling; the window close button still exits.
Raylib.SetExitKey(KeyboardKey.Null);
SetWindowIconFromResource();
Raylib.SetTargetFPS(Constants.TargetFps);

static unsafe void SetWindowIconFromResource()
{
	var assembly = Assembly.GetExecutingAssembly();
	using var stream = assembly.GetManifestResourceStream("helmet_icon.png");
	if (stream == null) return;

	byte[] data = new byte[stream.Length];
	stream.ReadExactly(data, 0, data.Length);

	byte[] extBytes = Encoding.ASCII.GetBytes(".png\0");
	fixed (byte* dataPtr = data)
	fixed (byte* extPtr = extBytes)
	{
		Image icon = Raylib.LoadImageFromMemory((sbyte*)extPtr, dataPtr, data.Length);
		Raylib.SetWindowIcon(icon);
		Raylib.UnloadImage(icon);
	}
}

GameSession? session = null;

try
{
	session = new GameSession();

	while (!Raylib.WindowShouldClose())
	{
		float dt = Raylib.GetFrameTime();
		session.Update(dt);

		Raylib.BeginDrawing();
		Raylib.ClearBackground(Palette.Background);
		session.Draw();
		Raylib.EndDrawing();
	}
}
finally
{
	session?.Dispose();
	Raylib.CloseWindow();
}
