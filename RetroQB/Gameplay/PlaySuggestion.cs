namespace RetroQB.Gameplay;

/// <summary>
/// Captures the football situation needed to recommend plays.
/// </summary>
public readonly record struct PlaySituation(
    int Down,
    float Distance,
    float LineOfScrimmage = float.NaN,
    float FirstDownLine = float.NaN)
{
    private const float ShortYardageThreshold = 3f;
    private const float LongYardageThreshold = 9f;
    private const float ThirdDownLongThreshold = 6f;
    private const float RedZoneThreshold = 20f;
    private const float TightRedZoneThreshold = 10f;
    private const int ThirdDown = 3;

    public bool HasFieldPosition => !float.IsNaN(LineOfScrimmage);
    public float YardsToGoal => HasFieldPosition
        ? MathF.Max(0f, FieldGeometry.OpponentGoalLine - LineOfScrimmage)
        : float.PositiveInfinity;
    public bool IsRedZone => YardsToGoal <= RedZoneThreshold;
    public bool IsTightRedZone => YardsToGoal <= TightRedZoneThreshold;
    public bool IsGoalToGo => HasFieldPosition && FirstDownLine >= FieldGeometry.OpponentGoalLine;
    public bool IsShortYardage => Distance <= ShortYardageThreshold;
    public bool IsLongYardage => Distance >= LongYardageThreshold ||
                                  (Down >= ThirdDown && Distance >= ThirdDownLongThreshold);
    public bool IsMustConvert => Down >= ThirdDown;
}

/// <summary>
/// Suggests plays based on down and distance situation.
/// Encapsulates play-calling AI logic separate from selection mechanics.
/// </summary>
public static class PlaySuggestion
{
    // Situational thresholds
    private const float VeryShortYardageThreshold = 2.5f;
    private const float ThirdDownLongThreshold = 6f;
    private const float QuickRouteFieldNeed = 3f;
    private const float IntermediateRouteFieldNeed = 8f;
    private const float DeepRouteFieldNeed = 18f;
    private const int FourthDown = 4;

    /// <summary>
    /// Gets the single best suggested play type for the situation.
    /// </summary>
    public static PlayType GetSuggested(int down, float distance)
    {
        return GetSuggested(new PlaySituation(down, distance));
    }

    /// <summary>
    /// Gets the single best suggested play type for the situation.
    /// </summary>
    public static PlayType GetSuggested(PlaySituation situation)
    {
        float passWeight = GetFamilyWeight(PlayType.Pass, situation);
        float runWeight = GetFamilyWeight(PlayType.Run, situation);
        return passWeight >= runWeight ? PlayType.Pass : PlayType.Run;
    }

    public static (PlayType Type, int Index) GetSuggestedPlay(
        int down,
        float distance,
        IReadOnlyList<PlayDefinition> passPlays,
        IReadOnlyList<PlayDefinition> runPlays)
    {
        return GetSuggestedPlay(new PlaySituation(down, distance), passPlays, runPlays);
    }

    public static (PlayType Type, int Index) GetSuggestedPlay(
        PlaySituation situation,
        IReadOnlyList<PlayDefinition> passPlays,
        IReadOnlyList<PlayDefinition> runPlays)
    {
        var bestPass = GetBestPlayCandidate(passPlays, situation);
        var bestRun = GetBestPlayCandidate(runPlays, situation);

        float passScore = GetFamilyWeight(PlayType.Pass, situation) * bestPass.Weight;
        float runScore = GetFamilyWeight(PlayType.Run, situation) * bestRun.Weight;

        return passScore >= runScore
            ? (PlayType.Pass, bestPass.Index)
            : (PlayType.Run, bestRun.Index);
    }

    /// <summary>
    /// Gets weighted list of play types for random selection.
    /// Types appear multiple times based on situational preference.
    /// </summary>
    public static List<PlayType> GetWeightedCandidates(int down, float distance)
    {
        return GetWeightedCandidates(new PlaySituation(down, distance));
    }

