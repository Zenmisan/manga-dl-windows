using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Extensions;
using MangaDl.Core.Http;

namespace MangaDl.Core.Downloads;

public sealed class DownloadManager : IDisposable
{
    private readonly MangaDatabase _db;
    private readonly ExtensionManager _extensions;
    private readonly HttpService _http;
    private readonly string _baseDownloadDirectory;

    private readonly ConcurrentQueue<DownloadEntity> _queue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _workerLock = new(1, 1);
    private bool _paused;
    private Task? _workerTask;

    public event Action<DownloadEntity>? DownloadProgressChanged;
    public event Action<DownloadEntity>? DownloadCompleted;
    public event Action<DownloadEntity, Exception>? DownloadFailed;

    public DownloadManager(
        MangaDatabase db,
        ExtensionManager extensions,
        HttpService http,
        string? downloadDirectory = null)
    {
        _db = db;
        _extensions = extensions;
        _http = http;

        _baseDownloadDirectory = downloadDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "manga-dl");

        StartWorker();
    }

    public void StartWorker()
    {
        if (_workerTask == null || _workerTask.IsCompleted)
        {
            _workerTask = Task.Run(ProcessQueueAsync);
        }
    }

    public void Pause() => _paused = true;

    public void Resume()
    {
        _paused = false;
        StartWorker();
    }

    public bool IsPaused => _paused;

    public async Task EnqueueChapterAsync(
        string provider,
        string mangaId,
        string mangaTitle,
        string chapterId,
        string? chapterTitle,
        double chapterNumber)
    {
        var id = $"{provider}/{mangaId}/{chapterId}";
        var existing = await _db.GetDownloadAsync(provider, mangaId, chapterId);
        if (existing != null && existing.Status == "completed")
        {
            return;
        }

        var entity = new DownloadEntity
        {
            Id = id,
            Provider = provider,
            MangaId = mangaId,
            MangaTitle = mangaTitle,
            ChapterId = chapterId,
            ChapterTitle = chapterTitle ?? $"Chapter {chapterNumber}",
            ChapterNumber = chapterNumber,
            Status = "queued",
            Progress = 0,
            QueuedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        await _db.EnqueueDownloadAsync(entity);
        _queue.Enqueue(entity);
        StartWorker();
    }

    private async Task ProcessQueueAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            if (_paused)
            {
                await Task.Delay(500, _cts.Token);
                continue;
            }

            if (!_queue.TryDequeue(out var item))
            {
                // Check if any queued item in database
                var pending = await _db.GetDownloadsAsync();
                var next = pending.FirstOrDefault(x => x.Status == "queued" || x.Status == "downloading");
                if (next == null)
                {
                    await Task.Delay(1000, _cts.Token);
                    continue;
                }
                item = next;
            }

            try
            {
                await ExecuteDownloadAsync(item, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                item.Status = "failed";
                await _db.UpdateDownloadProgressAsync(item.Id, item.Progress, item.TotalPages, "failed");
                DownloadFailed?.Invoke(item, ex);
            }
        }
    }

    private async Task ExecuteDownloadAsync(DownloadEntity item, CancellationToken ct)
    {
        item.Status = "downloading";
        await _db.UpdateDownloadProgressAsync(item.Id, 0, 0, "downloading");
        DownloadProgressChanged?.Invoke(item);

        // 1. Fetch page image URLs
        var pageUrls = await _extensions.GetPagesAsync(item.Provider, item.ChapterId);
        if (pageUrls.Count == 0)
        {
            throw new InvalidOperationException($"No page URLs returned for chapter {item.ChapterId}");
        }

        item.TotalPages = pageUrls.Count;

        // 2. Download page bytes
        var pagesData = new List<byte[]>();
        var extensions = new List<string>();

        for (var i = 0; i < pageUrls.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var url = pageUrls[i];
            var bytes = await _http.FetchBytesAsync(url, ct: ct);
            pagesData.Add(bytes);

            var ext = Path.GetExtension(new Uri(url).AbsolutePath);
            extensions.Add(string.IsNullOrEmpty(ext) ? ".jpg" : ext);

            item.Progress = (int)(((i + 1) / (double)pageUrls.Count) * 100);
            await _db.UpdateDownloadProgressAsync(item.Id, item.Progress, item.TotalPages, "downloading");
            DownloadProgressChanged?.Invoke(item);
        }

        // 3. Assemble CBZ archive
        var safeManga = SanitizeFileName(item.MangaTitle);
        var safeChapter = SanitizeFileName(item.ChapterTitle ?? $"Ch_{item.ChapterNumber}");
        var mangaFolder = Path.Combine(_baseDownloadDirectory, safeManga);
        var cbzPath = Path.Combine(mangaFolder, $"{safeChapter}.cbz");

        await CbzBuilder.BuildCbzAsync(
            cbzPath,
            item.MangaTitle,
            item.ChapterTitle ?? $"Chapter {item.ChapterNumber}",
            item.ChapterNumber.ToString(),
            pagesData,
            extensions,
            ct: ct);

        item.CbzPath = cbzPath;
        item.Status = "completed";
        item.Progress = 100;
        await _db.UpdateDownloadProgressAsync(item.Id, 100, item.TotalPages, "completed");

        DownloadCompleted?.Invoke(item);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var regex = new Regex($"[{Regex.Escape(invalid)}]");
        var clean = regex.Replace(name, "_").Trim();
        return string.IsNullOrEmpty(clean) ? "untitled" : clean;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _workerLock.Dispose();
    }
}
