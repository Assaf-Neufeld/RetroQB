using Raylib_cs;
using RetroQB.Gameplay;

namespace RetroQB.Rendering;

public static class PlayDiagramStyle
{
    public static readonly Color Pass = new(75, 174, 255, 255);
    public static readonly Color Run = new(255, 151, 65, 255);
    public static readonly Color Exchange = new(225, 225, 235, 230);
    public static string Label(ResolvedPlay play) => play.Backfield.Action == BackfieldAction.PlayAction
        ? "PLAY ACTION" : play.Family == PlayType.Run ? "RUN" : "PASS";
    public static Color Color(ResolvedPlay play) => play.Family == PlayType.Run ? Run : Pass;
}
