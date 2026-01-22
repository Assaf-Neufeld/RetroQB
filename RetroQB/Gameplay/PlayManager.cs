using RetroQB.AI;
using RetroQB.Core;

namespace RetroQB.Gameplay;

public enum PlayType
{
    QuickPass,
    LongPass,
    QbRunFocus
}

public enum PlayOutcome
{
    Ongoing,
    Tackle,
    Incomplete,
    Touchdown,
    Interception,
    Turnover
}

public sealed class PlayManager
{
    private readonly Dictionary<PlayType, List<PlayDefinition>> _playbook;
    private readonly Dictionary<PlayType, int> _selectedPlayIndexByFamily;
    private readonly List<PlayOption> _playOptions;
    public PlayType SelectedPlayFamily { get; private set; } = PlayType.QuickPass;

    public int Down { get; private set; } = 1;
    public float Distance { get; private set; } = 10f;
    public float LineOfScrimmage { get; private set; } = Constants.EndZoneDepth + 20f;
    public float FirstDownLine { get; private set; } = Constants.EndZoneDepth + 30f;
    public int Score { get; private set; }
    public int SelectedReceiver { get; set; }
    public PlayDefinition SelectedPlay => _playbook[SelectedPlayFamily][_selectedPlayIndexByFamily[SelectedPlayFamily]];
    public int SelectedPlayIndex => _selectedPlayIndexByFamily[SelectedPlayFamily];
    public float DefenderSpeedMultiplier { get; private set; } = 1.0f;
    public IReadOnlyList<PlayOption> PlayOptions => _playOptions;
    
    // Drive history - stores play results for the current drive
    public List<string> DriveHistory { get; } = new();
    public int PlayNumber { get; private set; } = 1;

    public PlayManager()
    {
        _playbook = BuildPlaybook();
        _selectedPlayIndexByFamily = new Dictionary<PlayType, int>
        {
            [PlayType.QuickPass] = 0,
            [PlayType.LongPass] = 0,
            [PlayType.QbRunFocus] = 0
        };
        _playOptions = BuildPlayOptions(_playbook);
    }

    public void StartNewDrive()
    {
        Down = 1;
        Distance = 10f;
        LineOfScrimmage = Constants.EndZoneDepth + 20f;
        FirstDownLine = LineOfScrimmage + Distance;
        DriveHistory.Clear();
        PlayNumber = 1;
        SelectedPlayFamily = PlayType.QuickPass;
    }

    public void StartPlay()
    {
        SelectedReceiver = 0;
    }

    public void SelectPlayFamily(PlayType family)
    {
        if (SelectedPlayFamily == family)
        {
            int count = _playbook[family].Count;
            _selectedPlayIndexByFamily[family] = (_selectedPlayIndexByFamily[family] + 1) % count;
        }
        else
        {
            SelectedPlayFamily = family;
        }
    }

    public bool SelectPlayByGlobalIndex(int globalIndex)
    {
        if (globalIndex < 0 || globalIndex >= _playOptions.Count)
        {
            return false;
        }

        var option = _playOptions[globalIndex];
        SelectedPlayFamily = option.Family;
        _selectedPlayIndexByFamily[option.Family] = option.Index;
        return true;
    }

    public string GetPlayLabel(PlayType family)
    {
        if (!_playbook.TryGetValue(family, out var plays) || plays.Count == 0)
        {
            return family.ToString();
        }

        int index = _selectedPlayIndexByFamily[family];
        string familyName = family switch
        {
            PlayType.QuickPass => "Quick",
            PlayType.LongPass => "Long",
            PlayType.QbRunFocus => "Run",
            _ => "Play"
        };
        return $"{familyName}: {plays[index].Name}";
    }

    public string ResolvePlay(float newBallY, bool incomplete, bool intercepted, bool touchdown)
    {
        string result;
        int playNum = PlayNumber;
        PlayNumber++;
        
        if (touchdown)
        {
            Score += 7;
            result = "TOUCHDOWN! +7";
            DriveHistory.Add($"#{playNum}: {result}");
            StartNewDrive();
            DefenderSpeedMultiplier += 0.03f;
            return result;
        }

        if (intercepted)
        {
            result = "INTERCEPTION!";
            DriveHistory.Add($"#{playNum}: {result}");
            StartNewDrive();
            return result;
        }

        if (incomplete)
        {
            Down++;
            result = HandleDowns(0f, true);
            DriveHistory.Add($"#{playNum}: {result}");
            return result;
        }

        float gain = newBallY - LineOfScrimmage;
        LineOfScrimmage = newBallY;
        if (LineOfScrimmage > Constants.EndZoneDepth + 100f)
        {
            LineOfScrimmage = Constants.EndZoneDepth + 100f;
        }

        if (gain >= Distance)
        {
            Down = 1;
            Distance = 10f;
            FirstDownLine = MathF.Min(LineOfScrimmage + Distance, Constants.EndZoneDepth + 100f);
            DefenderSpeedMultiplier += 0.02f;
            result = $"+{gain:F0} yds, 1ST DOWN!";
            DriveHistory.Add($"#{playNum}: {result}");
            return result;
        }

        Down++;
        Distance -= gain;
        FirstDownLine = MathF.Min(LineOfScrimmage + Distance, Constants.EndZoneDepth + 100f);
        result = HandleDowns(gain, false);
        DriveHistory.Add($"#{playNum}: {result}");
        return result;
    }

