namespace RetroQB.Gameplay;

public sealed class SimulatedDriveGenerator
{
    private const int MaxPlays = 12;
    private static readonly float OpponentTargetGoalLine = FieldGeometry.EndZoneDepth;
    private static readonly float PlayerKickoffStartY = FieldGeometry.PlayerKickoffStartY;

    public SimulatedDriveResult Generate(float startWorldY, SeasonStage stage, Random rng)
    {
        float start = MathF.Max(FieldGeometry.EndZoneDepth + 1f, MathF.Min(FieldGeometry.OpponentGoalLine - 5f, startWorldY));
        float lineOfScrimmage = start;
        float distance = 10f;
        int down = 1;
        List<SimulatedPlay> plays = new();

        float passIncompleteChance = stage switch
        {
            SeasonStage.RegularSeason => 0.40f,
            SeasonStage.Playoff => 0.35f,
            _ => 0.30f
        };

        float runBase = stage switch
        {
            SeasonStage.RegularSeason => 3.0f,
            SeasonStage.Playoff => 3.5f,
            _ => 4.0f
        };

        float passBase = stage switch
        {
            SeasonStage.RegularSeason => 6.0f,
            SeasonStage.Playoff => 7.0f,
            _ => 8.0f
        };

        for (int playCount = 0; playCount < MaxPlays; playCount++)
        {
            if (lineOfScrimmage <= OpponentTargetGoalLine)
            {
                return new SimulatedDriveResult(plays, SimulatedDriveOutcome.Touchdown, 7, start, lineOfScrimmage, PlayerKickoffStartY);
            }

            if (down == 4)
            {
                float fgDistance = GetFieldGoalDistance(lineOfScrimmage);
                bool shortYardage = distance <= 3.5f;
                bool fgRange = fgDistance <= 45f;

                // Short yardage — always go for it (fall through to run a play)
                if (!shortYardage)
                {
                    if (fgRange)
                    {
                        // Long 4th down in FG range — kick
                        bool made = rng.NextSingle() <= GetFieldGoalMakeChance(fgDistance);
                        string fgText = made ? $"FG GOOD ({fgDistance:F0} yds)" : $"FG NO GOOD ({fgDistance:F0} yds)";
                        plays.Add(new SimulatedPlay(down, distance, lineOfScrimmage, fgText, 0f, false));

                        return made
                            ? new SimulatedDriveResult(plays, SimulatedDriveOutcome.FieldGoal, 3, start, lineOfScrimmage, PlayerKickoffStartY)
                            : new SimulatedDriveResult(plays, SimulatedDriveOutcome.TurnoverOnDowns, 0, start, lineOfScrimmage, lineOfScrimmage);
                    }

                    // Long 4th down, out of FG range — turnover on downs
                    plays.Add(new SimulatedPlay(down, distance, lineOfScrimmage, "Turnover on downs", 0f, false));
                    return new SimulatedDriveResult(plays, SimulatedDriveOutcome.TurnoverOnDowns, 0, start, lineOfScrimmage, lineOfScrimmage);
                }
            }

            bool isPass = rng.NextSingle() < 0.45f;
            int thisDown = down;
            float thisDistance = distance;

            if (isPass && rng.NextSingle() < 0.02f)
            {
                plays.Add(new SimulatedPlay(thisDown, thisDistance, lineOfScrimmage, "Pass INTERCEPTED", 0f, false));
                return new SimulatedDriveResult(plays, SimulatedDriveOutcome.Interception, 0, start, lineOfScrimmage, PlayerKickoffStartY);
            }

            float gain;
            string desc;
            if (isPass)
            {
                if (rng.NextSingle() < passIncompleteChance)
                {
                    gain = 0f;
                    desc = "Incomplete pass";
                }
                else
                {
                    gain = ClampGain(passBase + RandomSpread(rng, 10f), -5f, 30f);
                    desc = gain >= 0f ? $"Pass complete +{gain:F0}" : $"Sack {gain:F0}";
                }
            }
            else
            {
                gain = ClampGain(runBase + RandomSpread(rng, 6f), -3f, 15f);
                desc = gain >= 0f ? $"Run +{gain:F0}" : $"Run stuffed {gain:F0}";
            }

            lineOfScrimmage = MathF.Max(FieldGeometry.EndZoneDepth, MathF.Min(FieldGeometry.OpponentGoalLine, lineOfScrimmage - gain));

            if (lineOfScrimmage <= OpponentTargetGoalLine)
            {
                plays.Add(new SimulatedPlay(thisDown, thisDistance, lineOfScrimmage, $"{desc}, TOUCHDOWN", gain, false));
                return new SimulatedDriveResult(plays, SimulatedDriveOutcome.Touchdown, 7, start, lineOfScrimmage, PlayerKickoffStartY);
            }

            if (gain >= distance)
            {
                plays.Add(new SimulatedPlay(thisDown, thisDistance, lineOfScrimmage, $"{desc}, 1ST DOWN", gain, true));
                down = 1;
                distance = 10f;
                continue;
            }

            plays.Add(new SimulatedPlay(thisDown, thisDistance, lineOfScrimmage, desc, gain, false));
            down++;
            distance -= gain;

            if (down > 4)
            {
                return new SimulatedDriveResult(plays, SimulatedDriveOutcome.TurnoverOnDowns, 0, start, lineOfScrimmage, lineOfScrimmage);
            }
        }

        // Max plays reached — kick FG if in range, otherwise turnover
        float endFgDistance = GetFieldGoalDistance(lineOfScrimmage);
        if (endFgDistance <= 45f)
        {
            bool made = rng.NextSingle() <= GetFieldGoalMakeChance(endFgDistance);
            string fgText = made ? $"FG GOOD ({endFgDistance:F0} yds)" : $"FG NO GOOD ({endFgDistance:F0} yds)";
            plays.Add(new SimulatedPlay(down, distance, lineOfScrimmage, fgText, 0f, false));

            return made
                ? new SimulatedDriveResult(plays, SimulatedDriveOutcome.FieldGoal, 3, start, lineOfScrimmage, PlayerKickoffStartY)
                : new SimulatedDriveResult(plays, SimulatedDriveOutcome.TurnoverOnDowns, 0, start, lineOfScrimmage, lineOfScrimmage);
        }

        plays.Add(new SimulatedPlay(down, distance, lineOfScrimmage, "Turnover on downs", 0f, false));
        return new SimulatedDriveResult(plays, SimulatedDriveOutcome.TurnoverOnDowns, 0, start, lineOfScrimmage, lineOfScrimmage);
    }

    private static float ClampGain(float gain, float min, float max)
    {
        return MathF.Max(min, MathF.Min(max, gain));
    }

    private static float RandomSpread(Random rng, float amplitude)
    {
        return (rng.NextSingle() - 0.5f) * amplitude;
    }

    /// <summary>
    /// Computes the FG distance from the given world-Y LOS.
    /// Adds 7 yards for the snap (ball is hiked ~7 yards behind the LOS).
    /// </summary>
    private static float GetFieldGoalDistance(float lineOfScrimmage)
    {
        return MathF.Max(18f, (lineOfScrimmage - FieldGeometry.EndZoneDepth) + 7f);
    }

    private static float GetFieldGoalMakeChance(float distance)
    {
        return distance switch
        {
            <= 25f => 0.95f,
            <= 35f => 0.87f,
            <= 45f => 0.75f,
            <= 50f => 0.6f,
            _ => 0.4f
        };
    }
}
