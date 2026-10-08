using System.Text.Json;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Http;

namespace MangaDl.Core.Sync;

/// <summary>
/// Bidirectional cloud sync service connecting the native SQLite database to Supabase PostgREST.
/// Mirrors the schema and sync protocol used by the web frontend and manga-dl-android:
/// - manga: Library subscriptions
/// - read_tracking: Chapter read status arrays
/// - reading_progress: Per-chapter page positions and timestamps for history
/// </summary>
public sealed class SupabaseSyncService
{
    private readonly HttpService _http;
    private readonly MangaDatabase _db;
    private readonly string _url;
    private readonly string _anonKey;
    private readonly Func<(string? UserId, string? AccessToken)>? _credentialProvider;

    public SupabaseSyncService(
        HttpService http,
        MangaDatabase database,
        string supabaseUrl,
        string anonKey,
        Func<(string? UserId, string? AccessToken)>? credentialProvider = null)
    {
        _http = http;
        _db = database;
        _url = (supabaseUrl ?? string.Empty).TrimEnd('/');
        _anonKey = anonKey ?? string.Empty;
        _credentialProvider = credentialProvider;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_url) && !string.IsNullOrWhiteSpace(_anonKey);

    // ----------------------------------------------------
    // Convenience Overloads (uses CredentialProvider)
    // ----------------------------------------------------

    public async Task<SyncResult> SyncAllAsync()
    {
        if (!IsConfigured)
            return new SyncResult(false, 0, 0, 0, 0, "Supabase is not configured.");

        var (userId, accessToken) = _credentialProvider?.Invoke() ?? (null, null);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(accessToken))
            return new SyncResult(false, 0, 0, 0, 0, "User is not authenticated.");

