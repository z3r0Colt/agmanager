using System.IO;
using System.Net.Http;
using System.Text.Json;
using AgApp.Models;

namespace AgApp.Services;

public class DownloadManager
{
    private readonly HttpClient _http;
    private readonly SettingsService _settings;
    private readonly ExtractionService _extractor;
    private readonly LibraryService _library;
    private readonly MetadataService _metadata;

    public event Action<DownloadJob>? DownloadStarted;
    public event Action<DownloadJob>? JobUpdated;
    public event Action<DownloadJob, string>? ExtractionCompleted;

    public DownloadManager(SettingsService settings, ExtractionService extractor,
        LibraryService library, MetadataService metadata)
    {
        _settings = settings;
        _extractor = extractor;
        _library = library;
        _metadata = metadata;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/126 Safari/537.36");
    }

    public async Task StartDownloadAsync(DownloadJob job)
    {
        _settings.EnsureDownloadFolderExists();
        var tempDir = Path.Combine(_settings.Current.DownloadFolder, ".tmp");
        Directory.CreateDirectory(tempDir);

        var fileName = SanitizeFileName(job.GameTitle) + ".zip";
        var finalPath = Path.Combine(_settings.Current.DownloadFolder, fileName);
        job.DestinationPath = finalPath;

        try
        {
            job.Status = DownloadStatus.Downloading;
            DownloadStarted?.Invoke(job);
            JobUpdated?.Invoke(job);

            var supportsRange = await CheckRangeSupportAsync(job.Url, job, job.CancellationSource.Token);

            if (supportsRange && job.TotalBytes > 10 * 1024 * 1024)
                await MultiConnectionDownloadAsync(job, tempDir, finalPath);
            else
                await SingleConnectionDownloadAsync(job, finalPath);

            if (job.Status == DownloadStatus.Cancelled) return;

            job.Status = DownloadStatus.Extracting;
            job.SpeedBytesPerSec = 0;
            JobUpdated?.Invoke(job);

            var gameFolder = await _extractor.ExtractAndDeleteAsync(
                finalPath, _settings.Current.DownloadFolder, job.CancellationSource.Token);

            var installed = _library.BuildFromExtractedFolder(job.GameId, job.GameTitle, job.CoverUrl, gameFolder);
            if (installed is not null)
            {
                _library.AddGame(installed);
                ExtractionCompleted?.Invoke(job, gameFolder);
                _ = _metadata.FetchAndApplyAsync(installed);
            }

            job.Status = DownloadStatus.Completed;
            JobUpdated?.Invoke(job);
        }
        catch (OperationCanceledException)
        {
            job.Status = DownloadStatus.Cancelled;
            JobUpdated?.Invoke(job);
        }
        catch (Exception ex)
        {
            job.Status = DownloadStatus.Failed;
            job.ErrorMessage = ex.Message;
            JobUpdated?.Invoke(job);
        }
    }

    private async Task<bool> CheckRangeSupportAsync(string url, DownloadJob job, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Head, url);
        using var resp = await _http.SendAsync(req, ct);
        job.TotalBytes = resp.Content.Headers.ContentLength ?? 0;
        return resp.Headers.AcceptRanges.Contains("bytes");
    }

    private async Task MultiConnectionDownloadAsync(DownloadJob job, string tempDir, string finalPath)
    {
        int connections = _settings.Current.ConnectionsPerDownload;
        long chunkSize = job.TotalBytes / connections;
        var tempFiles = new string[connections];
        var tasks = new Task[connections];

        for (int i = 0; i < connections; i++)
        {
            int idx = i;
            long start = idx * chunkSize;
            long end = idx == connections - 1 ? job.TotalBytes - 1 : start + chunkSize - 1;
            tempFiles[idx] = Path.Combine(tempDir, $"{job.Id}.part{idx}");
            tasks[idx] = DownloadChunkAsync(job, start, end, tempFiles[idx], job.CancellationSource.Token);
        }

        await Task.WhenAll(tasks);

        if (job.CancellationSource.Token.IsCancellationRequested) return;

        await MergeChunksAsync(tempFiles, finalPath);
        foreach (var f in tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    private async Task DownloadChunkAsync(DownloadJob job, long start, long end,
        string destFile, CancellationToken ct)
    {
        long alreadyDownloaded = 0;
        if (File.Exists(destFile))
        {
            alreadyDownloaded = new FileInfo(destFile).Length;
            if (alreadyDownloaded >= end - start + 1) return;
            start += alreadyDownloaded;
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, job.Url);
        req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var file = new FileStream(destFile, alreadyDownloaded > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None, 81920);

        var buffer = new byte[81920];
        var speedTimer = System.Diagnostics.Stopwatch.StartNew();
        long speedBytes = 0;

        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            lock (job)
            {
                job.DownloadedBytes += read;
                speedBytes += read;
            }

            if (speedTimer.Elapsed.TotalSeconds >= 1)
            {
                job.SpeedBytesPerSec = speedBytes / speedTimer.Elapsed.TotalSeconds;
                if (job.SpeedBytesPerSec > 0)
                    job.Eta = TimeSpan.FromSeconds((job.TotalBytes - job.DownloadedBytes) / job.SpeedBytesPerSec);
                speedBytes = 0;
                speedTimer.Restart();
                JobUpdated?.Invoke(job);
            }

            ApplyBandwidthThrottle(read);
        }
    }

    private async Task SingleConnectionDownloadAsync(DownloadJob job, string finalPath)
    {
        using var resp = await _http.GetAsync(job.Url, HttpCompletionOption.ResponseHeadersRead,
            job.CancellationSource.Token);
        resp.EnsureSuccessStatusCode();

        job.TotalBytes = resp.Content.Headers.ContentLength ?? 0;

        using var stream = await resp.Content.ReadAsStreamAsync(job.CancellationSource.Token);
        using var file = new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

        var buffer = new byte[81920];
        var speedTimer = System.Diagnostics.Stopwatch.StartNew();
        long speedBytes = 0;

        int read;
        while ((read = await stream.ReadAsync(buffer, job.CancellationSource.Token)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), job.CancellationSource.Token);
            job.DownloadedBytes += read;
            speedBytes += read;

            if (speedTimer.Elapsed.TotalSeconds >= 1)
            {
                job.SpeedBytesPerSec = speedBytes / speedTimer.Elapsed.TotalSeconds;
                if (job.SpeedBytesPerSec > 0)
                    job.Eta = TimeSpan.FromSeconds((job.TotalBytes - job.DownloadedBytes) / job.SpeedBytesPerSec);
                speedBytes = 0;
                speedTimer.Restart();
                JobUpdated?.Invoke(job);
            }

            ApplyBandwidthThrottle(read);
        }
    }

    private static async Task MergeChunksAsync(string[] parts, string output)
    {
        using var outStream = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None, 81920);
        foreach (var part in parts)
        {
            if (!File.Exists(part)) continue;
            using var partStream = File.OpenRead(part);
            await partStream.CopyToAsync(outStream);
        }
    }

    private void ApplyBandwidthThrottle(int bytesRead)
    {
        int maxKbps = _settings.Current.MaxBandwidthKbps;
        if (maxKbps <= 0) return;
        int delayMs = (int)(bytesRead / (maxKbps * 1024.0 / 1000.0));
        if (delayMs > 0) Thread.Sleep(delayMs);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Length > 60 ? name[..60] : name;
    }
}
