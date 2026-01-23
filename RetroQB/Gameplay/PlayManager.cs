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
                // Play 1: Quick Slant - Base formation, quick developing slant routes
                new(
                    "Quick Slant",
                    PlayType.QuickPass,
                    FormationType.BaseTripsRight,
                    RunningBackRole.Block,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Slant,      // WR1 - primary slant
                        [1] = RouteType.OutShallow, // WR2 - out route
                        [2] = RouteType.Curl,       // WR3 - curl
                        [3] = RouteType.Flat        // TE - flat
                    },
                    slantDirections: new Dictionary<int, bool>
                    {
                        [0] = true  // slant inside
                    }),
                // Play 2: Mesh Concept - Pass formation, crossing routes
                new(
                    "Mesh",
                    PlayType.QuickPass,
                    FormationType.PassSpread,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.InShallow,  // WR1 - shallow cross
                        [1] = RouteType.InShallow,  // WR2 - shallow cross opposite
                        [2] = RouteType.Curl,       // WR3 - curl
                        [3] = RouteType.OutShallow, // WR4 - out
                        [4] = RouteType.Flat        // TE - flat
                    }),
                // Play 3: Bunch Quick - Pass bunch formation
                new(
                    "Bunch Quick",
                    PlayType.QuickPass,
                    FormationType.PassBunch,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.OutShallow, // WR1 isolated - out
                        [1] = RouteType.Slant,      // WR2 bunch - slant
                        [2] = RouteType.Flat,       // WR3 bunch - flat
                        [3] = RouteType.Curl,       // WR4 bunch - curl
                        [4] = RouteType.InShallow   // TE - drag
                    },
                    slantDirections: new Dictionary<int, bool>
                    {
                        [1] = true
                    })
            },
            [PlayType.LongPass] = new List<PlayDefinition>
            {
                // Play 4: Four Verticals - Pass spread, all go routes
                new(
                    "Four Verts",
                    PlayType.LongPass,
                    FormationType.PassSpread,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,     // WR1 - go
                        [1] = RouteType.Go,     // WR2 - go
                        [2] = RouteType.Go,     // WR3 - go
                        [3] = RouteType.Go,     // WR4 - go
                        [4] = RouteType.Go      // TE - seam
                    }),
                // Play 5: Deep Post - Base formation, deep developing
                new(
                    "Deep Post",
                    PlayType.LongPass,
                    FormationType.BaseTripsLeft,
                    RunningBackRole.Block,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.PostDeep,   // WR1 - deep post
                        [1] = RouteType.Go,         // WR2 - go
                        [2] = RouteType.OutDeep,    // WR3 - out
                        [3] = RouteType.InDeep      // TE - seam
                    }),
                // Play 6: Flood - Pass empty, flood one side
                new(
                    "Flood",
                    PlayType.LongPass,
                    FormationType.PassSpread,
                    RunningBackRole.Route,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,         // WR1 - go deep
                        [1] = RouteType.OutDeep,    // WR2 - corner/out deep
                        [2] = RouteType.OutShallow, // WR3 - out shallow
                        [3] = RouteType.Curl,       // WR4 - curl
                        [4] = RouteType.Flat        // TE - flat
                    }),
                // Play 7: Smash - Base formation, hi-lo concept
                new(
                    "Smash",
                    PlayType.LongPass,
                    FormationType.BaseSplit,
                    RunningBackRole.Block,
                    TightEndRole.Route,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Curl,       // WR1 - hitch
                        [1] = RouteType.OutDeep,    // WR2 - corner
                        [2] = RouteType.Go,         // WR3 - clear out
                        [3] = RouteType.InShallow   // TE - drag
                    })
            },
            [PlayType.QbRunFocus] = new List<PlayDefinition>
            {
                // Play 8: Power Right - Run formation
                new(
                    "Power Right",
                    PlayType.QbRunFocus,
                    FormationType.RunPowerRight,
                    RunningBackRole.Route,
                    TightEndRole.Block,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,         // WR - clear out
                        [1] = RouteType.OutShallow  // RB - swing right
                    },
                    runningBackSide: 1),
                // Play 9: Power Left - Run formation
                new(
                    "Power Left",
                    PlayType.QbRunFocus,
                    FormationType.RunPowerLeft,
                    RunningBackRole.Route,
                    TightEndRole.Block,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Go,         // WR - clear out
                        [1] = RouteType.OutShallow  // RB - swing left
                    },
                    runningBackSide: -1),
                // Play 10: I-Form Dive - Run I-formation
                new(
                    "I-Form Dive",
                    PlayType.QbRunFocus,
                    FormationType.RunIForm,
                    RunningBackRole.Route,
                    TightEndRole.Block,
                    new Dictionary<int, RouteType>
                    {
                        [0] = RouteType.Slant,      // WR - slant to clear
                        [1] = RouteType.Go          // RB - straight ahead
                    },
                    runningBackSide: 0,
                    slantDirections: new Dictionary<int, bool>
                    {
                        [0] = true
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
