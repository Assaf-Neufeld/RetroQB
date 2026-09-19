namespace RetroQB.Data;

/// <summary>Shared stage scaling for CPU defensive units.</summary>
public static class TeamDifficulty
{
    public static DefensiveTeamAttributes ScaleDefense(DefensiveTeamAttributes baseDefense, SeasonStage stage)
    {
        // Apply stage difficulty multiplier to create a scaled-up version.
        // Pass-rush / DE speed uses a softer curve so the QB isn't instantly sacked.
        float stageMult = stage.GetDifficultyMultiplier();
        float rushMult  = stage.GetPassRushMultiplier();
        var scaledDefense = new DefensiveTeamAttributes
        {
            Name = baseDefense.Name,
            Description = baseDefense.Description,
            PrimaryColor = baseDefense.PrimaryColor,
            SecondaryColor = baseDefense.SecondaryColor,
            Roster = baseDefense.Roster,
            OverallRating = baseDefense.OverallRating * stageMult,
            SpeedMultiplier = baseDefense.SpeedMultiplier * stageMult,
            InterceptionAbility = baseDefense.InterceptionAbility * MathF.Sqrt(stageMult),
            TackleAbility = baseDefense.TackleAbility * stageMult,
            CoverageTightness = baseDefense.CoverageTightness * MathF.Sqrt(stageMult),
            PassRushAbility = baseDefense.PassRushAbility * rushMult,
            BlitzFrequency = baseDefense.BlitzFrequency * rushMult,
            BlitzSlotMultipliers = baseDefense.BlitzSlotMultipliers,
            DlSpeed = (baseDefense.DlSpeed > 0 ? baseDefense.DlSpeed : Constants.DlSpeed) * rushMult,
            DeSpeed = (baseDefense.DeSpeed > 0 ? baseDefense.DeSpeed : Constants.DeSpeed) * rushMult,
            LbSpeed = (baseDefense.LbSpeed > 0 ? baseDefense.LbSpeed : Constants.LbSpeed) * stageMult,
            DbSpeed = (baseDefense.DbSpeed > 0 ? baseDefense.DbSpeed : Constants.DbSpeed) * stageMult
        };

        return scaledDefense;
    }
}

