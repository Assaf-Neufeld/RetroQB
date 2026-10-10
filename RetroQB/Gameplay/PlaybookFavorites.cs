using System.Text.Json;

namespace RetroQB.Gameplay;

/// <summary>Preferences use permanent call IDs, independent of a match or its quick sheet.</summary>
public sealed class PlaybookFavorites
{
    private sealed record Saved(int Version, string[] Favorites);
    private readonly string _path;
    private readonly HashSet<string> _known;
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    public string Status { get; private set; } = "";
    public IReadOnlyCollection<string> Ids => _ids.Order(StringComparer.Ordinal).ToArray();
    public PlaybookFavorites(PlayCatalog catalog, string? path = null)
    {
        _path = Path.GetFullPath(path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RetroQB", "playbook-favorites.json"));
        _known = catalog.Plays.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        try
        {
            if (!File.Exists(_path)) return;
            if (new FileInfo(_path).Length > 32768) throw new JsonException();
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(_path));
            if (saved?.Version != 1 || saved.Favorites == null) throw new JsonException();
            foreach (string id in saved.Favorites.Where(id => id != null && _known.Contains(id))) _ids.Add(id);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { Status = "Favorites unavailable; starting an empty list."; }
    }
    public bool Contains(string id) => _ids.Contains(id);
    public bool Toggle(string id)
    {
        if (!_known.Contains(id)) return false;
        if (!_ids.Add(id)) _ids.Remove(id);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Saved(1, Ids.ToArray())));
            File.Move(temporary, _path, overwrite: true); Status = "Favorites saved.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { Status = "Favorite kept for this session; could not save."; }
        return true;
    }
}
