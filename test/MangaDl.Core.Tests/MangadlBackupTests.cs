using System.Text;
using System.Text.Json;
using MangaDl.Core.Backup;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using Xunit;

namespace MangaDl.Core.Tests;

public class MangadlBackupTests : IAsyncLifetime
{
    private readonly string _tempDbPath = Path.Combine(Path.GetTempPath(), $"manga_backup_test_{Guid.NewGuid()}.db");
    private MangaDatabase _db = null!;
    private MangadlBackupService _service = null!;

    public async Task InitializeAsync()
    {
        _db = new MangaDatabase(_tempDbPath);
        await _db.InitializeAsync();
        _service = new MangadlBackupService(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public void IsMangadlBackup_IdentifiesFormatCorrectly()
    {
        var validJson = Encoding.UTF8.GetBytes("{\"app\": \"manga-dl\", \"version\": \"1.0\", \"library\": []}");
        Assert.True(MangadlBackupService.IsMangadlBackup(validJson));

        var otherJson = Encoding.UTF8.GetBytes("{\"backupCategories\": [], \"backupManga\": []}");
        Assert.False(MangadlBackupService.IsMangadlBackup(otherJson));

        var gzipBytes = new byte[] { 0x1f, 0x8b, 0x08, 0x00, 0x00 };
        Assert.False(MangadlBackupService.IsMangadlBackup(gzipBytes));

        var invalidBytes = new byte[] { 0x00, 0x01 };
        Assert.False(MangadlBackupService.IsMangadlBackup(invalidBytes));
    }

    [Fact]
    public async Task ExportAndRestore_RoundTripFidelity()
    {
        // 1. Seed database with categories, manga, progress, and tracker binds
        await _db.AddCategoryAsync("favorites", "Favorites", 0);
        await _db.AddCategoryAsync("reading", "Reading", 1);

        await _db.AddToLibraryAsync(new LibraryEntity
        {
            Provider = "mangadex",
            MangaId = "series-123",
            Title = "Solo Leveling",
            CoverUrl = "https://example.com/cover.jpg",
            Url = "https://mangadex.org/title/series-123",
            Type = "manga",
        });

        await _db.SetMangaCategoriesAsync("mangadex", "series-123", ["favorites", "reading"]);

        await _db.SaveProgressAsync("mangadex", "series-123", "ch-1", 1.0, 15, completed: true);
        await _db.SaveProgressAsync("mangadex", "series-123", "ch-2", 2.0, 8, completed: false);

        await _db.SaveTrackerBindAsync(new TrackerBindEntity
        {
            Provider = "mangadex",
            MangaId = "series-123",
            Tracker = "AniList",
            RemoteId = 105398,
            RemoteTitle = "Solo Leveling",
            LastChapterRead = 1.0,
        });

        // 2. Export backup
        var backupBytes = await _service.ExportBackupBytesAsync();
        Assert.True(backupBytes.Length > 0);
        Assert.True(MangadlBackupService.IsMangadlBackup(backupBytes));

        // 3. Create fresh new database to restore into
        var restoreDbPath = Path.Combine(Path.GetTempPath(), $"manga_restore_test_{Guid.NewGuid()}.db");
        var restoreDb = new MangaDatabase(restoreDbPath);
        await restoreDb.InitializeAsync();
        var restoreService = new MangadlBackupService(restoreDb);

        try
        {
            var summary = await restoreService.RestoreBackupAsync(backupBytes);

            Assert.Equal(1, summary.MangaRestored);
            Assert.Equal(5, summary.CategoriesRestored); // 4 database defaults + "Favorites"
            Assert.Equal(2, summary.ProgressRestored);
            Assert.Equal(1, summary.TrackerBindsRestored);

            // Verify restored library entry
            var library = await restoreDb.GetLibraryAsync();
            var manga = Assert.Single(library);
            Assert.Equal("Solo Leveling", manga.Title);
            Assert.Equal("mangadex", manga.Provider);
            Assert.Equal("series-123", manga.MangaId);

            // Verify restored categories
            var cats = await restoreDb.GetMangaCategoriesAsync("mangadex", "series-123");
            Assert.Equal(2, cats.Count);
            Assert.Contains("favorites", cats);
            Assert.Contains("reading", cats);

            // Verify restored progress
            var p1 = await restoreDb.GetProgressAsync("mangadex", "series-123", "ch-1");
            Assert.NotNull(p1);
            Assert.Equal(15, p1.Page);
            Assert.Equal(1, p1.Completed);

            var p2 = await restoreDb.GetProgressAsync("mangadex", "series-123", "ch-2");
            Assert.NotNull(p2);
            Assert.Equal(8, p2.Page);
            Assert.Equal(0, p2.Completed);

            // Verify restored tracker binds
            var tracker = await restoreDb.GetTrackerBindAsync("mangadex", "series-123", "AniList");
            Assert.NotNull(tracker);
            Assert.Equal(105398, tracker.RemoteId);
            Assert.Equal("Solo Leveling", tracker.RemoteTitle);
            Assert.Equal(1.0, tracker.LastChapterRead);
        }
        finally
        {
            await restoreDb.DisposeAsync();
            try { if (File.Exists(restoreDbPath)) File.Delete(restoreDbPath); } catch { }
        }
    }
}
