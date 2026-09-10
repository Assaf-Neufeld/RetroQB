using RetroQB.Entities;
using static RetroQB.Entities.ReceiverSlot;
using static RetroQB.Routes.RouteType;

namespace RetroQB.Gameplay;

/// <summary>The existing catalog, authored by stable player slot with no setup-time substitutions.</summary>
public static class PlaybookBuilder
{
    private static readonly FormationType[] PassWildcardFormations =
    [
        FormationType.BaseTripsRight, FormationType.BaseTripsLeft, FormationType.BaseSplit,
        FormationType.BaseBunchRight, FormationType.BaseBunchLeft, FormationType.PassSpread,
        FormationType.PassBunchRight, FormationType.PassBunchLeft, FormationType.PassEmpty
    ];
    private static readonly FormationType[] RunWildcardFormations =
    [
        FormationType.RunIForm, FormationType.RunPowerRight, FormationType.RunPowerLeft,
        FormationType.RunPistolStrongRight, FormationType.RunPistolStrongLeft,
        FormationType.RunSweepRight, FormationType.RunSweepLeft,
        FormationType.RunStretchRight, FormationType.RunStretchLeft,
        FormationType.RunSinglebackTripsRight, FormationType.RunSinglebackTripsLeft
    ];

    public static List<PlayDefinition> BuildPassPlays() =>
    [
        CreatePassWildcardPlay(new Random(0)),
        Pass("pass.mesh", "Mesh", FormationType.PassSpread, new()
        {
            [WR1] = R(InShallow), [WR2] = R(Slant), [WR3] = R(InShallow), [WR4] = R(Go), [TE1] = R(Flat)
        }),
        // Preserve the original effective inside route, previously rewritten at setup time.
        Pass("pass.bunch-quick", "Bunch Quick", FormationType.PassBunchRight, new()
        {
            [WR1] = R(InShallow), [WR2] = R(Slant), [WR3] = R(Flat), [WR4] = R(DoubleMove), [TE1] = R(InShallow)
        }),
        Pass("pass.four-verts", "Four Verts", FormationType.PassSpread, new()
        {
            [WR1] = R(Go), [WR2] = R(Go), [WR3] = R(Go), [WR4] = R(Go), [TE1] = R(Go)
        }),
        Pass("pass.deep-ins", "Deep Ins", FormationType.BaseBunchLeft, new()
        {
            [WR1] = R(Go), [WR2] = R(InDeep), [WR3] = R(PostDeep), [TE1] = R(OutShallow), [RB1] = B()
        }),
        Pass("pass.flood", "Flood", FormationType.PassBunchLeft, new()
        {
            [WR1] = R(Flat), [WR2] = R(OutShallow), [WR3] = R(OutDeep), [WR4] = R(Go), [TE1] = R(InShallow)
        }),
        Pass("pass.smash", "Smash", FormationType.BaseSplit, new()
        {
            [WR1] = R(InShallow), [WR2] = R(OutDeep), [WR3] = R(Go), [TE1] = R(InShallow), [RB1] = B()
        }),
        Pass("pass.slant-flat", "Slant Flat", FormationType.BaseBunchRight, new()
        {
            [WR1] = R(Slant), [WR2] = R(Slant), [WR3] = R(Flat), [TE1] = R(Go), [RB1] = R(Flat, 1)
        }),
        new("pass.pa-deep", "PA Deep", PlayType.Pass, FormationCatalog.Get(FormationType.BaseSplit),
            new Dictionary<ReceiverSlot, PlayerAssignment>
        {
            [WR1] = R(Go), [WR2] = R(PostDeep), [WR3] = R(InDeep), [TE1] = R(DoubleMove), [RB1] = R(Flat, 1)
        }, backfield: new BackfieldSequence(BackfieldAction.PlayAction, RB1, new(.7f, -.5f), speedMultiplier: .9f),
            info: new("Split", "PA Deep", PlayCategory.PlayAction, "Fake the handoff, then read the post over the dig.")),
        Pass("pass.combo", "Combo", FormationType.BaseSplit, new()
        {
            [WR1] = R(InShallow), [WR2] = R(Go), [WR3] = R(InDeep), [TE1] = R(InShallow), [RB1] = R(Flat, 1)
        })
    ];

    public static List<PlayDefinition> BuildRunPlays() =>
    [
        CreateRunWildcardPlay(new Random(0)),
        Run("run.hb-dive", "HB Dive", FormationType.RunIForm, RunConcept.Dive, 0, new()
        {
            [WR1] = R(Slant), [RB1] = C(Flat), [TE1] = B(), [TE2] = B()
        }),
        Run("run.power-right", "Power Right", FormationType.RunPowerRight, RunConcept.Power, 1, Heavy(Go, OutShallow)),
        Run("run.power-left", "Power Left", FormationType.RunPowerLeft, RunConcept.Power, -1, Heavy(Go, OutShallow)),
        Run("run.counter-right", "Counter Right", FormationType.RunPistolStrongRight, RunConcept.Counter, 1, Heavy(InShallow, Flat)),
        Run("run.counter-left", "Counter Left", FormationType.RunPistolStrongLeft, RunConcept.Counter, -1, Heavy(InShallow, Flat)),
        Run("run.sweep-right", "Sweep Right", FormationType.RunSinglebackTripsRight, RunConcept.Sweep, 1, Spread(Flat)),
        Run("run.sweep-left", "Sweep Left", FormationType.RunSinglebackTripsLeft, RunConcept.Sweep, -1, Spread(Flat)),
        Run("run.stretch-right", "Stretch Right", FormationType.RunSinglebackTripsRight, RunConcept.Stretch, 1, Spread(OutShallow)),
        Run("run.draw", "Draw", FormationType.RunPistolStrongRight, RunConcept.Draw, 0, Heavy(Go, Flat))
    ];

