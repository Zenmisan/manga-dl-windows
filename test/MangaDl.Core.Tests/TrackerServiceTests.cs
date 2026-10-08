using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MangaDl.Core.Database;
using MangaDl.Core.Database.Entities;
using MangaDl.Core.Http;
using MangaDl.Core.Tracking;
using Xunit;

namespace MangaDl.Core.Tests;

public class TrackerServiceTests : IAsyncLifetime
{
    private readonly string _tempDbPath;
    private MangaDatabase _db = null!;

    public TrackerServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"manga_tracker_test_{Guid.NewGuid()}.db");
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
    public void TestGeneratePkce()
    {
        var (verifier1, challenge1) = TrackerService.GeneratePkce();
        var (verifier2, challenge2) = TrackerService.GeneratePkce();

        Assert.Equal(128, verifier1.Length);
        Assert.Equal(128, challenge1.Length);
        Assert.Equal(verifier1, challenge1);

        // Check valid unreserved characters: a-z, A-Z, 0-9, -, ., _, ~
        Assert.Matches("^[a-zA-Z0-9\\-._~]{128}$", verifier1);

        // Check randomness
        Assert.NotEqual(verifier1, verifier2);
    }

    [Fact]
    public void TestGetAnilistAuthUrl()
    {
        var clientId = "53109";
        var redirectUri = "http://localhost:5678/mal-callback";
        var url = TrackerService.GetAnilistAuthUrl(clientId, redirectUri);

        Assert.Contains("https://anilist.co/api/v2/oauth/authorize", url);
        Assert.Contains("client_id=53109", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains(Uri.EscapeDataString(redirectUri), url);
    }

    [Fact]
    public void TestGetMalAuthUrl()
    {
        var clientId = "4ee644500f513dd7887120b026b65f39";
        var (verifier, _) = TrackerService.GeneratePkce();
        var redirectUri = "http://localhost:5678/mal-callback";
        var url = TrackerService.GetMalAuthUrl(clientId, verifier, redirectUri);

        Assert.Contains("https://myanimelist.net/v1/oauth2/authorize", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains($"client_id={clientId}", url);
        Assert.Contains($"code_challenge={Uri.EscapeDataString(verifier)}", url);
        Assert.Contains("code_challenge_method=plain", url);
        Assert.Contains(Uri.EscapeDataString(redirectUri), url);
    }

    [Fact]
    public async Task TestExchangeAnilistTokenAsync()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("/api/v2/oauth/token"))
            {
                var json = JsonSerializer.Serialize(new
                {
                    access_token = "al_test_access_token_123",
                    token_type = "Bearer",
                    expires_in = 31536000,
                    refresh_token = "al_test_refresh_token_456"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (req.RequestUri!.AbsoluteUri.Contains("graphql.anilist.co"))
            {
                var json = JsonSerializer.Serialize(new
                {
                    data = new
                    {
                        Viewer = new
                        {
                            id = 12345,
                            name = "zenmi_al"
                        }
                    }
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new HttpClient(handler);
        var http = new HttpService(client);
        var service = new TrackerService(http);

        var result = await service.ExchangeAnilistTokenAsync("auth_code_123", "53109", "secret", "http://localhost:5678/mal-callback");

        Assert.NotNull(result);
        Assert.Equal("al_test_access_token_123", result.AccessToken);
        Assert.Equal("al_test_refresh_token_456", result.RefreshToken);
        Assert.Equal("zenmi_al", result.Username);
    }

    [Fact]
    public async Task TestExchangeMalTokenAsync()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("/v1/oauth2/token"))
            {
                var json = JsonSerializer.Serialize(new
                {
                    access_token = "mal_test_access_token_abc",
                    token_type = "Bearer",
                    expires_in = 2678400,
                    refresh_token = "mal_test_refresh_token_def"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (req.RequestUri!.AbsoluteUri.Contains("/v2/users/@me"))
            {
                var json = JsonSerializer.Serialize(new
                {
                    id = 67890,
                    name = "zenmi_mal"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new HttpClient(handler);
        var http = new HttpService(client);
        var service = new TrackerService(http);

        var result = await service.ExchangeMalTokenAsync("mal_code_123", "pkce_verifier", "client_id", "http://localhost:5678/mal-callback");

        Assert.NotNull(result);
        Assert.Equal("mal_test_access_token_abc", result.AccessToken);
        Assert.Equal("mal_test_refresh_token_def", result.RefreshToken);
        Assert.Equal("zenmi_mal", result.Username);
    }

    [Fact]
    public async Task TestSearchAnilistAsync()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = """
                {
                  "data": {
                    "Page": {
                      "media": [
                        {
                          "id": 105398,
                          "title": {
                            "romaji": "Chainsaw Man",
                            "english": "Chainsaw Man",
                            "userPreferred": "Chainsaw Man"
                          },
                          "chapters": 97,
                          "coverImage": {
                            "medium": "https://s4.anilist.co/file/anilistcdn/media/manga/cover/medium/bx105398.jpg",
                            "large": "https://s4.anilist.co/file/anilistcdn/media/manga/cover/large/bx105398.jpg"
                          }
                        }
                      ]
                    }
                  }
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var http = new HttpService(client);
        var service = new TrackerService(http);

        var results = await service.SearchAnilistAsync("Chainsaw Man");

        Assert.NotEmpty(results);
        Assert.Equal(105398, results[0].Id);
        Assert.Equal("Chainsaw Man", results[0].Title);
        Assert.Equal(97, results[0].TotalChapters);
        Assert.Equal("AniList", results[0].Tracker);
    }

    [Fact]
    public async Task TestSearchMalAsync()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var json = """
                {
                  "data": [
                    {
                      "node": {
                        "id": 114755,
                        "title": "Chainsaw Man",
                        "main_picture": {
                          "medium": "https://cdn.myanimelist.net/images/manga/3/216464.jpg"
                        },
                        "num_chapters": 97
                      }
                    }
                  ]
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var http = new HttpService(client);
        var service = new TrackerService(http);

        var results = await service.SearchMalAsync("Chainsaw Man", "test_mal_token");

        Assert.NotEmpty(results);
        Assert.Equal(114755, results[0].Id);
        Assert.Equal("Chainsaw Man", results[0].Title);
        Assert.Equal(97, results[0].TotalChapters);
        Assert.Equal("MyAnimeList", results[0].Tracker);
    }

    [Fact]
    public async Task TestSyncChapterProgressEndToEnd()
    {
        var updatedAniList = false;
        var updatedMal = false;

        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.AbsoluteUri;

            // AniList Search
            if (uri.Contains("graphql.anilist.co") && req.Content != null)
            {
                var body = req.Content.ReadAsStringAsync().Result;
                if (body.Contains("query Search"))
                {
                    var json = """
                        {
                          "data": {
                            "Page": {
                              "media": [
                                {
                                  "id": 105398,
                                  "title": { "romaji": "Chainsaw Man" },
                                  "chapters": 97
                                }
                              ]
                            }
                          }
                        }
                        """;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                }
                if (body.Contains("SaveMediaListEntry"))
                {
                    updatedAniList = true;
                    var json = """{ "data": { "SaveMediaListEntry": { "id": 999, "progress": 48 } } }""";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                    };
                }
            }

            // MAL Search
            if (uri.Contains("/v2/manga?q="))
            {
                var json = """
                    {
                      "data": [
                        {
                          "node": {
                            "id": 114755,
                            "title": "Chainsaw Man",
                            "num_chapters": 97
                          }
                        }
                      ]
                    }
                    """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            // MAL Update
            if (uri.Contains("/my_list_status") && req.Method == HttpMethod.Put)
            {
                updatedMal = true;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new HttpClient(handler);
        var http = new HttpService(client);
        var service = new TrackerService(http);

        // Call SyncChapterProgressAsync
        await service.SyncChapterProgressAsync(
            _db,
            "mangadex",
            "csm-123",
            "Chainsaw Man",
            48.0,
            isCompleted: false,
            trackerKey => trackerKey switch
            {
                "anilist_token" => "valid_al_token",
                "mal_token" => "valid_mal_token",
                _ => null
            },
            autoSyncEnabled: true,
            markReadingOnFirstChapter: true);

        Assert.True(updatedAniList, "AniList progress should have been updated");
        Assert.True(updatedMal, "MyAnimeList progress should have been updated");

        // Verify binds were saved in SQLite
        var binds = await _db.GetTrackerBindsAsync("mangadex", "csm-123");
        Assert.Equal(2, binds.Count);

        var alBind = binds.FirstOrDefault(b => b.Tracker == "AniList");
        Assert.NotNull(alBind);
        Assert.Equal(105398, alBind.RemoteId);
        Assert.Equal(48.0, alBind.LastChapterRead);

        var malBind = binds.FirstOrDefault(b => b.Tracker == "MyAnimeList");
        Assert.NotNull(malBind);
        Assert.Equal(114755, malBind.RemoteId);
        Assert.Equal(48.0, malBind.LastChapterRead);
    }

    [Fact]
    public async Task TestOAuthLoopbackListenerCapturesCode()
    {
        const int testPort = 5689;
        using var listener = new OAuthLoopbackListener(testPort);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var listenTask = listener.WaitForAuthCodeAsync(TimeSpan.FromSeconds(5), cts.Token);

        // Make an HTTP request simulating the browser redirect
        using var client = new HttpClient();

        // First simulate a browser favicon request that should be ignored
        var faviconResp = await client.GetAsync($"http://localhost:{testPort}/favicon.ico");
        Assert.Equal(HttpStatusCode.NoContent, faviconResp.StatusCode);

        // Then send the real callback with code
        var callbackResp = await client.GetAsync($"http://localhost:{testPort}/mal-callback?code=test_auth_code_789");
        Assert.Equal(HttpStatusCode.OK, callbackResp.StatusCode);

        var code = await listenTask;
        Assert.Equal("test_auth_code_789", code);
    }

    [Fact]
    public async Task TestOAuthLoopbackListenerTimeout()
    {
        const int testPort = 5690;
        using var listener = new OAuthLoopbackListener(testPort);

        // Very short timeout
        var code = await listener.WaitForAuthCodeAsync(TimeSpan.FromMilliseconds(200));
        Assert.Null(code);
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
