using System.Text.Json;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Local;

namespace MangaDl.Core.Backup;

/// <summary>
/// Universal Manga-DL backup engine. Exports and restores complete library,
/// reading progress, categories, and tracker bindings matching the cross-platform
/// Android and web backup schema.
/// </summary>
public sealed class MangadlBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly MangaDatabase _db;

    public MangadlBackupService(MangaDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Checks whether the raw bytes represent a Manga-DL JSON backup file.
    /// </summary>
    public static bool IsMangadlBackup(byte[] data)
    {
        if (data.Length < 2) return false;
        // Check for gzip header (Tachiyomi protobuf backups use gzip)
        if (data[0] == 0x1f && data[1] == 0x8b) return false;

        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("app", out var appProp))
            {
                var app = appProp.GetString();
                if (string.Equals(app, "manga-dl", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Not valid JSON
        }

        return false;
    }

    /// <summary>
    /// Exports the full database state into a universal MangaDlBackup instance.
    /// </summary>
    public async Task<MangaDlBackup> CreateBackupAsync()
    {
        var library = await _db.GetLibraryAsync();
        var categories = await _db.GetCategoriesAsync();
        var libraryCategories = await _db.GetAllLibraryCategoriesAsync();
        var progress = await _db.GetAllProgressAsync();
        var trackerBinds = await _db.GetAllTrackerBindsAsync();

        var catIdToName = categories.ToDictionary(c => c.Id, c => c.Name);
        var mangaCats = new Dictionary<string, List<string>>();

        foreach (var mapping in libraryCategories)
        {
            if (!mangaCats.TryGetValue(mapping.LibraryId, out var catList))
            {
                catList = [];
                mangaCats[mapping.LibraryId] = catList;
            }
            var name = catIdToName.GetValueOrDefault(mapping.CategoryId, mapping.CategoryId);
            if (!catList.Contains(name))
            {
                catList.Add(name);
            }
        }

        var backup = new MangaDlBackup
        {
            Version = "1.0",
            App = "manga-dl",
            ExportedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Categories = categories.Select(c => c.Name).ToList(),
            MangaCategories = mangaCats,
        };

        foreach (var item in library)
        {
            var libKey = $"{item.Provider}/{item.MangaId}";
            var cats = mangaCats.GetValueOrDefault(libKey, []);

            backup.Library.Add(new BackupMangaEntry
            {
                Id = item.MangaId,
                Title = item.Title,
                CoverUrl = item.CoverUrl ?? "",
                Provider = item.Provider,
                Url = item.Url ?? "",
                Type = item.Type ?? "manga",
                AddedAt = item.AddedAt,
                TotalChapters = item.TotalChapters,
                Categories = cats,
            });
        }

        foreach (var p in progress)
        {
            backup.Progress.Add(new BackupProgressEntry
            {
                MangaId = p.MangaId,
                ChapterId = p.ChapterId,
                Provider = p.Provider,
                Page = p.Page,
                Completed = p.Completed == 1,
                ChapterNumber = p.ChapterNumber,
                ReadAt = p.ReadAt,
            });
        }

        foreach (var t in trackerBinds)
        {
            backup.TrackerBinds.Add(new BackupTrackerBindEntry
            {
                Provider = t.Provider,
                MangaId = t.MangaId,
                Tracker = t.Tracker,
                RemoteId = t.RemoteId,
                RemoteTitle = t.RemoteTitle,
                LastChapterRead = t.LastChapterRead,
            });
        }

        return backup;
    }

    /// <summary>
    /// Exports the full database state as a formatted JSON byte array.
    /// </summary>
    public async Task<byte[]> ExportBackupBytesAsync()
    {
        var backup = await CreateBackupAsync();
        return JsonSerializer.SerializeToUtf8Bytes(backup, JsonOptions);
    }

    /// <summary>
    /// Restores a MangaDlBackup from JSON bytes into the database.
    /// </summary>
    public async Task<MangadlRestoreSummary> RestoreBackupAsync(byte[] data)
    {
        var backup = JsonSerializer.Deserialize<MangaDlBackup>(data, JsonOptions);
        if (backup == null)
        {
            throw new InvalidOperationException("Failed to parse Manga-DL backup JSON");
        }

        return await RestoreBackupAsync(backup);
    }

    /// <summary>
    /// Restores a MangaDlBackup instance into the database.
    /// </summary>
    public async Task<MangadlRestoreSummary> RestoreBackupAsync(MangaDlBackup backup)
    {
        var mangaRestored = 0;
        var progressRestored = 0;
        var categoriesRestored = 0;
        var trackerBindsRestored = 0;

        var categoryNameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var sortOrder = 0;
        foreach (var catName in backup.Categories)
        {
            var trimmed = catName.Trim();
            if (trimmed.Length == 0) continue;
            var id = FileNaming.Slugify(trimmed);
            await _db.AddCategoryAsync(id, trimmed, sortOrder++);
            categoryNameToId[trimmed] = id;
            categoriesRestored++;
        }

        foreach (var m in backup.Library)
        {
            var title = m.Title.Trim();
            if (title.Length == 0) continue;

            await _db.AddToLibraryAsync(new LibraryEntity
            {
                Provider = m.Provider,
                MangaId = m.Id,
                Title = title,
                CoverUrl = m.CoverUrl,
                Url = m.Url,
                Type = string.IsNullOrWhiteSpace(m.Type) ? "manga" : m.Type,
                TotalChapters = m.TotalChapters,
                AddedAt = m.AddedAt > 0 ? m.AddedAt : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            });
            mangaRestored++;

            // Reconnect categories
            var associatedCats = m.Categories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c =>
                {
                    if (categoryNameToId.TryGetValue(c, out var cid)) return cid;
                    var newCid = FileNaming.Slugify(c);
                    categoryNameToId[c] = newCid;
                    return newCid;
                })
                .Distinct()
                .ToList();

            if (associatedCats.Count > 0)
            {
                await _db.SetMangaCategoriesAsync(m.Provider, m.Id, associatedCats);
            }
        }

        foreach (var p in backup.Progress)
        {
            if (string.IsNullOrWhiteSpace(p.Provider) || string.IsNullOrWhiteSpace(p.MangaId) || string.IsNullOrWhiteSpace(p.ChapterId))
                continue;

            var page = p.Page > 0 ? p.Page : 1;
            await _db.SaveProgressAsync(p.Provider, p.MangaId, p.ChapterId, p.ChapterNumber, page, p.Completed);
            progressRestored++;
        }

        foreach (var t in backup.TrackerBinds)
        {
            if (string.IsNullOrWhiteSpace(t.Provider) || string.IsNullOrWhiteSpace(t.MangaId) || string.IsNullOrWhiteSpace(t.Tracker))
                continue;

            await _db.SaveTrackerBindAsync(new TrackerBindEntity
            {
                Provider = t.Provider,
                MangaId = t.MangaId,
                Tracker = t.Tracker,
                RemoteId = t.RemoteId,
                RemoteTitle = t.RemoteTitle,
                LastChapterRead = t.LastChapterRead,
            });
            trackerBindsRestored++;
        }

        return new MangadlRestoreSummary(mangaRestored, progressRestored, categoriesRestored, trackerBindsRestored);
    }
}
