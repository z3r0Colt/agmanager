using CommunityToolkit.Mvvm.ComponentModel;

namespace AgApp.Models;

public enum DownloadStatus { Queued, Downloading, Paused, Extracting, Completed, Failed, Cancelled }

public partial class DownloadJob : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString();

    [ObservableProperty] private string _gameTitle = "";
    [ObservableProperty] private string _gameId = "";
    [ObservableProperty] private string _coverUrl = "";
    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string _destinationPath = "";
    [ObservableProperty] private long _totalBytes;
    [ObservableProperty] private long _downloadedBytes;
    [ObservableProperty] private double _speedBytesPerSec;
    [ObservableProperty] private DownloadStatus _status = DownloadStatus.Queued;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private TimeSpan _eta;
    [ObservableProperty] private int _retryCount;
    [ObservableProperty] private int _queuePosition;
    [ObservableProperty] private string _pageUrl = "";

    public double Progress => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;

    public CancellationTokenSource CancellationSource { get; } = new();

    partial void OnDownloadedBytesChanged(long value) => OnPropertyChanged(nameof(Progress));
    partial void OnTotalBytesChanged(long value) => OnPropertyChanged(nameof(Progress));
}
