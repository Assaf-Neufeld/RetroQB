using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(Constants.ScreenWidth, Constants.ScreenHeight, "RetroQB");
string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "helmet_icon.png");
if (File.Exists(iconPath))
{
	Image windowIcon = Raylib.LoadImage(iconPath);
	Raylib.SetWindowIcon(windowIcon);
	Raylib.UnloadImage(windowIcon);
}
Raylib.SetTargetFPS(Constants.TargetFps);

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