    /// <summary>
    /// Gets weighted list of play types for random selection.
    /// Types appear multiple times based on situational preference.
    /// </summary>
    public static List<PlayType> GetWeightedCandidates(PlaySituation situation)
    {
        int passWeight = Math.Max(1, (int)MathF.Round(GetFamilyWeight(PlayType.Pass, situation)));
        int runWeight = Math.Max(1, (int)MathF.Round(GetFamilyWeight(PlayType.Run, situation)));

        var candidates = new List<PlayType>(passWeight + runWeight);
        for (int i = 0; i < passWeight; i++)
        {
            candidates.Add(PlayType.Pass);
        }

        for (int i = 0; i < runWeight; i++)
        {
            candidates.Add(PlayType.Run);
        }

        return candidates;
    }

    public static float GetFamilyWeight(PlayType type, int down, float distance)
    {
        return GetFamilyWeight(type, new PlaySituation(down, distance));
    }

    public static float GetFamilyWeight(PlayType type, PlaySituation situation)
    {
        if (situation.IsGoalToGo)
        {
            if (situation.Distance <= VeryShortYardageThreshold)
            {
                return type == PlayType.Run ? 2.7f : 1.2f;
            }

            if (situation.IsLongYardage)
            {
                return type == PlayType.Pass ? 2.6f : 0.65f;
            }

            return type == PlayType.Pass ? 1.75f : 1.35f;
        }

        if (situation.IsRedZone)
        {
            if (situation.Distance <= VeryShortYardageThreshold)
            {
                return type == PlayType.Run ? 2.45f : 1.25f;
            }

            if (situation.IsLongYardage)
            {
                return type == PlayType.Pass ? 2.35f : 0.75f;
            }

            return type == PlayType.Pass ? 1.45f : 1.25f;
        }

        if (situation.Down >= FourthDown)
        {
            if (situation.Distance <= VeryShortYardageThreshold)
            {
                return type == PlayType.Run ? 2.6f : 1.1f;
            }

            if (situation.Distance >= ThirdDownLongThreshold)
            {
                return type == PlayType.Pass ? 3.5f : 0.35f;
            }

            return type == PlayType.Pass ? 2.4f : 0.9f;
        }

        if (situation.IsLongYardage)
        {
            return type == PlayType.Pass ? 3.25f : 0.45f;
        }

        if (situation.IsShortYardage)
        {
            return type == PlayType.Run ? 2.25f : 1.15f;
        }

        if (situation.Down == 2 && situation.Distance >= 7f)
        {
            return type == PlayType.Pass ? 1.85f : 0.85f;
        }

        return type == PlayType.Pass ? 1.35f : 1.2f;
    }

    public static float GetPlayWeight(PlayDefinition play, int down, float distance)
    {
        return GetPlayWeight(play, new PlaySituation(down, distance));
    }

    public static float GetPlayWeight(PlayDefinition play, PlaySituation situation)
    {
        return play.Family == PlayType.Pass
            ? GetPassPlayWeight(play, situation)
            : GetRunPlayWeight(play, situation);
    }

    private static float GetPassPlayWeight(PlayDefinition play, PlaySituation situation)
    {
        RouteProfile profile = AnalyzeRoutes(play.Routes.Values);
        if (IsVerticalShot(profile) && situation.HasFieldPosition && situation.IsTightRedZone)
        {
            return 0f;
        }

        float weight = GetPassDistanceFit(profile, situation)
            * GetAvailableFieldPassFit(profile, situation)
            * GetRedZonePassFit(profile, play.Formation, situation)
            * GetPassProtectionFit(play, situation)
            * GetPassFormationFit(play.Formation, situation);

        if (play.Routes.Count == 0)
        {
            weight *= 0.75f;
        }

        return Math.Clamp(weight, 0.05f, 4f);
    }

    private static float GetRunPlayWeight(PlayDefinition play, PlaySituation situation)
    {
        float weight = GetRunDistanceFit(play.RunConcept, situation)
            * GetRedZoneRunFit(play, situation)
            * GetRunFormationFit(play.Formation, play.RunConcept, situation);

        if (play.Routes.Count == 0)
        {
            weight *= 0.85f;
        }

        return Math.Clamp(weight, 0.05f, 4f);
    }

    private static float GetPassDistanceFit(RouteProfile profile, PlaySituation situation)
    {
        if (situation.Distance <= VeryShortYardageThreshold)
        {
            return 0.85f
                + (profile.QuickRoutes * 0.22f)
                + (profile.IntermediateRoutes * 0.08f)
                - (profile.DeepRoutes * 0.12f);
        }

        if (situation.IsLongYardage)
        {
            return 0.65f
                + (profile.DeepRoutes * 0.28f)
                + (profile.IntermediateRoutes * 0.2f)
                + (profile.QuickRoutes * 0.03f);
        }

        return 0.9f
            + (profile.QuickRoutes * 0.12f)
            + (profile.IntermediateRoutes * 0.18f)
            + (profile.DeepRoutes * 0.1f);
    }

