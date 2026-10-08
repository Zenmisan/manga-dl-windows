using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Local;

namespace MangaDl.Core.Tests;

public class CategoriesTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly MangaDatabase _db;

    public CategoriesTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mangadl_test_categories_{Guid.NewGuid():N}.db");
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
    public async Task Initialize_SeedsDefaultCategories()
    {
        var cats = await _db.GetCategoriesAsync();
        Assert.True(cats.Count >= 4);

        var ids = cats.Select(c => c.Id).ToList();
        Assert.Contains("reading", ids);
        Assert.Contains("plan_to_read", ids);
        Assert.Contains("completed", ids);
        Assert.Contains("local_files", ids);
    }

    [Fact]
    public async Task AddAndRemoveCustomCategory_WorksCorrectly()
    {
        var customId = FileNaming.Slugify("Favorites 2026");
        Assert.Equal("favorites-2026", customId);

        await _db.AddCategoryAsync(customId, "Favorites 2026", 10);
        var cats = await _db.GetCategoriesAsync();
        Assert.Contains(cats, c => c.Id == customId && c.Name == "Favorites 2026");

        await _db.DeleteCategoryAsync(customId);
        var afterDelete = await _db.GetCategoriesAsync();
        Assert.DoesNotContain(afterDelete, c => c.Id == customId);
    }

    [Fact]
    public async Task SetAndGetMangaCategories_AssignsAndUpdatesCategories()
    {
        const string provider = "mangadex";
        const string mangaId = "solo-leveling";

        // Assign to 2 categories
        await _db.SetMangaCategoriesAsync(provider, mangaId, ["reading", "plan_to_read"]);
        var assigned = await _db.GetMangaCategoriesAsync(provider, mangaId);

        Assert.Equal(2, assigned.Count);
        Assert.Contains("reading", assigned);
        Assert.Contains("plan_to_read", assigned);

        // Update to 1 category
        await _db.SetMangaCategoriesAsync(provider, mangaId, ["completed"]);
        var updated = await _db.GetMangaCategoriesAsync(provider, mangaId);

        Assert.Single(updated);
        Assert.Equal("completed", updated[0]);
    }
}
