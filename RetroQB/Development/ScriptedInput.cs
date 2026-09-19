using System.Numerics;
using RetroQB.Input;

namespace RetroQB.Development;

internal sealed record InputFrame(Vector2 Movement = default, bool Sprint = false,
    bool Space = false, bool Enter = false, bool FieldGoal = false, bool Replay = false,
    bool Escape = false, bool Restart = false, int? ThrowTarget = null,
    int? PassSelection = null, int? RunSelection = null);

internal sealed class ScriptedInput : IGameInput
{
    public InputFrame Frame { get; set; } = new();
    public Vector2 GetMovementDirection() => Frame.Movement;
    public bool IsSprintHeld() => Frame.Sprint;
    public bool IsSpacePressed() => Frame.Space;
    public bool IsEnterPressed() => Frame.Enter;
    public bool IsFieldGoalPressed() => Frame.FieldGoal;
    public bool IsReplayPressed() => Frame.Replay;
    public bool IsReplaySkipPressed() => Frame.Space;
    public bool IsEscapePressed() => Frame.Escape;
    public bool IsRestartPressed() => Frame.Restart;
    public int? GetThrowTarget() => Frame.ThrowTarget;
    public int? GetPassPlaySelection() => Frame.PassSelection;
    public int? GetRunPlaySelection() => Frame.RunSelection;
}
