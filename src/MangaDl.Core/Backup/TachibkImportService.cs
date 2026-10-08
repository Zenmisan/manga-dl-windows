using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Local;

namespace MangaDl.Core.Backup;

public sealed record TachibkImportSummary(int MangaImported, int CategoriesImported, int ChaptersMarkedRead, int TrackerLinksImported);

/// <summary>
/// Imports a parsed Tachiyomi/Mihon backup into the local library. Unlike
/// LocalImportService, these are real scraped-source references being restored
/// (provider resolved from the backup's source name/URL) — not local files, so
/// there's no CBZ/download step, just library + progress + category + tracker rows.
/// </summary>
public sealed class TachibkImportService
{
    private readonly MangaDatabase _db;

    public TachibkImportService(MangaDatabase db)
    {
        _db = db;
    }

    public async Task<TachibkImportSummary> ImportAsync(TachiyomiBackupResult backup)
    {
        var mangaImported = 0;
        var chaptersMarkedRead = 0;
        var trackerLinksImported = 0;
        var categoryIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // name -> id

        foreach (var category in backup.Categories)
        {
            var name = category.Name.Trim();
            if (name.Length == 0) continue;
            var id = FileNaming.Slugify(name);
            await _db.AddCategoryAsync(id, name, category.Order);
            categoryIds[name] = id;
        }

        foreach (var manga in backup.Manga)
        {
            var title = manga.Title.Trim();
            if (title.Length == 0) continue;

            var provider = TachibkParser.ResolveProvider(manga.SourceName, manga.Url);
            var mangaId = TachibkParser.CleanMangaId(manga.Url, provider);

            await _db.AddToLibraryAsync(new LibraryEntity
            {
                Provider = provider,
                MangaId = mangaId,
                Title = title,
                CoverUrl = manga.ThumbnailUrl,
                Url = manga.Url,
                Type = "manga",
            });
            mangaImported++;

            var mangaCatIds = manga.CategoryNames
                .Where(n => !string.IsNullOrWhiteSpace(n) && categoryIds.ContainsKey(n))
                .Select(n => categoryIds[n])
                .ToList();
            if (mangaCatIds.Count > 0)
            {
                await _db.SetMangaCategoriesAsync(provider, mangaId, mangaCatIds);
            }

            foreach (var chapter in manga.Chapters)
            {
                if (!chapter.Read) continue;
                var chapterId = TachibkParser.CleanChapterId(chapter.Url, chapter.ChapterNumber);
                var page = chapter.LastPageRead > 0 ? chapter.LastPageRead : 1;
                await _db.SaveProgressAsync(provider, mangaId, chapterId, chapter.ChapterNumber, page, completed: true);
                chaptersMarkedRead++;
            }

            foreach (var tracking in manga.Tracking)
            {
                // tracker_binds only understands the two trackers this app actually
                // supports (TrackersSettingsPage) — Kitsu/Shikimori/Bangumi/MangaUpdates
                // links in the backup are silently skipped, same as the Python backend
                // does implicitly (TRACKER_SYNC_ID_MAP has no handler for them either).
                var trackerName = TachibkParser.TrackerSyncIdMap.GetValueOrDefault(tracking.SyncId) switch
                {
                    "mal" => "MyAnimeList",
                    "anilist" => "AniList",
                    _ => null,
                };
                if (trackerName is null || tracking.MediaId == 0) continue;

                await _db.SaveTrackerBindAsync(new TrackerBindEntity
                {
                    Provider = provider,
                    MangaId = mangaId,
                    Tracker = trackerName,
                    RemoteId = (int)tracking.MediaId,
                    RemoteTitle = string.IsNullOrWhiteSpace(tracking.Title) ? null : tracking.Title,
                    LastChapterRead = tracking.LastChapterRead,
                });
                trackerLinksImported++;
            }
        }

        return new TachibkImportSummary(mangaImported, categoryIds.Count, chaptersMarkedRead, trackerLinksImported);
    }
}
