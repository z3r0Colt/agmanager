using System.Collections.ObjectModel;
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
    private SemaphoreSlim? _gate;

    private static readonly string JobsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgApp", "jobs.json");

    public ObservableCollection<DownloadJob> Jobs { get; } = new();

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

    private SemaphoreSlim GetGate()
    {
        int max = Math.Max(1, _settings.Current.MaxConcurrentDownloads);
        if (_gate == null || _gate.CurrentCount == 0)
            _gate ??= new SemaphoreSlim(max, max);
        return _gate;
    }

    private static bool CheckDiskSpace(string destPath, long fileSize, out string errorMsg)
    {
        errorMsg = "";
        try
        {
            var drive = Path.GetPathRoot(destPath)!;
            var free = new DriveInfo(drive).AvailableFreeSpace;
            var needed = fileSize + fileSize * 2; // archive + 2x for extraction
            if (needed > 0 && free < needed)
            {
                errorMsg = $"Not enough disk space. Need ~{needed / 1024.0 / 1024 / 1024:F1} GB, have {free / 1024.0 / 1024 / 1024:F1} GB free.";
                return false;
            }
        }
        catch { }
        return true;
    }

    private void PersistJobs()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(JobsPath)!);
            var toSave = Jobs.Where(j => j.Status is not (DownloadStatus.Completed or DownloadStatus.Cancelled)).ToList();
            File.WriteAllText(JobsPath, JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { AppLogger.Warn("Failed to persist jobs", ex); }
    }

    public void LoadPersistedJobs()
    {
        try
        {
            if (!File.Exists(JobsPath)) return;
            var jobs = JsonSerializer.Deserialize<List<DownloadJob>>(File.ReadAllText(JobsPath)) ?? new();
            foreach (var job in jobs)
            {
                job.Status = DownloadStatus.Paused;
                Jobs.Add(job);
            }
        }
        catch (Exception ex) { AppLogger.Warn("Failed to load persisted jobs", ex); }
    }

    public void MoveUp(DownloadJob job)
    {
        var idx = Jobs.IndexOf(job);
        if (idx > 0)
        {
            Jobs.Move(idx, idx - 1);
            PersistJobs();
        }
    }

    public void MoveDown(DownloadJob job)
    {
        var idx = Jobs.IndexOf(job);
        if (idx >= 0 && idx < Jobs.Count - 1)
        {
            Jobs.Move(idx, idx + 1);
            PersistJobs();
        }
    }

    public async Task StartDownloadAsync(DownloadJob job)
    {
        _settings.EnsureDownloadFolderExists();
        var tempDir = Path.Combine(_settings.Current.DownloadFolder, ".tmp");
        Directory.CreateDirectory(tempDir);

        var fileName = SanitizeFileName(job.GameTitle) + ".zip";
        var finalPath = Path.Combine(_settings.Current.DownloadFolder, fileName);
        job.DestinationPath = finalPath;

        // Track job
        if (!Jobs.Contains(job))
            System.Windows.Application.Current?.Dispatcher.Invoke(() => Jobs.Insert(0, job));

        var gate = GetGate();
        await gate.WaitAsync(job.CancellationSource.Token);
        try
        {
            job.Status = DownloadStatus.Downloading;
            DownloadStarted?.Invoke(job);
            JobUpdated?.Invoke(job);

            // Disk space check (use total bytes if known, else skip)
            if (job.TotalBytes > 0 && !CheckDiskSpace(finalPath, job.TotalBytes, out var diskErr))
            {
                job.Status = DownloadStatus.Failed;
                job.ErrorMessage = diskErr;
                JobUpdated?.Invoke(job);
                return;
            }

            int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var supportsRange = await CheckRangeSupportAsync(job.Url, job, job.CancellationSource.Token);

                    // Re-check disk space with actual size
                    if (job.TotalBytes > 0 && !CheckDiskSpace(finalPath, job.TotalBytes, out var diskErr2))
                    {
                        job.Status = DownloadStatus.Failed;
                        job.ErrorMessage = diskErr2;
                        JobUpdated?.Invoke(job);
                        return;
                    }

                    if (supportsRange && job.TotalBytes > 10 * 1024 * 1024)
                        await MultiConnectionDownloadAsync(job, tempDir, finalPath);
                    else
                        await SingleConnectionDownloadAsync(job, finalPath);
                    break; // success
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    job.RetryCount = attempt + 1;
                    if (attempt >= maxRetries - 1) throw;
                    AppLogger.Warn($"Download attempt {attempt + 1} failed for {job.GameTitle}, retrying", ex);
                    job.Status = DownloadStatus.Downloading;
                    job.ErrorMessage = $"Retry {attempt + 1}/{maxRetries - 1}...";
                    JobUpdated?.Invoke(job);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), job.CancellationSource.Token);
                    job.ErrorMessage = "";
                }
            }

            if (job.Status == DownloadStatus.Cancelled) return;

            job.Status = DownloadStatus.Extracting;
            job.SpeedBytesPerSec = 0;
            JobUpdated?.Invoke(job);
            PersistJobs();

            var gameFolder = await _extractor.ExtractAndDeleteAsync(
                finalPath, _settings.Current.DownloadFolder, job.CancellationSource.Token);

            var installed = _library.BuildFromExtractedFolder(job.GameId, job.GameTitle, job.CoverUrl, gameFolder, job.PageUrl);
            if (installed is not null)
            {
                _library.AddGame(installed);
                ExtractionCompleted?.Invoke(job, gameFolder);
                _ = _metadata.FetchAndApplyAsync(installed);
            }

            job.Status = DownloadStatus.Completed;
            JobUpdated?.Invoke(job);
            PersistJobs();
        }
        catch (OperationCanceledException)
        {
            job.Status = DownloadStatus.Cancelled;
            JobUpdated?.Invoke(job);
            PersistJobs();
        }
        catch (Exception ex)
        {
            job.Status = DownloadStatus.Failed;
            job.ErrorMessage = ex.Message;
            JobUpdated?.Invoke(job);
            PersistJobs();
        }
        finally
        {
            gate.Release();
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
