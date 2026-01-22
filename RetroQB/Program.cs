using Raylib_cs;
using RetroQB.Core;
using RetroQB.Gameplay;

Raylib.InitWindow(Constants.ScreenWidth, Constants.ScreenHeight, "RetroQB");
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
