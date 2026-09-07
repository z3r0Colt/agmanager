using System.IO;

namespace AgApp.Models;

public class AppSettings
{
    public string DownloadFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AnkerGames");

    public int ConnectionsPerDownload { get; set; } = 8;
    public int MaxBandwidthKbps { get; set; } = 0;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool DeleteZipAfterExtract { get; set; } = true;
    public bool AutoStartWithWindows { get; set; } = false;
    public string RawgApiKey { get; set; } = "4cc0bf4ae2144a2c915f60269a3a171d";

    public int MaxConcurrentDownloads { get; set; } = 2;
    public bool NotifyOnExtractionComplete { get; set; } = true;
    public int UpdateCheckIntervalHours { get; set; } = 24;

    public string SaveBackupRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgApp", "SaveBackups");
    public int MaxBackupsPerGame { get; set; } = 5;
    public string DefaultBackupSchedule { get; set; } = "OnExit";

    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 780;
    public bool WindowMaximized { get; set; } = false;
}
