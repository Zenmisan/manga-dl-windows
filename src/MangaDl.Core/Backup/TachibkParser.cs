using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MangaDl.Core.Backup;

/// <summary>
/// Ports backend/app/core/tachibk.py line-for-line: a hand-rolled protobuf
/// wire-format decoder (no protobuf library — Tachiyomi/Mihon's backup schema
/// is small and stable enough that this is simpler than compiling a .proto),
/// plus the provider/id-resolution heuristics used to map a Tachiyomi source
/// onto one of manga-dl's own extension providers.
/// </summary>
public static class TachibkParser
{
    // ── protobuf wire-format decoder ────────────────────────────────────────

    private static (ulong Value, int Pos) ReadVarint(byte[] data, int pos)
    {
        ulong result = 0;
        var shift = 0;
        while (true)
        {
            // A corrupted/truncated backup could end mid-varint (trailing byte still
            // has the continuation bit set) — fail with a clear, expected exception
            // type instead of running off the end of the array or shifting past 64 bits.
            if (pos >= data.Length || shift >= 64)
            {
                throw new InvalidDataException("Truncated or corrupt varint in backup file");
            }
            var b = data[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return (result, pos);
            shift += 7;
        }
    }

    /// <summary>Decodes a raw protobuf message into {field_number: [values...]}.
    /// Values are boxed as ulong (varint/fixed64), uint (fixed32), or byte[]
    /// (length-delimited) depending on wire type — matches Python's dynamic dict.</summary>
    private static Dictionary<int, List<object>> DecodeMessage(byte[] data)
    {
        var fields = new Dictionary<int, List<object>>();
        var pos = 0;
        while (pos < data.Length)
        {
            var (tag, p1) = ReadVarint(data, pos);
            pos = p1;
            var fieldNum = (int)(tag >> 3);
            var wireType = (int)(tag & 0x07);

            object val;
            switch (wireType)
            {
                case 0: // varint
                    var (v, p2) = ReadVarint(data, pos);
                    pos = p2;
                    val = v;
                    break;
                case 1: // 64-bit
                    val = BitConverter.ToUInt64(data, pos);
                    pos += 8;
                    break;
                case 2: // length-delimited
                    var (len, p3) = ReadVarint(data, pos);
                    pos = p3;
                    val = data.AsSpan(pos, (int)len).ToArray();
                    pos += (int)len;
                    break;
                case 5: // 32-bit
                    val = BitConverter.ToUInt32(data, pos);
                    pos += 4;
                    break;
                default:
                    return fields;
            }

            if (!fields.TryGetValue(fieldNum, out var list))
            {
                list = new List<object>();
                fields[fieldNum] = list;
            }
            list.Add(val);
        }
        return fields;
    }

    private static string Str(Dictionary<int, List<object>> f, int num, string def = "")
    {
        if (!f.TryGetValue(num, out var vals) || vals.Count == 0) return def;
        return vals[0] switch
        {
            byte[] b => Encoding.UTF8.GetString(b),
            var other => other.ToString() ?? def,
        };
    }

    private static bool Bool(Dictionary<int, List<object>> f, int num, bool def = false)
    {
        if (!f.TryGetValue(num, out var vals) || vals.Count == 0) return def;
        return Convert.ToUInt64(vals[0]) != 0;
    }

    private static long Int(Dictionary<int, List<object>> f, int num, long def = 0)
    {
        if (!f.TryGetValue(num, out var vals) || vals.Count == 0) return def;
        return vals[0] is byte[] ? def : Convert.ToInt64(vals[0]);
    }

    /// <summary>Tachiyomi's "float" proto fields decode as a raw 32-bit pattern
    /// (varint or fixed32 depending on how small the value is) that needs
    /// reinterpreting as IEEE754, not converting numerically — ported as-is
    /// from the Python decoder's struct.pack/unpack round-trip.</summary>
    private static double FloatField(Dictionary<int, List<object>> f, int num, double def = 0.0)
    {
        if (!f.TryGetValue(num, out var vals) || vals.Count == 0) return def;
        if (vals[0] is byte[]) return def;
        var bits = unchecked((uint)Convert.ToUInt64(vals[0]));
        return BitConverter.Int32BitsToSingle(unchecked((int)bits));
    }

    private static List<byte[]> Msgs(Dictionary<int, List<object>> f, int num)
    {
        if (!f.TryGetValue(num, out var vals)) return [];
        return vals.OfType<byte[]>().ToList();
    }

    // ── model parsers ────────────────────────────────────────────────────────

    private static BackupChapter ParseChapter(byte[] raw)
    {
        var f = DecodeMessage(raw);
        var scanlator = Str(f, 3);
        return new BackupChapter(
            Str(f, 1), Str(f, 2),
            string.IsNullOrEmpty(scanlator) ? null : scanlator,
            Bool(f, 4), Bool(f, 5), (int)Int(f, 6), FloatField(f, 9), (int)Int(f, 10));
    }

    private static BackupHistoryEntry ParseHistory(byte[] raw)
    {
        var f = DecodeMessage(raw);
        return new BackupHistoryEntry(Str(f, 1), Int(f, 2));
    }

    private static BackupTracking ParseTracking(byte[] raw)
    {
        var f = DecodeMessage(raw);
        var mediaId = Int(f, 100);
        if (mediaId == 0) mediaId = Int(f, 3);
        return new BackupTracking((int)Int(f, 1), mediaId, Str(f, 5), FloatField(f, 6), FloatField(f, 8), (int)Int(f, 9), Str(f, 4));
    }

    private static BackupCategoryEntry ParseCategory(byte[] raw)
    {
        var f = DecodeMessage(raw);
        return new BackupCategoryEntry(Str(f, 1), (int)Int(f, 2));
    }

    private static BackupSourceEntry ParseSource(byte[] raw)
    {
        var f = DecodeMessage(raw);
        return new BackupSourceEntry(Str(f, 1), Int(f, 2));
    }

    private static (BackupManga Manga, List<int> CategoryIds) ParseMangaRaw(byte[] raw)
    {
        var f = DecodeMessage(raw);
        var artist = Str(f, 4);
        var author = Str(f, 5);
        var description = Str(f, 6);
        var thumbnail = Str(f, 9);

        var catIds = f.TryGetValue(17, out var catVals)
            ? catVals.Select(v => (int)Convert.ToInt64(v)).ToList()
            : [];

        var manga = new BackupManga(
            Int(f, 1), Str(f, 2), Str(f, 3),
            string.IsNullOrEmpty(artist) ? null : artist,
            string.IsNullOrEmpty(author) ? null : author,
            string.IsNullOrEmpty(description) ? null : description,
            string.IsNullOrEmpty(thumbnail) ? null : thumbnail,
            Bool(f, 100, true),
            Msgs(f, 16).Select(ParseChapter).ToList(),
            Msgs(f, 18).Select(ParseTracking).ToList(),
            Msgs(f, 104).Select(ParseHistory).ToList(),
            [], "");

        return (manga, catIds);
    }

    /// <summary>Decodes a .tachibk file (gzip-compressed protobuf).</summary>
    public static TachiyomiBackupResult DecodeTachibk(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0x1f && data[1] == 0x8b)
        {
            data = Gunzip(data);
        }

        var root = DecodeMessage(data);
        var categories = Msgs(root, 2).Select(ParseCategory).ToList();
        var sources = Msgs(root, 101).Select(ParseSource).ToList();

        var catMap = new Dictionary<int, string>();
        for (var i = 0; i < categories.Count; i++) catMap[i + 1] = categories[i].Name;

        var sourceMap = new Dictionary<long, string>();
        foreach (var s in sources)
        {
            if (s.SourceId != 0) sourceMap[s.SourceId] = s.Name;
        }

        var mangaList = new List<BackupManga>();
        foreach (var rawManga in Msgs(root, 1))
        {
            var (manga, catIds) = ParseMangaRaw(rawManga);
            var catNames = catIds.Select(cid => catMap.GetValueOrDefault(cid, "")).ToList();
            var sourceName = sourceMap.GetValueOrDefault(manga.Source, "");
            mangaList.Add(manga with { CategoryNames = catNames, SourceName = sourceName });
        }

        return new TachiyomiBackupResult(mangaList, categories, sources);
    }

