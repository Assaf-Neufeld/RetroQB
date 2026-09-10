namespace RetroQB.Gameplay;

public enum PlayCategory { Quick, Crossers, Intermediate, Vertical, PlayAction, InsideRun, GapRun, EdgeRun, Draw, Surprise }

/// <summary>Human-readable organization, independent of permanent IDs and hotkeys.</summary>
public sealed record PlaybookInfo(string Formation, string Concept, PlayCategory Category, string Description)
{
    public string CategoryLabel => Category switch
    {
        PlayCategory.PlayAction => "Play action", PlayCategory.InsideRun => "Inside run",
        PlayCategory.GapRun => "Gap run", PlayCategory.EdgeRun => "Edge run", _ => Category.ToString()
    };

    public static PlaybookInfo Legacy(string name, PlayType family, FormationDefinition formation, RunConcept run, bool wildcard) =>
        new(System.Text.RegularExpressions.Regex.Replace(formation.Type.ToString(), "(?<=[a-z])(?=[A-Z])", " ")
            .Replace("Base ", "").Replace("Pass ", "").Replace("Run ", ""), name,
            wildcard ? PlayCategory.Surprise : family == PlayType.Pass ? name switch
            {
                "Mesh" => PlayCategory.Crossers, "Bunch Quick" or "Slant Flat" => PlayCategory.Quick,
                "Four Verts" or "Deep Ins" => PlayCategory.Vertical, "PA Deep" => PlayCategory.PlayAction,
                _ => PlayCategory.Intermediate
            } : RunCategory(run),
            wildcard ? "A generated combination; select again to reroll." : family == PlayType.Pass ? name switch
            {
                "Mesh" => "Read the shallow crossers under the clear-out.",
                "Bunch Quick" => "Use the bunch release to find a quick inside throw.",
                "Four Verts" => "Read the safeties and attack an open vertical lane.",
                "Deep Ins" => "Look for the deep inside break behind the linebackers.",
                "Flood" => "Work the sideline from the deep out to the flat.",
                "Smash" => "Read the deep out above the short inside route.",
                "Slant Flat" => "Read the flat defender and throw to the open level.",
                _ => "Read the coverage and work through the routes."
            } : "Follow the blocking toward the called gap.");

    public static PlayCategory RunCategory(RunConcept run) => run switch
    {
        RunConcept.Dive => PlayCategory.InsideRun, RunConcept.Power or RunConcept.Counter => PlayCategory.GapRun,
        RunConcept.Draw => PlayCategory.Draw, _ => PlayCategory.EdgeRun
    };
}
