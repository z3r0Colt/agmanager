using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgApp.Models;

namespace AgApp.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // An unset window position uses NaN. Preserve that sentinel on first save.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public SettingsService() : this(Path.Combine(
        AppPaths.DataDirectory, "settings.json")) { }

    public SettingsService(string settingsPath) => _settingsPath = settingsPath;

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), JsonOptions) ?? new();
        }
        catch { Current = new(); }
    }

    public AppSettings CreateSnapshot() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(Current, JsonOptions), JsonOptions)!;

    public void Save(AppSettings? settings = null)
    {
        var candidate = settings ?? Current;
        var json = JsonSerializer.Serialize(candidate, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var tempPath = _settingsPath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsPath, overwrite: true);
            Current = candidate;
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public void EnsureDownloadFolderExists() =>
        PrepareWritableFolder(Current.DownloadFolder);

    public static string PrepareWritableFolder(string? folder)
    {
        var path = Environment.ExpandEnvironmentVariables(folder?.Trim() ?? "");
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)
            || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || path.Contains('*') || path.Contains('?'))
            throw new ArgumentException("Enter a full folder path, such as D:\\Games, or choose a folder.");

        path = Path.GetFullPath(path);
        Directory.CreateDirectory(path);
        // Creating a directory alone doesn't prove we can write files into it.
        using var probe = new FileStream(Path.Combine(path, $".agmanager-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        return path;
    }
}
