using System.Net.Http;
using System.Text.Json;
using AgApp.Models;

namespace AgApp.Services;

public class MetadataService
{
    private readonly HttpClient _http;
    private readonly SettingsService _settings;
    private readonly LibraryService _library;

    public MetadataService(SettingsService settings, LibraryService library)
    {
        _settings = settings;
        _library = library;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "AgApp/1.0");
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    // ── Public API ────────────────────────────────────────────────────────

    public async Task FetchAndApplyAsync(InstalledGame game, CancellationToken ct = default)
    {
        try
        {
            var key = ApiKey();
            var url = $"https://api.rawg.io/api/games?search={Uri.EscapeDataString(game.Title)}&page_size=5{key}";
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");
            if (results.GetArrayLength() == 0) return;

            var best = PickBestMatch(game.Title, results);
            if (best is null) return;

            if (best.Value.TryGetProperty("id", out var idEl))
                await ApplyFromRawgIdAsync(game, idEl.GetInt32(), ct);
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    public async Task<List<MetadataSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        var results = new List<MetadataSearchResult>();
        try
        {
            var key = ApiKey();
            var url = $"https://api.rawg.io/api/games?search={Uri.EscapeDataString(query)}&page_size=10{key}";
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);

            foreach (var item in doc.RootElement.GetProperty("results").EnumerateArray())
            {
                var r = new MetadataSearchResult();
                if (item.TryGetProperty("id", out var id)) r.RawgId = id.GetInt32();
                if (item.TryGetProperty("name", out var name)) r.Title = name.GetString() ?? "";
                if (item.TryGetProperty("background_image", out var img) && img.ValueKind == JsonValueKind.String)
                    r.ThumbnailUrl = img.GetString() ?? "";
                if (item.TryGetProperty("released", out var rel) && rel.ValueKind == JsonValueKind.String)
                    if (DateTime.TryParse(rel.GetString(), out var dt)) r.Released = dt.Year;
                if (item.TryGetProperty("rating", out var rat)) r.Rating = rat.GetDouble();

                var genres = new List<string>();
                if (item.TryGetProperty("genres", out var gs))
                    foreach (var g in gs.EnumerateArray())
                        if (g.TryGetProperty("name", out var gn)) genres.Add(gn.GetString() ?? "");
                r.Genres = string.Join(", ", genres.Take(3));

                if (r.RawgId > 0 && !string.IsNullOrEmpty(r.Title))
                    results.Add(r);
            }
        }
        catch { }
        return results;
    }