        return await SyncAllAsync(userId, accessToken);
    }

    public async Task SyncMangaSubscriptionAsync(LibraryEntity item, bool subscribed)
    {
        if (!IsConfigured) return;
        var (userId, accessToken) = _credentialProvider?.Invoke() ?? (null, null);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(accessToken)) return;

        try
        {
            await PushLibraryItemAsync(userId, accessToken, item, subscribed);
        }
        catch
        {
            // Non-fatal: offline changes will be pushed during next full sync
        }
    }

    public async Task SyncChapterReadAsync(
        string provider,
        string mangaId,
        string chapterId,
        int page = 0,
        string? mangaTitle = null,
        string? chapterTitle = null,
        bool isCompleted = true)
    {
        if (!IsConfigured) return;
        var (userId, accessToken) = _credentialProvider?.Invoke() ?? (null, null);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(accessToken)) return;

        try
        {
            await PushChapterProgressAsync(userId, accessToken, provider, mangaId, chapterId, page, mangaTitle, chapterTitle, isCompleted);
        }
        catch
        {
            // Non-fatal: offline changes will be pushed during next full sync
        }
    }

    // ----------------------------------------------------
    // Full Bidirectional Sync
    // ----------------------------------------------------

    public async Task<SyncResult> SyncAllAsync(string userId, string accessToken)
    {
        if (!IsConfigured)
            return new SyncResult(false, 0, 0, 0, 0, "Supabase is not configured.");

        try
        {
            int pulledLib = await PullLibraryAsync(userId, accessToken);
            int pulledTracking = await PullReadTrackingAsync(userId, accessToken);
            int pulledProgress = await PullReadingProgressAndHistoryAsync(userId, accessToken);

            int pushedLib = await PushAllLibraryAsync(userId, accessToken);
            int pushedProgress = await PushAllReadTrackingAsync(userId, accessToken);

            return new SyncResult(
                Success: true,
                PulledLibraryCount: pulledLib,
                PulledProgressCount: pulledTracking + pulledProgress,
                PushedLibraryCount: pushedLib,
                PushedProgressCount: pushedProgress);
        }
        catch (Exception ex)
        {
            return new SyncResult(false, 0, 0, 0, 0, ex.Message);
        }
    }

    // ----------------------------------------------------
    // Pull from Cloud
    // ----------------------------------------------------

    public async Task<int> PullLibraryAsync(string userId, string accessToken)
    {
        var path = $"/rest/v1/subscriptions?user_id=eq.{Uri.EscapeDataString(userId)}&select=*";
        var res = await GetAsync(path, accessToken);
        if (!res.IsSuccess) return 0;

        var items = JsonSerializer.Deserialize<List<SupabaseSubscriptionRecord>>(res.Text) ?? new();
        int count = 0;
        foreach (var remote in items)
        {
            if (string.IsNullOrWhiteSpace(remote.Provider) || string.IsNullOrWhiteSpace(remote.MangaId))
                continue;

            var existing = await _db.GetLibraryItemAsync(remote.Provider, remote.MangaId);
            var entity = new LibraryEntity
            {
                Id = $"{remote.Provider}/{remote.MangaId}",
                Provider = remote.Provider,
                MangaId = remote.MangaId,
                Title = remote.Title,
                CoverUrl = remote.CoverUrl,
                Type = remote.Type,
                AddedAt = existing?.AddedAt ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            await _db.AddToLibraryAsync(entity);
            count++;
        }
        return count;
    }

    public async Task<int> PullReadTrackingAsync(string userId, string accessToken)
    {
        var path = $"/rest/v1/read_tracking?user_id=eq.{Uri.EscapeDataString(userId)}&select=*";
        var res = await GetAsync(path, accessToken);
        if (!res.IsSuccess) return 0;

        var records = JsonSerializer.Deserialize<List<SupabaseReadTrackingRecord>>(res.Text) ?? new();
        int count = 0;
        foreach (var rec in records)
        {
            if (string.IsNullOrWhiteSpace(rec.Provider) || string.IsNullOrWhiteSpace(rec.MangaId))
                continue;

            foreach (var chapterId in rec.ChapterIds)
            {
                if (string.IsNullOrWhiteSpace(chapterId)) continue;
                var existing = await _db.GetProgressAsync(rec.Provider, rec.MangaId, chapterId);
                int page = existing?.Page ?? 1;
                await _db.SaveProgressAsync(rec.Provider, rec.MangaId, chapterId, 0, page, completed: true);
                count++;
            }
        }
        return count;
    }

    public async Task<int> PullReadingProgressAndHistoryAsync(string userId, string accessToken)
    {
        var path = $"/rest/v1/reading_progress?user_id=eq.{Uri.EscapeDataString(userId)}&order=updated_at.desc&select=*";
        var res = await GetAsync(path, accessToken);
        if (!res.IsSuccess) return 0;

        var records = JsonSerializer.Deserialize<List<SupabaseReadingProgressRecord>>(res.Text) ?? new();
        int count = 0;
        foreach (var rec in records)
        {
            if (string.IsNullOrWhiteSpace(rec.Provider) || string.IsNullOrWhiteSpace(rec.MangaId) || string.IsNullOrWhiteSpace(rec.ChapterId))
                continue;

            // 1. Record reading progress
            var existing = await _db.GetProgressAsync(rec.Provider, rec.MangaId, rec.ChapterId);
            bool isCompleted = existing?.Completed == 1;
            await _db.SaveProgressAsync(rec.Provider, rec.MangaId, rec.ChapterId, 0, rec.LastPage, completed: isCompleted);

            // 2. Populate reading history
            await _db.RecordHistoryAsync(
                rec.Provider,
                rec.MangaId,
                rec.MangaTitle ?? rec.MangaId,
                rec.ChapterId,
                rec.ChapterTitle ?? rec.ChapterId,
                coverUrl: null);
            count++;
        }
        return count;
    }

    // ----------------------------------------------------
    // Push to Cloud
    // ----------------------------------------------------

    public async Task PushLibraryItemAsync(string userId, string accessToken, LibraryEntity item, bool subscribed)
    {
        var recordId = $"{item.Provider}/{item.MangaId}";

        if (!subscribed)
        {
            // subscriptions has no "subscribed" flag — removing a manga from the
            // library is a delete, matching web's removeSubscription.
            var path = $"/rest/v1/subscriptions?user_id=eq.{Uri.EscapeDataString(userId)}&id=eq.{Uri.EscapeDataString(recordId)}";
            await DeleteAsync(path, accessToken);
            return;
        }

        var record = new SupabaseSubscriptionRecord
        {
            Id = recordId,
            UserId = userId,
            Provider = item.Provider,
            MangaId = item.MangaId,
            Title = item.Title,
            CoverUrl = item.CoverUrl,
            Type = item.Type,
            AddedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        var body = JsonSerializer.Serialize(record);
        await PostUpsertAsync("/rest/v1/subscriptions?on_conflict=user_id,id", body, accessToken);
    }

    public async Task PushChapterProgressAsync(
        string userId,
        string accessToken,
        string provider,
        string mangaId,
        string chapterId,
        int page,
        string? mangaTitle,
        string? chapterTitle,
        bool isCompleted)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // 1. Upsert reading_progress
        var progressRecord = new SupabaseReadingProgressRecord
        {
            UserId = userId,
            Provider = provider,
            MangaId = mangaId,
            ChapterId = chapterId,
            LastPage = page,
            MangaTitle = mangaTitle,
            ChapterTitle = chapterTitle,
            UpdatedAt = now
        };
        await PostUpsertAsync("/rest/v1/reading_progress", JsonSerializer.Serialize(progressRecord), accessToken);

        // 2. If completed, update read_tracking chapter list
        if (isCompleted)
        {
            var fetchPath = $"/rest/v1/read_tracking?user_id=eq.{Uri.EscapeDataString(userId)}&provider=eq.{Uri.EscapeDataString(provider)}&manga_id=eq.{Uri.EscapeDataString(mangaId)}&select=*";
            var res = await GetAsync(fetchPath, accessToken);
            var existingTracking = res.IsSuccess
                ? (JsonSerializer.Deserialize<List<SupabaseReadTrackingRecord>>(res.Text)?.FirstOrDefault())
                : null;

            var chapterIds = existingTracking?.ChapterIds?.ToHashSet() ?? new HashSet<string>();
            chapterIds.Add(chapterId);

            var trackingRecord = new SupabaseReadTrackingRecord
            {
                UserId = userId,
                Provider = provider,
                MangaId = mangaId,
                ChapterIds = chapterIds.ToList(),
                UpdatedAt = now
            };
            await PostUpsertAsync("/rest/v1/read_tracking", JsonSerializer.Serialize(trackingRecord), accessToken);
        }
    }

    public async Task<int> PushAllLibraryAsync(string userId, string accessToken)
    {
        var localLibrary = await _db.GetLibraryAsync();
        int count = 0;
        foreach (var item in localLibrary)
        {
            try
            {
                await PushLibraryItemAsync(userId, accessToken, item, subscribed: true);
                count++;
            }
            catch
            {
                // Continue with remaining items
            }
        }
        return count;
    }

    public async Task<int> PushAllReadTrackingAsync(string userId, string accessToken)
    {
        var completedList = await _db.GetAllCompletedProgressAsync();
        var byManga = completedList.GroupBy(x => $"{x.Provider}:::{x.MangaId}");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        int count = 0;

        foreach (var group in byManga)
        {
            var parts = group.Key.Split(":::");
            if (parts.Length != 2) continue;
            var provider = parts[0];
            var mangaId = parts[1];
            var chapterIds = group.Select(x => x.ChapterId).Distinct().ToList();

            var trackingRecord = new SupabaseReadTrackingRecord
            {
                UserId = userId,
                Provider = provider,
                MangaId = mangaId,
                ChapterIds = chapterIds,
                UpdatedAt = now
            };

            try
            {
                await PostUpsertAsync("/rest/v1/read_tracking", JsonSerializer.Serialize(trackingRecord), accessToken);
                count++;
            }
            catch
            {
                // Continue with remaining groups
            }
        }
        return count;
    }

    // ----------------------------------------------------
    // HTTP Helpers
    // ----------------------------------------------------

    private async Task<HttpResponseResult> GetAsync(string path, string accessToken)
    {
        var headers = new Dictionary<string, string>
        {
            ["apikey"] = _anonKey,
            ["Authorization"] = $"Bearer {accessToken}"
        };
        return await _http.FetchAsync($"{_url}{path}", "GET", headers);
    }

    private async Task<HttpResponseResult> DeleteAsync(string path, string accessToken)
    {
        var headers = new Dictionary<string, string>
        {
            ["apikey"] = _anonKey,
            ["Authorization"] = $"Bearer {accessToken}"
        };
        return await _http.FetchAsync($"{_url}{path}", "DELETE", headers);
    }

    private async Task<HttpResponseResult> PostUpsertAsync(string path, string body, string accessToken)
    {
        var headers = new Dictionary<string, string>
        {
            ["apikey"] = _anonKey,
            ["Authorization"] = $"Bearer {accessToken}",
            ["Content-Type"] = "application/json",
            ["Prefer"] = "resolution=merge-duplicates"
        };
        return await _http.FetchAsync($"{_url}{path}", "POST", headers, body);
    }
}
