using System.Text.Json;
using System.Windows.Threading;
using AgApp.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace AgApp.Services;

public class ScraperService
{
    private WebView2? _webView;
    private Dispatcher? _dispatcher;
    private readonly SemaphoreSlim _navLock = new(1, 1);

    private const string BaseUrl = "https://ankergames.net";

    // Uses offsetWidth/offsetHeight instead of getBoundingClientRect so a small WebView2 viewport doesn't filter everything out
    private const string ListingExtractorJs = """
        (function() {
            const candidates = [
                'article', 'li.post', '.post', '.game', '.item', '.entry',
                '[class*="post-item"]', '[class*="game-item"]', '[class*="game_item"]',
                '[class*="entry"]', '[class*="card"]', '.col', '.grid-item'
            ];

            let cards = [];
            let usedSel = '';

            for (const sel of candidates) {
                try {
                    const found = [...document.querySelectorAll(sel)].filter(el =>
                        el.offsetWidth > 0
                        && el.querySelector('img[src]')
                        && el.querySelector('a[href]')
                        && (el.innerText || '').trim().length > 2
                    );
                    if (found.length >= 3) { cards = found; usedSel = sel; break; }
                } catch(e) {}
            }

            // last-resort: any in-DOM element with img + link + text
            if (cards.length === 0) {
                cards = [...document.querySelectorAll('div,li,article')].filter(el =>
                    el.offsetWidth > 0
                    && el.querySelector('img[src]')
                    && el.querySelector('a[href]')
                    && (el.innerText || '').trim().length > 2
                );
            }

            const seen = new Set();
            const results = [];
            for (const card of cards.slice(0, 80)) {
                const a = card.querySelector('a[href]');
                const img = card.querySelector('img[src]');
                const titleEl = card.querySelector('h1,h2,h3,h4,h5,[class*="title"],[class*="name"]') || a;
                const title = (titleEl?.innerText || titleEl?.textContent || '').trim();
                const pageUrl = a?.href || '';
                if (!title || !pageUrl || seen.has(pageUrl) || pageUrl.endsWith('#')) continue;
                seen.add(pageUrl);
                results.push({
                    title,
                    pageUrl,
                    coverUrl: img?.src || img?.dataset?.src || img?.dataset?.lazySrc || '',
                    genre: (card.querySelector('[class*="cat"] a,[class*="genre"],[class*="tag"] a')?.innerText || '').trim(),
                    size: (card.querySelector('[class*="size"],[class*="filesize"]')?.innerText || '').trim(),
                    id: pageUrl.replace(/\W/g,'_').slice(-40),
                    dbg_sel: usedSel
                });
            }
            return JSON.stringify(results);
        })()
        """;

    private const string DetailExtractorJs = """
        (function() {
            const title = (
                document.querySelector('h1,[class*="title"],[class*="entry-title"]')?.innerText
                || document.title
            ).trim();

            const cover = (
                document.querySelector('[class*="featured"] img,[class*="thumbnail"] img,[class*="cover"] img,article img,.post img')
                || document.querySelector('img[src]')
            )?.src || '';

            const desc = (
                document.querySelector('[class*="description"],[class*="entry-content"],[class*="content"]')?.innerText
                || ''
            ).trim().slice(0, 800);

            const genre = (document.querySelector('[class*="cat"] a,[class*="genre"],[class*="tag"] a')?.innerText || '').trim();
            const sizeMatch = document.body.innerText.match(/[\d.,]+\s*(?:GB|MB)/i);
            const size = sizeMatch ? sizeMatch[0] : '';

            const dlLinks = [...document.querySelectorAll('a[href]')].filter(a => {
                const h = a.href.toLowerCase();
                const t = (a.innerText || '').toLowerCase();
                return (h.includes('.zip') || h.includes('.rar') || h.includes('download') ||
                        h.includes('mediafire') || h.includes('drive.google') ||
                        h.includes('1fichier') || h.includes('pixeldrain') ||
                        h.includes('gofile') || h.includes('mega.nz') ||
                        t.includes('download') || t.includes('direct link') ||
                        t.includes('free download'))
                    && !h.startsWith('javascript')
                    && a.href !== location.href
                    && !h.includes('mailto');
            }).map(a => ({ text: (a.innerText || a.href).trim().slice(0, 80), url: a.href }));

            const shots = [...document.querySelectorAll('img[src]')]
                .filter(img => img.naturalWidth > 150 && img.naturalHeight > 100 && img.src !== cover)
                .map(img => img.src).slice(0, 6);

            return JSON.stringify({ title, cover, desc, genre, size, dlLinks, shots });
        })()
        """;

