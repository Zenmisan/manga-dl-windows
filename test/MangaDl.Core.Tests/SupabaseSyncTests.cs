using System.Text.Json;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Http;
using MangaDl.Core.Sync;
using Xunit;

namespace MangaDl.Core.Tests;

public class SupabaseSyncTests : IAsyncLifetime
{
    private readonly string _tempDbPath;
    private MangaDatabase _db = null!;

    public SupabaseSyncTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"manga_sync_test_{Guid.NewGuid()}.db");
    }

    public async Task InitializeAsync()
    {
        _db = new MangaDatabase(_tempDbPath);
        await _db.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public void TestMangaRecordSerialization()
    {
        var record = new SupabaseMangaRecord
        {
            Id = "mangadex:test-123:user-456",
            Provider = "mangadex",
            ProviderMangaId = "test-123",
            Title = "Chainsaw Man",
            CoverUrl = "https://example.com/cover.jpg",
            Url = "https://mangadex.org/title/test-123",
            Subscribed = true,
            UserId = "user-456",
            LastSynced = "2026-10-07T06:00:00Z"
        };

        var json = JsonSerializer.Serialize(record);
        Assert.Contains("\"provider_manga_id\":\"test-123\"", json);
        Assert.Contains("\"cover_url\":", json);
        Assert.Contains("\"last_synced\":", json);
        Assert.Contains("\"subscribed\":true", json);

        var deserialized = JsonSerializer.Deserialize<SupabaseMangaRecord>(json);
        Assert.NotNull(deserialized);
        Assert.Equal("Chainsaw Man", deserialized.Title);
        Assert.Equal("test-123", deserialized.ProviderMangaId);
        Assert.True(deserialized.Subscribed);
    }

    [Fact]
    public void TestReadTrackingRecordSerialization()
    {
        var record = new SupabaseReadTrackingRecord
        {
            UserId = "user-456",
            Provider = "mangakakalot",
            MangaId = "manga-abc",
            ChapterIds = new List<string> { "ch-1", "ch-2", "ch-3" },
            UpdatedAt = "2026-10-07T06:00:00Z"
        };

        var json = JsonSerializer.Serialize(record);
        Assert.Contains("\"chapter_ids\":[\"ch-1\",\"ch-2\",\"ch-3\"]", json);
        Assert.Contains("\"manga_id\":\"manga-abc\"", json);

        var deserialized = JsonSerializer.Deserialize<SupabaseReadTrackingRecord>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.ChapterIds.Count);
        Assert.Equal("ch-2", deserialized.ChapterIds[1]);
    }

    [Fact]
    public void TestReadingProgressRecordSerialization()
    {
        var record = new SupabaseReadingProgressRecord
        {
            UserId = "user-456",
            Provider = "mangadex",
            MangaId = "manga-xyz",
            ChapterId = "ch-15",
            LastPage = 24,
            MangaTitle = "One Piece",
            ChapterTitle = "Chapter 1000",
            UpdatedAt = "2026-10-07T06:00:00Z"
        };

        var json = JsonSerializer.Serialize(record);
        Assert.Contains("\"last_page\":24", json);
        Assert.Contains("\"manga_title\":\"One Piece\"", json);
        Assert.Contains("\"chapter_title\":\"Chapter 1000\"", json);

        var deserialized = JsonSerializer.Deserialize<SupabaseReadingProgressRecord>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(24, deserialized.LastPage);
        Assert.Equal("One Piece", deserialized.MangaTitle);
    }

    [Fact]
    public void TestUserCategoriesRecordSerialization()
    {
        var record = new SupabaseUserCategoriesRecord
        {
            UserId = "user-456",
            CustomCategories = new List<string> { "Favorites", "Weekly Reads" },
            MangaAssignments = new Dictionary<string, List<string>>
            {
                ["chainsaw man"] = new List<string> { "Favorites" }
            },
            UpdatedAt = "2026-10-07T06:00:00Z"
        };

        var json = JsonSerializer.Serialize(record);
        Assert.Contains("\"custom_categories\":[\"Favorites\",\"Weekly Reads\"]", json);
        Assert.Contains("\"manga_assignments\":{", json);

        var deserialized = JsonSerializer.Deserialize<SupabaseUserCategoriesRecord>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.CustomCategories.Count);
        Assert.True(deserialized.MangaAssignments.ContainsKey("chainsaw man"));
    }

    [Fact]
    public async Task TestDatabaseCompletedProgressFilter()
    {
        await _db.SaveProgressAsync("mangadex", "m-1", "c-1", 1.0, 10, completed: true);
        await _db.SaveProgressAsync("mangadex", "m-1", "c-2", 2.0, 5, completed: false);
        await _db.SaveProgressAsync("mangakakalot", "m-2", "c-1", 1.0, 20, completed: true);

        var all = await _db.GetAllProgressAsync();
        Assert.Equal(3, all.Count);

        var completed = await _db.GetAllCompletedProgressAsync();
        Assert.Equal(2, completed.Count);
        Assert.All(completed, c => Assert.Equal(1, c.Completed));
    }

    [Fact]
    public async Task TestOfflineAndUnconfiguredSync()
    {
        var http = new HttpService();
        var unconfiguredSync = new SupabaseSyncService(http, _db, "", "");
        Assert.False(unconfiguredSync.IsConfigured);

        var resUnconfigured = await unconfiguredSync.SyncAllAsync();
        Assert.False(resUnconfigured.Success);
        Assert.Equal("Supabase is not configured.", resUnconfigured.ErrorMessage);

        var configuredNoAuth = new SupabaseSyncService(
            http,
            _db,
            "https://test.supabase.co",
            "fake-anon-key",
            credentialProvider: () => (null, null));
        Assert.True(configuredNoAuth.IsConfigured);

        var resNoAuth = await configuredNoAuth.SyncAllAsync();
        Assert.False(resNoAuth.Success);
        Assert.Equal("User is not authenticated.", resNoAuth.ErrorMessage);
    }

    [Fact]
    public async Task TestPullLibraryEndToEnd()
    {
        var remoteData = new List<SupabaseMangaRecord>
        {
            new()
            {
                Id = "mangadex:solo-1:user-1",
                Provider = "mangadex",
                ProviderMangaId = "solo-1",
                Title = "Solo Leveling",
                CoverUrl = "https://example.com/solo.jpg",
                Url = "https://mangadex.org/title/solo-1",
                Subscribed = true,
                UserId = "user-1"
            },
            new()
            {
                Id = "novelbin:shadow-1:user-1",
                Provider = "novelbin",
                ProviderMangaId = "shadow-1",
                Title = "Shadow Slave",
                CoverUrl = "https://example.com/shadow.jpg",
                Url = "https://novelbin.com/b/shadow-1",
                Subscribed = true,
                UserId = "user-1"
            }
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/rest/v1/manga"))
            {
                var content = JsonSerializer.Serialize(remoteData);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        using var client = new HttpClient(handler);
        var http = new HttpService(client);
        var sync = new SupabaseSyncService(http, _db, "https://test.supabase.co", "test-key");

        var count = await sync.PullLibraryAsync("user-1", "access-token");
        Assert.Equal(2, count);

        var local = await _db.GetLibraryAsync();
        Assert.Equal(2, local.Count);
        Assert.Contains(local, x => x.Title == "Solo Leveling" && x.Provider == "mangadex");
        Assert.Contains(local, x => x.Title == "Shadow Slave" && x.Provider == "novelbin");
    }

    [Fact]
    public async Task TestPullReadTrackingAndHistoryEndToEnd()
    {
        var trackingData = new List<SupabaseReadTrackingRecord>
        {
            new()
            {
                UserId = "user-1",
                Provider = "mangadex",
                MangaId = "solo-1",
                ChapterIds = new List<string> { "ch-1", "ch-2" }
            }
        };

        var progressData = new List<SupabaseReadingProgressRecord>
        {
            new()
            {
                UserId = "user-1",
                Provider = "mangadex",
                MangaId = "solo-1",
                ChapterId = "ch-2",
                LastPage = 15,
                MangaTitle = "Solo Leveling",
                ChapterTitle = "Chapter 2"
            }
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/rest/v1/read_tracking"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(trackingData), System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (req.RequestUri!.PathAndQuery.Contains("/rest/v1/reading_progress"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(progressData), System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        using var client = new HttpClient(handler);
        var http = new HttpService(client);
        var sync = new SupabaseSyncService(http, _db, "https://test.supabase.co", "test-key");

        var trackCount = await sync.PullReadTrackingAsync("user-1", "access-token");
        Assert.Equal(2, trackCount);

        var progressCount = await sync.PullReadingProgressAndHistoryAsync("user-1", "access-token");
        Assert.Equal(1, progressCount);

        var history = await _db.GetHistoryAsync(10);
        Assert.Single(history);
        Assert.Equal("Solo Leveling", history[0].MangaTitle);
        Assert.Equal("Chapter 2", history[0].ChapterTitle);

        var p1 = await _db.GetProgressAsync("mangadex", "solo-1", "ch-1");
        Assert.NotNull(p1);
        Assert.Equal(1, p1.Completed);
    }

    [Fact]
    public async Task TestPushAllLibraryAndReadTrackingEndToEnd()
    {
        await _db.AddToLibraryAsync(new LibraryEntity
        {
            Provider = "mangadex",
            MangaId = "test-title",
            Title = "Test Manga",
            CoverUrl = "https://test.com/cover.jpg",
            Type = "manga"
        });

        await _db.SaveProgressAsync("mangadex", "test-title", "ch-10", 10.0, 1, completed: true);

        var postedPaths = new List<string>();
        var handler = new MockHttpMessageHandler(req =>
        {
            postedPaths.Add(req.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var http = new HttpService(client);
        var sync = new SupabaseSyncService(http, _db, "https://test.supabase.co", "test-key");

        var pushedLib = await sync.PushAllLibraryAsync("user-1", "token-1");
        Assert.Equal(1, pushedLib);
        Assert.Contains(postedPaths, p => p.Contains("/rest/v1/manga"));

        var pushedTracking = await sync.PushAllReadTrackingAsync("user-1", "token-1");
        Assert.Equal(1, pushedTracking);
        Assert.Contains(postedPaths, p => p.Contains("/rest/v1/read_tracking"));
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
