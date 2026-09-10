using System.Collections.ObjectModel;

namespace RetroQB.Gameplay;

/// <summary>All authored calls. Catalog size has no relationship to keyboard capacity.</summary>
public sealed class PlayCatalog
{
    private readonly IReadOnlyDictionary<string, PlayDefinition> _byId;
    public IReadOnlyList<PlayDefinition> Plays { get; }

    public PlayCatalog(IEnumerable<PlayDefinition> plays)
    {
        var copy = plays.ToArray();
        if (copy.Length == 0 || copy.Any(p => p is null) || copy.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("A catalog needs plays with unique, permanent IDs.");
        Plays = Array.AsReadOnly(copy);
        _byId = new ReadOnlyDictionary<string, PlayDefinition>(copy.ToDictionary(p => p.Id, StringComparer.Ordinal));
    }

    public PlayDefinition this[string id] => _byId.TryGetValue(id, out var play)
        ? play : throw new ArgumentException($"Unknown play ID: {id}", nameof(id));
}

/// <summary>A stable pre-snap selection of catalog IDs mapped to the existing hotkeys.</summary>
public sealed class PlayCallSheet
{
    public const int PlaysPerFamily = 10;
    public PlayCatalog Catalog { get; }
    public IReadOnlyList<string> PassIds { get; }
    public IReadOnlyList<string> RunIds { get; }

    public PlayCallSheet(PlayCatalog catalog, IEnumerable<string> passIds, IEnumerable<string> runIds)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
        PassIds = Validate(passIds, PlayType.Pass);
        RunIds = Validate(runIds, PlayType.Run);
    }

    private IReadOnlyList<string> Validate(IEnumerable<string> ids, PlayType family)
    {
        var copy = ids.ToArray();
        if (copy.Length != PlaysPerFamily || copy.Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException($"A call sheet requires {PlaysPerFamily} distinct {family} calls.");
        if (copy.Any(id => Catalog[id].Family != family))
            throw new ArgumentException("Call sheet play family does not match its hotkey bank.");
        return Array.AsReadOnly(copy);
    }

    public static PlayCallSheet Default(PlayCatalog catalog) => new(catalog,
        catalog.Plays.Where(p => p.Family == PlayType.Pass).Take(PlaysPerFamily).Select(p => p.Id),
        catalog.Plays.Where(p => p.Family == PlayType.Run).Take(PlaysPerFamily).Select(p => p.Id));
}
