using RetroQB.Entities;
using RetroQB.Routes;

namespace RetroQB.Gameplay;

public sealed record BrowserInput(bool Toggle = false, int Move = 0, int Page = 0, int Jump = 0,
    bool Family = false, bool Formation = false, bool Category = false, bool Concept = false,
    bool Favorites = false, bool Star = false, bool Select = false, bool Close = false, bool Flip = false, bool Reset = false);

/// <summary>Browsing owns a separate preview. Only confirmation mutates the active call.</summary>
public sealed class PlaybookBrowser
{
    public const int PageSize = 12;
    private readonly PlayManager _plays;
    private int _cursor;
    public OffensiveTeamAttributes Team { get; set; } = OffensiveTeamAttributes.Default;
    public PlaybookFavorites Favorites { get; }
    public bool IsOpen { get; private set; }
    public PlayType? Family { get; private set; }
    public string? Formation { get; private set; }
    public PlayCategory? Category { get; private set; }
    public string? Concept { get; private set; }
    public bool FavoritesOnly { get; private set; }
    public IReadOnlyList<PlayDefinition> Results { get; private set; } = [];
    public int Cursor => _cursor;
    public ResolvedPlay? Preview { get; private set; }
    public FormationResult? Actors { get; private set; }
    public string Filters => $"{Family?.ToString() ?? "All calls"} | {Category?.ToString() ?? "All categories"}\n"
        + $"{Formation ?? "All formations"}\n{Concept ?? "All concepts"}{(FavoritesOnly ? " | Favorites" : "")}";
    public PlaybookBrowser(PlayManager plays, PlaybookFavorites? favorites = null)
    { _plays = plays; Favorites = favorites ?? new(plays.Catalog); }
    public void Open()
    {
        IsOpen = true; Refresh(_plays.SelectedPlay.Id);
        if (Results.Count == 0) { Family = null; Formation = null; Category = null; Concept = null; FavoritesOnly = false; Refresh(_plays.SelectedPlay.Id); }
    }
    public void Close() => IsOpen = false;
    public string? Update(BrowserInput input)
    {
        if (input.Toggle || input.Close) { if (IsOpen) Close(); else if (input.Toggle) Open(); return null; }
        if (!IsOpen) return null;
        var previous = Preview;
        string? keep = previous?.Id;
        if (input.Reset) { Family = null; Formation = null; Category = null; Concept = null; FavoritesOnly = false; }
        if (input.Family) Family = Family switch { null => PlayType.Pass, PlayType.Pass => PlayType.Run, _ => null };
        if (input.Formation) Formation = Next(_plays.Catalog.Plays.Select(p => p.Info.Formation).Distinct().Order().ToArray(), Formation);
        if (input.Category) Category = Next(Enum.GetValues<PlayCategory>(), Category);
        if (input.Concept) Concept = Next(_plays.Catalog.Plays.Select(p => p.Info.Concept).Distinct().Order().ToArray(), Concept);
        if (input.Favorites) FavoritesOnly = !FavoritesOnly;
        if (input.Star && keep != null) Favorites.Toggle(keep);
        if (input.Family || input.Formation || input.Category || input.Concept || input.Favorites || input.Star || input.Reset) Refresh(keep);
        if (previous != null && Preview?.Id == previous.Id && !ReferenceEquals(Preview, previous))
        { Preview = previous; BuildActors(); }
        if (Results.Count == 0) return null;
        int cursor = Math.Clamp(input.Jump != 0 ? input.Jump < 0 ? 0 : Results.Count - 1
            : _cursor + input.Move + input.Page * PageSize, 0, Results.Count - 1);
        if (cursor != _cursor) { _cursor = cursor; BuildPreview(); }
        if (input.Flip) { Preview = Preview!.Flip(); BuildActors(); }
        if (!input.Select) return null;
        Close(); return Preview!.Id;
    }
    public void Refresh(string? keep = null)
    {
        Results = _plays.Catalog.Plays.Where(p => (!Family.HasValue || p.Family == Family)
                && (Formation == null || p.Info.Formation == Formation) && (!Category.HasValue || p.Info.Category == Category)
                && (Concept == null || p.Info.Concept == Concept) && (!FavoritesOnly || Favorites.Contains(p.Id)))
            .OrderBy(p => p.Family).ThenBy(p => p.Info.Formation, StringComparer.Ordinal)
            .ThenBy(p => p.Info.Concept, StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray();
        _cursor = Math.Max(0, Results.ToList().FindIndex(p => p.Id == keep)); BuildPreview();
    }
    public void RebasePreview() { if (IsOpen) BuildActors(); }
    private void BuildPreview() { Preview = Results.Count == 0 ? null : _plays.Resolved(Results[_cursor].Id); BuildActors(); }
    private void BuildActors()
    {
        Actors = Preview == null ? null : new FormationFactory().CreateFormation(Preview, _plays.LineOfScrimmage, Team);
        if (Actors != null) RouteAssigner.AssignRoutes(Actors.Receivers, Preview!);
    }
    private static string? Next(string[] options, string? value)
    { int index = Array.IndexOf(options, value); return index + 1 == options.Length ? null : options[index + 1]; }
    private static T? Next<T>(T[] options, T? value) where T : struct
    { int index = value.HasValue ? Array.IndexOf(options, value.Value) : -1; return index + 1 == options.Length ? null : options[index + 1]; }
}
