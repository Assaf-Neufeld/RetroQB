using RetroQB.Entities;

namespace RetroQB.AI;

public static class CoverageRoleExtensions
{
    public static bool IsDeepZone(this CoverageRole role) =>
        role is CoverageRole.DeepLeft or CoverageRole.DeepMiddle or CoverageRole.DeepRight
            or CoverageRole.DeepQuarterLeft or CoverageRole.DeepQuarterRight;

    public static bool IsHookZone(this CoverageRole role) =>
        role is CoverageRole.HookLeft or CoverageRole.HookMiddle or CoverageRole.HookRight or CoverageRole.Robber;

    public static bool IsFlatZone(this CoverageRole role) =>
        role is CoverageRole.FlatLeft or CoverageRole.FlatRight;
}