    public static PlayCatalog BuildCatalog() => new(BuildPassPlays().Concat(BuildRunPlays()).Concat(ExpandedPlaybook.Build()));

    public static PlayDefinition CreatePassWildcardPlay(Random rng)
    {
        var formation = FormationCatalog.Get(PassWildcardFormations[rng.Next(PassWildcardFormations.Length)]);
        bool blockRb = rng.Next(2) == 0;
        bool blockTe = rng.Next(2) == 0;
        var slots = formation.AlignmentSlots;
        var wide = slots.Select((slot, i) => (slot, x: formation.Alignment.SkillPositions[i].XFraction))
            .Where(p => p.slot.IsWideReceiverSlot()).OrderBy(p => p.x).ToArray();
        var assignments = new Dictionary<ReceiverSlot, PlayerAssignment>();
        foreach (var slot in slots)
        {
            int? side = slot.IsRunningBackSlot() ? (rng.Next(2) == 0 ? -1 : 1) : null;
            if ((slot.IsRunningBackSlot() && blockRb) || (slot.IsTightEndSlot() && blockTe))
            {
                assignments[slot] = B() with { RouteSide = side };
                continue;
            }
            RouteType route = PickWildcardPassRoute(rng.Next(100));
            if (slot.IsRunningBackSlot())
                route = route switch
                {
                    DoubleMove or Go or PostDeep or PostShallow => Flat,
                    InDeep or OutDeep => OutShallow,
                    _ => route
                };
            if (slot == wide[0].slot || slot == wide[^1].slot)
                route = route switch { OutShallow => InShallow, OutDeep => InDeep, _ => route };
            assignments[slot] = R(route, side);
        }
        return new PlayDefinition("pass.wildcard", "Wildcard", PlayType.Pass, formation, assignments, isWildcard: true);
    }

    public static PlayDefinition CreateRunWildcardPlay(Random rng)
    {
        var formation = RunWildcardFormations[rng.Next(RunWildcardFormations.Length)];
        var concept = formation switch
        {
            FormationType.RunIForm => RunConcept.Dive,
            FormationType.RunPowerRight or FormationType.RunPowerLeft => RunConcept.Power,
            FormationType.RunPistolStrongRight or FormationType.RunPistolStrongLeft => rng.Next(3) switch
            {
                0 => RunConcept.Power, 1 => RunConcept.Counter, _ => RunConcept.Stretch
            },
            FormationType.RunSweepRight or FormationType.RunSweepLeft => RunConcept.Sweep,
            FormationType.RunStretchRight or FormationType.RunStretchLeft => RunConcept.Stretch,
            _ => rng.Next(2) == 0 ? RunConcept.Sweep : RunConcept.Draw
        };
        int side = formation switch
        {
            FormationType.RunIForm => 0,
            FormationType.RunSinglebackTripsRight => rng.Next(2) == 0 ? 1 : 0,
            FormationType.RunSinglebackTripsLeft => rng.Next(2) == 0 ? -1 : 0,
            FormationType.RunPowerLeft or FormationType.RunPistolStrongLeft or FormationType.RunSweepLeft or FormationType.RunStretchLeft => -1,
            _ => 1
        };
        RouteType wrRoute = concept switch { RunConcept.Dive => Slant, RunConcept.Counter => InShallow, _ => Go };
        RouteType rbRoute = concept is RunConcept.Dive or RunConcept.Counter or RunConcept.Sweep or RunConcept.Draw ? Flat : OutShallow;
        var assignments = formation is FormationType.RunSinglebackTripsRight or FormationType.RunSinglebackTripsLeft
            ? Spread(rbRoute) : Heavy(wrRoute, rbRoute);
        return new PlayDefinition("run.wildcard", "Wildcard", PlayType.Run, FormationCatalog.Get(formation),
            assignments, concept, side, isWildcard: true);
    }

    private static PlayDefinition Pass(string id, string name, FormationType formation, Dictionary<ReceiverSlot, PlayerAssignment> assignments) =>
        new(id, name, PlayType.Pass, FormationCatalog.Get(formation), assignments);
    private static PlayDefinition Run(string id, string name, FormationType formation, RunConcept concept, int side,
        Dictionary<ReceiverSlot, PlayerAssignment> assignments) =>
        new(id, name, PlayType.Run, FormationCatalog.Get(formation), assignments, concept, side);
    private static PlayerAssignment R(RouteType route, int? side = null) => new(AssignmentRole.Route, route, side);
    private static PlayerAssignment B() => new(AssignmentRole.Block);
    private static PlayerAssignment C(RouteType route) => new(AssignmentRole.BallCarrier, route);
    private static Dictionary<ReceiverSlot, PlayerAssignment> Heavy(RouteType wrRoute, RouteType rbRoute) => new()
    {
        [WR1] = R(wrRoute), [RB1] = C(rbRoute), [TE1] = B(), [TE2] = B()
    };
    private static Dictionary<ReceiverSlot, PlayerAssignment> Spread(RouteType rbRoute) => new()
    {
        [WR1] = R(Go), [RB1] = C(rbRoute), [WR2] = R(OutShallow), [WR3] = R(Go), [TE1] = B()
    };
    private static RouteType PickWildcardPassRoute(int roll) =>
        roll < 20 ? Slant : roll < 35 ? OutShallow : roll < 50 ? InShallow : roll < 62 ? DoubleMove :
        roll < 74 ? Go : roll < 84 ? PostShallow : roll < 90 ? PostDeep : roll < 95 ? OutDeep : InDeep;
}
