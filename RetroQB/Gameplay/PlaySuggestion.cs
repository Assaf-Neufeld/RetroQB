namespace RetroQB.Gameplay;

/// <summary>
/// Suggests plays based on down and distance situation.
/// Encapsulates play-calling AI logic separate from selection mechanics.
/// </summary>
public static class PlaySuggestion
{
    // Situational thresholds
    private const float VeryShortYardageThreshold = 2.5f;
    private const float LongYardageThreshold = 9f;
    private const float ThirdDownLongThreshold = 6f;
    private const float ShortYardageThreshold = 3f;
    private const int ThirdDown = 3;
    private const int FourthDown = 4;

    /// <summary>
    /// Gets the single best suggested play type for the situation.
    /// </summary>
    public static PlayType GetSuggested(int down, float distance)
    {
        float passWeight = GetFamilyWeight(PlayType.Pass, down, distance);
        float runWeight = GetFamilyWeight(PlayType.Run, down, distance);
        return passWeight >= runWeight ? PlayType.Pass : PlayType.Run;
    }

    public static (PlayType Type, int Index) GetSuggestedPlay(
        int down,
        float distance,
        IReadOnlyList<PlayDefinition> passPlays,
        IReadOnlyList<PlayDefinition> runPlays)
    {
        var bestPass = GetBestPlayCandidate(passPlays, down, distance);
        var bestRun = GetBestPlayCandidate(runPlays, down, distance);

        float passScore = GetFamilyWeight(PlayType.Pass, down, distance) * bestPass.Weight;
        float runScore = GetFamilyWeight(PlayType.Run, down, distance) * bestRun.Weight;

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
        int passWeight = Math.Max(1, (int)MathF.Round(GetFamilyWeight(PlayType.Pass, down, distance)));
        int runWeight = Math.Max(1, (int)MathF.Round(GetFamilyWeight(PlayType.Run, down, distance)));

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
        if (down >= FourthDown)
        {
            if (distance <= VeryShortYardageThreshold)
            {
                return type == PlayType.Run ? 2.6f : 1.1f;
            }

            if (distance >= ThirdDownLongThreshold)
            {
                return type == PlayType.Pass ? 3.5f : 0.35f;
            }

            return type == PlayType.Pass ? 2.4f : 0.9f;
        }

        if (IsLongYardageSituation(down, distance))
        {
            return type == PlayType.Pass ? 3.25f : 0.45f;
        }

        if (IsShortYardageSituation(distance))
        {
            return type == PlayType.Run ? 2.25f : 1.15f;
        }

        if (down == 2 && distance >= 7f)
        {
            return type == PlayType.Pass ? 1.85f : 0.85f;
        }

        return type == PlayType.Pass ? 1.35f : 1.2f;
    }

    public static float GetPlayWeight(PlayDefinition play, int down, float distance)
    {
        return play.Family == PlayType.Pass
            ? GetPassPlayWeight(play, down, distance)
            : GetRunPlayWeight(play, down, distance);
    }

    private static float GetPassPlayWeight(PlayDefinition play, int down, float distance)
    {
        RouteProfile profile = AnalyzeRoutes(play.Routes.Values);
        float protectionBonus = 0f;
        if (play.RunningBackRole == RunningBackRole.Block)
        {
            protectionBonus += 0.2f;
        }

        if (play.TightEndRole == TightEndRole.Block)
        {
            protectionBonus += 0.18f;
        }

        float formationBonus = GetPassFormationBonus(play.Formation, down, distance);
        float weight;

        if (distance <= VeryShortYardageThreshold)
        {
            weight = 0.85f
                + (profile.QuickRoutes * 0.26f)
                + (profile.IntermediateRoutes * 0.1f)
                + protectionBonus
                + formationBonus
                - (profile.DeepRoutes * 0.12f);
        }
        else if (IsLongYardageSituation(down, distance))
        {
            weight = 0.6f
                + (profile.DeepRoutes * 0.3f)
                + (profile.IntermediateRoutes * 0.22f)
                + (protectionBonus * 0.9f)
                + formationBonus
                - (profile.QuickRoutes * 0.04f);
        }
        else
        {
            weight = 0.9f
                + (profile.QuickRoutes * 0.14f)
                + (profile.IntermediateRoutes * 0.2f)
                + (profile.DeepRoutes * 0.16f)
                + (protectionBonus * 0.6f)
                + formationBonus;
        }

        return Math.Clamp(weight, 0.2f, 3.5f);
    }

