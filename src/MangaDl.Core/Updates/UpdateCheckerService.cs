using System.Globalization;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Downloads;
using MangaDl.Core.Extensions;
using MangaDl.Core.Local;

namespace MangaDl.Core.Updates;

public sealed record UpdateCheckResult(
    int TotalChecked,
    int NewChaptersFound,
    IReadOnlyList<NewChapterEntity> NewChapters,
    IReadOnlyList<string> Errors);

public sealed class UpdateCheckerService
{
    private readonly MangaDatabase _db;
    private readonly ExtensionManager _extensions;
    private readonly DownloadManager? _downloads;

    public UpdateCheckerService(
        MangaDatabase db,
        ExtensionManager extensions,
        DownloadManager? downloads = null)
    {
        _db = db;
        _extensions = extensions;
        _downloads = downloads;
    }

    /// <summary>
    /// Checks all library manga against scrapers, detects newly published chapters,
    /// persists them to the new_chapters table, updates total_chapters counts, and
    /// optionally queues downloads.
    /// </summary>
    public async Task<UpdateCheckResult> CheckUpdatesAsync(
        bool autoDownload = false,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var library = await _db.GetLibraryAsync();
        var remoteManga = library
            .Where(m => !string.Equals(m.Provider, LocalImportService.Provider, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var checkedCount = 0;
        var newChapters = new List<NewChapterEntity>();
        var errors = new List<string>();

        for (var i = 0; i < remoteManga.Count; i++)
        {
            if (ct.IsCancellationRequested) break;

            var manga = remoteManga[i];
            progress?.Report($"Checking {manga.Title} ({i + 1}/{remoteManga.Count})...");

            try
            {
                var detail = await _extensions.GetMangaDetailAsync(manga.Provider, manga.MangaId);
                if (detail == null || detail.Chapters.Count == 0)
                {
                    checkedCount++;
                    continue;
                }

                var latestCount = detail.Chapters.Count;
                if (manga.TotalChapters == 0)
                {
                    // Initial baseline: initialize total chapters count
                    await _db.UpdateTotalChaptersAsync(manga.Provider, manga.MangaId, latestCount);
                    manga.TotalChapters = latestCount;
                }
                else if (latestCount > manga.TotalChapters)
                {
                    var newCount = latestCount - manga.TotalChapters;
                    var sortedChapters = detail.Chapters
                        .OrderByDescending(c => ParseChapterNumber(c.Number))
                        .Take(newCount)
                        .ToList();

                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var detected = sortedChapters.Select(ch =>
                    {
                        var chNum = ParseChapterNumber(ch.Number);
                        return new NewChapterEntity
                        {
                            Id = $"{manga.Provider}/{manga.MangaId}/{ch.Id}",
                            MangaId = manga.MangaId,
                            MangaTitle = manga.Title,
                            CoverUrl = manga.CoverUrl ?? detail.CoverUrl,
                            Provider = manga.Provider,
                            ChapterId = ch.Id,
                            ChapterTitle = string.IsNullOrWhiteSpace(ch.Title) ? $"Chapter {ch.Number}" : ch.Title,
                            ChapterNumber = chNum,
                            DetectedAt = now,
                        };
                    }).ToList();

                    if (detected.Count > 0)
                    {
                        await _db.SaveNewChaptersAsync(detected);
                        newChapters.AddRange(detected);

                        if (autoDownload && _downloads != null)
                        {
                            foreach (var ch in detected)
                            {
                                await _downloads.EnqueueChapterAsync(
                                    ch.Provider,
                                    ch.MangaId,
                                    ch.MangaTitle,
                                    ch.ChapterId,
                                    ch.ChapterTitle,
                                    ch.ChapterNumber);
                            }
                        }
                    }

                    await _db.UpdateTotalChaptersAsync(manga.Provider, manga.MangaId, latestCount);
                }

                checkedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{manga.Title}: {ex.Message}");
            }

            // Slight throttle to avoid rate-limiting between scraping requests
            if (i < remoteManga.Count - 1)
            {
                try { await Task.Delay(100, ct); } catch (OperationCanceledException) { break; }
            }
        }

        return new UpdateCheckResult(checkedCount, newChapters.Count, newChapters, errors);
    }

    /// <summary>
    /// Groups NewChapterEntity entries into chronological buckets ("Today", "Yesterday", "Earlier")
    /// matching the Android and web app presentation.
    /// </summary>
    public static List<UpdateGroup> GroupUpdates(IEnumerable<NewChapterEntity> entries)
    {
        var list = entries.OrderByDescending(e => e.DetectedAt).ToList();
        if (list.Count == 0) return [];

        var todayStart = DateTime.Today.ToUniversalTime().Ticks / TimeSpan.TicksPerMillisecond;
        var yesterdayStart = todayStart - 86_400_000L;

        var groups = new List<UpdateGroup>();

        void Bucket(IEnumerable<NewChapterEntity> items, string label)
        {
            var itemList = items.Select(e =>
            {
                var manga = new Manga(
                    Id: e.MangaId,
                    Title: e.MangaTitle,
                    Cover: e.CoverUrl ?? "#1A2433",
                    Unread: 0,
                    Downloaded: false,
                    InLibrary: true,
                    Source: e.Provider);

                var numText = e.ChapterNumber % 1 == 0
                    ? ((int)e.ChapterNumber).ToString(CultureInfo.InvariantCulture)
                    : e.ChapterNumber.ToString(CultureInfo.InvariantCulture);

                var titleSuffix = string.IsNullOrWhiteSpace(e.ChapterTitle) ? "" : $" · {e.ChapterTitle}";
                var chapterText = $"Ch. {numText}{titleSuffix}";

                return new UpdateItem(
                    Manga: manga,
                    Chapter: chapterText,
                    Source: e.Provider,
                    ChapterId: e.ChapterId,
                    ChapterNumber: e.ChapterNumber,
                    DetectedAt: e.DetectedAt);
            }).ToList();

            if (itemList.Count > 0)
            {
                groups.Add(new UpdateGroup(label, itemList));
            }
        }

        Bucket(list.Where(e => e.DetectedAt >= todayStart), "Today");
        Bucket(list.Where(e => e.DetectedAt >= yesterdayStart && e.DetectedAt < todayStart), "Yesterday");
        Bucket(list.Where(e => e.DetectedAt < yesterdayStart), "Earlier");

        return groups;
    }

    public static double ParseChapterNumber(string? numberStr)
    {
        if (string.IsNullOrWhiteSpace(numberStr)) return 0;

        // Try direct parse
        if (double.TryParse(numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
        {
            return num;
        }

        // Try extracting first numeric/decimal match
        var match = System.Text.RegularExpressions.Regex.Match(numberStr, @"\d+(\.\d+)?");
        if (match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }

        return 0;
    }
}
