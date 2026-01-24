using System.Reflection;
using System.Text;
using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(Constants.ScreenWidth, Constants.ScreenHeight, "RetroQB");
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

GameSession session = new();

while (!Raylib.WindowShouldClose())
{
	float dt = Raylib.GetFrameTime();
	session.Update(dt);

	Raylib.BeginDrawing();
	Raylib.ClearBackground(Palette.Background);
	session.Draw();
	Raylib.EndDrawing();
}

Raylib.CloseWindow();
