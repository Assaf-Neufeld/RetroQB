namespace RetroQB.Gameplay.Defense;

/// <summary>One preview owner's last resolution. Bounded, with no global state; visible geometry,
/// call version, explicit personnel and resolution seed are the complete P1 adjustment key.</summary>
public sealed class DefensivePlanCache
{
    private ResolvedDefensivePlan? _last;
    private DefensePackage? _requestedPackage;

    public ResolvedDefensivePlan Resolve(OffensiveAlignmentSnapshot offense, DefensiveCallDefinition call,
        DefensePackage? package = null, int resolutionSeed = 101, ManCoverageAdjustments? adjustments = null, PressureSelection? pressure = null, DefensiveFront front = DefensiveFront.Even)
    {
        if (_last != null && _last.Front == front && _last.RequestedCall == call && _requestedPackage == package
            && _last.ResolutionSeed == resolutionSeed && _last.Offense.SameGeometry(offense)
            && _last.Pressure.Selection == (pressure ?? PressureSelection.For(call))
            && _last.Adjustments.Equivalent(adjustments ?? ManCoverageAdjustments.None)) return _last;
        var next = DefensivePlanCompiler.Compile(offense, call, package, resolutionSeed, adjustments, pressure, front);
        _requestedPackage = package; _last = next; return next;
    }

    public void Clear() => _last = null;
}
