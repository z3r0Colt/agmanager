using System.Windows;
using AgApp.Models;
using AgApp.Services;
using AgApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AgApp;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var collection = new ServiceCollection();

        collection.AddSingleton<SettingsService>();
        collection.AddSingleton<AdBlockService>();
        collection.AddSingleton<ExtractionService>();
        collection.AddSingleton<LibraryService>();
        collection.AddSingleton<MetadataService>();
        collection.AddSingleton<DownloadManager>();
        collection.AddSingleton<GameSessionService>();
        collection.AddSingleton<SaveBackupService>();

        collection.AddSingleton<BrowseViewModel>();
        collection.AddSingleton<DownloadsViewModel>();
        collection.AddSingleton<LibraryViewModel>();
        collection.AddSingleton<SettingsViewModel>();
        collection.AddSingleton<RedistViewModel>();
        collection.AddSingleton<MainViewModel>();

        collection.AddSingleton<MainWindow>();

        Services = collection.BuildServiceProvider();

        var settings = Services.GetRequiredService<SettingsService>();
        settings.Load();
        settings.EnsureDownloadFolderExists();

        Services.GetRequiredService<LibraryService>().Load();

        // Wire session backup
        var session = Services.GetRequiredService<GameSessionService>();
        var backup  = Services.GetRequiredService<SaveBackupService>();
        var appSettings = Services.GetRequiredService<SettingsService>();
        session.SessionEnded += async (game, _) =>
        {
            var sched = string.IsNullOrEmpty(game.BackupSchedule)
                ? appSettings.Current.DefaultBackupSchedule : game.BackupSchedule;
            if (sched == "OnExit")
                await backup.BackupAsync(game);
        };

        Services.GetRequiredService<MainWindow>().Show();
    }
}