    private static float GetAvailableFieldPassFit(RouteProfile profile, PlaySituation situation)
    {
        if (!situation.HasFieldPosition || profile.TotalRoutes == 0)
        {
            return 1f;
        }

        float usefulField = MathF.Max(situation.YardsToGoal, situation.Distance);
        float weightedNeed = ((profile.QuickRoutes * QuickRouteFieldNeed) +
                              (profile.IntermediateRoutes * IntermediateRouteFieldNeed) +
                              (profile.DeepRoutes * DeepRouteFieldNeed)) /
                             profile.TotalRoutes;

        float fit = usefulField / weightedNeed;
        if (IsVerticalShot(profile) && situation.YardsToGoal <= IntermediateRouteFieldNeed)
        {
            return 0.02f;
        }

        if (IsVerticalShot(profile) && situation.IsRedZone)
        {
            fit *= 0.18f;
        }

        return Math.Clamp(fit, 0.05f, 1.2f);
    }

    private static float GetRedZonePassFit(RouteProfile profile, FormationType formation, PlaySituation situation)
    {
        if (!situation.IsRedZone)
        {
            return 1f;
        }

        float weight = 1f
            + (profile.QuickRoutes * 0.13f)
            + (profile.IntermediateRoutes * 0.04f)
            - (profile.DeepRoutes * (situation.IsTightRedZone ? 0.22f : 0.13f));

        if (IsVerticalShot(profile))
        {
            weight *= situation.IsTightRedZone ? 0.35f : 0.55f;
        }

        if (formation is FormationType.BaseBunchRight or FormationType.BaseBunchLeft or
            FormationType.PassBunchRight or FormationType.PassBunchLeft)
        {
            weight += 0.12f;
        }

        if (formation == FormationType.PassEmpty && situation.IsTightRedZone)
        {
            weight -= 0.08f;
        }

        return Math.Clamp(weight, 0.2f, 1.7f);
    }

    private static float GetPassProtectionFit(PlayDefinition play, PlaySituation situation)
    {
        float weight = 1f;
        if (play.RunningBackRole == RunningBackRole.Block)
        {
            weight += situation.IsLongYardage ? 0.18f : 0.1f;
        }

        if (play.TightEndRole == TightEndRole.Block)
        {
            weight += situation.IsLongYardage ? 0.14f : 0.08f;
        }

        return weight;
    }

    private static float GetPassFormationFit(FormationType formation, PlaySituation situation)
    {
        if (situation.Distance <= VeryShortYardageThreshold)
        {
            return formation switch
            {
                FormationType.BaseBunchRight or FormationType.BaseBunchLeft or FormationType.PassBunchRight or FormationType.PassBunchLeft => 1.14f,
                FormationType.PassEmpty => 0.92f,
                _ => 1f
            };
        }

        if (situation.IsLongYardage && !situation.IsRedZone)
        {
            return formation switch
            {
                FormationType.PassSpread or FormationType.PassEmpty => 1.12f,
                FormationType.PassBunchRight or FormationType.PassBunchLeft => 1.07f,
                _ => 1f
            };
        }

        return formation switch
        {
            FormationType.BaseBunchRight or FormationType.BaseBunchLeft or FormationType.PassBunchRight or FormationType.PassBunchLeft => 1.06f,
            FormationType.PassSpread => 1.04f,
            _ => 1f
        };
    }

    private static float GetRunDistanceFit(RunConcept concept, PlaySituation situation)
    {
        return concept switch
        {
            RunConcept.Dive => situation.Distance <= VeryShortYardageThreshold ? 1.65f : situation.IsLongYardage ? 0.5f : 0.95f,
            RunConcept.Power => situation.Distance <= VeryShortYardageThreshold ? 1.5f : situation.IsLongYardage ? 0.6f : 1.15f,
            RunConcept.Counter => situation.Distance <= VeryShortYardageThreshold ? 0.95f : situation.IsLongYardage ? 0.85f : 1.1f,
            RunConcept.Sweep => situation.Distance <= VeryShortYardageThreshold ? 0.85f : situation.IsLongYardage ? 0.7f : 1.08f,
            RunConcept.Stretch => situation.Distance <= VeryShortYardageThreshold ? 0.9f : situation.IsLongYardage ? 0.75f : 1.05f,
            RunConcept.Draw => situation.Distance <= VeryShortYardageThreshold ? 0.65f : situation.IsLongYardage ? 1.2f : 0.92f,
            _ => 1f
        };
    }

