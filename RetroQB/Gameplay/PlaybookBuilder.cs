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
        FormationType.RunPowerRight,
        FormationType.RunPowerLeft,
        FormationType.RunIForm,
        FormationType.RunSweepRight,
        FormationType.RunSweepLeft,
        FormationType.RunStretchRight,
        FormationType.RunStretchLeft
    };

    public static List<PlayDefinition> BuildPassPlays()
    {
        return new List<PlayDefinition>
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

            // 7 - Smash (corner/flat combo)
            new(
                "Smash",
                PlayType.Pass,
                FormationType.BaseSplit,
                RunningBackRole.Block,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.DoubleMove,
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
                    [3] = RouteType.Go
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
                    [0] = RouteType.PostDeep,
                    [1] = RouteType.Go,
                    [2] = RouteType.InDeep,
                    [3] = RouteType.Go,
                    [4] = RouteType.DoubleMove
                }),

            // 0 - Double Move (double-move concept)
            new(
                "Double Move",
                PlayType.Pass,
                FormationType.BaseSplit,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.DoubleMove,
                    [1] = RouteType.Flat,
                    [2] = RouteType.DoubleMove,
                    [3] = RouteType.Go
                },
                slantDirections: new Dictionary<int, bool> { [0] = true, [2] = false })
        };
    }

    public static List<PlayDefinition> BuildRunPlays()
    {
        return new List<PlayDefinition>
        {
            // Q - Wildcard (regenerated at selection time)
            new(
                "Wildcard",
                PlayType.Run,
                FormationType.RunPowerRight,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>(),
                runningBackSide: 1),

            // W - Power Right (strong side run)
            new(
                "Power Right",
                PlayType.Run,
                FormationType.RunPowerRight,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.OutShallow
                },
                runningBackSide: 1),

            // E - Power Left (weak side run)
            new(
                "Power Left",
                PlayType.Run,
                FormationType.RunPowerLeft,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.OutShallow
                },
                runningBackSide: -1),

            // R - Sweep Right (outside run)
            new(
                "Sweep Right",
                PlayType.Run,
                FormationType.RunSweepRight,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.Flat
                },
                runningBackSide: 1),

            // T - Sweep Left (outside run)
            new(
                "Sweep Left",
                PlayType.Run,
                FormationType.RunSweepLeft,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.Flat
                },
                runningBackSide: -1),

            // Y - I-Form Dive (up the middle)
            new(
                "I-Form Dive",
                PlayType.Run,
                FormationType.RunIForm,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Slant,
                    [1] = RouteType.Go
                },
                runningBackSide: 0,
                slantDirections: new Dictionary<int, bool> { [0] = true }),

            // U - Counter Right (misdirection)
            new(
                "Counter Right",
                PlayType.Run,
                FormationType.RunIForm,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.InShallow,
                    [1] = RouteType.Flat
                },
                runningBackSide: 1),

            // I - Counter Left (misdirection)
            new(
                "Counter Left",
                PlayType.Run,
                FormationType.RunIForm,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.InShallow,
                    [1] = RouteType.Flat
                },
                runningBackSide: -1),

            // O - Stretch Right (outside zone)
            new(
                "Stretch Right",
                PlayType.Run,
                FormationType.RunStretchRight,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.OutShallow
                },
                runningBackSide: 1),

            // P - Stretch Left (outside zone)
            new(
                "Stretch Left",
                PlayType.Run,
                FormationType.RunStretchLeft,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.OutShallow
                },
                runningBackSide: -1)
        };
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
        int runningBackSide = formation switch
        {
            FormationType.RunPowerRight => 1,
            FormationType.RunPowerLeft => -1,
            FormationType.RunSweepRight => 1,
            FormationType.RunSweepLeft => -1,
            FormationType.RunStretchRight => 1,
            FormationType.RunStretchLeft => -1,
            _ => 0
        };

        Dictionary<int, RouteType> routes = GetRunRoutesForFormation(formation);
        Dictionary<int, bool>? slantDirections = GetRunSlantDirectionsForFormation(formation);

        return new PlayDefinition(
            "Wildcard",
            PlayType.Run,
            formation,
            RunningBackRole.Route,
            TightEndRole.Block,
            routes,
            runningBackSide: runningBackSide,
            slantDirections: slantDirections);
    }

    private static Dictionary<int, RouteType> GetRunRoutesForFormation(FormationType formation)
    {
        return formation switch
        {
            FormationType.RunSweepRight or FormationType.RunSweepLeft => new Dictionary<int, RouteType>
            {
                [0] = RouteType.Go,
                [1] = RouteType.Flat
            },
            FormationType.RunIForm => new Dictionary<int, RouteType>
            {
                [0] = RouteType.Slant,
                [1] = RouteType.Go
            },
            FormationType.RunStretchRight or FormationType.RunStretchLeft => new Dictionary<int, RouteType>
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

    private static Dictionary<int, bool>? GetRunSlantDirectionsForFormation(FormationType formation)
    {
        return formation == FormationType.RunIForm
            ? new Dictionary<int, bool> { [0] = true }
            : null;
    }
}
