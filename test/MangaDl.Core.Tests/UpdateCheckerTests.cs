using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Updates;

namespace MangaDl.Core.Tests;

public class UpdateCheckerTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly MangaDatabase _db;

    public UpdateCheckerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mangadl_test_updates_{Guid.NewGuid():N}.db");
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

    [Theory]
    [InlineData("1", 1.0)]
    [InlineData("51.5", 51.5)]
    [InlineData("Ch. 42", 42.0)]
    [InlineData("Chapter 103.5 - The End", 103.5)]
    [InlineData("", 0.0)]
    [InlineData(null, 0.0)]
    public void ParseChapterNumber_ParsesVariousFormats(string? input, double expected)
    {
        var result = UpdateCheckerService.ParseChapterNumber(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task NewChapterDatabase_SaveAndRetrieve_PreservesOrdering()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var ch1 = new NewChapterEntity
        {
            Provider = "mangadex",
            MangaId = "series-1",
            MangaTitle = "Series One",
            ChapterId = "ch-1",
            ChapterTitle = "Beginning",
            ChapterNumber = 1.0,
            DetectedAt = now - 10000,
        };

        var ch2 = new NewChapterEntity
        {
            Provider = "mangadex",
            MangaId = "series-1",
            MangaTitle = "Series One",
            ChapterId = "ch-2",
            ChapterTitle = "Next Step",
            ChapterNumber = 2.0,
            DetectedAt = now,
        };

        await _db.SaveNewChaptersAsync([ch1, ch2]);

        var retrieved = await _db.GetNewChaptersAsync();
        Assert.Equal(2, retrieved.Count);
        // Ordered by DetectedAt desc
        Assert.Equal("ch-2", retrieved[0].ChapterId);
        Assert.Equal("ch-1", retrieved[1].ChapterId);

        // Remove single
        await _db.RemoveNewChapterAsync(ch2.Id);
        var remaining = await _db.GetNewChaptersAsync();
        Assert.Single(remaining);
        Assert.Equal("ch-1", remaining[0].ChapterId);

        // Clear all
        await _db.ClearNewChaptersAsync();
        var empty = await _db.GetNewChaptersAsync();
        Assert.Empty(empty);
    }

    [Fact]
    public async Task UpdateTotalChaptersAsync_UpdatesLibraryRow()
    {
        var manga = new LibraryEntity
        {
            Provider = "mangadex",
            MangaId = "series-abc",
            Title = "Series ABC",
            TotalChapters = 10,
        };
        await _db.AddToLibraryAsync(manga);

        await _db.UpdateTotalChaptersAsync("mangadex", "series-abc", 15);

        var lib = await _db.GetLibraryAsync();
        var updated = lib.FirstOrDefault(x => x.MangaId == "series-abc");
        Assert.NotNull(updated);
        Assert.Equal(15, updated.TotalChapters);
    }

    [Fact]
    public void GroupUpdates_BucketsIntoTodayYesterdayEarlier()
    {
        var now = DateTime.UtcNow;
        var todayMs = DateTime.Today.ToUniversalTime().Ticks / TimeSpan.TicksPerMillisecond + 1000;
        var yesterdayMs = todayMs - 86_400_000L + 500;
        var earlierMs = todayMs - 200_000_000L;

        var entries = new List<NewChapterEntity>
        {
            new() { MangaId = "m1", MangaTitle = "Manga 1", ChapterId = "c1", ChapterNumber = 10, DetectedAt = todayMs, Provider = "mangadex" },
            new() { MangaId = "m2", MangaTitle = "Manga 2", ChapterId = "c2", ChapterNumber = 5, DetectedAt = yesterdayMs, Provider = "mangadex" },
            new() { MangaId = "m3", MangaTitle = "Manga 3", ChapterId = "c3", ChapterNumber = 1, DetectedAt = earlierMs, Provider = "mangadex" },
        };

        var groups = UpdateCheckerService.GroupUpdates(entries);

        Assert.Equal(3, groups.Count);
        Assert.Equal("Today", groups[0].Label);
        Assert.Single(groups[0].Items);
        Assert.Equal("Yesterday", groups[1].Label);
        Assert.Single(groups[1].Items);
        Assert.Equal("Earlier", groups[2].Label);
        Assert.Single(groups[2].Items);
    }
}