    private static float GetRedZoneRunFit(PlayDefinition play, PlaySituation situation)
    {
        if (!situation.IsRedZone)
        {
            return 1f;
        }

        return play.RunConcept switch
        {
            RunConcept.Dive => situation.IsTightRedZone ? 1.35f : 1.18f,
            RunConcept.Power => situation.IsTightRedZone ? 1.25f : 1.15f,
            RunConcept.Counter => situation.IsTightRedZone ? 0.82f : 1.02f,
            RunConcept.Sweep => situation.IsTightRedZone ? 0.68f : 0.92f,
            RunConcept.Stretch => situation.IsTightRedZone ? 0.76f : 0.98f,
            RunConcept.Draw => situation.IsLongYardage && !situation.IsGoalToGo ? 0.95f : 0.55f,
            _ => 1f
        };
    }

    private static float GetRunFormationFit(FormationType formation, RunConcept concept, PlaySituation situation)
    {
        if (situation.Distance <= VeryShortYardageThreshold)
        {
            return formation switch
            {
                FormationType.RunIForm => 1.2f,
                FormationType.RunPowerRight or FormationType.RunPowerLeft => 1.12f,
                _ => 1f
            };
        }

        return (formation, concept) switch
        {
            (FormationType.RunSinglebackTripsRight, RunConcept.Sweep) or (FormationType.RunSinglebackTripsLeft, RunConcept.Sweep) => 1.12f,
            (FormationType.RunSinglebackTripsRight, RunConcept.Draw) or (FormationType.RunSinglebackTripsLeft, RunConcept.Draw) => 1.1f,
            (FormationType.RunPistolStrongRight, RunConcept.Counter) or (FormationType.RunPistolStrongLeft, RunConcept.Counter) => 1.08f,
            _ => 1f
        };
    }

    private static RouteProfile AnalyzeRoutes(IEnumerable<RouteType> routes)
    {
        RouteProfile profile = default;
        foreach (RouteType route in routes)
        {
            switch (route)
            {
                case RouteType.Slant:
                case RouteType.OutShallow:
                case RouteType.InShallow:
                case RouteType.Flat:
                    profile.QuickRoutes++;
                    profile.TotalRoutes++;
                    break;

                case RouteType.OutDeep:
                case RouteType.InDeep:
                case RouteType.PostShallow:
                    profile.IntermediateRoutes++;
                    profile.TotalRoutes++;
                    break;

                case RouteType.Go:
                case RouteType.PostDeep:
                case RouteType.DoubleMove:
                    profile.DeepRoutes++;
                    profile.TotalRoutes++;
                    break;
            }
        }

        return profile;
    }

    private static (int Index, float Weight) GetBestPlayCandidate(
        IReadOnlyList<PlayDefinition> plays,
        PlaySituation situation)
    {
        int bestIndex = 0;
        float bestWeight = float.MinValue;

        for (int index = 0; index < plays.Count; index++)
        {
            float weight = GetPlayWeight(plays[index], situation);

            // Wildcard has no fixed route tree, so named plays should usually win the suggestion slot.
            if (index == 0 && plays[index].Routes.Count == 0)
            {
                weight *= 0.45f;
            }

            if (weight > bestWeight)
            {
                bestIndex = index;
                bestWeight = weight;
            }
        }

        return (bestIndex, bestWeight);
    }

    private static bool IsVerticalShot(RouteProfile profile)
    {
        return profile.TotalRoutes > 0 &&
               profile.DeepRoutes >= Math.Max(3, profile.TotalRoutes - 1) &&
               profile.QuickRoutes == 0;
    }

    private struct RouteProfile
    {
        public int TotalRoutes;
        public int QuickRoutes;
        public int IntermediateRoutes;
        public int DeepRoutes;
    }
}