    private string HandleDowns(float gain, bool incomplete)
    {
        if (Down > 4)
        {
            string tod = "TURNOVER ON DOWNS";
            StartNewDrive();
            return tod;
        }

        if (incomplete)
        {
            return "Incomplete";
        }

        return $"+{gain:F0} yds";
    }

    public float GetYardLineDisplay(float worldY)
    {
        float yardLine = worldY - Constants.EndZoneDepth;
        yardLine = MathF.Max(0f, MathF.Min(100f, yardLine));
        return yardLine;
    }

    private static Dictionary<PlayType, List<PlayDefinition>> BuildPlaybook()
    {
        return new Dictionary<PlayType, List<PlayDefinition>>
        {
            [PlayType.QuickPass] = new List<PlayDefinition>
            {
                new(
                    "Stick",
                    PlayType.QuickPass,
                    RunningBackRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Out,
                        [1] = RouteType.Curl,
                        [2] = RouteType.Slant,
                        [3] = RouteType.Out,
                        [4] = RouteType.Flat
                    }),
                new(
                    "Slants",
                    PlayType.QuickPass,
                    RunningBackRole.Block,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Slant,
                        [1] = RouteType.Slant,
                        [2] = RouteType.Slant,
                        [3] = RouteType.Curl
                    }),
                new(
                    "Spacing",
                    PlayType.QuickPass,
                    RunningBackRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Out,
                        [1] = RouteType.Curl,
                        [2] = RouteType.Out,
                        [3] = RouteType.Curl,
                        [4] = RouteType.Flat
                    })
            },
            [PlayType.LongPass] = new List<PlayDefinition>
            {
                new(
                    "Verts",
                    PlayType.LongPass,
                    RunningBackRole.Block,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,
                        [1] = RouteType.Go,
                        [2] = RouteType.Go,
                        [3] = RouteType.Post
                    }),
                new(
                    "Post-Cross",
                    PlayType.LongPass,
                    RunningBackRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Post,
                        [1] = RouteType.Go,
                        [2] = RouteType.Post,
                        [3] = RouteType.Out,
                        [4] = RouteType.Flat
                    }),
                new(
                    "Deep Outs",
                    PlayType.LongPass,
                    RunningBackRole.Block,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,
                        [1] = RouteType.Post,
                        [2] = RouteType.Go,
                        [3] = RouteType.Out
                    }),
                new(
                    "Dagger",
                    PlayType.LongPass,
                    RunningBackRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,
                        [1] = RouteType.Out,
                        [2] = RouteType.Post,
                        [3] = RouteType.Curl,
                        [4] = RouteType.Flat
                    })
            },
            [PlayType.QbRunFocus] = new List<PlayDefinition>
            {
                new(
                    "QB Draw",
                    PlayType.QbRunFocus,
                    RunningBackRole.Block,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Curl,
                        [1] = RouteType.Out,
                        [2] = RouteType.Curl,
                        [3] = RouteType.Out
                    }),
                new(
                    "Boot Flat",
                    PlayType.QbRunFocus,
                    RunningBackRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Slant,
                        [1] = RouteType.Out,
                        [2] = RouteType.Out,
                        [3] = RouteType.Curl,
                        [4] = RouteType.Flat
                    },
                    runningBackSide: 1),
                new(
                    "Sprint Option",
                    PlayType.QbRunFocus,
                    RunningBackRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Out,
                        [1] = RouteType.Curl,
                        [2] = RouteType.Out,
                        [3] = RouteType.Curl,
                        [4] = RouteType.Flat
                    },
                    runningBackSide: -1)
            }
        };
    }

    private static List<PlayOption> BuildPlayOptions(Dictionary<PlayType, List<PlayDefinition>> playbook)
    {
        var list = new List<PlayOption>();
        PlayType[] order = { PlayType.QuickPass, PlayType.LongPass, PlayType.QbRunFocus };

        foreach (var family in order)
        {
            if (!playbook.TryGetValue(family, out var plays))
            {
                continue;
            }

            for (int i = 0; i < plays.Count; i++)
            {
                list.Add(new PlayOption(family, i, plays[i].Name));
            }
        }

        return list;
    }

    public readonly record struct PlayOption(PlayType Family, int Index, string Name);
}
