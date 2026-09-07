using System.Collections.ObjectModel;
using System.Windows;
using AgApp.Models;
using AgApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgApp.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly DownloadManager _downloader;

    public ObservableCollection<DownloadJob> Jobs => _downloader.Jobs;

    public DownloadsViewModel(DownloadManager downloader)
    {
        _downloader = downloader;
        _downloader.JobUpdated += OnJobUpdated;
        _downloader.ExtractionCompleted += OnExtractionCompleted;
    }

    private void OnJobUpdated(DownloadJob job)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (!Jobs.Contains(job))
                Jobs.Insert(0, job);
        });
    }

    private void OnExtractionCompleted(DownloadJob job, string folder)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowBalloonTip($"{job.GameTitle} installed!", "Click Library to play.");
        });
    }

    [RelayCommand]
    public void PauseResume(DownloadJob job)
    {
        if (job.Status == DownloadStatus.Downloading)
        {
            job.CancellationSource.Cancel();
            job.Status = DownloadStatus.Paused;
        }
        else if (job.Status == DownloadStatus.Paused)
        {
            var newJob = new DownloadJob
            {
                GameTitle = job.GameTitle,
                GameId = job.GameId,
                CoverUrl = job.CoverUrl,
                Url = job.Url
            };
            Jobs.Remove(job);
            Jobs.Insert(0, newJob);
            _ = _downloader.StartDownloadAsync(newJob);
        }
    }

    [RelayCommand]
    public void CancelJob(DownloadJob job)
    {
        job.CancellationSource.Cancel();
        job.Status = DownloadStatus.Cancelled;
        Jobs.Remove(job);
    }

    [RelayCommand]
    public void ClearCompleted()
    {
        var done = Jobs.Where(j => j.Status is DownloadStatus.Completed or DownloadStatus.Cancelled
                                                                          or DownloadStatus.Failed).ToList();
        foreach (var j in done) Jobs.Remove(j);
    }

    [RelayCommand]
    public void MoveUp(DownloadJob job) => _downloader.MoveUp(job);

    [RelayCommand]
    public void MoveDown(DownloadJob job) => _downloader.MoveDown(job);
}
