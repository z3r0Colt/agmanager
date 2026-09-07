using CommunityToolkit.Mvvm.ComponentModel;

namespace AgApp.ViewModels;

public partial class BrowseViewModel : ObservableObject
{
    [ObservableProperty] private string _addressBarText = "https://ankergames.net";
    [ObservableProperty] private string _pageTitle = "Browse";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _canGoBack;
    [ObservableProperty] private bool _canGoForward;
}
