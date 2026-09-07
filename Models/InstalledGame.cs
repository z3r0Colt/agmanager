using CommunityToolkit.Mvvm.ComponentModel;

namespace AgApp.Models;

public partial class InstalledGame : ObservableObject
{
    public string Id { get; set; } = "";

    // Core
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _coverUrl = "";
    [ObservableProperty] private string _heroUrl = "";
    [ObservableProperty] private string _installPath = "";
    [ObservableProperty] private string _executablePath = "";
    [ObservableProperty] private string _launchArguments = "";
    [ObservableProperty] private string _version = "";
    [ObservableProperty] private long _installedSizeBytes;
    [ObservableProperty] private DateTime _installedAt;
    [ObservableProperty] private TimeSpan _playTime;
    [ObservableProperty] private DateTime? _lastPlayed;

    // Metadata
    [ObservableProperty] private string _genre = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _developer = "";
    [ObservableProperty] private string _publisher = "";
    [ObservableProperty] private int _releaseYear;
    [ObservableProperty] private string _esrbRating = "";        // "E", "E10+", "T", "M", "AO", "RP"
    [ObservableProperty] private int _metacriticScore;           // 0 = no score
    [ObservableProperty] private string _playerSupport = "";     // "Single-player", "Multiplayer", etc.
    [ObservableProperty] private string _controllerSupport = ""; // "Full", "Partial", "None"
    [ObservableProperty] private string _website = "";

    // Personal
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private int _userRating;               // 0-5 stars
    [ObservableProperty] private string _personalNotes = "";

    public List<string> Screenshots { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    // Session state (not persisted)
    [System.Text.Json.Serialization.JsonIgnore]
    [ObservableProperty] private bool _isNowPlaying;
    [System.Text.Json.Serialization.JsonIgnore]
    [ObservableProperty] private TimeSpan _currentSessionTime;

    // Save backup settings
    [ObservableProperty] private string _saveFolder = "";
    [ObservableProperty] private string _backupSchedule = ""; // "" = global default

    // Launch options (persisted)
    [ObservableProperty] private string _launchTargetOverride = "";
    [ObservableProperty] private bool _runAsAdmin;
    [ObservableProperty] private string _workingDirectoryOverride = "";
    [ObservableProperty] private string _preLaunchPath = "";
    [ObservableProperty] private string _preLaunchArgs = "";

    // ── Computed display ──────────────────────────────────────────────────

    public string HeroOrCoverUrl => !string.IsNullOrEmpty(HeroUrl) ? HeroUrl : CoverUrl;

    public string TagsDisplay => Tags.Count > 0 ? string.Join(", ", Tags) : "";

    public string PlayTimeDisplay => PlayTime == TimeSpan.Zero
        ? "Never played"
        : $"{(int)PlayTime.TotalHours}h {PlayTime.Minutes}m";

    public string InstalledSizeDisplay
    {
        get
        {
            if (InstalledSizeBytes <= 0) return "";
            if (InstalledSizeBytes >= 1_073_741_824) return $"{InstalledSizeBytes / 1_073_741_824.0:F1} GB";
            if (InstalledSizeBytes >= 1_048_576) return $"{InstalledSizeBytes / 1_048_576.0:F0} MB";
            return $"{InstalledSizeBytes / 1024.0:F0} KB";
        }
    }

    public string InstalledAtDisplay => InstalledAt == default ? "" : InstalledAt.ToString("MMM d, yyyy");
    public string LastPlayedDisplay => LastPlayed.HasValue ? LastPlayed.Value.ToString("MMM d, yyyy") : "Never";

    // Star rating helpers (true = filled star)
    public bool Star1 => UserRating >= 1;
    public bool Star2 => UserRating >= 2;
    public bool Star3 => UserRating >= 3;
    public bool Star4 => UserRating >= 4;
    public bool Star5 => UserRating >= 5;

    // ── Property-changed hooks ────────────────────────────────────────────

    partial void OnPlayTimeChanged(TimeSpan value) => OnPropertyChanged(nameof(PlayTimeDisplay));
    partial void OnLastPlayedChanged(DateTime? value) => OnPropertyChanged(nameof(LastPlayedDisplay));
    partial void OnInstalledSizeBytesChanged(long value) => OnPropertyChanged(nameof(InstalledSizeDisplay));
    partial void OnInstalledAtChanged(DateTime value) => OnPropertyChanged(nameof(InstalledAtDisplay));

    partial void OnCoverUrlChanged(string value) => OnPropertyChanged(nameof(HeroOrCoverUrl));
    partial void OnHeroUrlChanged(string value) => OnPropertyChanged(nameof(HeroOrCoverUrl));

    partial void OnUserRatingChanged(int value)
    {
        OnPropertyChanged(nameof(Star1));
        OnPropertyChanged(nameof(Star2));
        OnPropertyChanged(nameof(Star3));
        OnPropertyChanged(nameof(Star4));
        OnPropertyChanged(nameof(Star5));
    }
}
