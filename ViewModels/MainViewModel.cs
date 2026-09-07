using AgApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _currentPage = "Browse";
    [ObservableProperty] private string _nowPlayingText = "";
    [ObservableProperty] private bool _isNowPlaying;
    [ObservableProperty] private string _pendingBrowseUrl = "";

    public BrowseViewModel Browse    { get; }
    public DownloadsViewModel Downloads { get; }
    public LibraryViewModel Library  { get; }
    public SettingsViewModel Settings { get; }
    public RedistViewModel Redist    { get; }
    public AboutViewModel About      { get; }
    public GameSessionService Session { get; }

    private System.Windows.Threading.DispatcherTimer? _elapsedTimer;

    public MainViewModel(BrowseViewModel browse, DownloadsViewModel downloads,
        LibraryViewModel library, SettingsViewModel settings, RedistViewModel redist,
        AboutViewModel about, GameSessionService session)
    {
        Browse    = browse;
        Downloads = downloads;
        Library   = library;
        Settings  = settings;
        Redist    = redist;
        About     = about;
        Session   = session;

        session.SessionStarted += g =>
        {
            IsNowPlaying = true;
            NowPlayingText = g.Title;
            StartElapsedTimer();
        };
        session.SessionEnded += (g, t) =>
        {
            IsNowPlaying = false;
            NowPlayingText = "";
            _elapsedTimer?.Stop();
        };
    }

    private void StartElapsedTimer()
    {
        _elapsedTimer?.Stop();
        _elapsedTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) =>
        {
            if (Session.CurrentGame != null)
                NowPlayingText = $"{Session.CurrentGame.Title} — {FormatElapsed(Session.CurrentElapsed)}";
        };
        _elapsedTimer.Start();
    }

    private static string FormatElapsed(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:D2}m" : $"{t.Minutes}m {t.Seconds:D2}s";

    [RelayCommand]
    public void Navigate(string page)
    {
        CurrentPage = page;
        if (page == "Library") Library.Refresh();
    }
}
