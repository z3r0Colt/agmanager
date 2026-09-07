using System.Net.Http;
using AgApp.Models;

namespace AgApp.Services;

public class UpdateCheckService
{
    private readonly LibraryService _library;
    private readonly SettingsService _settings;
    private readonly HttpClient _http;
    private System.Threading.Timer? _timer;

    public event Action<InstalledGame>? UpdateFound;

    public UpdateCheckService(LibraryService library, SettingsService settings)
    {
        _library = library;
        _settings = settings;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public void StartBackgroundChecks()
    {
        try
        {
            var interval = TimeSpan.FromHours(Math.Max(1, _settings.Current.UpdateCheckIntervalHours));
            _timer = new System.Threading.Timer(async _ => await CheckAllAsync(), null, interval, interval);
            AppLogger.Info("Update check background timer started.");
        }
        catch (Exception ex) { AppLogger.Warn("Failed to start update check timer", ex); }
    }

    public async Task CheckAllAsync(CancellationToken ct = default)
    {
        foreach (var game in _library.Games.ToList())
        {
            if (ct.IsCancellationRequested) break;
            if (!string.IsNullOrEmpty(game.PageUrl))
                await CheckAsync(game, ct);
        }
    }

    public async Task CheckAsync(InstalledGame game, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(game.PageUrl)) return;
        try
        {
            game.LastUpdateCheck = DateTime.Now;
            var html = await _http.GetStringAsync(game.PageUrl, ct);
            var version = ParseVersion(html);
            if (!string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(game.VersionString)
                && version != game.VersionString)
            {
                game.HasUpdate = true;
                _library.Save();
                UpdateFound?.Invoke(game);
                AppLogger.Info($"Update found for {game.Title}: {game.VersionString} -> {version}");
            }
        }
        catch (Exception ex) { AppLogger.Warn($"Update check failed for {game.Title}", ex); }
    }

    private static string ParseVersion(string html)
    {
        var patterns = new[]
        {
            @"[Vv]ersion\s*:?\s*v?(\d+[\.\d]+)",
            @"v(\d+\.\d+[\.\d]*)",
            @"Release\s+(\d+[\.\d]+)"
        };
        foreach (var pat in patterns)
        {
            var m = System.Text.RegularExpressions.Regex.Match(html, pat);
            if (m.Success) return m.Groups[1].Value;
        }
        return "";
    }
}
