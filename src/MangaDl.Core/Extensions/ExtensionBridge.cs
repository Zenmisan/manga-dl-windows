using System.Text.Json;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using MangaDl.Core.Http;

namespace MangaDl.Core.Extensions;

public sealed class ExtensionBridge : IDisposable
{
    private readonly HttpService _http;
    private readonly Engine _engine;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _loaded;

    public bool IsLoaded => _loaded;

    public ExtensionBridge(HttpService http)
    {
        _http = http;
        _engine = new Engine(options =>
        {
            options.LimitMemory(64 * 1024 * 1024);
            options.TimeoutInterval(TimeSpan.FromSeconds(30));
        });

        SetupEnvironment();
    }

    private void SetupEnvironment()
    {
        // 1. Console
        _engine.SetValue("__log", new Action<string>(msg => System.Diagnostics.Debug.WriteLine($"[JS] {msg}")));
        _engine.Execute(@"
            globalThis.console = {
                log: function() { var args = Array.prototype.slice.call(arguments); __log(args.join(' ')); },
                warn: function() { var args = Array.prototype.slice.call(arguments); __log('[WARN] ' + args.join(' ')); },
                error: function() { var args = Array.prototype.slice.call(arguments); __log('[ERR] ' + args.join(' ')); }
            };
        ");

        // 2. Base64
        _engine.SetValue("btoa", new Func<string, string>(s => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s ?? ""))));
        _engine.SetValue("atob", new Func<string, string>(s => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s ?? ""))));

        // 3. URI encoding
        _engine.SetValue("encodeURIComponent", new Func<string, string>(Uri.EscapeDataString));
        _engine.SetValue("decodeURIComponent", new Func<string, string>(Uri.UnescapeDataString));

        // 4. DOMParser backed by AngleSharp
        _engine.SetValue("__domParse", new Func<string, string?, DomDocument>(DomShim.ParseFromString));
        _engine.Execute(@"
            globalThis.DOMParser = function() {};
            globalThis.DOMParser.prototype.parseFromString = function(html, mime) {
                return __domParse(html || '', mime || 'text/html');
            };
        ");

        // 5. apiFetch native bridge
        _engine.SetValue("__nativeFetch", new Func<string, JsValue, object>(NativeFetchHandler));
        _engine.Execute(@"
            globalThis.apiFetch = function(url, options) {
                return Promise.resolve(__nativeFetch(url, options || {}));
            };
        ");
    }

    private object NativeFetchHandler(string url, JsValue options)
    {
        string method = "GET";
        Dictionary<string, string>? headers = null;
        string? body = null;

        if (options is ObjectInstance obj)
        {
            var m = obj.Get("method");
            if (!m.IsUndefined() && !m.IsNull()) method = m.AsString();

            var h = obj.Get("headers");
            if (h is ObjectInstance hObj)
            {
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, desc) in hObj.GetOwnProperties())
                {
                    var val = desc.Value;
                    if (!val.IsUndefined() && !val.IsNull())
                    {
                        headers[key.ToString()] = val.AsString();
                    }
                }
            }

            var b = obj.Get("body");
            if (!b.IsUndefined() && !b.IsNull()) body = b.AsString();
        }

        var result = _http.FetchAsync(url, method, headers, body).GetAwaiter().GetResult();

        var dict = new Dictionary<string, object?>
        {
            ["status"] = result.StatusCode,
            ["text"] = result.Text,
            ["html"] = result.Text,
            ["url"] = result.Url
        };

        // Try parsing JSON so both res.json and direct property access work
        try
        {
            var trimmed = result.Text.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                using var doc = JsonDocument.Parse(trimmed);
                var jsonParsed = JsonElementToClr(doc.RootElement);
                dict["json"] = jsonParsed;

                if (jsonParsed is Dictionary<string, object?> jsonDict)
                {
                    foreach (var (k, v) in jsonDict)
                    {
                        if (!dict.ContainsKey(k))
                        {
                            dict[k] = v;
                        }
                    }
                }
            }
        }
        catch
        {
            dict["json"] = null;
        }

        return dict;
    }

    private static object? JsonElementToClr(JsonElement elem) => elem.ValueKind switch
    {
        JsonValueKind.Object => elem.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToClr(p.Value)),
        JsonValueKind.Array => elem.EnumerateArray().Select(JsonElementToClr).ToList(),
        JsonValueKind.String => elem.GetString(),
        JsonValueKind.Number => elem.TryGetInt64(out var l) ? (object)l : elem.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    public async Task LoadScriptAsync(string jsSource)
    {
        await _lock.WaitAsync();
        try
        {
            _engine.Execute(jsSource);
            _loaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<MangaSearchResult>> SearchAsync(string query, int page = 1)
    {
        await _lock.WaitAsync();
        try
        {
            _engine.SetValue("__q", query);
            _engine.SetValue("__page", page);
            _engine.Execute(@"
                var __searchResult = null;
                (async () => {
                    try {
                        if (typeof extension !== 'undefined' && typeof extension.search === 'function') {
                            __searchResult = await extension.search(__q, __page);
                        }
                    } catch (e) {
                        __log('Search error: ' + e);
                        __searchResult = [];
                    }
                })();
            ");

            return ParseSearchResults(_engine.GetValue("__searchResult"));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<MangaDetailResult?> GetMangaDetailAsync(string mangaId)
    {
        await _lock.WaitAsync();
        try
        {
            _engine.SetValue("__id", mangaId);
            _engine.Execute(@"
                var __detailResult = null;
                (async () => {
                    try {
                        if (typeof extension !== 'undefined' && typeof extension.getMangaDetail === 'function') {
                            __detailResult = await extension.getMangaDetail(__id);
                        }
                    } catch (e) {
                        __log('getMangaDetail error: ' + e);
                        __detailResult = null;
                    }
                })();
            ");

            return ParseMangaDetail(_engine.GetValue("__detailResult"), mangaId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetPagesAsync(string chapterId)
    {
        await _lock.WaitAsync();
        try
        {
            _engine.SetValue("__chId", chapterId);
            _engine.Execute(@"
                var __pagesResult = null;
                (async () => {
                    try {
                        if (typeof extension !== 'undefined' && typeof extension.getPages === 'function') {
                            __pagesResult = await extension.getPages(__chId);
                        }
                    } catch (e) {
                        __log('getPages error: ' + e);
                        __pagesResult = [];
                    }
                })();
            ");

            var val = _engine.GetValue("__pagesResult");
            var list = new List<string>();
            if (val is ObjectInstance arr && arr.IsArray())
            {
                var len = (int)arr.Get("length").AsNumber();
                for (var i = 0; i < len; i++)
                {
                    var item = arr.Get(i.ToString());
                    if (!item.IsUndefined() && !item.IsNull())
                    {
                        list.Add(item.AsString());
                    }
                }
            }
            return list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ChapterTextResult?> GetChapterTextAsync(string chapterId)
    {
        await _lock.WaitAsync();
        try
        {
            _engine.SetValue("__chId", chapterId);
            _engine.Execute(@"
                var __textResult = null;
                (async () => {
                    try {
                        if (typeof extension !== 'undefined' && typeof extension.getChapterText === 'function') {
                            __textResult = await extension.getChapterText(__chId);
                        }
                    } catch (e) {
                        __log('getChapterText error: ' + e);
                        __textResult = null;
                    }
                })();
            ");

            var val = _engine.GetValue("__textResult");
            if (val is ObjectInstance obj)
            {
                var content = obj.Get("content");
                var format = obj.Get("format");
                return new ChapterTextResult(
                    content.IsUndefined() || content.IsNull() ? string.Empty : content.AsString(),
                    format.IsUndefined() || format.IsNull() ? "plain" : format.AsString());
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<MangaSearchResult>> GetPopularAsync(int page = 1)
    {
        await _lock.WaitAsync();
        try
        {
            _engine.SetValue("__page", page);
            _engine.Execute(@"
                var __popResult = null;
                (async () => {
                    try {
                        if (typeof extension !== 'undefined' && typeof extension.getPopular === 'function') {
                            __popResult = await extension.getPopular(__page);
                        } else if (typeof extension !== 'undefined' && typeof extension.search === 'function') {
                            __popResult = await extension.search('', __page);
                        }
                    } catch (e) {
                        __log('getPopular error: ' + e);
                        __popResult = [];
                    }
                })();
            ");

            return ParseSearchResults(_engine.GetValue("__popResult"));
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string SafeString(JsValue val, string fallback = "")
    {
        if (val.IsUndefined() || val.IsNull()) return fallback;
        return val.IsString() ? val.AsString() : val.ToString();
    }

    private static IReadOnlyList<MangaSearchResult> ParseSearchResults(JsValue val)
    {
        var list = new List<MangaSearchResult>();
        if (val is ObjectInstance arr && arr.IsArray())
        {
            var len = (int)arr.Get("length").AsNumber();
            for (var i = 0; i < len; i++)
            {
                var item = arr.Get(i.ToString());
                if (item is ObjectInstance obj)
                {
                    var id = SafeString(obj.Get("id"));
                    var title = SafeString(obj.Get("title"), "Untitled");
                    var cover = obj.Get("cover_url");
                    var provider = SafeString(obj.Get("provider"), "unknown");
                    var url = obj.Get("url");
                    var status = obj.Get("status");

                    list.Add(new MangaSearchResult(
                        id,
                        title,
                        cover.IsUndefined() || cover.IsNull() ? null : SafeString(cover),
                        provider,
                        url.IsUndefined() || url.IsNull() ? null : SafeString(url),
                        status.IsUndefined() || status.IsNull() ? null : SafeString(status)));
                }
            }
        }
        return list;
    }

    private static MangaDetailResult? ParseMangaDetail(JsValue val, string fallbackId)
    {
        if (val is not ObjectInstance obj) return null;

        var idVal = obj.Get("id");
        var id = SafeString(idVal, fallbackId);
        var title = SafeString(obj.Get("title"), "Untitled");
        var desc = obj.Get("description");
        var cover = obj.Get("cover_url");
        var author = obj.Get("author");
        var artist = obj.Get("artist");
        var status = obj.Get("status");

        var genres = new List<string>();
        var gVal = obj.Get("genres");
        if (gVal is ObjectInstance gArr && gArr.IsArray())
        {
            var gLen = (int)gArr.Get("length").AsNumber();
            for (var i = 0; i < gLen; i++)
            {
                var g = gArr.Get(i.ToString());
                if (!g.IsUndefined() && !g.IsNull()) genres.Add(SafeString(g));
            }
        }

        var chapters = new List<ChapterItem>();
        var chVal = obj.Get("chapters");
        if (chVal is ObjectInstance chArr && chArr.IsArray())
        {
            var chLen = (int)chArr.Get("length").AsNumber();
            for (var i = 0; i < chLen; i++)
            {
                if (chArr.Get(i.ToString()) is ObjectInstance cObj)
                {
                    var cId = SafeString(cObj.Get("id"), $"{i + 1}");
                    var cNum = SafeString(cObj.Get("number"), $"{i + 1}");
                    var cTitle = SafeString(cObj.Get("title"), $"Chapter {cNum}");
                    var cDate = SafeString(cObj.Get("date"), string.Empty);
                    var cScan = cObj.Get("scanlator");
                    var cUrl = cObj.Get("url");

                    chapters.Add(new ChapterItem(
                        cId,
                        cNum,
                        cTitle,
                        cDate,
                        cScan.IsUndefined() || cScan.IsNull() ? null : SafeString(cScan),
                        cUrl.IsUndefined() || cUrl.IsNull() ? null : SafeString(cUrl)));
                }
            }
        }

        return new MangaDetailResult(
            id,
            title,
            desc.IsUndefined() || desc.IsNull() ? null : SafeString(desc),
            cover.IsUndefined() || cover.IsNull() ? null : SafeString(cover),
            author.IsUndefined() || author.IsNull() ? null : SafeString(author),
            artist.IsUndefined() || artist.IsNull() ? null : SafeString(artist),
            status.IsUndefined() || status.IsNull() ? null : SafeString(status),
            genres,
            chapters);
    }

    public void Dispose()
    {
        _lock.Dispose();
        _engine.Dispose();
    }
}
