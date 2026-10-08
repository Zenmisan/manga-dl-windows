using System.IO.Compression;
using MangaDl.Core.Downloads;
using Xunit;

namespace MangaDl.Core.Tests;

public class NovelEpubBuilderTests : IAsyncLifetime
{
    private readonly string _outDir = Path.Combine(Path.GetTempPath(), $"manga_epub_build_{Guid.NewGuid()}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_outDir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_outDir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BuildEpubAsync_PlainText_ProducesValidEpubStructure()
    {
        var path = Path.Combine(_outDir, "chapter.epub");
        await NovelEpubBuilder.BuildEpubAsync(path, "My Novel", "Chapter 1", "First paragraph.\n\nSecond paragraph.", "plain");

        Assert.True(File.Exists(path));

        using var archive = ZipFile.OpenRead(path);

        // EPUB spec: "mimetype" must be the first entry and stored uncompressed.
        var first = archive.Entries[0];
        Assert.Equal("mimetype", first.Name);
        Assert.Equal(first.Length, first.CompressedLength); // STORE, not DEFLATE

        Assert.NotNull(archive.GetEntry("META-INF/container.xml"));
        Assert.NotNull(archive.GetEntry("OEBPS/style.css"));
        Assert.NotNull(archive.GetEntry("OEBPS/nav.xhtml"));
        Assert.NotNull(archive.GetEntry("OEBPS/content.opf"));

        var chapterEntry = archive.GetEntry("OEBPS/chapter.xhtml")!;
        using var reader = new StreamReader(chapterEntry.Open());
        var xhtml = await reader.ReadToEndAsync();
        Assert.Contains("<p>First paragraph.</p>", xhtml);
        Assert.Contains("<p>Second paragraph.</p>", xhtml);
        Assert.Contains("<h1>Chapter 1</h1>", xhtml);

        var opfEntry = archive.GetEntry("OEBPS/content.opf")!;
        using var opfReader = new StreamReader(opfEntry.Open());
        var opf = await opfReader.ReadToEndAsync();
        Assert.Contains("<dc:title>Chapter 1</dc:title>", opf);
        Assert.Contains("<dc:creator>My Novel</dc:creator>", opf); // manga title as creator, matching web
    }

    [Fact]
    public async Task BuildEpubAsync_HtmlContent_StripsTagsIntoParagraphs()
    {
        var path = Path.Combine(_outDir, "chapter-html.epub");
        var html = "<p>Hello &amp; welcome.</p><br/><p>Second line.</p>";
        await NovelEpubBuilder.BuildEpubAsync(path, "My Novel", "Ch. 2", html, "html");

        using var archive = ZipFile.OpenRead(path);
        var chapterEntry = archive.GetEntry("OEBPS/chapter.xhtml")!;
        using var reader = new StreamReader(chapterEntry.Open());
        var xhtml = await reader.ReadToEndAsync();

        Assert.Contains("Hello &amp; welcome.", xhtml);
        Assert.Contains("Second line.", xhtml);
        Assert.DoesNotContain("<br", xhtml.Replace("<br/>", "")); // no raw <br> leaking into output
    }

    [Fact]
    public async Task BuildEpubAsync_EscapesXmlSpecialCharactersInTitles()
    {
        var path = Path.Combine(_outDir, "chapter-escape.epub");
        await NovelEpubBuilder.BuildEpubAsync(path, "Tom & Jerry's \"Adventure\"", "Ch. 1 <Test>", "content", "plain");

        using var archive = ZipFile.OpenRead(path);
        var opfEntry = archive.GetEntry("OEBPS/content.opf")!;
        using var reader = new StreamReader(opfEntry.Open());
        var opf = await reader.ReadToEndAsync();

        Assert.DoesNotContain("Tom & Jerry", opf); // raw & would make this invalid XML
        Assert.Contains("Tom &amp; Jerry", opf);
        Assert.Contains("&lt;Test&gt;", opf);
    }
}