    public async Task ApplyFromRawgIdAsync(InstalledGame game, int rawgId, CancellationToken ct = default)
    {
        try
        {
            var key = ApiKey();
            var detailUrl = $"https://api.rawg.io/api/games/{rawgId}?{key.TrimStart('&')}";
            var detail = await _http.GetStringAsync(detailUrl, ct);
            using var doc = JsonDocument.Parse(detail);
            var root = doc.RootElement;

            ApplyDetailFields(game, root);

            // Screenshots endpoint
            var screenshotsUrl = $"https://api.rawg.io/api/games/{rawgId}/screenshots?page_size=8{key}";
            try
            {
                var ssJson = await _http.GetStringAsync(screenshotsUrl, ct);
                using var ssDoc = JsonDocument.Parse(ssJson);
                var urls = new List<string>();
                foreach (var ss in ssDoc.RootElement.GetProperty("results").EnumerateArray())
                    if (ss.TryGetProperty("image", out var img) && img.ValueKind == JsonValueKind.String)
                        urls.Add(img.GetString()!);
                if (urls.Count > 0) game.Screenshots = urls;
            }
            catch { }

            _library.Save();
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    // ── Field mapping ─────────────────────────────────────────────────────

    private static void ApplyDetailFields(InstalledGame game, JsonElement root)
    {
        // Background / cover
        if (root.TryGetProperty("background_image", out var bg) && bg.ValueKind == JsonValueKind.String)
        {
            var url = bg.GetString() ?? "";
            if (!string.IsNullOrEmpty(url))
            {
                if (string.IsNullOrEmpty(game.CoverUrl)) game.CoverUrl = url;
                game.HeroUrl = url;
            }
        }

        // Additional hero (better quality when available)
        if (root.TryGetProperty("background_image_additional", out var hero) && hero.ValueKind == JsonValueKind.String)
        {
            var url = hero.GetString() ?? "";
            if (!string.IsNullOrEmpty(url)) game.HeroUrl = url;
        }

        // Description
        if (root.TryGetProperty("description_raw", out var desc))
        {
            var text = desc.GetString() ?? "";
            if (!string.IsNullOrEmpty(text))
            {
                var paras = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                game.Description = string.Join("\n\n", paras.Take(4));
            }
        }

        // Genres
        var genres = ExtractNames(root, "genres");
        if (genres.Count > 0) game.Genre = string.Join(", ", genres.Take(3));

        // Tags
        var tags = ExtractNames(root, "tags");
        if (tags.Count > 0) game.Tags = tags.Take(12).ToList();

        // Developers / Publishers
        var devs = ExtractNames(root, "developers");
        if (devs.Count > 0) game.Developer = string.Join(", ", devs.Take(2));
        var pubs = ExtractNames(root, "publishers");
        if (pubs.Count > 0) game.Publisher = string.Join(", ", pubs.Take(2));

        // Release year
        if (root.TryGetProperty("released", out var rel) && rel.ValueKind == JsonValueKind.String)
            if (DateTime.TryParse(rel.GetString(), out var dt)) game.ReleaseYear = dt.Year;

        // Metacritic
        if (root.TryGetProperty("metacritic", out var mc) && mc.ValueKind == JsonValueKind.Number)
            game.MetacriticScore = mc.GetInt32();

        // ESRB
        if (root.TryGetProperty("esrb_rating", out var esrb) && esrb.ValueKind == JsonValueKind.Object)
            if (esrb.TryGetProperty("name", out var esrbName))
                game.EsrbRating = MapEsrb(esrbName.GetString() ?? "");

        // Website
        if (root.TryGetProperty("website", out var web) && web.ValueKind == JsonValueKind.String)
            game.Website = web.GetString() ?? "";

        // Multiplayer / players
        var platforms = new List<string>();
        if (root.TryGetProperty("tags", out var tagsEl))
            foreach (var t in tagsEl.EnumerateArray())
                if (t.TryGetProperty("slug", out var slug))
                {
                    var s = slug.GetString() ?? "";
                    if (s == "multiplayer" || s == "co-op" || s == "online-multiplayer")
                        platforms.Add("Multiplayer");
                }

        if (root.TryGetProperty("playtime", out var pt))
        {
            // RAWG "playtime" is avg hours — not what we need here
        }

        // Player count: infer from add_achievements / tags
        if (game.PlayerSupport == "")
        {
            bool hasMulti = game.Tags.Any(t =>
                t.Contains("multiplayer", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("co-op", StringComparison.OrdinalIgnoreCase));
            game.PlayerSupport = hasMulti ? "Single-player, Multiplayer" : "Single-player";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static List<string> ExtractNames(JsonElement root, string property)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(property, out var arr)) return list;
        foreach (var item in arr.EnumerateArray())
            if (item.TryGetProperty("name", out var n))
                list.Add(n.GetString() ?? "");
        return list;
    }

    private static string MapEsrb(string rawg) => rawg switch
    {
        "Everyone" => "E",
        "Everyone 10+" => "E10+",
        "Teen" => "T",
        "Mature" => "M",
        "Adults Only" => "AO",
        "Rating Pending" => "RP",
        _ => rawg
    };

    private static JsonElement? PickBestMatch(string title, JsonElement results)
    {
        JsonElement? best = null;
        int bestScore = int.MaxValue;
        var lower = title.ToLowerInvariant();

        foreach (var item in results.EnumerateArray())
        {
            if (!item.TryGetProperty("name", out var nameEl)) continue;
            var name = nameEl.GetString()?.ToLowerInvariant() ?? "";
            var dist = EditDistance(lower, name);
            if (dist < bestScore) { bestScore = dist; best = item; }
        }

        return bestScore <= Math.Max(10, title.Length / 2) ? best : null;
    }

    private static int EditDistance(string a, string b)
    {
        int m = a.Length, n = b.Length;
        var dp = new int[m + 1, n + 1];
        for (int i = 0; i <= m; i++) dp[i, 0] = i;
        for (int j = 0; j <= n; j++) dp[0, j] = j;
        for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
                dp[i, j] = a[i - 1] == b[j - 1]
                    ? dp[i - 1, j - 1]
                    : 1 + Math.Min(dp[i - 1, j - 1], Math.Min(dp[i - 1, j], dp[i, j - 1]));
        return dp[m, n];
    }

    private const string DefaultRawgKey = "4cc0bf4ae2144a2c915f60269a3a171d";

    private string ApiKey()
    {
        var key = _settings.Current.RawgApiKey?.Trim() ?? "";
        if (key.Length == 0) key = DefaultRawgKey;
        return $"&key={Uri.EscapeDataString(key)}";
    }
}
