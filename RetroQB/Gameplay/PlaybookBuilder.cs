using RetroQB.AI;

namespace RetroQB.Gameplay;

/// <summary>
/// Builds the game's playbook with all available plays.
/// Pass plays: 10 plays (1 as wildcard, 2-9 plus 0 as regular plays)
/// Run plays: 10 plays (Q as wildcard, W-P as regular plays)
/// </summary>
public static class PlaybookBuilder
{
    private static readonly FormationType[] PassWildcardFormations =
    {
        FormationType.BaseTripsRight,
        FormationType.BaseTripsLeft,
        FormationType.BaseSplit,
        FormationType.BaseBunchRight,
        FormationType.BaseBunchLeft,
        FormationType.PassSpread,
        FormationType.PassBunchRight,
        FormationType.PassBunchLeft,
        FormationType.PassEmpty
    };

    private static readonly FormationType[] RunWildcardFormations =
    {
        FormationType.RunIForm,
        FormationType.RunPowerRight,
        FormationType.RunPowerLeft,
        FormationType.RunPistolStrongRight,
        FormationType.RunPistolStrongLeft,
        FormationType.RunSweepRight,
        FormationType.RunSweepLeft,
        FormationType.RunStretchRight,
        FormationType.RunStretchLeft,
        FormationType.RunSinglebackTripsRight,
        FormationType.RunSinglebackTripsLeft
    };

    public static List<PlayDefinition> BuildPassPlays()
    {
        var plays = new List<PlayDefinition>
        {
            // 1 - Wildcard (regenerated at selection time)
            new(
                "Wildcard",
                PlayType.Pass,
                FormationType.BaseTripsRight,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>()),

            // 2 - Mesh (quick crossing routes)
            new(
                "Mesh",
                PlayType.Pass,
                FormationType.PassSpread,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.InShallow,
                    [1] = RouteType.DoubleMove,
                    [2] = RouteType.InShallow,
                    [3] = RouteType.Go,
                    [4] = RouteType.Flat
                }),

            // 3 - Bunch Quick (tight formation, quick release)
            new(
                "Bunch Quick",
                PlayType.Pass,
                FormationType.PassBunchRight,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.OutShallow,
                    [1] = RouteType.Slant,
                    [2] = RouteType.Flat,
                    [3] = RouteType.DoubleMove,
                    [4] = RouteType.InShallow
                },
                slantDirections: new Dictionary<int, bool> { [1] = true }),

