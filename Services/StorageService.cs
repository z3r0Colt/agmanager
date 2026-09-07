using System.IO;
using AgApp.Models;

namespace AgApp.Services;

public class StorageService
{
    public List<DriveUsageEntry> GetDriveUsage(IReadOnlyList<InstalledGame> games)
    {
        var byDrive = games
            .Where(g => !string.IsNullOrEmpty(g.InstallPath))
            .GroupBy(g => Path.GetPathRoot(g.InstallPath)?.ToUpperInvariant() ?? "")
            .ToDictionary(g => g.Key, g => g.Sum(x => x.InstalledSizeBytes));

        var result = new List<DriveUsageEntry>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var key = drive.RootDirectory.FullName.ToUpperInvariant();
                byDrive.TryGetValue(key, out var libBytes);
                result.Add(new DriveUsageEntry(drive.Name, drive.TotalSize, drive.AvailableFreeSpace, libBytes));
            }
        }
        catch { }
        return result;
    }

    public async Task MoveGameAsync(InstalledGame game, string newParentFolder,
        IProgress<(string status, double pct)>? progress = null,
        CancellationToken ct = default)
    {
        var originalPath = game.InstallPath;
        var destFolder   = Path.Combine(newParentFolder, Path.GetFileName(originalPath)!);
        Directory.CreateDirectory(destFolder);

        var allFiles  = Directory.GetFiles(originalPath, "*", SearchOption.AllDirectories).ToList();
        long totalSize = allFiles.Sum(f => new FileInfo(f).Length);
        long copied   = 0;

        foreach (var src in allFiles)
        {
            ct.ThrowIfCancellationRequested();
            var rel  = Path.GetRelativePath(originalPath, src);
            var dest = Path.Combine(destFolder, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(src, dest, overwrite: true);
            copied += new FileInfo(src).Length;
            progress?.Report(($"Copying {Path.GetFileName(src)}", copied * 100.0 / Math.Max(totalSize, 1)));
        }

        // Verify
        var destFiles = Directory.GetFiles(destFolder, "*", SearchOption.AllDirectories);
        long destSize = destFiles.Sum(f => new FileInfo(f).Length);
        if (destFiles.Length != allFiles.Count || Math.Abs(destSize - totalSize) > 1024)
            throw new InvalidOperationException("Verification failed: mismatch after copy.");

        // Update library
        game.ExecutablePath = game.ExecutablePath.Replace(
            originalPath, destFolder, StringComparison.OrdinalIgnoreCase);
        game.InstallPath = destFolder;

        // Delete original
        progress?.Report(("Cleaning up original...", 99));
        await Task.Run(() => Directory.Delete(originalPath, recursive: true), ct);
        progress?.Report(("Done", 100));
    }
}

public record DriveUsageEntry(string Name, long TotalBytes, long FreeBytes, long LibraryBytes);