    private static float GetRunPlayWeight(PlayDefinition play, int down, float distance)
    {
        float weight = play.RunConcept switch
        {
            RunConcept.Dive => distance <= VeryShortYardageThreshold ? 1.65f : IsLongYardageSituation(down, distance) ? 0.55f : 0.95f,
            RunConcept.Power => distance <= VeryShortYardageThreshold ? 1.5f : IsLongYardageSituation(down, distance) ? 0.65f : 1.15f,
            RunConcept.Counter => distance <= VeryShortYardageThreshold ? 1.0f : IsLongYardageSituation(down, distance) ? 0.9f : 1.15f,
            RunConcept.Sweep => distance <= VeryShortYardageThreshold ? 0.9f : IsLongYardageSituation(down, distance) ? 0.75f : 1.1f,
            RunConcept.Stretch => distance <= VeryShortYardageThreshold ? 0.95f : IsLongYardageSituation(down, distance) ? 0.8f : 1.05f,
            RunConcept.Draw => distance <= VeryShortYardageThreshold ? 0.75f : IsLongYardageSituation(down, distance) ? 1.25f : 0.95f,
            _ => 1f
        };

        weight += GetRunFormationBonus(play.Formation, play.RunConcept, distance);
        return Math.Clamp(weight, 0.2f, 3.5f);
    }

    private static float GetPassFormationBonus(FormationType formation, int down, float distance)
    {
        if (distance <= VeryShortYardageThreshold)
        {
            return formation switch
            {
                FormationType.BaseBunchRight or FormationType.BaseBunchLeft or FormationType.PassBunchRight or FormationType.PassBunchLeft => 0.18f,
                FormationType.PassEmpty => -0.08f,
                _ => 0f
            };
        }

        if (IsLongYardageSituation(down, distance))
        {
            return formation switch
            {
                FormationType.PassSpread or FormationType.PassEmpty => 0.18f,
                FormationType.PassBunchRight or FormationType.PassBunchLeft => 0.1f,
                _ => 0f
            };
        }

        return formation switch
        {
            FormationType.BaseBunchRight or FormationType.BaseBunchLeft or FormationType.PassBunchRight or FormationType.PassBunchLeft => 0.08f,
            FormationType.PassSpread => 0.06f,
            _ => 0f
        };
    }

    private static float GetRunFormationBonus(FormationType formation, RunConcept concept, float distance)
    {
        if (distance <= VeryShortYardageThreshold)
        {
            return formation switch
            {
                FormationType.RunIForm => 0.25f,
                FormationType.RunPowerRight or FormationType.RunPowerLeft => 0.15f,
                _ => 0f
            };
        }

        return (formation, concept) switch
        {
            (FormationType.RunSinglebackTripsRight, RunConcept.Sweep) or (FormationType.RunSinglebackTripsLeft, RunConcept.Sweep) => 0.15f,
            (FormationType.RunSinglebackTripsRight, RunConcept.Draw) or (FormationType.RunSinglebackTripsLeft, RunConcept.Draw) => 0.12f,
            (FormationType.RunPistolStrongRight, RunConcept.Counter) or (FormationType.RunPistolStrongLeft, RunConcept.Counter) => 0.1f,
            _ => 0f
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
                    break;

                case RouteType.OutDeep:
                case RouteType.InDeep:
                case RouteType.PostShallow:
                    profile.IntermediateRoutes++;
                    break;

                case RouteType.Go:
                case RouteType.PostDeep:
                case RouteType.DoubleMove:
                    profile.DeepRoutes++;
                    break;
            }
        }

        return profile;
    }

    private static (int Index, float Weight) GetBestPlayCandidate(
        IReadOnlyList<PlayDefinition> plays,
        int down,
        float distance)
    {
        int bestIndex = 0;
        float bestWeight = float.MinValue;

        for (int index = 0; index < plays.Count; index++)
        {
            float weight = GetPlayWeight(plays[index], down, distance);

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

    private static bool IsLongYardageSituation(int down, float distance)
    {
        return distance >= LongYardageThreshold || 
               (down >= ThirdDown && distance >= ThirdDownLongThreshold);
    }

    private static bool IsShortYardageSituation(float distance)
    {
        return distance <= ShortYardageThreshold;
    }

    private struct RouteProfile
    {
        public int QuickRoutes;
        public int IntermediateRoutes;
        public int DeepRoutes;
    }
}