    private const string DiagnosticJs = """
        (function() {
            const title = document.title;
            const url = location.href;
            const bodyText = (document.body?.innerText || '').slice(0, 400);
            const topClasses = [...document.querySelectorAll('body > *, body > * > *')]
                .map(el => (typeof el.className === 'string' ? el.className : '')).filter(Boolean).slice(0, 15);
            const imgCount = document.querySelectorAll('img[src]').length;
            const linkCount = document.querySelectorAll('a[href]').length;
            const articleCount = document.querySelectorAll('article').length;
            const postCount = document.querySelectorAll('.post,.item,.entry,.card,.game').length;
            return JSON.stringify({ title, url, bodyText, topClasses, imgCount, linkCount, articleCount, postCount });
        })()
        """;

    public void Initialize(WebView2 webView)
    {
        _webView = webView;
        _dispatcher = webView.Dispatcher;
    }

    public async Task<List<Game>> FetchListingAsync(int page = 1, string? search = null)
    {
        var url = search is not null
            ? $"{BaseUrl}/?s={Uri.EscapeDataString(search)}"
            : page <= 1 ? BaseUrl : $"{BaseUrl}/page/{page}/";

        var raw = await NavigateAndExtractAsync(url, ListingExtractorJs);
        var json = UnwrapString(raw);
        if (string.IsNullOrWhiteSpace(json) || json == "null") return new();

        try
        {
            return JsonSerializer.Deserialize<List<JsonElement>>(json)?
                .Select(el => new Game
                {
                    Id = Str(el, "id"),
                    Title = Str(el, "title"),
                    CoverUrl = Str(el, "coverUrl"),
                    PageUrl = Str(el, "pageUrl"),
                    Size = Str(el, "size"),
                    Genre = Str(el, "genre"),
                })
                .Where(g => !string.IsNullOrWhiteSpace(g.Title))
                .ToList() ?? new();
        }
        catch { return new(); }
    }

