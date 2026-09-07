using System.Windows;
using System.Windows.Forms;
using AgApp.Models;
using AgApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

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

    public static string[] BackupScheduleOptions = ["OnExit", "Daily", "Weekly", "Off"];

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
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
        _settingsService.Save();

        System.Windows.MessageBox.Show("Settings saved.", "Davey Jones' Locker",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
