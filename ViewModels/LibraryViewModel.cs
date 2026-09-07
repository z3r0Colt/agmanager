using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AgApp.Models;
using AgApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgApp.ViewModels;

public partial class ScreenshotEntry : ObservableObject
{
    [ObservableProperty] private string _url = "";
    public ScreenshotEntry(string url) => _url = url;
}

public partial class LibraryViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly MetadataService _metadata;
    private readonly GameSessionService _session;
    private readonly SaveBackupService _backup;
    private readonly UpdateCheckService _updateChecker;

    [ObservableProperty] private ObservableCollection<InstalledGame> _games = new();
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private string _sortBy = "Date";
    [ObservableProperty] private InstalledGame? _selectedGame;
    [ObservableProperty] private bool _showDetail;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _fetchingMetadata;
    [ObservableProperty] private ObservableCollection<ScreenshotEntry> _editScreenshots = new();
    [ObservableProperty] private string _editTagsText = "";

    // Metadata search panel
    [ObservableProperty] private bool _showMetadataSearch;
    [ObservableProperty] private string _metadataSearchQuery = "";
    [ObservableProperty] private bool _isSearchingMetadata;
    [ObservableProperty] private ObservableCollection<MetadataSearchResult> _metadataSearchResults = new();

    // Screenshot lightbox
    [ObservableProperty] private bool _showScreenshotViewer;
    [ObservableProperty] private string _viewerImageUrl = "";

    // Carousel index
    [ObservableProperty] private int _screenshotIndex;

    // Launch options
    private string _suggestedExe = "";
    public string SuggestedExe
    {
        get => _suggestedExe;
        private set { _suggestedExe = value; OnPropertyChanged(); }
    }

    // Save backup
    [ObservableProperty] private ObservableCollection<string> _saveCandidates = new();

    public static string[] BackupScheduleOptions = ["", "OnExit", "Daily", "Weekly", "Off"];

    private InstalledGame? _editSnapshot;

    public static IReadOnlyList<string> SortOptions { get; } = ["Name", "Date", "Playtime", "Last Played", "Favorites"];
    public static IReadOnlyList<string> EsrbOptions { get; } = ["", "E", "E10+", "T", "M", "AO", "RP"];
    public static IReadOnlyList<string> ControllerOptions { get; } = ["", "Full", "Partial", "None"];

    public LibraryViewModel(LibraryService library, MetadataService metadata, GameSessionService session,
        SaveBackupService backup, UpdateCheckService updateChecker)
    {
        _library = library;
        _metadata = metadata;
        _session = session;
        _backup = backup;
        _updateChecker = updateChecker;
    }

    public void Refresh()
    {
        Games.Clear();
        var filter = FilterText.ToLower();
        var query = _library.Games
            .Where(g => string.IsNullOrWhiteSpace(filter) ||
                        g.Title.Contains(filter, StringComparison.OrdinalIgnoreCase));

        query = SortBy switch
        {
            "Name"        => query.OrderBy(g => g.Title),
            "Playtime"    => query.OrderByDescending(g => g.PlayTime),
            "Last Played" => query.OrderByDescending(g => g.LastPlayed ?? DateTime.MinValue),
            "Favorites"   => query.OrderByDescending(g => g.IsFavorite).ThenBy(g => g.Title),
            _             => query.OrderByDescending(g => g.InstalledAt)
        };

        foreach (var g in query) Games.Add(g);
    }

    partial void OnFilterTextChanged(string value) => Refresh();
    partial void OnSortByChanged(string value) => Refresh();

    // ── Navigation ────────────────────────────────────────────────────────

    [RelayCommand]
    public void SelectGame(InstalledGame game)
    {
        SelectedGame = game;
        ShowDetail = true;
        IsEditing = false;
        ShowMetadataSearch = false;
        ScreenshotIndex = 0;
        try
        {
            SuggestedExe = LibraryService.ParseBatForExe(
                LibraryService.FindLauncherPublic(game.InstallPath)) ?? "";
        }
        catch { SuggestedExe = ""; }
    }

    [RelayCommand]
    public void BackToLibrary()
    {
        IsEditing = false;
        ShowDetail = false;
        ShowMetadataSearch = false;
        SelectedGame = null;
        Refresh();
    }

    // ── Metadata auto-fetch ───────────────────────────────────────────────

    [RelayCommand]
    public async Task FetchMetadata()
    {
        if (SelectedGame is null || FetchingMetadata) return;
        FetchingMetadata = true;
        try { await _metadata.FetchAndApplyAsync(SelectedGame); }
        finally { FetchingMetadata = false; }
    }

    // ── Metadata search ───────────────────────────────────────────────────

    [RelayCommand]
    public void OpenMetadataSearch()
    {
        if (SelectedGame is null) return;
        MetadataSearchQuery = SelectedGame.Title;
        MetadataSearchResults.Clear();
        ShowMetadataSearch = true;
    }

    [RelayCommand]
    public void CloseMetadataSearch() => ShowMetadataSearch = false;

    [RelayCommand]
    public async Task RunMetadataSearch()
    {
        if (string.IsNullOrWhiteSpace(MetadataSearchQuery) || IsSearchingMetadata) return;
        IsSearchingMetadata = true;
        MetadataSearchResults.Clear();
        try
        {
            var results = await _metadata.SearchAsync(MetadataSearchQuery);
            foreach (var r in results) MetadataSearchResults.Add(r);
        }
        finally { IsSearchingMetadata = false; }
    }

    [RelayCommand]
    public async Task ApplySearchResult(MetadataSearchResult result)
    {
        if (SelectedGame is null) return;
        ShowMetadataSearch = false;
        FetchingMetadata = true;
        try { await _metadata.ApplyFromRawgIdAsync(SelectedGame, result.RawgId); }
        finally { FetchingMetadata = false; }
    }

    // ── Edit mode ─────────────────────────────────────────────────────────

    [RelayCommand]
    public void StartEdit()
    {
        if (SelectedGame is null) return;
        var g = SelectedGame;
        _editSnapshot = new InstalledGame
        {
            Id = g.Id, Title = g.Title, Genre = g.Genre, Description = g.Description,
            CoverUrl = g.CoverUrl, HeroUrl = g.HeroUrl, Version = g.Version,
            InstallPath = g.InstallPath, ExecutablePath = g.ExecutablePath,
            LaunchArguments = g.LaunchArguments, InstalledAt = g.InstalledAt,
            InstalledSizeBytes = g.InstalledSizeBytes, PlayTime = g.PlayTime,
            LastPlayed = g.LastPlayed, Screenshots = new List<string>(g.Screenshots),
            Developer = g.Developer, Publisher = g.Publisher, ReleaseYear = g.ReleaseYear,
            EsrbRating = g.EsrbRating, MetacriticScore = g.MetacriticScore,
            PlayerSupport = g.PlayerSupport, ControllerSupport = g.ControllerSupport,
            Website = g.Website, PersonalNotes = g.PersonalNotes,
            IsFavorite = g.IsFavorite, UserRating = g.UserRating,
            Tags = new List<string>(g.Tags)
        };

        EditScreenshots.Clear();
        foreach (var s in g.Screenshots) EditScreenshots.Add(new ScreenshotEntry(s));
        EditTagsText = string.Join(", ", g.Tags);

        IsEditing = true;
    }

    [RelayCommand]
    public void SaveEdit()
    {
        if (SelectedGame is null) return;
        SelectedGame.Screenshots = EditScreenshots
            .Select(e => e.Url.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList();
        SelectedGame.Tags = EditTagsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
        _library.Save();
        IsEditing = false;
        _editSnapshot = null;
        ScreenshotIndex = 0;
    }

    [RelayCommand]
    public void CancelEdit()
    {
        if (SelectedGame is not null && _editSnapshot is not null)
        {
            var g = SelectedGame;
            var s = _editSnapshot;
            g.Title = s.Title; g.Genre = s.Genre; g.Description = s.Description;
            g.CoverUrl = s.CoverUrl; g.HeroUrl = s.HeroUrl; g.Version = s.Version;
            g.Developer = s.Developer; g.Publisher = s.Publisher;
            g.ReleaseYear = s.ReleaseYear; g.EsrbRating = s.EsrbRating;
            g.MetacriticScore = s.MetacriticScore; g.PlayerSupport = s.PlayerSupport;
            g.ControllerSupport = s.ControllerSupport; g.Website = s.Website;
            g.PersonalNotes = s.PersonalNotes; g.LaunchArguments = s.LaunchArguments;
            g.Screenshots = s.Screenshots; g.Tags = s.Tags;
            EditScreenshots.Clear();
            foreach (var url in s.Screenshots) EditScreenshots.Add(new ScreenshotEntry(url));
            EditTagsText = string.Join(", ", s.Tags);
        }
        IsEditing = false;
        _editSnapshot = null;
    }

    [RelayCommand]
    public void BrowseCoverImage()
    {
        if (SelectedGame is null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select cover image",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|All files|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        SelectedGame.CoverUrl = new Uri(dlg.FileName).AbsoluteUri;
    }

    [RelayCommand]
    public void BrowseHeroImage()
    {
        if (SelectedGame is null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select hero/banner image",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|All files|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        SelectedGame.HeroUrl = new Uri(dlg.FileName).AbsoluteUri;
    }

    [RelayCommand]
    public void AddScreenshot() => EditScreenshots.Add(new ScreenshotEntry(""));

    [RelayCommand]
    public void RemoveScreenshot(ScreenshotEntry entry) => EditScreenshots.Remove(entry);

    // ── Screenshot carousel ───────────────────────────────────────────────

    [RelayCommand]
    public void PrevScreenshot()
    {
        if (SelectedGame is null || SelectedGame.Screenshots.Count == 0) return;
        ScreenshotIndex = (ScreenshotIndex - 1 + SelectedGame.Screenshots.Count) % SelectedGame.Screenshots.Count;
    }

    [RelayCommand]
    public void NextScreenshot()
    {
        if (SelectedGame is null || SelectedGame.Screenshots.Count == 0) return;
        ScreenshotIndex = (ScreenshotIndex + 1) % SelectedGame.Screenshots.Count;
    }

    [RelayCommand]
    public void OpenScreenshot(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        ViewerImageUrl = url;
        ShowScreenshotViewer = true;
    }

    [RelayCommand]
    public void CloseScreenshotViewer() => ShowScreenshotViewer = false;

    // ── Personal metadata ─────────────────────────────────────────────────

    [RelayCommand]
    public void ToggleFavorite(InstalledGame game)
    {
        game.IsFavorite = !game.IsFavorite;
        _library.Save();
        if (SortBy == "Favorites") Refresh();
    }

    [RelayCommand]
    public void SetUserRating(object parameter)
    {
        if (SelectedGame is null) return;
        if (parameter is int stars || (parameter is string s && int.TryParse(s, out stars)))
        {
            SelectedGame.UserRating = SelectedGame.UserRating == stars ? 0 : stars;
            _library.Save();
        }
    }

    [RelayCommand]
    public void OpenWebsite(InstalledGame game)
    {
        if (string.IsNullOrWhiteSpace(game.Website)) return;
        try { Process.Start(new ProcessStartInfo(game.Website) { UseShellExecute = true }); }
        catch { }
    }

    // ── Game actions ──────────────────────────────────────────────────────

    [RelayCommand]
    public void LaunchGame(InstalledGame game)
    {
        try { _library.LaunchGame(game, _session); }
        catch (Exception ex) { AppLogger.Error("LaunchGame failed", ex); }
    }

    [RelayCommand]
    public void SaveLaunchOptions(InstalledGame game)
    {
        try { _library.Save(); }
        catch (Exception ex) { AppLogger.Error("SaveLaunchOptions failed", ex); }
    }

    [RelayCommand]
    public void BrowseLaunchTarget(InstalledGame game)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select launcher",
            Filter = "Launchers (*.bat;*.exe)|*.bat;*.exe|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(game.InstallPath) ? game.InstallPath : null
        };
        if (dlg.ShowDialog() == true)
            game.LaunchTargetOverride = dlg.FileName;
    }

    [RelayCommand]
    public void BrowseWorkingDir(InstalledGame game)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select any file in the desired working directory",
            InitialDirectory = Directory.Exists(game.InstallPath) ? game.InstallPath : null
        };
        if (dlg.ShowDialog() == true)
            game.WorkingDirectoryOverride = Path.GetDirectoryName(dlg.FileName) ?? "";
    }

    [RelayCommand]
    public void BrowsePreLaunch(InstalledGame game)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Select pre-launch program" };
        if (dlg.ShowDialog() == true) game.PreLaunchPath = dlg.FileName;
    }

    // ── Save Backup commands ──────────────────────────────────────────────

    [RelayCommand]
    public async Task BackupNowAsync(InstalledGame game)
    {
        try { await _backup.BackupAsync(game); }
        catch (Exception ex) { AppLogger.Error("BackupNow failed", ex); }
    }

    [RelayCommand]
    public async Task BackupAllAsync()
    {
        try { await _backup.BackupAllAsync(); }
        catch (Exception ex) { AppLogger.Error("BackupAll failed", ex); }
    }

    [RelayCommand]
    public void DetectSaveFolder(InstalledGame game)
    {
        if (game == null) return;
        SaveCandidates = new ObservableCollection<string>(_backup.DetectCandidates(game));
    }

    [RelayCommand]
    public void SelectSaveCandidate(string path)
    {
        if (SelectedGame != null) SelectedGame.SaveFolder = path;
    }

    [RelayCommand]
    public void BrowseSaveFolder(InstalledGame game)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Select any file in your save folder" };
        if (dlg.ShowDialog() == true)
            game.SaveFolder = Path.GetDirectoryName(dlg.FileName) ?? "";
    }

    public List<(string Path, DateTime Created, long SizeBytes)> GetBackups(InstalledGame game) =>
        _backup.ListBackups(game);

    [RelayCommand]
    public async Task RestoreBackupAsync(string backupPath)
    {
        if (SelectedGame == null) return;
        try { await _backup.RestoreAsync(SelectedGame, backupPath); }
        catch (Exception ex) { AppLogger.Error("Restore failed", ex); }
    }

    // ── Update Detection commands ─────────────────────────────────────────

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        try { await _updateChecker.CheckAllAsync(); }
        catch (Exception ex) { AppLogger.Error("CheckForUpdates failed", ex); }
    }

    [RelayCommand]
    public void OpenGamePage(InstalledGame? game)
    {
        if (game == null || string.IsNullOrEmpty(game.PageUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(game.PageUrl) { UseShellExecute = true });
        }
        catch (Exception ex) { AppLogger.Warn("OpenGamePage failed", ex); }
    }

    [RelayCommand]
    public void OpenFolder(InstalledGame game) => _library.OpenFolder(game);

    [RelayCommand]
    public void DeleteGame(InstalledGame game)
    {
        var result = MessageBox.Show(
            $"Delete \"{game.Title}\" and all its files?\n\nThis cannot be undone.", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        _library.DeleteGame(game);
        if (ShowDetail && SelectedGame?.Id == game.Id)
            BackToLibrary();
        else
            Refresh();
    }
}
