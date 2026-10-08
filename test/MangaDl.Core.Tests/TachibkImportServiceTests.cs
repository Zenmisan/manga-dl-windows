using MangaDl.Core.Backup;
using MangaDl.Core.Database;
using Xunit;

namespace MangaDl.Core.Tests;

public class TachibkImportServiceTests : IAsyncLifetime
{
    private readonly string _tempDbPath = Path.Combine(Path.GetTempPath(), $"manga_tachibk_test_{Guid.NewGuid()}.db");
    private MangaDatabase _db = null!;
    private TachibkImportService _importer = null!;

    public async Task InitializeAsync()
    {
        _db = new MangaDatabase(_tempDbPath);
        await _db.InitializeAsync();
        _importer = new TachibkImportService(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    private static TachiyomiBackupResult BuildFakeBackup() => new(
        Manga:
        [
            new BackupManga(
                Source: 7,
                Url: "https://mangadex.org/title/550e8400-e29b-41d4-a716-446655440000",
                Title: "Solo Leveling",
                Artist: null, Author: null, Description: null,
                ThumbnailUrl: "https://example.com/cover.jpg",
                Favorite: true,
                Chapters:
                [
                    new BackupChapter("https://mangadex.org/chapter/ch-1", "Chapter 1", null, Read: true, Bookmark: false, LastPageRead: 10, ChapterNumber: 1, SourceOrder: 0),
                    new BackupChapter("https://mangadex.org/chapter/ch-2", "Chapter 2", null, Read: false, Bookmark: false, LastPageRead: 0, ChapterNumber: 2, SourceOrder: 1),
                ],
                Tracking: [new BackupTracking(SyncId: 2, MediaId: 12345, Title: "Solo Leveling", LastChapterRead: 1, Score: 0, Status: 1, TrackingUrl: "")],
                History: [],
                CategoryNames: ["Favorites"],
                SourceName: "MangaDex"),
        ],
        Categories: [new BackupCategoryEntry("Favorites", 0)],
        Sources: [new BackupSourceEntry("MangaDex", 7)]);

    [Fact]
    public async Task ImportAsync_RegistersLibraryProgressCategoryAndTrackerBind()
    {
        var summary = await _importer.ImportAsync(BuildFakeBackup());

        Assert.Equal(1, summary.MangaImported);
        Assert.Equal(1, summary.CategoriesImported);
        Assert.Equal(1, summary.ChaptersMarkedRead); // only ch-1 was Read
        Assert.Equal(1, summary.TrackerLinksImported); // AniList only; sync_id 2

        var library = await _db.GetLibraryAsync();
        var entry = Assert.Single(library);
        Assert.Equal("mangadex", entry.Provider);
        Assert.Equal("550e8400-e29b-41d4-a716-446655440000", entry.MangaId);
        Assert.Equal("Solo Leveling", entry.Title);

        var categories = await _db.GetMangaCategoriesAsync(entry.Provider, entry.MangaId);
        Assert.Contains("favorites", categories);

        var progress = await _db.GetProgressAsync(entry.Provider, entry.MangaId, "ch-1");
        Assert.NotNull(progress);
        Assert.Equal(1, progress.Completed);

        var notRead = await _db.GetProgressAsync(entry.Provider, entry.MangaId, "ch-2");
        Assert.Null(notRead);

        var binds = await _db.GetTrackerBindsAsync(entry.Provider, entry.MangaId);
        var bind = Assert.Single(binds);
        Assert.Equal("AniList", bind.Tracker);
        Assert.Equal(12345, bind.RemoteId);
    }

    [Fact]
    public async Task ImportAsync_SkipsMangaWithBlankTitle()
    {
        var backup = BuildFakeBackup() with
        {
            Manga = [BuildFakeBackup().Manga[0] with { Title = "   " }]
        };

        var summary = await _importer.ImportAsync(backup);

        Assert.Equal(0, summary.MangaImported);
        Assert.Empty(await _db.GetLibraryAsync());
    }
}
