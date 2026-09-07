using System.Collections.ObjectModel;
using System.IO;
using AgApp.Models;
using AgApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgApp.ViewModels;

public partial class StorageViewModel : ObservableObject
{
    private readonly StorageService _storage;
    private readonly LibraryService _library;

    [ObservableProperty] private ObservableCollection<DriveUsageEntry> _drives = new();
    [ObservableProperty] private ObservableCollection<InstalledGame> _games = new();
    [ObservableProperty] private string _moveStatus = "";
    [ObservableProperty] private double _moveProgress;

    public StorageViewModel(StorageService storage, LibraryService library)
    {
        _storage = storage;
        _library = library;
    }

    public void Refresh()
    {
        try
        {
            Drives.Clear();
            foreach (var d in _storage.GetDriveUsage(_library.Games))
                Drives.Add(d);

            Games.Clear();
            foreach (var g in _library.Games.OrderByDescending(g => g.InstalledSizeBytes))
                Games.Add(g);
        }
        catch (Exception ex) { AppLogger.Error("StorageViewModel.Refresh failed", ex); }
    }

    [RelayCommand]
    public async Task MoveGameAsync(InstalledGame game)
    {
        if (game == null) return;
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select any file in the destination parent folder",
                CheckFileExists = false
            };
            if (dlg.ShowDialog() != true) return;
            var targetDir = Path.GetDirectoryName(dlg.FileName) ?? "";
            if (!Directory.Exists(targetDir)) return;

            MoveStatus = "Moving...";
            MoveProgress = 0;
            var progress = new Progress<(string status, double pct)>(p =>
            {
                MoveStatus = p.status;
                MoveProgress = p.pct;
            });

            await _storage.MoveGameAsync(game, targetDir, progress);
            _library.Save();
            MoveStatus = "Done!";
            Refresh();
        }
        catch (Exception ex)
        {
            AppLogger.Error("MoveGame failed", ex);
            MoveStatus = $"Error: {ex.Message}";
        }
    }
}
