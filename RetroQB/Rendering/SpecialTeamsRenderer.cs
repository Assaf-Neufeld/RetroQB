using Raylib_cs;
using RetroQB.Entities;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

public static class SpecialTeamsRenderer
{
    public static void Draw(SpecialTeamsPlay play, Color kickingColor, Color receivingColor, bool userReceiving)
    {
        foreach (var player in play.Coverage.Append(play.Kicker))
            PixelPlayerRenderer.Draw(Constants.WorldToScreen(player.Position), player.Velocity, player.Role, kickingColor, new(PlayerPose.Normal, 0, play.Seconds, player.Velocity));
        foreach (var player in play.Blockers.Append(play.Returner))
            PixelPlayerRenderer.Draw(Constants.WorldToScreen(player.Position), player.Velocity, player.Role, receivingColor, new(PlayerPose.Normal, 0, play.Seconds, player.Velocity));
        FootballRenderer.Draw(play.BallPosition, play.Phase == SpecialTeamsPhase.Flight ? System.Numerics.Vector2.UnitY : System.Numerics.Vector2.Zero,
            play.Phase == SpecialTeamsPhase.Return ? BallState.HeldByReceiver : BallState.InAir,
            play.BallHeight, play.Seconds, play.Phase == SpecialTeamsPhase.Return ? play.Returner.Position : null);
        var controlled = userReceiving ? play.Returner : play.Coverage[play.ControlledCoverageIndex];
        var screen = Constants.WorldToScreen(controlled.Position);
        Raylib.DrawCircleLines((int)screen.X, (int)screen.Y, 14, Palette.Gold);
    }
}
