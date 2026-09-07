using Microsoft.Web.WebView2.Core;

namespace AgApp.Services;

public class AdBlockService
{
    private static readonly string[] BlockPatterns =
    [
        "*googlesyndication.com*", "*doubleclick.net*", "*googleadservices.com*",
        "*adnxs.com*", "*rubiconproject.com*", "*openx.net*", "*criteo.com*",
        "*outbrain.com*", "*taboola.com*", "*adroll.com*", "*amazon-adsystem.com*",
        "*scorecardresearch.com*", "*quantserve.com*", "*exoclick.com*",
        "*trafficjunky.net*", "*popads.net*", "*popcash.net*", "*ero-advertising.com*",
        "*ads.pubmatic.com*", "*advertising.com*", "*revcontent.com*"
    ];

    private const string HideCss = @"
        ins.adsbygoogle, [id*='google_ad'], [id*='div-gpt-ad'],
        [class*='adsbygoogle'], [class*='ad-container'], [class*='ads-wrapper'],
        [class*='advertisement'], [class*='banner-ad'], [id*='banner-ad'],
        iframe[src*='googlesyndication'], iframe[src*='doubleclick'],
        iframe[src*='exoclick'], iframe[src*='trafficjunky'],
        .widget_ads, #sidebar-ads, .wp-ads, [data-ad='true'],
        .popup-overlay, .popup-backdrop, [class*='popup-ad']
        { display:none!important; visibility:hidden!important; }
    ";

    public void Attach(CoreWebView2 core)
    {
        foreach (var p in BlockPatterns)
            core.AddWebResourceRequestedFilter(p, CoreWebView2WebResourceContext.All);

        core.WebResourceRequested += OnResourceRequested;

        // Block popup windows (ad pop-unders, etc.)
        core.NewWindowRequested += (s, e) => e.Handled = true;

        // Inject CSS + popup blocker before any page script runs
        _ = core.AddScriptToExecuteOnDocumentCreatedAsync($$"""
            (function() {
                const s = document.createElement('style');
                s.textContent = `{{HideCss}}`;
                (document.head || document.documentElement).appendChild(s);
                const _open = window.open.bind(window);
                window.open = (url, target) => (target === '_blank' || !target) ? null : _open(url, target);
            })();
            """);
    }

    private static void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        // Return a 204 No Content to silently drop the blocked request
        if (sender is CoreWebView2 core)
            e.Response = core.Environment.CreateWebResourceResponse(null, 204, "Blocked", "");
    }
}
