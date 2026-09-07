using System.Windows;
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

        Services.GetRequiredService<MainWindow>().Show();
    }
}
