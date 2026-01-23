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

    public PlayType GetSuggestedPlayFamily()
    {
        if (Distance >= 9f || (Down >= 3 && Distance >= 6f))
        {
            return PlayType.LongPass;
        }

        if (Distance <= 3f)
        {
            return PlayType.QbRunFocus;
        }

        return PlayType.QuickPass;
    }

    public bool AutoSelectPlayBySituation(Random rng)
    {
        var candidates = GetSuggestedPlayFamilies();
        if (candidates.Count == 0)
        {
            return false;
        }

        PlayType pickedFamily = candidates[rng.Next(candidates.Count)];
        SelectedPlayFamily = pickedFamily;

        if (_playbook.TryGetValue(pickedFamily, out var plays) && plays.Count > 0)
        {
            _selectedPlayIndexByFamily[pickedFamily] = rng.Next(plays.Count);
            return true;
        }

        return false;
    }

    private List<PlayType> GetSuggestedPlayFamilies()
    {
        var families = new List<PlayType>();

        if (Distance >= 9f || (Down >= 3 && Distance >= 6f))
        {
            families.Add(PlayType.LongPass);
            families.Add(PlayType.LongPass);
            families.Add(PlayType.QuickPass);
            return families;
        }

        if (Distance <= 3f)
        {
            families.Add(PlayType.QbRunFocus);
            families.Add(PlayType.QbRunFocus);
            families.Add(PlayType.QuickPass);
            return families;
        }

        families.Add(PlayType.QuickPass);
        families.Add(PlayType.QuickPass);
        families.Add(PlayType.QbRunFocus);
        families.Add(PlayType.LongPass);
        return families;
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
                    "Quick Slant",
                    PlayType.QuickPass,
                    FormationType.ShotgunTrips,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.OutShallow,
                        [1] = RouteType.Slant,
                        [2] = RouteType.Curl,
                        [3] = RouteType.Curl
                    },
                    slantDirections: new Dictionary<int, bool>
                    {
                        [1] = true
                    }),
                new(
                    "Smash",
                    PlayType.QuickPass,
                    FormationType.Bunch,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.OutDeep,
                        [1] = RouteType.Curl,
                        [2] = RouteType.Slant,
                        [3] = RouteType.OutShallow
                    },
                    slantDirections: new Dictionary<int, bool>
                    {
                        [2] = false
                    }),
                new(
                    "Mesh",
                    PlayType.QuickPass,
                    FormationType.Spread,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.InShallow,
                        [1] = RouteType.InDeep,
                        [2] = RouteType.Curl,
                        [3] = RouteType.Curl
                    })
            },
            [PlayType.LongPass] = new List<PlayDefinition>
            {
                new(
                    "Deep Post",
                    PlayType.LongPass,
                    FormationType.Spread,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,
                        [1] = RouteType.PostDeep,
                        [2] = RouteType.OutShallow,
                        [3] = RouteType.Curl
                    }),
                new(
                    "Levels",
                    PlayType.LongPass,
                    FormationType.Spread,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.InDeep,
                        [1] = RouteType.InShallow,
                        [2] = RouteType.Curl,
                        [3] = RouteType.Curl
                    }),
                new(
                    "Slant Go",
                    PlayType.LongPass,
                    FormationType.Slot,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,
                        [1] = RouteType.PostDeep,
                        [2] = RouteType.Slant,
                        [3] = RouteType.Curl
                    },
                    slantDirections: new Dictionary<int, bool>
                    {
                        [2] = false
                    })
            },
            [PlayType.QbRunFocus] = new List<PlayDefinition>
            {
                new(
                    "RB Inside Zone",
                    PlayType.QbRunFocus,
                    FormationType.Shotgun,
                    RunningBackRole.Route,
                    TightEndRole.Block,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,
                        [1] = RouteType.Go,
                        [2] = RouteType.Curl,
                        [3] = RouteType.Curl
                    },
                    runningBackSide: 0),
                new(
                    "RB Power Right",
                    PlayType.QbRunFocus,
                    FormationType.Pistol,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Slant,
                        [1] = RouteType.Go,
                        [2] = RouteType.OutShallow,
                        [3] = RouteType.OutShallow
                    },
                    runningBackSide: 1,
                    slantDirections: new Dictionary<int, bool>
                    {
                        [0] = true
                    }),
                new(
                    "RB Sweep Left",
                    PlayType.QbRunFocus,
                    FormationType.Trips,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Slant,
                        [1] = RouteType.Go,
                        [2] = RouteType.Curl,
                        [3] = RouteType.OutShallow
                    },
                    runningBackSide: -1,
                    slantDirections: new Dictionary<int, bool>
                    {
                        [0] = false
                    }),
                new(
                    "RB Draw",
                    PlayType.QbRunFocus,
                    FormationType.Spread,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,
                        [1] = RouteType.Curl,
                        [2] = RouteType.Slant,
                        [3] = RouteType.Curl
                    },
                    runningBackSide: 0,
                    slantDirections: new Dictionary<int, bool>
                    {
                        [2] = true
                    })
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
