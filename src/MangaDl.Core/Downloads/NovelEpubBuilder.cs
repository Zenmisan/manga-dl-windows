using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace MangaDl.Core.Downloads;

/// <summary>
/// Ports frontend/src/lib/clientDownloader.ts's buildNovelEpub exactly — same
/// file layout (mimetype, META-INF/container.xml, OEBPS/{style.css,nav.xhtml,
/// chapter.xhtml,content.opf}), one EPUB per chapter, manga title as dc:creator.
/// Web converts arbitrary HTML into XHTML via a real browser DOMParser; without
/// one here, HTML content is normalized into plain paragraphs first (same
/// br/p-tag-aware stripping NovelReaderPage already uses to render the same
/// content on screen) and then wrapped in guaranteed-well-formed &lt;p&gt; tags —
/// less faithful to the source markup, but never produces malformed XHTML.
/// </summary>
public static class NovelEpubBuilder
{
    public static async Task BuildEpubAsync(
        string targetFilePath,
        string mangaTitle,
        string chapterTitle,
        string content,
        string format, // "html" | "plain"
        CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempFile = targetFilePath + ".tmp";
        if (File.Exists(tempFile)) File.Delete(tempFile);

        try
        {
            using (var zipStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // EPUB spec: "mimetype" must be the first entry, uncompressed.
                var mimetypeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (var s = mimetypeEntry.Open())
                {
                    await s.WriteAsync(Encoding.ASCII.GetBytes("application/epub+zip"), ct);
                }

                await WriteTextEntryAsync(zip, "META-INF/container.xml", ContainerXml, ct);
                await WriteTextEntryAsync(zip, "OEBPS/style.css", StyleCss, ct);
                await WriteTextEntryAsync(zip, "OEBPS/nav.xhtml", BuildNavXhtml(chapterTitle), ct);
                await WriteTextEntryAsync(zip, "OEBPS/chapter.xhtml", BuildChapterXhtml(chapterTitle, content, format), ct);

                var bookId = $"urn:uuid:{Guid.NewGuid()}";
                var nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                await WriteTextEntryAsync(zip, "OEBPS/content.opf", BuildContentOpf(bookId, chapterTitle, mangaTitle, nowIso), ct);
            }

            if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
            File.Move(tempFile, targetFilePath);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private static async Task WriteTextEntryAsync(ZipArchive zip, string name, string content, CancellationToken ct)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        await stream.WriteAsync(Encoding.UTF8.GetBytes(content), ct);
    }

    private static string Esc(string s) => SecurityElement.Escape(s) ?? s;

    private const string ContainerXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles>
            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;

    private const string StyleCss = """
        body {
          font-family: Georgia, serif;
          line-height: 1.7;
          margin: 5%;
          color: #1a1a1a;
          background-color: #fff;
        }
        h1 {
          text-align: center;
          font-size: 1.6em;
          margin-top: 1em;
          margin-bottom: 1.5em;
          color: #111;
        }
        p {
          margin-bottom: 1em;
          text-indent: 1.5em;
        }
        """;

    private static string BuildNavXhtml(string chapterTitle) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
          <head>
            <title>{Esc(chapterTitle)}</title>
            <link rel="stylesheet" type="text/css" href="style.css" />
          </head>
          <body>
            <nav epub:type="toc" id="toc">
              <h1>Table of Contents</h1>
              <ol>
                <li><a href="chapter.xhtml">{Esc(chapterTitle)}</a></li>
              </ol>
            </nav>
          </body>
        </html>
        """;

    private static string BuildContentOpf(string bookId, string chapterTitle, string mangaTitle, string nowIso) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <package xmlns="http://www.idpf.org/2007/opf" unique-identifier="BookId" version="3.0">
          <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
            <dc:identifier id="BookId">{bookId}</dc:identifier>
            <dc:title>{Esc(chapterTitle)}</dc:title>
            <dc:language>en</dc:language>
            <dc:creator>{Esc(mangaTitle)}</dc:creator>
            <meta property="dcterms:modified">{nowIso}</meta>
          </metadata>
          <manifest>
            <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
            <item id="style" href="style.css" media-type="text/css"/>
            <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml"/>
          </manifest>
          <spine>
            <itemref idref="chapter"/>
          </spine>
        </package>
        """;

    private static string BuildChapterXhtml(string chapterTitle, string content, string format)
    {
        var plain = format == "html" ? HtmlToPlainParagraphs(content) : content;
        var paragraphs = plain
            .Split(["\r\n\r\n", "\n\n", "\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => $"    <p>{Esc(p)}</p>")
            .ToList();

        var body = paragraphs.Count > 0 ? string.Join("\n", paragraphs) : "    <p></p>";

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml">
              <head>
                <title>{Esc(chapterTitle)}</title>
                <link rel="stylesheet" type="text/css" href="style.css" />
              </head>
              <body>
                <h1>{Esc(chapterTitle)}</h1>
            {body}
              </body>
            </html>
            """;
    }

    /// <summary>Same br/p-tag-aware stripping NovelReaderPage.LoadChapterTextAsync already
    /// uses to render extension-provided HTML on screen — reused here so the downloaded
    /// EPUB's paragraph breaks match what the in-app reader actually showed.</summary>
    private static string HtmlToPlainParagraphs(string html)
    {
        var text = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</p>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }
}
