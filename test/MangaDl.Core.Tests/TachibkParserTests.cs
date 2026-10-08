using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MangaDl.Core.Backup;
using Xunit;

namespace MangaDl.Core.Tests;

/// <summary>
/// TachibkParser has no protobuf library backing it — it's a hand-rolled wire-format
/// decoder ported from backend/app/core/tachibk.py. These tests hand-encode a tiny
/// fake .tachibk file (using the same minimal encoder helpers below) so the decoder
/// is verified against real bytes, not just "it compiles".
/// </summary>
public class TachibkParserTests
{
    // ── minimal protobuf encoder, test-only, mirrors the wire format the parser reads ──

    private static byte[] WriteVarint(ulong value)
    {
        var bytes = new List<byte>();
        do
        {
            var b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) b |= 0x80;
            bytes.Add(b);
        } while (value != 0);
        return bytes.ToArray();
    }

    private static byte[] Tag(int field, int wireType) => WriteVarint((ulong)((field << 3) | wireType));

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    private static byte[] WriteString(int field, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Concat(Tag(field, 2), WriteVarint((ulong)bytes.Length), bytes);
    }

    private static byte[] WriteVarintField(int field, long value) => Concat(Tag(field, 0), WriteVarint((ulong)value));

    private static byte[] WriteBool(int field, bool value) => WriteVarintField(field, value ? 1 : 0);

    private static byte[] WriteFixed32Float(int field, float value)
    {
        var bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
        return Concat(Tag(field, 5), BitConverter.GetBytes(bits));
    }

    private static byte[] WriteMessage(int field, byte[] inner) => Concat(Tag(field, 2), WriteVarint((ulong)inner.Length), inner);

    private static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(data);
        }
        return output.ToArray();
    }

    [Fact]
    public void DecodeTachibk_RoundTripsAFakeBackup()
    {
        // One chapter: url, name, read=true, chapter_number=12.5 (fixed32 float)
        var chapter = Concat(
            WriteString(1, "https://mangadex.org/chapter/abc-123"),
            WriteString(2, "Chapter 12"),
            WriteBool(4, true),
            WriteFixed32Float(9, 12.5f));

        // One manga: source=42, url (contains a mangadex-style UUID), title, thumbnail,
        // one chapter (field 16), one category id (field 17) = 1
        var manga = Concat(
            WriteVarintField(1, 42),
            WriteString(2, "https://mangadex.org/title/550e8400-e29b-41d4-a716-446655440000"),
            WriteString(3, "Solo Leveling"),
            WriteString(9, "https://example.com/cover.jpg"),
            WriteMessage(16, chapter),
            WriteVarintField(17, 1));

        var category = Concat(WriteString(1, "Favorites"), WriteVarintField(2, 0));
        var source = Concat(WriteString(1, "MangaDex"), WriteVarintField(2, 42));

        var root = Concat(
            WriteMessage(1, manga),
            WriteMessage(2, category),
            WriteMessage(101, source));

        var tachibk = Gzip(root);

        var result = TachibkParser.DecodeTachibk(tachibk);

        var m = Assert.Single(result.Manga);
        Assert.Equal("Solo Leveling", m.Title);
        Assert.Equal("MangaDex", m.SourceName);
        Assert.Equal(["Favorites"], m.CategoryNames);

        var ch = Assert.Single(m.Chapters);
        Assert.Equal("Chapter 12", ch.Name);
        Assert.True(ch.Read);
        Assert.Equal(12.5, ch.ChapterNumber, precision: 3); // verifies the fixed32 bit-reinterpret, not a numeric cast

        Assert.Single(result.Categories);
        Assert.Single(result.Sources);
    }

    [Fact]
    public void DecodeTachibk_UngzippedInputAlsoWorks()
    {
        var manga = Concat(WriteString(2, "https://example.com/x"), WriteString(3, "No Gzip Here"));
        var root = WriteMessage(1, manga);

        var result = TachibkParser.DecodeTachibk(root); // not gzip-prefixed

        var m = Assert.Single(result.Manga);
        Assert.Equal("No Gzip Here", m.Title);
    }

    [Theory]
    [InlineData("MangaDex", "", "mangadex")]
    [InlineData("NovelBin.me", "", "novelbin")]
    [InlineData("Some Random Source", "https://unknownsite.com/x", "somerandomsource")]
    [InlineData("", "", "tachiyomi")]
    public void ResolveProvider_MatchesKeywordsOrFallsBack(string sourceName, string url, string expected)
    {
        Assert.Equal(expected, TachibkParser.ResolveProvider(sourceName, url));
    }

    [Fact]
    public void CleanMangaId_ExtractsMangadexUuid()
    {
        var id = TachibkParser.CleanMangaId("https://mangadex.org/title/550e8400-e29b-41d4-a716-446655440000/some-slug", "mangadex");
        Assert.Equal("550e8400-e29b-41d4-a716-446655440000", id);
    }

    [Fact]
    public void CleanMangaId_StripsKnownPrefixForNonMangadexProviders()
    {
        var id = TachibkParser.CleanMangaId("https://example.com/manga/my-series-slug", "example");
        Assert.Equal("my-series-slug", id);
    }

    [Fact]
    public void CleanChapterId_TakesLastUrlSegment()
    {
        Assert.Equal("chapter-12", TachibkParser.CleanChapterId("https://example.com/manga/x/chapter-12", 12));
        Assert.Equal("5", TachibkParser.CleanChapterId("", 5));
        Assert.Equal("ch-1", TachibkParser.CleanChapterId("", 0));
    }

    [Fact]
    public void ParseTachiyomiBackup_LegacyJsonFormatParsesCorrectly()
    {
        var json = JsonSerializer.Serialize(new
        {
            backupManga = new[]
            {
                new
                {
                    source = 7,
                    url = "https://example.com/manga/legacy-series",
                    title = "Legacy Series",
                    favorite = true,
                    categories = new[] { 1 },
                    chapters = new[]
                    {
                        new { url = "https://example.com/manga/legacy-series/ch-1", name = "Chapter 1", read = true, lastPageRead = 3, chapterNumber = 1.0 }
                    }
                }
            },
            backupCategories = new[] { new { name = "Reading", order = 0 } },
            backupSources = new[] { new { name = "ExampleSource", sourceId = 7 } },
        });

        var result = TachibkParser.ParseTachiyomiBackup(Encoding.UTF8.GetBytes(json), "backup.json");

        var m = Assert.Single(result.Manga);
        Assert.Equal("Legacy Series", m.Title);
        Assert.Equal("ExampleSource", m.SourceName);
        Assert.Equal(["Reading"], m.CategoryNames);
        var ch = Assert.Single(m.Chapters);
        Assert.True(ch.Read);
        Assert.Equal(3, ch.LastPageRead);
    }
}
