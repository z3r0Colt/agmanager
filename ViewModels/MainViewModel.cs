using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _currentPage = "Browse";

    public BrowseViewModel Browse    { get; }
    public DownloadsViewModel Downloads { get; }
    public LibraryViewModel Library  { get; }
    public SettingsViewModel Settings { get; }
    public RedistViewModel Redist    { get; }

    public MainViewModel(BrowseViewModel browse, DownloadsViewModel downloads,
        LibraryViewModel library, SettingsViewModel settings, RedistViewModel redist)
    {
        Browse    = browse;
        Downloads = downloads;
        Library   = library;
        Settings  = settings;
        Redist    = redist;
    }

    [RelayCommand]
    public void Navigate(string page)
    {
        CurrentPage = page;
        if (page == "Library") Library.Refresh();
    }
}
