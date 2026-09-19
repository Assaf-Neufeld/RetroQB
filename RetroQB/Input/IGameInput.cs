namespace RetroQB.Input;

/// <summary>Input boundary shared by the keyboard and scripted development runs.</summary>
public interface IGameInput : IPlayerMovementInput
{
    bool IsEscapePressed() => false;
    bool IsEnterPressed() => false;
    bool IsFlipPlayPressed() => false;
    bool IsSpacePressed() => false;
    bool IsFieldGoalPressed() => false;
    bool IsBackspacePressed() => false;
    bool IsReplayPressed() => false;
    bool IsReplaySkipPressed() => false;
    bool IsRestartPressed() => false;
    bool IsLeaderboardPressed() => false;
    bool IsSecretTeamPressed() => false;
    bool IsDriveSummaryScrollOlderPressed() => false;
    bool IsDriveSummaryScrollNewerPressed() => false;
    float GetMouseWheelMove() => 0;
    int GetTeamNavigation() => 0;
    int? GetTeamSelection() => null;
    int? GetNameConflictChoice() => null;
    int? GetPassPlaySelection() => null;
    int? GetRunPlaySelection() => null;
    int? GetThrowTarget() => null;
    string ReadTextInput(int maxLength) => string.Empty;
}
