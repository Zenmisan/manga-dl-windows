using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MangaDl.Core.Local;

public sealed record EpubChapter(string Title, string Html);

public sealed record EpubBook(string Title, List<EpubChapter> Chapters);

/// <summary>
/// Minimal EPUB reader: unzip, follow META-INF/container.xml to the OPF file,
/// read the manifest (id -> href) and spine (reading order), extract each
/// spine item's XHTML in order. No external EPUB library — the container/OPF
/// format is simple, stable XML and this is the only thing we need from it.
/// </summary>
public static class EpubParser
{
    private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";

    public static EpubBook Parse(byte[] epubBytes)
    {
        using var zipStream = new MemoryStream(epubBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var containerEntry = archive.GetEntry("META-INF/container.xml")
            ?? throw new InvalidDataException("Not a valid EPUB — missing META-INF/container.xml");

        var containerDoc = XDocument.Load(containerEntry.Open());
        var containerNs = containerDoc.Root!.Name.Namespace;
        var rootfilePath = containerDoc.Descendants(containerNs + "rootfile").First().Attribute("full-path")!.Value;

        var opfEntry = archive.GetEntry(rootfilePath)
            ?? throw new InvalidDataException("EPUB's OPF file referenced by container.xml was not found");

        var opfDoc = XDocument.Load(opfEntry.Open());
        var opfNs = opfDoc.Root!.Name.Namespace;

        var title = opfDoc.Descendants(DcNs + "title").FirstOrDefault()?.Value?.Trim();

        var opfDir = rootfilePath.Contains('/') ? rootfilePath[..rootfilePath.LastIndexOf('/')] : "";

        var manifest = opfDoc.Descendants(opfNs + "item")
            .Where(item => item.Attribute("id") != null && item.Attribute("href") != null)
            .ToDictionary(item => item.Attribute("id")!.Value, item => item.Attribute("href")!.Value);

        var spineIds = opfDoc.Descendants(opfNs + "itemref")
            .Select(x => x.Attribute("idref")?.Value)
            .Where(id => id != null)
            .Select(id => id!)
            .ToList();

        var chapters = new List<EpubChapter>();
        foreach (var id in spineIds)
        {
            if (!manifest.TryGetValue(id, out var href)) continue;

            var path = Uri.UnescapeDataString(string.IsNullOrEmpty(opfDir) ? href : $"{opfDir}/{href}");
            // Some EPUBs write manifest hrefs with "../" segments relative to the OPF's
            // own directory (e.g. opfDir "OEBPS", href "../Text/ch1.xhtml") — ZIP entries
            // are literal path keys, so ".." must be resolved before GetEntry, not left in.
            var entry = archive.GetEntry(NormalizeZipPath(path)) ?? archive.GetEntry(path);
            if (entry is null) continue;

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var html = reader.ReadToEnd();
            chapters.Add(new EpubChapter(ExtractChapterTitle(html) ?? $"Chapter {chapters.Count + 1}", html));
        }

        return new EpubBook(string.IsNullOrWhiteSpace(title) ? "Untitled" : title, chapters);
    }

    private static string NormalizeZipPath(string path)
    {
        var stack = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == ".") continue;
            if (segment == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(segment);
        }
        return string.Join('/', stack);
    }

    private static string? ExtractChapterTitle(string html)
    {
        var titleMatch = Regex.Match(html, "<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleMatch.Success)
        {
            var decoded = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
            if (decoded.Length > 0) return decoded;
        }

        var headingMatch = Regex.Match(html, "<h[12][^>]*>(.*?)</h[12]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (headingMatch.Success)
        {
            var decoded = System.Net.WebUtility.HtmlDecode(Regex.Replace(headingMatch.Groups[1].Value, "<[^>]+>", "").Trim());
            if (decoded.Length > 0) return decoded;
        }

        return null;
    }
}
