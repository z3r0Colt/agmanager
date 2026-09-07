using System.IO;
using System.Text.Json;
using AgApp.Models;

namespace AgApp.Services;

public class LibraryService
{
    private readonly SettingsService _settings;
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgApp", "library.json");

    private List<InstalledGame> _games = new();

    public IReadOnlyList<InstalledGame> Games => _games;

    public LibraryService(SettingsService settings) => _settings = settings;

    public void Load()
    {
        try
        {
            if (File.Exists(LibraryPath))
                _games = JsonSerializer.Deserialize<List<InstalledGame>>(File.ReadAllText(LibraryPath)) ?? new();
        }
        catch { _games = new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
        File.WriteAllText(LibraryPath, JsonSerializer.Serialize(_games, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void AddGame(InstalledGame game)
    {
        _games.RemoveAll(g => g.Id == game.Id);
        _games.Add(game);
        Save();
    }

    public void RemoveGame(string id)
    {
        _games.RemoveAll(g => g.Id == id);
        Save();
    }

    public void UpdatePlayTime(string id, TimeSpan elapsed)
    {
        var game = _games.FirstOrDefault(g => g.Id == id);
        if (game is null) return;
        game.PlayTime += elapsed;
        game.LastPlayed = DateTime.Now;
        Save();
    }

    public InstalledGame? BuildFromExtractedFolder(string gameId, string gameTitle,
        string coverUrl, string gameFolder)
    {
        if (!Directory.Exists(gameFolder)) return null;

        // Prefer "Run Me!.bat" — the standard launcher for games from this site.
        // Fall back to the largest exe if no bat is found.
        var launcher = FindLauncherInFolder(gameFolder);

        long sizeBytes = 0;
        try
        {
            sizeBytes = Directory.EnumerateFiles(gameFolder, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch { }

        return new InstalledGame
        {
            Id = gameId,
            Title = gameTitle,
            CoverUrl = coverUrl,
            InstallPath = gameFolder,
            ExecutablePath = launcher,
            InstalledSizeBytes = sizeBytes,
            InstalledAt = DateTime.Now
        };
    }

    public void LaunchGame(InstalledGame game)
    {
        // If the stored launcher is gone, try to re-locate it
        var path = File.Exists(game.ExecutablePath)
            ? game.ExecutablePath
            : FindLauncherInFolder(game.InstallPath);

        if (!File.Exists(path)) return;

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path) ?? game.InstallPath,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
    }

    public void DeleteGame(InstalledGame game)
    {
        if (Directory.Exists(game.InstallPath))
            Directory.Delete(game.InstallPath, recursive: true);
        RemoveGame(game.Id);
    }

    public void OpenFolder(InstalledGame game)
    {
        if (Directory.Exists(game.InstallPath))
            System.Diagnostics.Process.Start("explorer.exe", game.InstallPath);
    }

    private static string FindLauncherInFolder(string folder)
    {
        if (!Directory.Exists(folder)) return "";
        return Directory
            .EnumerateFiles(folder, "Run Me!.bat", SearchOption.AllDirectories)
            .FirstOrDefault()
            ?? Directory.EnumerateFiles(folder, "*.exe", SearchOption.AllDirectories)
               .OrderByDescending(f => new FileInfo(f).Length)
               .FirstOrDefault()
            ?? "";
    }
}
