using System.IO;
using System.IO.Compression;
using AgApp.Models;

namespace AgApp.Services;

public class SaveBackupService
{
    private readonly LibraryService _library;
    private readonly SettingsService _settings;

    public SaveBackupService(LibraryService library, SettingsService settings)
    {
        _library = library;
        _settings = settings;
    }

    public List<string> DetectCandidates(InstalledGame game)
    {
        var results = new List<string>();
        var names = new List<string> { game.Title };

        // Also try exe name
        try
        {
            var launcher = LibraryService.FindLauncherPublic(game.InstallPath);
            if (!string.IsNullOrEmpty(launcher))
                names.Add(Path.GetFileNameWithoutExtension(launcher));
        }
        catch { }

        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData  = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var docs     = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var profile  = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localLow = Path.Combine(profile, "AppData", "LocalLow");

        foreach (var name in names)
        {
            var candidates = new[]
            {
                Path.Combine(localApp, name),
                Path.Combine(appData,  name),
                Path.Combine(docs,     name),
                Path.Combine(docs,     "My Games", name),
                Path.Combine(profile,  "Saved Games", name),
                Path.Combine(localLow, name),
                Path.Combine(game.InstallPath, "saves"),
                Path.Combine(game.InstallPath, "save"),
                Path.Combine(game.InstallPath, "SaveData"),
            };
            foreach (var c in candidates)
            {
                try
                {
                    if (Directory.Exists(c) && Directory.EnumerateFiles(c, "*", SearchOption.AllDirectories).Any())
                        if (!results.Contains(c, StringComparer.OrdinalIgnoreCase))
                            results.Add(c);
                }
                catch { }
            }
        }
        return results;
    }

    public async Task<string?> BackupAsync(InstalledGame game, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(game.SaveFolder) || !Directory.Exists(game.SaveFolder))
            return null;
        try
        {
            var dest = Path.Combine(_settings.Current.SaveBackupRoot, game.Id);
            Directory.CreateDirectory(dest);
            var zipPath = Path.Combine(dest, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");
            await Task.Run(() =>
            {
                ZipFile.CreateFromDirectory(game.SaveFolder, zipPath, CompressionLevel.Optimal, false);
            }, ct);
            Prune(game);
            AppLogger.Info($"Backup created for {game.Title}: {zipPath}");
            return zipPath;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Backup failed for {game.Title}", ex);
            return null;
        }
    }

    public List<(string Path, DateTime Created, long SizeBytes)> ListBackups(InstalledGame game)
    {
        try
        {
            var dir = Path.Combine(_settings.Current.SaveBackupRoot, game.Id);
            if (!Directory.Exists(dir)) return new();
            return Directory.GetFiles(dir, "*.zip")
                .Select(f => (f, File.GetCreationTime(f), new FileInfo(f).Length))
                .OrderByDescending(x => x.Item2)
                .ToList();
        }
        catch { return new(); }
    }

    public async Task<bool> RestoreAsync(InstalledGame game, string backupZipPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(game.SaveFolder)) return false;
        try
        {
            // Safety backup first
            await BackupAsync(game, ct);
            // Extract over save folder
            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(backupZipPath, game.SaveFolder, overwriteFiles: true);
            }, ct);
            AppLogger.Info($"Restore complete for {game.Title} from {backupZipPath}");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Restore failed for {game.Title}", ex);
            return false;
        }
    }

    public async Task BackupAllAsync(CancellationToken ct = default)
    {
        foreach (var game in _library.Games.Where(g => !string.IsNullOrWhiteSpace(g.SaveFolder)).ToList())
        {
            if (ct.IsCancellationRequested) break;
            await BackupAsync(game, ct);
        }
    }

    private void Prune(InstalledGame game)
    {
        try
        {
            var dir = Path.Combine(_settings.Current.SaveBackupRoot, game.Id);
            var files = Directory.GetFiles(dir, "*.zip")
                .OrderByDescending(File.GetCreationTime).ToList();
            foreach (var old in files.Skip(_settings.Current.MaxBackupsPerGame))
                File.Delete(old);
        }
        catch { }
    }
}
