using System.IO;
using System.Windows;
using System.Windows.Controls;
using AgApp.Models;
using AgApp.Services;
using AgApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace AgApp.Views;

public partial class BrowsePage : UserControl
{
    private BrowseViewModel? _vm;
    private DownloadManager? _downloader;

    public BrowsePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as BrowseViewModel;
        _downloader = App.Services.GetRequiredService<DownloadManager>();
        var adBlock = App.Services.GetRequiredService<AdBlockService>();

        await Browser.EnsureCoreWebView2Async();
        Browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x0a, 0x0d, 0x14);
        var core = Browser.CoreWebView2;

        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;

        adBlock.Attach(core);

        core.NavigationStarting += (s, ev) =>
        {
            if (_vm is null) return;
            _vm.IsLoading = true;
            // Do NOT cancel here — download links redirect through CDN hosts that
            // don't look like file URLs. We need the server response to arrive so
            // that WebView2 can raise DownloadStarting. Domain enforcement happens
            // in NavigationCompleted instead.
        };

        core.NavigationCompleted += (s, ev) =>
        {
            if (_vm is null) return;
            _vm.IsLoading = false;
            _vm.CanGoBack = core.CanGoBack;
            _vm.CanGoForward = core.CanGoForward;

            // For file downloads, WebView2 raises DownloadStarting and then
            // NavigationCompleted with IsSuccess=false — the page never changes.
            // For real page navigations to external sites, IsSuccess=true and
            // core.Source will be the external URL; go back in that case.
            if (ev.IsSuccess &&
                Uri.TryCreate(core.Source, UriKind.Absolute, out var current))
            {
                var host = current.Host.ToLowerInvariant();
                if (host != "ankergames.net" && !host.EndsWith(".ankergames.net"))
                    core.GoBack();
            }
        };

        // Block popup windows from opening external pages; allow same-domain popups.
        core.NewWindowRequested += (s, ev) =>
        {
            ev.Handled = true;
            if (Uri.TryCreate(ev.Uri, UriKind.Absolute, out var uri))
            {
                var host = uri.Host.ToLowerInvariant();
                if (host == "ankergames.net" || host.EndsWith(".ankergames.net"))
                    core.Navigate(ev.Uri);
            }
        };

        // Intercept file downloads → route to DownloadManager instead of browser dialog.
        core.DownloadStarting += OnDownloadStarting;

        core.Navigate("https://ankergames.net");
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        e.Cancel = true;

        var uri = e.DownloadOperation.Uri;
        var raw = Path.GetFileNameWithoutExtension(
            e.DownloadOperation.ResultFilePath.Length > 0
                ? e.DownloadOperation.ResultFilePath
                : uri);

        var suggestedName = CleanGameTitle(raw);

        var job = new DownloadJob
        {
            GameTitle = suggestedName,
            GameId = uri.GetHashCode().ToString("x"),
            Url = uri,
            PageUrl = Browser.CoreWebView2?.Source ?? ""
        };

        _ = _downloader?.StartDownloadAsync(job);

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowBalloonTip("Download started", $"{suggestedName} — check the Downloads tab.");
        });
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoBack == true)
            Browser.CoreWebView2.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2?.CanGoForward == true)
            Browser.CoreWebView2.GoForward();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) =>
        Browser.CoreWebView2?.Reload();

    private static string CleanGameTitle(string raw)
    {
        string[] suffixes = ["-ankergames", "_ankergames", " ankergames",
                             "-anker-games", " - ankergames", "(ankergames)"];
        var lower = raw.ToLowerInvariant();
        foreach (var s in suffixes)
        {
            if (lower.EndsWith(s, StringComparison.OrdinalIgnoreCase))
            {
                raw = raw[..^s.Length].TrimEnd('-', '_', ' ');
                break;
            }
        }
        raw = raw.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ');
        while (raw.Contains("  ")) raw = raw.Replace("  ", " ");
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(raw.Trim().ToLowerInvariant());
    }
}
