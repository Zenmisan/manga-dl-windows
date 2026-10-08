using System.IO.Compression;
using System.Text;
using MangaDl.Core.Database;
using MangaDl.Core.Local;
using Xunit;

namespace MangaDl.Core.Tests;

public class EpubImportTests : IAsyncLifetime
{
    private readonly string _tempDbPath = Path.Combine(Path.GetTempPath(), $"manga_epub_test_{Guid.NewGuid()}.db");
    private readonly string _libraryRoot = Path.Combine(Path.GetTempPath(), $"manga_epub_lib_{Guid.NewGuid()}");
    private readonly string _sourceDir = Path.Combine(Path.GetTempPath(), $"manga_epub_src_{Guid.NewGuid()}");
    private MangaDatabase _db = null!;
    private LocalImportService _importer = null!;

    public async Task InitializeAsync()
    {
        _db = new MangaDatabase(_tempDbPath);
        await _db.InitializeAsync();
        _importer = new LocalImportService(_db, _libraryRoot);
        Directory.CreateDirectory(_sourceDir);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        foreach (var path in new[] { _tempDbPath })
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
        foreach (var dir in new[] { _libraryRoot, _sourceDir })
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    /// <summary>Builds a minimal but structurally real EPUB: container.xml -> OPF
    /// (with a dc:title, a 2-item manifest, a 2-item spine) -> two XHTML chapters.</summary>
    private static byte[] BuildFakeEpub()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void WriteEntry(string path, string content)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }

            WriteEntry("mimetype", "application/epub+zip");

            WriteEntry("META-INF/container.xml", """
                <?xml version="1.0"?>
                <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """);

            WriteEntry("OEBPS/content.opf", """
                <?xml version="1.0"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="2.0">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:title>My Fake Novel</dc:title>
                  </metadata>
                  <manifest>
                    <item id="ch1" href="Text/chapter1.xhtml" media-type="application/xhtml+xml"/>
                    <item id="ch2" href="Text/chapter2.xhtml" media-type="application/xhtml+xml"/>
                  </manifest>
                  <spine>
                    <itemref idref="ch1"/>
                    <itemref idref="ch2"/>
                  </spine>
                </package>
                """);

            WriteEntry("OEBPS/Text/chapter1.xhtml", """
                <html><head><title>Chapter One: Beginnings</title></head>
                <body><p>It was a dark and stormy night.</p><p>The end of chapter one.</p></body></html>
                """);

            WriteEntry("OEBPS/Text/chapter2.xhtml", """
                <html><head><title>Chapter Two: Endings</title></head>
                <body><p>Everything changed after that night.</p></body></html>
                """);
        }
        return ms.ToArray();
    }

    /// <summary>Same as BuildFakeEpub but the manifest href escapes one level up with
    /// "../" from the OPF's own directory — a real-world EPUB authoring pattern
    /// (OPF in OEBPS/, text files one level up) that a literal string-join won't
    /// resolve against ZIP's literal path keys without normalizing ".." first.</summary>
    private static byte[] BuildFakeEpubWithRelativeHrefs()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void WriteEntry(string path, string content)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }

            WriteEntry("META-INF/container.xml", """
                <?xml version="1.0"?>
                <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """);

            WriteEntry("OEBPS/content.opf", """
                <?xml version="1.0"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="2.0">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:title>Relative Href Novel</dc:title>
                  </metadata>
                  <manifest>
                    <item id="ch1" href="../Text/chapter1.xhtml" media-type="application/xhtml+xml"/>
                  </manifest>
                  <spine>
                    <itemref idref="ch1"/>
                  </spine>
                </package>
                """);

            // Note: lives at "Text/chapter1.xhtml" from the zip root, NOT under OEBPS/.
            WriteEntry("Text/chapter1.xhtml", """
                <html><head><title>Escaped Chapter</title></head>
                <body><p>Reached via a relative ../ href.</p></body></html>
                """);
        }
        return ms.ToArray();
    }

    [Fact]
    public void EpubParser_ResolvesRelativeDotDotHrefsAgainstOpfDirectory()
    {
        var book = EpubParser.Parse(BuildFakeEpubWithRelativeHrefs());

        Assert.Equal("Relative Href Novel", book.Title);
        var chapter = Assert.Single(book.Chapters);
        Assert.Equal("Escaped Chapter", chapter.Title);
        Assert.Contains("Reached via a relative", chapter.Html);
    }

    [Fact]
    public void EpubParser_ExtractsTitleAndChaptersInSpineOrder()
    {
        var book = EpubParser.Parse(BuildFakeEpub());

        Assert.Equal("My Fake Novel", book.Title);
        Assert.Equal(2, book.Chapters.Count);
        Assert.Equal("Chapter One: Beginnings", book.Chapters[0].Title);
        Assert.Contains("dark and stormy night", book.Chapters[0].Html);
        Assert.Equal("Chapter Two: Endings", book.Chapters[1].Title);
    }

    [Fact]
    public async Task ImportStandaloneFileAsync_Epub_RegistersNovelWithChaptersOnDisk()
    {
        var epubPath = Path.Combine(_sourceDir, "My Fake Novel.epub");
        await File.WriteAllBytesAsync(epubPath, BuildFakeEpub());

        var result = (await _importer.ImportStandaloneFileAsync(epubPath)).Single();

        Assert.Equal(LocalImportStatus.Done, result.Status);

        var library = await _db.GetLibraryAsync();
        var entry = Assert.Single(library);
        Assert.Equal(LocalImportService.Provider, entry.Provider);
        Assert.Equal("novel", entry.Type);
        Assert.Equal("My Fake Novel", entry.Title);

        var downloads = await _db.GetDownloadsAsync();
        Assert.Equal(2, downloads.Count);
        var first = downloads.OrderBy(d => d.ChapterNumber).First();
        Assert.Equal("Chapter One: Beginnings", first.ChapterTitle);
        Assert.True(File.Exists(first.CbzPath));
        var savedHtml = await File.ReadAllTextAsync(first.CbzPath!);
        Assert.Contains("dark and stormy night", savedHtml);
    }

    [Fact]
    public async Task ImportStandaloneFileAsync_EmptyEpub_ReturnsError()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Not even a valid container.xml.
            archive.CreateEntry("mimetype");
        }

        var badPath = Path.Combine(_sourceDir, "broken.epub");
        await File.WriteAllBytesAsync(badPath, ms.ToArray());

        var result = (await _importer.ImportStandaloneFileAsync(badPath)).Single();

        Assert.Equal(LocalImportStatus.Error, result.Status);
        Assert.Empty(await _db.GetLibraryAsync());
    }
}