            // 4 - Four Verts (deep stretch)
            new(
                "Four Verts",
                PlayType.Pass,
                FormationType.PassSpread,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.Go,
                    [2] = RouteType.Go,
                    [3] = RouteType.Go,
                    [4] = RouteType.Go
                }),

            // 5 - Deep Ins (crossing patterns)
            new(
                "Deep Ins",
                PlayType.Pass,
                FormationType.BaseBunchLeft,
                RunningBackRole.Block,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.InDeep,
                    [2] = RouteType.PostDeep,
                    [3] = RouteType.OutShallow
                }),

            // 6 - Flood (sideline attack)
            new(
                "Flood",
                PlayType.Pass,
                FormationType.PassBunchLeft,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Flat,
                    [1] = RouteType.OutShallow,
                    [2] = RouteType.OutDeep,
                    [3] = RouteType.Go,
                    [4] = RouteType.InShallow
                }),

            // 7 - Smash (smash-style high-low combo)
            new(
                "Smash",
                PlayType.Pass,
                FormationType.BaseSplit,
                RunningBackRole.Block,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.InShallow,
                    [1] = RouteType.OutDeep,
                    [2] = RouteType.Go,
                    [3] = RouteType.InShallow
                }),

            // 8 - Slant Flat (quick slants with flat option)
            new(
                "Slant Flat",
                PlayType.Pass,
                FormationType.BaseBunchRight,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Slant,
                    [1] = RouteType.Slant,
                    [2] = RouteType.Flat,
                    [3] = RouteType.Go,
                    [4] = RouteType.Flat
                },
                slantDirections: new Dictionary<int, bool> { [0] = true, [1] = true }),

            // 9 - PA Deep (play action deep routes)
            new(
                "PA Deep",
                PlayType.Pass,
                FormationType.PassSpread,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.PostDeep,
                    [2] = RouteType.InDeep,
                    [3] = RouteType.Go,
                    [4] = RouteType.DoubleMove
                }),

            // 0 - Combo (layered in-breaking concept)
            new(
                "Combo",
                PlayType.Pass,
                FormationType.BaseSplit,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.InShallow,
                    [1] = RouteType.Go,
                    [2] = RouteType.InDeep,
                    [3] = RouteType.InShallow,
                    [4] = RouteType.Flat
                })
        };
        return plays;
    }

    public static List<PlayDefinition> BuildRunPlays()
    {
        var plays = new List<PlayDefinition>
        {
            // Q - Wildcard (regenerated at selection time)
            CreateRunPlay(
                "Wildcard",
                FormationType.RunPowerRight,
                RunConcept.Power,
                runningBackSide: 1,
                new Dictionary<int, RouteType>()),

            // W - HB Dive (downhill inside run)
            CreateRunPlay(
                "HB Dive",
                FormationType.RunIForm,
                RunConcept.Dive,
                runningBackSide: 0,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Slant,
                    [1] = RouteType.Flat
                },
                new Dictionary<int, bool> { [0] = true }),

            // E - Power Right (heavy strong-side run)
            CreateRunPlay(
                "Power Right",
                FormationType.RunPowerRight,
                RunConcept.Power,
                runningBackSide: 1,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.OutShallow
                }),

            // R - Power Left (heavy weak-side run)
            CreateRunPlay(
                "Power Left",
                FormationType.RunPowerLeft,
                RunConcept.Power,
                runningBackSide: -1,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.OutShallow
                }),

            // T - Counter Right (pistol misdirection)
            CreateRunPlay(
                "Counter Right",
                FormationType.RunPistolStrongRight,
                RunConcept.Counter,
                runningBackSide: 1,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.InShallow,
                    [1] = RouteType.Flat
                }),

            // Y - Counter Left (pistol misdirection)
            CreateRunPlay(
                "Counter Left",
                FormationType.RunPistolStrongLeft,
                RunConcept.Counter,
                runningBackSide: -1,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.InShallow,
                    [1] = RouteType.Flat
                }),

            // U - Sweep Right (spread edge run)
            CreateRunPlay(
                "Sweep Right",
                FormationType.RunSinglebackTripsRight,
                RunConcept.Sweep,
                runningBackSide: 1,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.Flat,
                    [2] = RouteType.OutShallow,
                    [3] = RouteType.Go
                }),

            // I - Sweep Left (spread edge run)
            CreateRunPlay(
                "Sweep Left",
                FormationType.RunSinglebackTripsLeft,
                RunConcept.Sweep,
                runningBackSide: -1,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.Flat,
                    [2] = RouteType.OutShallow,
                    [3] = RouteType.Go
                }),

            // O - Stretch Right (spread outside zone)
            CreateRunPlay(
                "Stretch Right",
                FormationType.RunSinglebackTripsRight,
                RunConcept.Stretch,
                runningBackSide: 1,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.OutShallow,
                    [2] = RouteType.OutShallow,
                    [3] = RouteType.Go
                }),

            // P - Draw (pistol delayed handoff)
            CreateRunPlay(
                "Draw",
                FormationType.RunPistolStrongRight,
                RunConcept.Draw,
                runningBackSide: 0,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.Flat
                })
        };

        return plays;
    }

    public static PlayDefinition CreatePassWildcardPlay(Random rng)
    {
        var formation = PassWildcardFormations[rng.Next(PassWildcardFormations.Length)];
        var rbRole = rng.Next(2) == 0 ? RunningBackRole.Block : RunningBackRole.Route;
        var teRole = rng.Next(2) == 0 ? TightEndRole.Block : TightEndRole.Route;

        return new PlayDefinition(
            "Wildcard",
            PlayType.Pass,
            formation,
            rbRole,
            teRole,
            new Dictionary<int, RouteType>());
    }

    public static PlayDefinition CreateRunWildcardPlay(Random rng)
    {
        var formation = RunWildcardFormations[rng.Next(RunWildcardFormations.Length)];
        RunConcept runConcept = GetWildcardRunConcept(formation, rng);
        int runningBackSide = formation switch
        {
            FormationType.RunPowerRight => 1,
            FormationType.RunPowerLeft => -1,
            FormationType.RunPistolStrongRight => 1,
            FormationType.RunPistolStrongLeft => -1,
            FormationType.RunSweepRight => 1,
            FormationType.RunSweepLeft => -1,
            FormationType.RunStretchRight => 1,
            FormationType.RunStretchLeft => -1,
            FormationType.RunSinglebackTripsRight => rng.Next(2) == 0 ? 1 : 0,
            FormationType.RunSinglebackTripsLeft => rng.Next(2) == 0 ? -1 : 0,
            _ => 0
        };

        Dictionary<int, RouteType> routes = GetRunRoutesForConcept(runConcept, formation);
        Dictionary<int, bool>? slantDirections = GetRunSlantDirections(runConcept);

        return CreateRunPlay(
            "Wildcard",
            formation,
            runConcept,
            runningBackSide,
            routes,
            slantDirections);
    }

    private static RunConcept GetWildcardRunConcept(FormationType formation, Random rng)
    {
        return formation switch
        {
            FormationType.RunIForm => RunConcept.Dive,
            FormationType.RunPowerRight or FormationType.RunPowerLeft => RunConcept.Power,
            FormationType.RunPistolStrongRight or FormationType.RunPistolStrongLeft => rng.Next(3) switch
            {
                0 => RunConcept.Power,
                1 => RunConcept.Counter,
                _ => RunConcept.Stretch
            },
            FormationType.RunSweepRight or FormationType.RunSweepLeft => RunConcept.Sweep,
            FormationType.RunStretchRight or FormationType.RunStretchLeft => RunConcept.Stretch,
            FormationType.RunSinglebackTripsRight or FormationType.RunSinglebackTripsLeft => rng.Next(2) == 0 ? RunConcept.Sweep : RunConcept.Draw,
            _ => RunConcept.Power
        };
    }

    private static PlayDefinition CreateRunPlay(
        string name,
        FormationType formation,
        RunConcept runConcept,
        int runningBackSide,
        IReadOnlyDictionary<int, RouteType> routes,
        IReadOnlyDictionary<int, bool>? slantDirections = null)
    {
        return new PlayDefinition(
            name,
            PlayType.Run,
            formation,
            RunningBackRole.Route,
            TightEndRole.Block,
            routes,
            runConcept,
            runningBackSide,
            slantDirections);
    }

    private static Dictionary<int, RouteType> GetRunRoutesForConcept(RunConcept runConcept, FormationType formation)
    {
        return runConcept switch
        {
            RunConcept.Dive => new Dictionary<int, RouteType>
            {
                [0] = RouteType.Slant,
                [1] = RouteType.Flat
            },
            RunConcept.Counter => new Dictionary<int, RouteType>
            {
                [0] = RouteType.InShallow,
                [1] = RouteType.Flat
            },
            RunConcept.Sweep => new Dictionary<int, RouteType>
            {
                [0] = RouteType.Go,
                [1] = RouteType.Flat,
                [2] = RouteType.OutShallow,
                [3] = RouteType.Go
            },
            RunConcept.Draw when formation is FormationType.RunSinglebackTripsRight or FormationType.RunSinglebackTripsLeft => new Dictionary<int, RouteType>
            {
                [0] = RouteType.Go,
                [1] = RouteType.Flat,
                [2] = RouteType.InShallow,
                [3] = RouteType.Go
            },
            RunConcept.Stretch => new Dictionary<int, RouteType>
            {
                [0] = RouteType.Go,
                [1] = RouteType.OutShallow
            },
            _ => new Dictionary<int, RouteType>
            {
                [0] = RouteType.Go,
                [1] = RouteType.OutShallow
            }
        };
    }

    private static Dictionary<int, bool>? GetRunSlantDirections(RunConcept runConcept)
    {
        return runConcept == RunConcept.Dive
            ? new Dictionary<int, bool> { [0] = true }
            : null;
    }
}
