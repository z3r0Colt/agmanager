using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;
using AgApp.Models;
using AgApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AgApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly LibraryService _libraryService;

    private const string StartupRegKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupAppName = "DaveyJonesLocker";

    [ObservableProperty] private string _downloadFolder = "";
    [ObservableProperty] private int _connectionsPerDownload = 8;
    [ObservableProperty] private int _maxBandwidthKbps;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private bool _showNotifications = true;
    [ObservableProperty] private bool _deleteZipAfterExtract = true;
    [ObservableProperty] private string _rawgApiKey = "";
    [ObservableProperty] private string _saveBackupRoot = "";
    [ObservableProperty] private int _maxBackupsPerGame = 5;
    [ObservableProperty] private string _defaultBackupSchedule = "OnExit";
    [ObservableProperty] private int _maxConcurrentDownloads = 2;
    [ObservableProperty] private bool _notifyOnExtractionComplete = true;
    [ObservableProperty] private int _updateCheckIntervalHours = 24;
    [ObservableProperty] private bool _enableDiscordRichPresence;
    [ObservableProperty] private string _discordClientId = "";
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _closeToTray;

    public static string[] BackupScheduleOptions = ["OnExit", "Daily", "Weekly", "Off"];

    public SettingsViewModel(SettingsService settingsService, LibraryService libraryService)
    {
        _settingsService = settingsService;
        _libraryService = libraryService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Current;
        DownloadFolder = s.DownloadFolder;
        ConnectionsPerDownload = s.ConnectionsPerDownload;
        MaxBandwidthKbps = s.MaxBandwidthKbps;
        MinimizeToTray = s.MinimizeToTray;
        ShowNotifications = s.ShowNotifications;
        DeleteZipAfterExtract = s.DeleteZipAfterExtract;
        RawgApiKey = s.RawgApiKey;
        SaveBackupRoot = s.SaveBackupRoot;
        MaxBackupsPerGame = s.MaxBackupsPerGame;
        DefaultBackupSchedule = s.DefaultBackupSchedule;
        MaxConcurrentDownloads = s.MaxConcurrentDownloads;
        NotifyOnExtractionComplete = s.NotifyOnExtractionComplete;
        UpdateCheckIntervalHours = s.UpdateCheckIntervalHours;
        EnableDiscordRichPresence = s.EnableDiscordRichPresence;
        DiscordClientId = s.DiscordClientId;
        StartWithWindows = s.StartWithWindows;
        CloseToTray = s.CloseToTray;
    }

    [RelayCommand]
    public void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { SelectedPath = DownloadFolder };
        if (dlg.ShowDialog() == DialogResult.OK)
            DownloadFolder = dlg.SelectedPath;
    }

    [RelayCommand]
    public void Save()
    {
        var s = _settingsService.Current;
        s.DownloadFolder = DownloadFolder;
        s.ConnectionsPerDownload = Math.Clamp(ConnectionsPerDownload, 1, 32);
        s.MaxBandwidthKbps = Math.Max(0, MaxBandwidthKbps);
        s.MinimizeToTray = MinimizeToTray;
        s.ShowNotifications = ShowNotifications;
        s.DeleteZipAfterExtract = DeleteZipAfterExtract;
        s.RawgApiKey = RawgApiKey;
        s.SaveBackupRoot = SaveBackupRoot;
        s.MaxBackupsPerGame = Math.Max(1, MaxBackupsPerGame);
        s.DefaultBackupSchedule = DefaultBackupSchedule;
        s.MaxConcurrentDownloads = Math.Clamp(MaxConcurrentDownloads, 1, 5);
        s.NotifyOnExtractionComplete = NotifyOnExtractionComplete;
        s.UpdateCheckIntervalHours = Math.Max(1, UpdateCheckIntervalHours);
        s.EnableDiscordRichPresence = EnableDiscordRichPresence;
        s.DiscordClientId = DiscordClientId;
        s.StartWithWindows = StartWithWindows;
        s.CloseToTray = CloseToTray;
        _settingsService.Save();

        ApplyStartWithWindows(StartWithWindows);

        System.Windows.MessageBox.Show("Settings saved.", "Davey Jones' Locker",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static void ApplyStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegKey, writable: true);
            if (key == null) return;
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (enable && !string.IsNullOrEmpty(exePath))
                key.SetValue(StartupAppName, $"\"{exePath}\"");
            else
                key.DeleteValue(StartupAppName, throwOnMissingValue: false);
        }
        catch (Exception ex) { AppLogger.Warn("[Settings] ApplyStartWithWindows failed", ex); }
    }

    [RelayCommand]
    public async Task ExportLibraryAsync()
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Library",
                Filter = "JSON files (*.json)|*.json",
                FileName = $"library-export-{DateTime.Now:yyyyMMdd}.json",
                DefaultExt = ".json",
            };
            if (dlg.ShowDialog() != true) return;

            var opts  = new JsonSerializerOptions { WriteIndented = true };
            var games = _libraryService.Games;
            var json  = JsonSerializer.Serialize(games, opts);
            await File.WriteAllTextAsync(dlg.FileName, json).ConfigureAwait(false);

            System.Windows.MessageBox.Show($"Exported {games.Count} games to:\n{dlg.FileName}",
                "Davey Jones' Locker", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("[Settings] ExportLibrary failed", ex);
            System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Davey Jones' Locker",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ImportLibraryAsync()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Library",
                Filter = "JSON files (*.json)|*.json",
            };
            if (dlg.ShowDialog() != true) return;

            var json  = await File.ReadAllTextAsync(dlg.FileName).ConfigureAwait(false);
            var opts  = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var games = JsonSerializer.Deserialize<List<InstalledGame>>(json, opts);
            if (games == null || games.Count == 0)
            {
                System.Windows.MessageBox.Show("No games found in the selected file.",
                    "Davey Jones' Locker", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var g in games)
                _libraryService.AddOrUpdate(g);

            System.Windows.MessageBox.Show($"Imported {games.Count} games.",
                "Davey Jones' Locker", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("[Settings] ImportLibrary failed", ex);
            System.Windows.MessageBox.Show($"Import failed: {ex.Message}", "Davey Jones' Locker",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
