using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;

namespace MangaDl.Core.Tests;

public class BookmarkAndTrackerTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly MangaDatabase _db;

    public BookmarkAndTrackerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mangadl_test_bm_tracker_{Guid.NewGuid():N}.db");
        _db = new MangaDatabase(_dbPath);
    }

    public Task InitializeAsync() => _db.InitializeAsync();

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task AddAndQueryBookmarks_WorksCorrectly()
    {
        const string provider = "asura";
        const string mangaId = "reaper-of-the-drifting-moon";

        Assert.False(await _db.IsBookmarkedAsync(provider, mangaId, "ch-10"));

        await _db.AddBookmarkAsync(new BookmarkEntity
        {
            Provider = provider,
            MangaId = mangaId,
            ChapterId = "ch-10",
            ChapterNumber = 10.0,
            ChapterTitle = "Chapter 10"
        });

        await _db.AddBookmarkAsync(new BookmarkEntity
        {
            Provider = provider,
            MangaId = mangaId,
            ChapterId = "ch-5",
            ChapterNumber = 5.0,
            ChapterTitle = "Chapter 5"
        });

        Assert.True(await _db.IsBookmarkedAsync(provider, mangaId, "ch-10"));
        Assert.True(await _db.IsBookmarkedAsync(provider, mangaId, "ch-5"));
        Assert.False(await _db.IsBookmarkedAsync(provider, mangaId, "ch-1"));

        var list = await _db.GetBookmarksAsync(provider, mangaId);
        Assert.Equal(2, list.Count);
        // Ordered by ChapterNumber
        Assert.Equal(5.0, list[0].ChapterNumber);
        Assert.Equal(10.0, list[1].ChapterNumber);

        // Remove bookmark
        await _db.RemoveBookmarkAsync(provider, mangaId, "ch-5");
        Assert.False(await _db.IsBookmarkedAsync(provider, mangaId, "ch-5"));

        var remaining = await _db.GetBookmarksAsync(provider, mangaId);
        Assert.Single(remaining);
        Assert.Equal("ch-10", remaining[0].ChapterId);
    }

    [Fact]
    public async Task MarkChaptersReadAndBatchOperations_WorksCorrectly()
    {
        const string provider = "flamecomics";
        const string mangaId = "omniscient-reader";

        var chapters = new List<(string ChapterId, double ChapterNumber, string? Title)>
        {
            ("ch-1", 1.0, "Prologue"),
            ("ch-2", 2.0, "Ep. 1 - Starting"),
            ("ch-3", 3.0, "Ep. 2 - Protagonist")
        };

        await _db.MarkChaptersReadAsync(provider, mangaId, chapters, coverUrl: "https://example.com/cover.jpg", mangaTitle: "Omniscient Reader");

        var progressList = await _db.GetMangaProgressAsync(provider, mangaId);
        Assert.Equal(3, progressList.Count);
        Assert.All(progressList, p => Assert.Equal(1, p.Completed));

        // Mark ch-2 as unread
        await _db.DeleteProgressAsync(provider, mangaId, "ch-2");

        var updatedProgress = await _db.GetMangaProgressAsync(provider, mangaId);
        Assert.Equal(2, updatedProgress.Count);
        Assert.DoesNotContain(updatedProgress, p => p.ChapterId == "ch-2");
    }

    [Fact]
    public async Task TrackerBinds_StoresScoreAndStatus()
    {
        const string provider = "mangadex";
        const string mangaId = "chainsaw-man";

        var bind = new TrackerBindEntity
        {
            Provider = provider,
            MangaId = mangaId,
            Tracker = "AniList",
            RemoteId = 105778,
            RemoteTitle = "Chainsaw Man",
            LastChapterRead = 97.0,
            Score = 9.5,
            Status = "COMPLETED"
        };

        await _db.SaveTrackerBindAsync(bind);

        var retrieved = await _db.GetTrackerBindAsync(provider, mangaId, "AniList");
        Assert.NotNull(retrieved);
        Assert.Equal(105778, retrieved.RemoteId);
        Assert.Equal("Chainsaw Man", retrieved.RemoteTitle);
        Assert.Equal(9.5, retrieved.Score);
        Assert.Equal("COMPLETED", retrieved.Status);
        Assert.Equal(97.0, retrieved.LastChapterRead);

        // Update score and status
        retrieved.Score = 10.0;
        retrieved.Status = "RE-READING";
        await _db.SaveTrackerBindAsync(retrieved);

        var updated = await _db.GetTrackerBindAsync(provider, mangaId, "AniList");
        Assert.NotNull(updated);
        Assert.Equal(10.0, updated.Score);
        Assert.Equal("RE-READING", updated.Status);

        // Delete bind
        await _db.DeleteTrackerBindAsync(provider, mangaId, "AniList");
        var afterDelete = await _db.GetTrackerBindAsync(provider, mangaId, "AniList");
        Assert.Null(afterDelete);
    }
}