    private static byte[] Gunzip(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    // ── provider / id resolution ─────────────────────────────────────────────

    public static readonly Dictionary<int, string> TrackerSyncIdMap = new()
    {
        [1] = "mal",
        [2] = "anilist",
        [3] = "kitsu",
        [4] = "shikimori",
        [5] = "bangumi",
        [6] = "mangaupdates",
    };

    private static readonly (string Keyword, string Provider)[] ProviderKeywords =
    [
        ("mangadex", "mangadex"), ("asura", "asurascans"), ("omega", "omegascans"), ("flame", "flamescans"),
        ("katana", "mangakatana"), ("kakalot", "mangakakalot"), ("manganato", "manganato"), ("nato", "manganato"),
        ("batoto", "bato"), ("bato", "bato"), ("pill", "mangapill"), ("tcb", "tcbscans"), ("mangahere", "mangahere"),
        ("webtoon", "webtoons"), ("mangaplus", "mangaplus"), ("komga", "komga"), ("suwayomi", "suwayomi"),
        ("royalroad", "royalroad"), ("scribblehub", "scribblehub"), ("novelbin", "novelbin"), ("novelfull", "novelfull"),
        ("wuxiaworld", "wuxiaworld"), ("lightnovelworld", "lightnovelworld"),
    ];

    public static string ResolveProvider(string? sourceName, string? mangaUrl = "")
    {
        var s = (sourceName ?? "").ToLowerInvariant().Trim();
        var u = (mangaUrl ?? "").ToLowerInvariant().Trim();
        var target = $"{s} {u}";
        foreach (var (keyword, provider) in ProviderKeywords)
        {
            if (target.Contains(keyword)) return provider;
        }
        var slug = Regex.Replace(s, "[^a-z0-9]", "");
        return slug.Length > 0 ? slug : "tachiyomi";
    }

    public static string CleanMangaId(string? url, string provider = "")
    {
        if (string.IsNullOrEmpty(url)) return "unknown";
        var u = url.Trim();
        var schemeIdx = u.IndexOf("://", StringComparison.Ordinal);
        if (schemeIdx >= 0)
        {
            u = u[(schemeIdx + 3)..];
            var slashIdx = u.IndexOf('/');
            if (slashIdx >= 0) u = "/" + u[(slashIdx + 1)..];
        }
        u = u.Split('?')[0].Split('#')[0].Trim('/');

        if (provider == "mangadex")
        {
            var m = Regex.Match(u, "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase);
            if (m.Success) return m.Value.ToLowerInvariant();
        }

        foreach (var prefix in new[] { "manga/", "series/", "title/", "comic/" })
        {
            if (u.StartsWith(prefix, StringComparison.Ordinal))
            {
                u = u[prefix.Length..];
                break;
            }
        }

        u = u.Trim('/');
        return u.Length > 0 ? u : "unknown";
    }

    public static string CleanChapterId(string? url, double chapterNumber = 0.0)
    {
        if (string.IsNullOrEmpty(url)) return chapterNumber != 0 ? chapterNumber.ToString() : "ch-1";
        var u = url.Trim().Split('?')[0].Split('#')[0].Trim('/');
        if (u.Contains('/'))
        {
            var last = u[(u.LastIndexOf('/') + 1)..];
            if (last.Length > 0) return last;
        }
        return u.Length > 0 ? u : (chapterNumber != 0 ? chapterNumber.ToString() : "ch-1");
    }

    // ── public entrypoint — handles both .tachibk and legacy .json backups ──

    public static TachiyomiBackupResult ParseTachiyomiBackup(byte[] data, string filename = "")
    {
        var isJson = filename.ToLowerInvariant().EndsWith(".json") ||
                     (!(data.Length >= 2 && data[0] == 0x1f && data[1] == 0x8b) && LooksLikeJson(data));

        if (isJson)
        {
            try
            {
                return ParseJsonBackup(data);
            }
            catch
            {
                // Fall through to the protobuf decoder, matching the Python fallback.
            }
        }

        return DecodeTachibk(data);
    }

    private static bool LooksLikeJson(byte[] data)
    {
        foreach (var b in data)
        {
            if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\n' || b == (byte)'\r') continue;
            return b == (byte)'{';
        }
        return false;
    }

    private static TachiyomiBackupResult ParseJsonBackup(byte[] data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        var catsRaw = FirstArray(root, "backupCategories", "categories");
        var categories = new List<BackupCategoryEntry>();
        var i = 0;
        foreach (var c in catsRaw)
        {
            var name = c.ValueKind == JsonValueKind.Object && c.TryGetProperty("name", out var n) ? n.GetString() ?? "" : c.ToString();
            var order = c.ValueKind == JsonValueKind.Object && c.TryGetProperty("order", out var o) ? o.GetInt32() : i;
            categories.Add(new BackupCategoryEntry(name, order));
            i++;
        }

        var sourcesRaw = FirstArray(root, "backupSources", "sources");
        var sources = new List<BackupSourceEntry>();
        foreach (var s in sourcesRaw)
        {
            if (s.ValueKind == JsonValueKind.Object)
            {
                var name = s.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var sourceId = s.TryGetProperty("sourceId", out var sid) ? sid.GetInt64()
                    : s.TryGetProperty("source_id", out var sid2) ? sid2.GetInt64() : 0;
                sources.Add(new BackupSourceEntry(name, sourceId));
            }
            else
            {
                sources.Add(new BackupSourceEntry(s.ToString(), 0));
            }
        }

        var sourceMap = sources.Where(s => s.SourceId != 0).ToDictionary(s => s.SourceId, s => s.Name);
        var catMap = new Dictionary<int, string>();
        for (var ci = 0; ci < categories.Count; ci++) catMap[ci + 1] = categories[ci].Name;

        var mangaList = new List<BackupManga>();
        foreach (var m in FirstArray(root, "backupManga", "manga", "library"))
        {
            var catIds = m.TryGetProperty("categories", out var catsEl) ? catsEl
                : m.TryGetProperty("category_ids", out var catIds2) ? catIds2
                : default;
            var catNames = new List<string>();
            if (catIds.ValueKind == JsonValueKind.Array)
            {
                foreach (var cid in catIds.EnumerateArray())
                {
                    if (cid.ValueKind == JsonValueKind.String) catNames.Add(cid.GetString() ?? "");
                    else if (cid.ValueKind == JsonValueKind.Number && catMap.TryGetValue(cid.GetInt32(), out var cn)) catNames.Add(cn);
                }
            }

            var chapters = new List<BackupChapter>();
            if (m.TryGetProperty("chapters", out var chEl) && chEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var ch in chEl.EnumerateArray())
                {
                    chapters.Add(new BackupChapter(
                        GetStr(ch, "url"), GetStr(ch, "name"), GetStrOrNull(ch, "scanlator"),
                        GetBool(ch, "read"), GetBool(ch, "bookmark"),
                        GetInt(ch, "lastPageRead", GetInt(ch, "last_page_read", 0)),
                        GetDouble(ch, "chapterNumber", GetDouble(ch, "chapter_number", 0)),
                        GetInt(ch, "sourceOrder", GetInt(ch, "source_order", 0))));
                }
            }

            var tracking = new List<BackupTracking>();
            if (m.TryGetProperty("tracking", out var trEl) && trEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var tr in trEl.EnumerateArray())
                {
                    tracking.Add(new BackupTracking(
                        GetInt(tr, "syncId", GetInt(tr, "sync_id", 0)),
                        GetInt(tr, "mediaId", GetInt(tr, "media_id", 0)),
                        GetStr(tr, "title"),
                        GetDouble(tr, "lastChapterRead", GetDouble(tr, "last_chapter_read", 0)),
                        GetDouble(tr, "score", 0),
                        GetInt(tr, "status", 0),
                        GetStr(tr, "trackingUrl", GetStr(tr, "tracking_url", ""))));
                }
            }

            var sourceVal = m.TryGetProperty("source", out var srcEl) ? srcEl : default;
            var sourceNum = sourceVal.ValueKind == JsonValueKind.Number ? sourceVal.GetInt64() : 0;
            var sourceName = sourceMap.GetValueOrDefault(sourceNum, "");

            mangaList.Add(new BackupManga(
                sourceNum, GetStr(m, "url"), GetStr(m, "title"),
                GetStrOrNull(m, "artist"), GetStrOrNull(m, "author"), GetStrOrNull(m, "description"),
                GetStrOrNull(m, "thumbnailUrl") ?? GetStrOrNull(m, "thumbnail_url"),
                GetBool(m, "favorite", true),
                chapters, tracking, [], catNames, sourceName));
        }

        return new TachiyomiBackupResult(mangaList, categories, sources);
    }

    private static List<JsonElement> FirstArray(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Array)
                return el.EnumerateArray().ToList();
        }
        return [];
    }

    private static string GetStr(JsonElement el, string key, string def = "") =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;

    private static string? GetStrOrNull(JsonElement el, string key) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(JsonElement el, string key, bool def = false) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : def;

    private static int GetInt(JsonElement el, string key, int def = 0) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;

    private static double GetDouble(JsonElement el, string key, double def = 0) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;
}
