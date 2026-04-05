using RetroQB.AI;
using RetroQB.Entities;

namespace RetroQB.Gameplay;

internal enum CoverageUnitType
{
    Linebacker,
    Safety
}

internal readonly record struct CoverageRoleSet(CoverageRole Left, CoverageRole Middle, CoverageRole Right);

internal readonly record struct CoverageSchemePolicy(
    bool IsZoneScheme,
    bool LinebackersPlayMan,
    bool SafetiesPlayMan,
    CoverageRoleSet BaseLbRoles,
    CoverageRoleSet NickelLbRoles);

internal static class CoverageSchemePolicies
{
    private static readonly IReadOnlyDictionary<CoverageScheme, CoverageSchemePolicy> Policies =
        new Dictionary<CoverageScheme, CoverageSchemePolicy>
        {
            [CoverageScheme.Cover0] = new(
                IsZoneScheme: false,
                LinebackersPlayMan: true,
                SafetiesPlayMan: true,
                BaseLbRoles: new CoverageRoleSet(CoverageRole.None, CoverageRole.None, CoverageRole.None),
                NickelLbRoles: new CoverageRoleSet(CoverageRole.None, CoverageRole.None, CoverageRole.None)),

            [CoverageScheme.Cover1] = new(
                // Hybrid: underneath is man, but safety/help responsibilities are zone roles.
                IsZoneScheme: true,
                LinebackersPlayMan: true,
                SafetiesPlayMan: false,
                BaseLbRoles: new CoverageRoleSet(CoverageRole.None, CoverageRole.None, CoverageRole.None),
                NickelLbRoles: new CoverageRoleSet(CoverageRole.None, CoverageRole.None, CoverageRole.None)),

            [CoverageScheme.Cover2Zone] = new(
                IsZoneScheme: true,
                LinebackersPlayMan: false,
                SafetiesPlayMan: false,
                BaseLbRoles: new CoverageRoleSet(CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight),
                NickelLbRoles: new CoverageRoleSet(CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight)),

            [CoverageScheme.Cover3Zone] = new(
                IsZoneScheme: true,
                LinebackersPlayMan: false,
                SafetiesPlayMan: false,
                BaseLbRoles: new CoverageRoleSet(CoverageRole.FlatLeft, CoverageRole.HookMiddle, CoverageRole.FlatRight),
                NickelLbRoles: new CoverageRoleSet(CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight)),

            [CoverageScheme.Cover4Zone] = new(
                IsZoneScheme: true,
                LinebackersPlayMan: false,
                SafetiesPlayMan: false,
                BaseLbRoles: new CoverageRoleSet(CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight),
                NickelLbRoles: new CoverageRoleSet(CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight)),

            [CoverageScheme.Cover3Match] = new(
                IsZoneScheme: true,
                LinebackersPlayMan: false,
                SafetiesPlayMan: false,
                BaseLbRoles: new CoverageRoleSet(CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight),
                NickelLbRoles: new CoverageRoleSet(CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight)),

            [CoverageScheme.QuartersMatch] = new(
                IsZoneScheme: true,
                LinebackersPlayMan: false,
                SafetiesPlayMan: false,
                BaseLbRoles: new CoverageRoleSet(CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight),
                NickelLbRoles: new CoverageRoleSet(CoverageRole.HookLeft, CoverageRole.HookMiddle, CoverageRole.HookRight)),

            [CoverageScheme.Cover2Man] = new(
                // Hybrid: corners/LBs carry man underneath while safeties still play deep zone halves.
                IsZoneScheme: true,
                LinebackersPlayMan: true,
                SafetiesPlayMan: false,
                BaseLbRoles: new CoverageRoleSet(CoverageRole.None, CoverageRole.None, CoverageRole.None),
                NickelLbRoles: new CoverageRoleSet(CoverageRole.None, CoverageRole.None, CoverageRole.None)),

            [CoverageScheme.Robber] = new(
                // Hybrid: man underneath with a zone robber/deep-middle safety structure.
                IsZoneScheme: true,
                LinebackersPlayMan: true,
                SafetiesPlayMan: false,
                BaseLbRoles: new CoverageRoleSet(CoverageRole.None, CoverageRole.None, CoverageRole.None),
                NickelLbRoles: new CoverageRoleSet(CoverageRole.None, CoverageRole.None, CoverageRole.None))
        };

    public static bool IsZoneScheme(CoverageScheme scheme)
    {
        return GetPolicy(scheme).IsZoneScheme;
    }

    public static bool IsManForUnit(CoverageScheme scheme, CoverageUnitType unit)
    {
        CoverageSchemePolicy policy = GetPolicy(scheme);
        return unit switch
        {
            CoverageUnitType.Linebacker => policy.LinebackersPlayMan,
            CoverageUnitType.Safety => policy.SafetiesPlayMan,
            _ => false
        };
    }

    public static CoverageRoleSet GetLbRoles(CoverageScheme scheme, bool hasNickelPackage)
    {
        CoverageSchemePolicy policy = GetPolicy(scheme);
        return hasNickelPackage ? policy.NickelLbRoles : policy.BaseLbRoles;
    }

    private static CoverageSchemePolicy GetPolicy(CoverageScheme scheme)
    {
        if (!Policies.TryGetValue(scheme, out CoverageSchemePolicy policy))
        {
            throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "No policy registered for coverage scheme.");
        }

        return policy;
    }
}
