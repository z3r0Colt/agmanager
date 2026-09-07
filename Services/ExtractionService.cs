using System.IO;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace AgApp.Services;

public class ExtractionService
{
    public event Action<string, double>? ProgressChanged;

    public static bool IsMultiPart(string path)
    {
        var ext  = Path.GetExtension(path).ToLowerInvariant();
        var name = Path.GetFileName(path).ToLowerInvariant();
        return ext is ".001" or ".r00"
            || name.EndsWith(".part1.rar", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".part01.rar", StringComparison.OrdinalIgnoreCase);
        // SharpCompress's ArchiveFactory.OpenArchive handles multi-part RAR automatically
        // when the first part is provided. No additional logic needed.
    }

    public async Task<string> ExtractAndDeleteAsync(string archivePath, string destinationFolder,
        CancellationToken ct = default)
    {
        var gameFolder = Path.Combine(destinationFolder,
            Path.GetFileNameWithoutExtension(archivePath));
        Directory.CreateDirectory(gameFolder);

        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath);

            long totalBytes = archive.Entries
                .Where(e => !e.IsDirectory)
                .Sum(e => e.Size);
            long extractedBytes = 0;

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();

                if (entry.IsDirectory) continue;

                // Zip-slip guard
                var destPath = Path.GetFullPath(Path.Combine(gameFolder, entry.Key ?? ""));
                if (!destPath.StartsWith(gameFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && destPath != gameFolder)
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                entry.WriteToFile(destPath, new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });

                extractedBytes += entry.Size;

                if (totalBytes > 0)
                    ProgressChanged?.Invoke(archivePath, (double)extractedBytes / totalBytes * 100);
            }
        }, ct);

        File.Delete(archivePath);
        return gameFolder;
    }
}
