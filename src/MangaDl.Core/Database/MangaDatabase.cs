using MangaDl.Core.Database.Entities;
using SQLite;

namespace MangaDl.Core.Database;

public sealed class MangaDatabase : IAsyncDisposable
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public MangaDatabase(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            await _db.CreateTableAsync<LibraryEntity>();
            await _db.CreateTableAsync<ProgressEntity>();
            await _db.CreateTableAsync<DownloadEntity>();
            await _db.CreateTableAsync<CategoryEntity>();
            await _db.CreateTableAsync<LibraryCategoryEntity>();
            await _db.CreateTableAsync<HistoryEntity>();

            // Ensure default categories exist
            var count = await _db.Table<CategoryEntity>().CountAsync();
            if (count == 0)
            {
                await _db.InsertAllAsync(new[]
                {
                    new CategoryEntity { Id = "reading", Name = "Reading", SortOrder = 0 },
                    new CategoryEntity { Id = "plan_to_read", Name = "Plan to read", SortOrder = 1 },
                    new CategoryEntity { Id = "completed", Name = "Completed", SortOrder = 2 },
                    new CategoryEntity { Id = "local_files", Name = "Local files", SortOrder = 3 }
                });
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ----------------------------------------------------
    // Library
    // ----------------------------------------------------

    public async Task<List<LibraryEntity>> GetLibraryAsync()
    {
        await InitializeAsync();
        return await _db.Table<LibraryEntity>().OrderByDescending(x => x.UpdatedAt).ToListAsync();
    }

    public async Task<LibraryEntity?> GetLibraryItemAsync(string provider, string mangaId)
    {
        await InitializeAsync();
        var id = $"{provider}/{mangaId}";
        return await _db.Table<LibraryEntity>().Where(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<bool> IsInLibraryAsync(string provider, string mangaId)
    {
        await InitializeAsync();
        var id = $"{provider}/{mangaId}";
        var count = await _db.Table<LibraryEntity>().Where(x => x.Id == id).CountAsync();
        return count > 0;
    }

    public async Task AddToLibraryAsync(LibraryEntity item)
    {
        await InitializeAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (item.AddedAt == 0) item.AddedAt = now;
        item.UpdatedAt = now;
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Id = $"{item.Provider}/{item.MangaId}";
        }
        await _db.InsertOrReplaceAsync(item);
    }

    public async Task RemoveFromLibraryAsync(string provider, string mangaId)
    {
        await InitializeAsync();
        var id = $"{provider}/{mangaId}";
        await _db.Table<LibraryEntity>().DeleteAsync(x => x.Id == id);
        await _db.Table<LibraryCategoryEntity>().DeleteAsync(x => x.LibraryId == id);
    }

    // ----------------------------------------------------
    // Reading Progress
    // ----------------------------------------------------

    public async Task<ProgressEntity?> GetProgressAsync(string provider, string mangaId, string chapterId)
    {
        await InitializeAsync();
        var id = $"{provider}/{mangaId}/{chapterId}";
        return await _db.Table<ProgressEntity>().Where(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<List<ProgressEntity>> GetMangaProgressAsync(string provider, string mangaId)
    {
        await InitializeAsync();
        return await _db.Table<ProgressEntity>()
            .Where(x => x.Provider == provider && x.MangaId == mangaId)
            .ToListAsync();
    }

    public async Task SaveProgressAsync(string provider, string mangaId, string chapterId, double chapterNumber, int page, bool completed)
    {
        await InitializeAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entity = new ProgressEntity
        {
            Id = $"{provider}/{mangaId}/{chapterId}",
            Provider = provider,
            MangaId = mangaId,
            ChapterId = chapterId,
            ChapterNumber = chapterNumber,
            Page = page,
            Completed = completed ? 1 : 0,
            ReadAt = now
        };
        await _db.InsertOrReplaceAsync(entity);
    }

    // ----------------------------------------------------
    // History
    // ----------------------------------------------------

    public async Task<List<HistoryEntity>> GetHistoryAsync(int limit = 50)
    {
        await InitializeAsync();
        return await _db.Table<HistoryEntity>()
            .OrderByDescending(x => x.ReadAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task RecordHistoryAsync(string provider, string mangaId, string mangaTitle, string chapterId, string? chapterTitle, string? coverUrl)
    {
        await InitializeAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entity = new HistoryEntity
        {
            Id = $"{provider}/{mangaId}/{chapterId}",
            Provider = provider,
            MangaId = mangaId,
            MangaTitle = mangaTitle,
            ChapterId = chapterId,
            ChapterTitle = chapterTitle,
            CoverUrl = coverUrl,
            ReadAt = now
        };
        await _db.InsertOrReplaceAsync(entity);
    }

    public async Task ClearHistoryAsync()
    {
        await InitializeAsync();
        await _db.DeleteAllAsync<HistoryEntity>();
    }

    // ----------------------------------------------------
    // Downloads
    // ----------------------------------------------------

    public async Task<List<DownloadEntity>> GetDownloadsAsync()
    {
        await InitializeAsync();
        return await _db.Table<DownloadEntity>().OrderByDescending(x => x.QueuedAt).ToListAsync();
    }

    public async Task<DownloadEntity?> GetDownloadAsync(string provider, string mangaId, string chapterId)
    {
        await InitializeAsync();
        var id = $"{provider}/{mangaId}/{chapterId}";
        return await _db.Table<DownloadEntity>().Where(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task EnqueueDownloadAsync(DownloadEntity item)
    {
        await InitializeAsync();
        if (string.IsNullOrEmpty(item.Id))
        {
            item.Id = $"{item.Provider}/{item.MangaId}/{item.ChapterId}";
        }
        if (item.QueuedAt == 0) item.QueuedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _db.InsertOrReplaceAsync(item);
    }

    public async Task UpdateDownloadProgressAsync(string id, int progress, int totalPages, string status)
    {
        await InitializeAsync();
        var item = await _db.Table<DownloadEntity>().Where(x => x.Id == id).FirstOrDefaultAsync();
        if (item != null)
        {
            item.Progress = progress;
            item.TotalPages = totalPages;
            item.Status = status;
            if (status == "completed")
            {
                item.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            await _db.UpdateAsync(item);
        }
    }

    public async Task RemoveDownloadAsync(string id)
    {
        await InitializeAsync();
        await _db.Table<DownloadEntity>().DeleteAsync(x => x.Id == id);
    }

    // ----------------------------------------------------
    // Categories
    // ----------------------------------------------------

    public async Task<List<CategoryEntity>> GetCategoriesAsync()
    {
        await InitializeAsync();
        return await _db.Table<CategoryEntity>().OrderBy(x => x.SortOrder).ToListAsync();
    }

    public async Task AddCategoryAsync(string id, string name, int sortOrder = 0)
    {
        await InitializeAsync();
        await _db.InsertOrReplaceAsync(new CategoryEntity
        {
            Id = id,
            Name = name,
            SortOrder = sortOrder
        });
    }

    public async Task DeleteCategoryAsync(string id)
    {
        await InitializeAsync();
        await _db.Table<CategoryEntity>().DeleteAsync(x => x.Id == id);
        await _db.Table<LibraryCategoryEntity>().DeleteAsync(x => x.CategoryId == id);
    }

    public async Task SetMangaCategoriesAsync(string provider, string mangaId, IEnumerable<string> categoryIds)
    {
        await InitializeAsync();
        var libId = $"{provider}/{mangaId}";
        await _db.Table<LibraryCategoryEntity>().DeleteAsync(x => x.LibraryId == libId);
        var entries = categoryIds.Select(catId => new LibraryCategoryEntity
        {
            LibraryId = libId,
            CategoryId = catId
        });
        await _db.InsertAllAsync(entries);
    }

    public async Task<List<string>> GetMangaCategoriesAsync(string provider, string mangaId)
    {
        await InitializeAsync();
        var libId = $"{provider}/{mangaId}";
        var mappings = await _db.Table<LibraryCategoryEntity>().Where(x => x.LibraryId == libId).ToListAsync();
        return mappings.Select(x => x.CategoryId).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.CloseAsync();
        _initLock.Dispose();
    }
}
