using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MangaDl.Core.Http;

namespace MangaDl.Core.Extensions;

public sealed class ExtensionManager : IDisposable
{
    private static readonly HashSet<string> NovelSourceIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "royalroad", "novelbin", "novelfull", "freewebnovel", "novelfire", "allnovel",
        "novelphoenix", "readnovelfull", "libread", "brightnovel", "chrysanthemumgarden",
        "comrademao", "lightnoveltranslations", "bestlightnovel", "asianovel", "novelbuddy",
        "readlightnovel", "scribblehub", "lightnovelworld", "wuxiaworld", "ranobes",
        "novelsonline", "readhive"
    };

    private static readonly Dictionary<string, (string Name, string Color)> SourceBrandColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mangadex"] = ("MangaDex", "#FF6740"),
        ["asurascans"] = ("Asura Scans", "#9333EA"),
        ["mangakatana"] = ("MangaKatana", "#EF4444"),
        ["bato"] = ("Bato.to", "#10B981"),
        ["mangapill"] = ("MangaPill", "#3B82F6"),
        ["mangaplus"] = ("MangaPlus", "#E11D48"),
        ["webtoons"] = ("Webtoons", "#00DC64"),
        ["comixto"] = ("Comix.to", "#8B5CF6"),
        ["flamecomics"] = ("Flame Comics", "#F59E0B"),
        ["royalroad"] = ("Royal Road", "#F97316"),
        ["novelbin"] = ("NovelBin", "#06B6D4"),
        ["novelfull"] = ("NovelFull", "#6366F1"),
        ["ranobes"] = ("Ranobes", "#EC4899"),
        ["wuxiaworld"] = ("WuxiaWorld", "#3B82F6"),
        ["scribblehub"] = ("Scribble Hub", "#14B8A6")
    };

    private readonly HttpService _http;
    private readonly string _extensionsDirectory;
    private readonly ConcurrentDictionary<string, ExtensionBridge> _bridges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExtensionMeta> _registry = new(StringComparer.OrdinalIgnoreCase);

    public ExtensionManager(HttpService http, string? extensionsDirectory = null)
    {
        _http = http;
        _extensionsDirectory = extensionsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets", "extensions");
        ScanExtensions();
    }

    public void ScanExtensions()
    {
        _registry.Clear();
        if (!Directory.Exists(_extensionsDirectory)) return;

        foreach (var file in Directory.GetFiles(_extensionsDirectory, "*.js"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (id.EndsWith(".template", StringComparison.OrdinalIgnoreCase)) continue;

            var isNovel = NovelSourceIds.Contains(id);
            var (name, color) = SourceBrandColors.TryGetValue(id, out var brand)
                ? brand
                : (FormatSourceName(id), "#4B5563");

            var initial = name.Length > 0 ? name[..1].ToUpperInvariant() : "M";

            _registry[id] = new ExtensionMeta(
                id,
                name,
                "EN",
                "1.0.0",
                isNovel ? "novel" : "manga",
                initial,
                color,
                file);
        }
    }

    private static string FormatSourceName(string id)
    {
        return char.ToUpperInvariant(id[0]) + id[1..];
    }

    public IReadOnlyList<ExtensionMeta> GetExtensions() => _registry.Values.ToList();

    public ExtensionMeta? GetExtension(string id) => _registry.TryGetValue(id, out var meta) ? meta : null;

    public ExtensionMeta? FindExtension(string id) => GetExtension(id);

    public bool IsNovel(string id) => NovelSourceIds.Contains(id);

    public async Task<ExtensionBridge> GetBridgeAsync(string sourceId)
    {
        if (_bridges.TryGetValue(sourceId, out var existing))
        {
            return existing;
        }

        if (!_registry.TryGetValue(sourceId, out var meta))
        {
            throw new KeyNotFoundException($"Extension '{sourceId}' not found in registry.");
        }

        var jsSource = await File.ReadAllTextAsync(meta.ScriptPath);
        var bridge = new ExtensionBridge(_http);
        await bridge.LoadScriptAsync(jsSource);

        _bridges[sourceId] = bridge;
        return bridge;
    }

    public async Task<IReadOnlyList<MangaSearchResult>> SearchAsync(string sourceId, string query, int page = 1)
    {
        var bridge = await GetBridgeAsync(sourceId);
        return await bridge.SearchAsync(query, page);
    }

    public async Task<IReadOnlyList<SearchGroup>> GlobalSearchAsync(string query, int maxSources = 6)
    {
        var sourcesToSearch = _registry.Values.Take(maxSources).ToList();
        var tasks = sourcesToSearch.Select(async meta =>
        {
            try
            {
                var bridge = await GetBridgeAsync(meta.Id);
                var results = await bridge.SearchAsync(query, 1);
                var mappedResults = results.Select(r => new Manga(
                    r.Id,
                    r.Title,
                    r.CoverUrl ?? meta.Color,
                    0,
                    false,
                    false,
                    meta.Name)).ToList();

                return new SearchGroup(meta.Name, $"{meta.Language} · {results.Count} results", mappedResults);
            }
            catch
            {
                return new SearchGroup(meta.Name, $"{meta.Language} · 0 results", Array.Empty<Manga>());
            }
        });

        var groups = await Task.WhenAll(tasks);
        return groups.Where(g => g.Results.Count > 0).ToList();
    }

    public async Task<MangaDetailResult?> GetMangaDetailAsync(string sourceId, string mangaId)
    {
        var bridge = await GetBridgeAsync(sourceId);
        return await bridge.GetMangaDetailAsync(mangaId);
    }

    public async Task<IReadOnlyList<string>> GetPagesAsync(string sourceId, string chapterId)
    {
        var bridge = await GetBridgeAsync(sourceId);
        return await bridge.GetPagesAsync(chapterId);
    }

    public async Task<ChapterTextResult?> GetChapterTextAsync(string sourceId, string chapterId)
    {
        var bridge = await GetBridgeAsync(sourceId);
        return await bridge.GetChapterTextAsync(chapterId);
    }

    public async Task<IReadOnlyList<MangaSearchResult>> GetPopularAsync(string sourceId, int page = 1)
    {
        var bridge = await GetBridgeAsync(sourceId);
        return await bridge.GetPopularAsync(page);
    }

    public void Dispose()
    {
        foreach (var bridge in _bridges.Values)
        {
            bridge.Dispose();
        }
        _bridges.Clear();
    }
}
