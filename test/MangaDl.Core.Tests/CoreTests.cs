using System.IO.Compression;
using System.Text;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Downloads;
using MangaDl.Core.Extensions;
using MangaDl.Core.Http;
using Xunit;

namespace MangaDl.Core.Tests;

public class CoreTests : IAsyncLifetime
{
    private readonly string _tempDbPath;
    private MangaDatabase _db = null!;

    public CoreTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"manga_test_{Guid.NewGuid()}.db");
    }

    public async Task InitializeAsync()
    {
        _db = new MangaDatabase(_tempDbPath);
        await _db.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public async Task TestLibraryOperations()
    {
        var item = new LibraryEntity
        {
            Provider = "mangadex",
            MangaId = "test-manga-1",
            Title = "Solo Leveling",
            CoverUrl = "https://example.com/cover.jpg",
            Type = "manga"
        };

        await _db.AddToLibraryAsync(item);
        var inLib = await _db.IsInLibraryAsync("mangadex", "test-manga-1");
        Assert.True(inLib);

        var retrieved = await _db.GetLibraryItemAsync("mangadex", "test-manga-1");
        Assert.NotNull(retrieved);
        Assert.Equal("Solo Leveling", retrieved.Title);

        var list = await _db.GetLibraryAsync();
        Assert.Single(list);

        await _db.RemoveFromLibraryAsync("mangadex", "test-manga-1");
        inLib = await _db.IsInLibraryAsync("mangadex", "test-manga-1");
        Assert.False(inLib);
    }

    [Fact]
    public async Task TestReadingProgressAndHistory()
    {
        await _db.SaveProgressAsync("asurascans", "ashen-blade", "ch-10", 10.0, 15, false);

        var prog = await _db.GetProgressAsync("asurascans", "ashen-blade", "ch-10");
        Assert.NotNull(prog);
        Assert.Equal(15, prog.Page);
        Assert.Equal(0, prog.Completed);

        await _db.RecordHistoryAsync("asurascans", "ashen-blade", "Ashen Blade", "ch-10", "Chapter 10", "https://cover");
        var history = await _db.GetHistoryAsync(10);
        Assert.Single(history);
        Assert.Equal("Ashen Blade", history[0].MangaTitle);
    }

    [Fact]
    public async Task TestCategories()
    {
        var cats = await _db.GetCategoriesAsync();
        Assert.NotEmpty(cats); // Default categories initialized

        await _db.AddCategoryAsync("custom", "Favorites", 10);
        cats = await _db.GetCategoriesAsync();
        Assert.Contains(cats, c => c.Id == "custom" && c.Name == "Favorites");

        await _db.SetMangaCategoriesAsync("mangadex", "m1", new[] { "custom", "reading" });
        var assigned = await _db.GetMangaCategoriesAsync("mangadex", "m1");
        Assert.Equal(2, assigned.Count);
        Assert.Contains("custom", assigned);
        Assert.Contains("reading", assigned);
    }

    [Fact]
    public void TestHttpProxyUrlResolution()
    {
        using var http = new HttpService();
        var rawUrl = "/manga/proxy/html?url=https%3A%2F%2Fapi.mangadex.org%2Fmanga%3Ftitle%3Dnaruto";
        var resolved = http.ResolveUrl(rawUrl);
        Assert.Equal("https://api.mangadex.org/manga?title=naruto", resolved);

        var directUrl = "https://example.com/api/test";
        Assert.Equal(directUrl, http.ResolveUrl(directUrl));
    }

    [Fact]
    public async Task TestCbzCreationAndComicInfoXml()
    {
        var tempCbz = Path.Combine(Path.GetTempPath(), $"test_chapter_{Guid.NewGuid()}.cbz");

        try
        {
            var fakePage1 = Encoding.UTF8.GetBytes("fake-jpeg-image-1");
            var fakePage2 = Encoding.UTF8.GetBytes("fake-jpeg-image-2");

            await CbzBuilder.BuildCbzAsync(
                tempCbz,
                "Hollow Crown",
                "Chapter 48",
                "48",
                new[] { fakePage1, fakePage2 },
                new[] { ".jpg", ".jpg" });

            Assert.True(File.Exists(tempCbz));

            // Verify zip contents
            using (var zip = ZipFile.OpenRead(tempCbz))
            {
                var entry001 = zip.GetEntry("001.jpg");
                Assert.NotNull(entry001);

                var entry002 = zip.GetEntry("002.jpg");
                Assert.NotNull(entry002);

                var entryXml = zip.GetEntry("ComicInfo.xml");
                Assert.NotNull(entryXml);

                using var stream = entryXml.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var xml = await reader.ReadToEndAsync();
                Assert.Contains("<Series>Hollow Crown</Series>", xml);
                Assert.Contains("<Title>Chapter 48</Title>", xml);
                Assert.Contains("<PageCount>2</PageCount>", xml);
            }
        }
        finally
        {
            if (File.Exists(tempCbz))
            {
                try { File.Delete(tempCbz); } catch { }
            }
        }
    }

    [Fact]
    public async Task TestExtensionBridgeContract()
    {
        using var http = new HttpService();
        using var bridge = new ExtensionBridge(http);

        var script = @"
            var extension = {
                async search(query, page) {
                    return [
                        { id: 'm1', title: 'Result for ' + query, cover_url: 'https://c.jpg', provider: 'test', url: 'https://u.com' }
                    ];
                },
                async getMangaDetail(id) {
                    return {
                        id: id,
                        title: 'Title ' + id,
                        description: 'Desc',
                        cover_url: 'https://c.jpg',
                        genres: ['Action', 'Fantasy'],
                        chapters: [
                            { id: 'c1', number: '1', title: 'Chapter 1', date: '2026-10-07' }
                        ]
                    };
                },
                async getPages(chapterId) {
                    return ['https://img1.jpg', 'https://img2.jpg'];
                },
                async getChapterText(chapterId) {
                    return { content: '<p>Chapter prose</p>', format: 'html' };
                }
            };
        ";

        await bridge.LoadScriptAsync(script);

        var searchResults = await bridge.SearchAsync("test-query");
        Assert.Single(searchResults);
        Assert.Equal("Result for test-query", searchResults[0].Title);

        var detail = await bridge.GetMangaDetailAsync("m1");
        Assert.NotNull(detail);
        Assert.Equal("Title m1", detail.Title);
        Assert.Equal(2, detail.Genres.Count);
        Assert.Single(detail.Chapters);

        var pages = await bridge.GetPagesAsync("c1");
        Assert.Equal(2, pages.Count);
        Assert.Equal("https://img1.jpg", pages[0]);

        var text = await bridge.GetChapterTextAsync("c1");
        Assert.NotNull(text);
        Assert.Equal("<p>Chapter prose</p>", text.Content);
        Assert.Equal("html", text.Format);
    }
}