    public async Task<Game?> FetchGameDetailAsync(Game game)
    {
        if (string.IsNullOrWhiteSpace(game.PageUrl)) return null;

        var raw = await NavigateAndExtractAsync(game.PageUrl, DetailExtractorJs);
        var json = UnwrapString(raw);
        if (string.IsNullOrWhiteSpace(json) || json == "null") return game;

        try
        {
            var el = JsonSerializer.Deserialize<JsonElement>(json);
            game.Description = Str(el, "desc");
            if (!string.IsNullOrWhiteSpace(Str(el, "genre"))) game.Genre = Str(el, "genre");
            if (!string.IsNullOrWhiteSpace(Str(el, "size")))  game.Size  = Str(el, "size");
            if (!string.IsNullOrWhiteSpace(Str(el, "cover"))) game.CoverUrl = Str(el, "cover");

            if (el.TryGetProperty("dlLinks", out var links) && links.ValueKind == JsonValueKind.Array)
                game.DownloadLinks = links.EnumerateArray()
                    .Select(l => new DownloadLink { Text = Str(l, "text"), Url = Str(l, "url") })
                    .Where(l => !string.IsNullOrWhiteSpace(l.Url))
                    .ToList();

            if (el.TryGetProperty("shots", out var shots) && shots.ValueKind == JsonValueKind.Array)
                game.Screenshots = shots.EnumerateArray()
                    .Select(s => s.GetString() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
        }
        catch { }

        return game;
    }

    public async Task<string> DiagnoseAsync()
    {
        if (_webView is null) return "WebView2 not initialized.";

        string raw = "", json = "";
        try
        {
            raw = await NavigateAndExtractAsync(BaseUrl, DiagnosticJs);
            json = UnwrapString(raw);

            if (string.IsNullOrWhiteSpace(json))
                return $"Empty response.\nRaw ({raw.Length} chars): {raw[..Math.Min(300, raw.Length)]}";

            var el = JsonSerializer.Deserialize<JsonElement>(json);
            var title    = el.TryGetProperty("title",        out var t)  ? t.GetString()        : "?";
            var url      = el.TryGetProperty("url",          out var u)  ? u.GetString()        : "?";
            var imgs     = el.TryGetProperty("imgCount",     out var ic) ? ic.GetInt32()        : 0;
            var lnks     = el.TryGetProperty("linkCount",    out var lc) ? lc.GetInt32()        : 0;
            var arts     = el.TryGetProperty("articleCount", out var ac) ? ac.GetInt32()        : 0;
            var posts    = el.TryGetProperty("postCount",    out var pc) ? pc.GetInt32()        : 0;
            var body     = el.TryGetProperty("bodyText",     out var bt) ? bt.GetString()       : "";
            var classes  = el.TryGetProperty("topClasses",   out var cls) && cls.ValueKind == JsonValueKind.Array
                ? string.Join(", ", cls.EnumerateArray().Select(c => c.GetString()))
                : "";

            return $"URL: {url}\nTitle: {title}\nImages: {imgs}  Links: {lnks}  <article>: {arts}  .post/.item/.card: {posts}\n\nTop CSS classes:\n{classes}\n\nBody snippet:\n{body}";
        }
        catch (Exception ex)
        {
            var rawSnip  = raw.Length  > 0 ? raw[..Math.Min(300, raw.Length)]   : "(empty)";
            var jsonSnip = json.Length > 0 ? json[..Math.Min(300, json.Length)] : "(empty)";
            return $"Error: {ex.Message}\n\nRaw ({raw.Length} chars):\n{rawSnip}\n\nJSON ({json.Length} chars):\n{jsonSnip}";
        }
    }

    // ExecuteScriptAsync wraps string return values in an extra JSON string layer — unwrap once.
    private static string UnwrapString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        if (raw.StartsWith('"'))
        {
            try { return JsonSerializer.Deserialize<string>(raw) ?? raw; }
            catch { }
        }
        return raw;
    }

    private static string Str(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";

    private async Task<string> NavigateAndExtractAsync(string url, string script)
    {
        if (_webView is null || _dispatcher is null)
            throw new InvalidOperationException("ScraperService not initialized");

        await _navLock.WaitAsync();
        try
        {
            return await _dispatcher.InvokeAsync(async () =>
            {
                var tcs = new TaskCompletionSource<bool>();
                void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    _webView.CoreWebView2.NavigationCompleted -= Handler;
                    tcs.TrySetResult(true);
                }
                _webView.CoreWebView2.NavigationCompleted += Handler;
                _webView.CoreWebView2.Navigate(url);
                await tcs.Task;

                // Poll until Cloudflare challenge clears (up to ~15 s)
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(750);
                    var cf = await _webView.ExecuteScriptAsync(
                        "(document.title.includes('Just a moment')||document.body.innerText.includes('Checking your browser')).toString()");
                    if (cf == "\"false\"") break;
                }

                await Task.Delay(400); // let dynamic content settle

                return await _webView.ExecuteScriptAsync(script);
            }).Task.Unwrap();
        }
        finally
        {
            _navLock.Release();
        }
    }
}
