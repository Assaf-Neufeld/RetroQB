using RetroQB.AI;

namespace RetroQB.Gameplay;

/// <summary>
/// Builds the game's playbook with all available plays.
/// Separated from PlayManager to allow easy extension and testing.
/// </summary>
public static class PlaybookBuilder
{
    private static readonly FormationType[] WildcardFormations =
    {
        FormationType.BaseTripsRight,
        FormationType.BaseTripsLeft,
        FormationType.BaseSplit,
        FormationType.PassSpread,
        FormationType.PassBunch,
        FormationType.PassEmpty
    };

    public static Dictionary<PlayType, List<PlayDefinition>> Build()
    {
        return new Dictionary<PlayType, List<PlayDefinition>>
        {
            [PlayType.QuickPass] = BuildQuickPassPlays(),
            [PlayType.LongPass] = BuildLongPassPlays(),
            [PlayType.QbRunFocus] = BuildRunPlays()
        };
    }

    public static PlayDefinition CreateWildcardPlay(Random rng)
    {
        var formation = WildcardFormations[rng.Next(WildcardFormations.Length)];
        var rbRole = rng.Next(2) == 0 ? RunningBackRole.Block : RunningBackRole.Route;
        var teRole = rng.Next(2) == 0 ? TightEndRole.Block : TightEndRole.Route;

        return new PlayDefinition(
            "Wildcard",
            PlayType.QuickPass,
            formation,
            rbRole,
            teRole,
            new Dictionary<int, RouteType>());
    }

    private static List<PlayDefinition> BuildQuickPassPlays()
    {
        return new List<PlayDefinition>
        {
            // Wildcard placeholder - regenerated at selection time
            new(
                "Wildcard",
                PlayType.QuickPass,
                FormationType.BaseTripsRight,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>()),

            new(
                "Mesh",
                PlayType.QuickPass,
                FormationType.PassSpread,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.InShallow,
                    [1] = RouteType.InShallow,
                    [2] = RouteType.Curl,
                    [3] = RouteType.OutShallow,
                    [4] = RouteType.Flat
                }),

            new(
                "Bunch Quick",
                PlayType.QuickPass,
                FormationType.PassBunch,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.OutShallow,
                    [1] = RouteType.Slant,
                    [2] = RouteType.Flat,
                    [3] = RouteType.Curl,
                    [4] = RouteType.InShallow
                },
                slantDirections: new Dictionary<int, bool> { [1] = true })
        };
    }

    private static List<PlayDefinition> BuildLongPassPlays()
    {
        return new List<PlayDefinition>
        {
            new(
                "Four Verts",
                PlayType.LongPass,
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

            new(
                "Deep Ins",
                PlayType.LongPass,
                FormationType.BaseTripsLeft,
                RunningBackRole.Block,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.InDeep,
                    [2] = RouteType.PostDeep,
                    [3] = RouteType.OutShallow
                }),

            new(
                "Flood",
                PlayType.LongPass,
                FormationType.PassSpread,
                RunningBackRole.Route,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.InDeep,
                    [1] = RouteType.Curl,
                    [2] = RouteType.OutDeep,
                    [3] = RouteType.Go,
                    [4] = RouteType.Flat
                }),

            new(
                "Smash",
                PlayType.LongPass,
                FormationType.BaseSplit,
                RunningBackRole.Block,
                TightEndRole.Route,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Curl,
                    [1] = RouteType.OutDeep,
                    [2] = RouteType.Go,
                    [3] = RouteType.InShallow
                })
        };
    }

    private static List<PlayDefinition> BuildRunPlays()
    {
        return new List<PlayDefinition>
        {
            new(
                "Power Right",
                PlayType.QbRunFocus,
                FormationType.RunPowerRight,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.OutShallow
                },
                runningBackSide: 1),

            new(
                "Power Left",
                PlayType.QbRunFocus,
                FormationType.RunPowerLeft,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Go,
                    [1] = RouteType.OutShallow
                },
                runningBackSide: -1),

            new(
                "I-Form Dive",
                PlayType.QbRunFocus,
                FormationType.RunIForm,
                RunningBackRole.Route,
                TightEndRole.Block,
                new Dictionary<int, RouteType>
                {
                    [0] = RouteType.Slant,
                    [1] = RouteType.Go
                },
                runningBackSide: 0,
                slantDirections: new Dictionary<int, bool> { [0] = true })
        };
    }
}
