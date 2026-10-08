using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MangaDl.Core.Http;

namespace MangaDl.Core.Tracking;

/// <summary>
/// Service managing external manga tracking accounts (AniList & MyAnimeList).
/// Handles OAuth authorization, token exchange, user profiles, search, and reading progress updates.
/// </summary>
public sealed class TrackerService
{
    private readonly HttpService _http;

    public const string AnilistGraphqlUrl = "https://graphql.anilist.co";
    public const string AnilistTokenUrl = "https://anilist.co/api/v2/oauth/token";
    public const string MalApiUrl = "https://api.myanimelist.net/v2";
    public const string MalTokenUrl = "https://myanimelist.net/v1/oauth2/token";

    public TrackerService(HttpService http)
    {
        _http = http;
    }

    // =========================================================================
    // AniList
    // =========================================================================

    public static string GetAnilistAuthUrl(string clientId, string redirectUri)
    {
        return $"https://anilist.co/api/v2/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}";
    }

    public async Task<TrackerTokenResult?> ExchangeAnilistTokenAsync(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri)
    {
        var payload = JsonSerializer.Serialize(new
        {
            grant_type = "authorization_code",
            client_id = int.TryParse(clientId, out var parsedId) ? (object)parsedId : clientId,
            client_secret = clientSecret,
            redirect_uri = redirectUri,
            code = code
        });

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Accept"] = "application/json"
        };

        var res = await _http.FetchAsync(AnilistTokenUrl, "POST", headers, payload);
        if (!res.IsSuccess) return null;

        using var doc = JsonDocument.Parse(res.Text);
        var root = doc.RootElement;
        if (!root.TryGetProperty("access_token", out var tokenEl)) return null;

        var accessToken = tokenEl.GetString() ?? string.Empty;
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : (int?)null;

        var username = await GetAnilistViewerAsync(accessToken);

        return new TrackerTokenResult(accessToken, refreshToken, expiresIn, username);
    }

    public async Task<string?> GetAnilistViewerAsync(string token)
    {
        var query = new { query = "query { Viewer { id name } }" };
        var body = JsonSerializer.Serialize(query);
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {token}",
            ["Content-Type"] = "application/json",
            ["Accept"] = "application/json"
        };

        var res = await _http.FetchAsync(AnilistGraphqlUrl, "POST", headers, body);
        if (!res.IsSuccess) return null;

        try
        {
            using var doc = JsonDocument.Parse(res.Text);
            var root = doc.RootElement;
            return root.GetProperty("data").GetProperty("Viewer").GetProperty("name").GetString();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TrackerSearchResult>> SearchAnilistAsync(string query, string? token = null)
    {
        var gql = """
            query Search($search: String) {
              Page(perPage: 10) {
                media(search: $search, type: MANGA) {
                  id
                  title {
                    romaji
                    english
                    userPreferred
                  }
                  chapters
                  coverImage {
                    medium
                    large
                  }
                }
              }
            }
            """;

        var payload = JsonSerializer.Serialize(new
        {
            query = gql,
            variables = new { search = query }
        });

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Accept"] = "application/json"
        };
        if (!string.IsNullOrEmpty(token))
        {
            headers["Authorization"] = $"Bearer {token}";
        }

        var res = await _http.FetchAsync(AnilistGraphqlUrl, "POST", headers, payload);
        if (!res.IsSuccess) return new List<TrackerSearchResult>();

        var results = new List<TrackerSearchResult>();
        try
        {
            using var doc = JsonDocument.Parse(res.Text);
            var media = doc.RootElement.GetProperty("data").GetProperty("Page").GetProperty("media");
            foreach (var item in media.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var titleObj = item.GetProperty("title");
                string title = titleObj.TryGetProperty("english", out var en) && en.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(en.GetString())
                    ? en.GetString()!
                    : titleObj.TryGetProperty("userPreferred", out var up) && up.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(up.GetString())
                        ? up.GetString()!
                        : titleObj.GetProperty("romaji").GetString() ?? "Unknown";

                var chapters = item.TryGetProperty("chapters", out var ch) && ch.ValueKind == JsonValueKind.Number ? ch.GetInt32() : 0;
                var cover = item.TryGetProperty("coverImage", out var ci) && ci.TryGetProperty("medium", out var m) ? m.GetString() ?? "" : "";

                results.Add(new TrackerSearchResult(id, title, chapters, cover, "AniList"));
            }
        }
        catch { }

        return results;
    }

    public async Task<bool> UpdateAnilistProgressAsync(string token, int mediaId, int progress, bool isCompleted)
    {
        var gql = """
            mutation SaveMediaList($mediaId: Int, $progress: Int, $status: MediaListStatus) {
              SaveMediaListEntry(mediaId: $mediaId, progress: $progress, status: $status) {
                id
                progress
                status
              }
            }
            """;

        var payload = JsonSerializer.Serialize(new
        {
            query = gql,
            variables = new
            {
                mediaId = mediaId,
                progress = progress,
                status = isCompleted ? "COMPLETED" : "CURRENT"
            }
        });

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {token}",
            ["Content-Type"] = "application/json",
            ["Accept"] = "application/json"
        };

        var res = await _http.FetchAsync(AnilistGraphqlUrl, "POST", headers, payload);
        return res.IsSuccess;
    }

    // =========================================================================
    // MyAnimeList (MAL)
    // =========================================================================

    public static (string Verifier, string Challenge) GeneratePkce()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
        var bytes = new byte[128];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(128);
        foreach (var b in bytes)
        {
            sb.Append(chars[b % chars.Length]);
        }
        var verifier = sb.ToString();
        // MAL requires code_challenge_method=plain
        return (verifier, verifier);
    }

    public static string GetMalAuthUrl(string clientId, string codeVerifier, string redirectUri)
    {
        return $"https://myanimelist.net/v1/oauth2/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}&code_challenge={Uri.EscapeDataString(codeVerifier)}&code_challenge_method=plain&redirect_uri={Uri.EscapeDataString(redirectUri)}";
    }

    public async Task<TrackerTokenResult?> ExchangeMalTokenAsync(
        string code,
        string codeVerifier,
        string clientId,
        string redirectUri)
    {
        var formData = new StringBuilder();
        formData.Append($"client_id={Uri.EscapeDataString(clientId)}");
        formData.Append($"&code={Uri.EscapeDataString(code)}");
        formData.Append($"&code_verifier={Uri.EscapeDataString(codeVerifier)}");
        formData.Append("&grant_type=authorization_code");
        formData.Append($"&redirect_uri={Uri.EscapeDataString(redirectUri)}");

        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/x-www-form-urlencoded"
        };

        var res = await _http.FetchAsync(MalTokenUrl, "POST", headers, formData.ToString());
        if (!res.IsSuccess) return null;

        using var doc = JsonDocument.Parse(res.Text);
        var root = doc.RootElement;
        if (!root.TryGetProperty("access_token", out var tokenEl)) return null;

        var accessToken = tokenEl.GetString() ?? string.Empty;
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : (int?)null;

        var username = await GetMalUserAsync(accessToken);

        return new TrackerTokenResult(accessToken, refreshToken, expiresIn, username);
    }

    public async Task<string?> GetMalUserAsync(string token)
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {token}"
        };

        var res = await _http.FetchAsync($"{MalApiUrl}/users/@me", "GET", headers);
        if (!res.IsSuccess) return null;

        try
        {
            using var doc = JsonDocument.Parse(res.Text);
            return doc.RootElement.GetProperty("name").GetString();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TrackerSearchResult>> SearchMalAsync(string query, string token)
    {
        var escaped = Uri.EscapeDataString(query.Length > 64 ? query[..64] : query);
        var url = $"{MalApiUrl}/manga?q={escaped}&nsfw=true&fields=id,title,num_chapters,main_picture";

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {token}"
        };

        var res = await _http.FetchAsync(url, "GET", headers);
        if (!res.IsSuccess) return new List<TrackerSearchResult>();

        var results = new List<TrackerSearchResult>();
        try
        {
            using var doc = JsonDocument.Parse(res.Text);
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                foreach (var item in dataEl.EnumerateArray())
                {
                    var node = item.GetProperty("node");
                    var id = node.GetProperty("id").GetInt32();
                    var title = node.GetProperty("title").GetString() ?? "Unknown";
                    var chapters = node.TryGetProperty("num_chapters", out var ch) ? ch.GetInt32() : 0;
                    var cover = node.TryGetProperty("main_picture", out var mp) && mp.TryGetProperty("medium", out var m) ? m.GetString() ?? "" : "";

                    results.Add(new TrackerSearchResult(id, title, chapters, cover, "MyAnimeList"));
                }
            }
        }
        catch { }

        return results;
    }

    public async Task<bool> UpdateMalProgressAsync(string token, int malId, int progress, bool isCompleted)
    {
        var status = isCompleted ? "completed" : "reading";
        var form = $"status={Uri.EscapeDataString(status)}&num_chapters_read={progress}";

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {token}",
            ["Content-Type"] = "application/x-www-form-urlencoded"
        };

        var res = await _http.FetchAsync($"{MalApiUrl}/manga/{malId}/my_list_status", "PUT", headers, form);
        return res.IsSuccess;
    }

    // =========================================================================
    // High-Level Synchronization
    // =========================================================================

    public async Task SyncChapterProgressAsync(
        Database.MangaDatabase db,
        string provider,
        string mangaId,
        string mangaTitle,
        double chapterNumber,
        bool isCompleted,
        Func<string, string?> tokenProvider,
        bool autoSyncEnabled = true,
        bool markReadingOnFirstChapter = true)
    {
        if (!autoSyncEnabled) return;

        try
        {
            var binds = await db.GetTrackerBindsAsync(provider, mangaId);

            // 1. AniList
            var anilistToken = tokenProvider("anilist_token");
            if (!string.IsNullOrEmpty(anilistToken))
            {
                try
                {
                    var bind = binds.FirstOrDefault(b => b.Tracker.Equals("AniList", StringComparison.OrdinalIgnoreCase));
                    if (bind == null && !string.IsNullOrWhiteSpace(mangaTitle))
                    {
                        var results = await SearchAnilistAsync(mangaTitle, anilistToken);
                        if (results.Count > 0)
                        {
                            bind = new Database.Entities.TrackerBindEntity
                            {
                                Provider = provider,
                                MangaId = mangaId,
                                Tracker = "AniList",
                                RemoteId = results[0].Id,
                                RemoteTitle = results[0].Title,
                                LastChapterRead = 0
                            };
                            await db.SaveTrackerBindAsync(bind);
                        }
                    }

                    if (bind != null && (chapterNumber > bind.LastChapterRead || isCompleted))
                    {
                        var progress = (int)Math.Floor(chapterNumber);
                        var ok = await UpdateAnilistProgressAsync(anilistToken, bind.RemoteId, progress, isCompleted);
                        if (ok)
                        {
                            bind.LastChapterRead = Math.Max(bind.LastChapterRead, chapterNumber);
                            await db.SaveTrackerBindAsync(bind);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AniList Sync Error] {ex.Message}");
                }
            }

            // 2. MyAnimeList
            var malToken = tokenProvider("mal_token");
            if (!string.IsNullOrEmpty(malToken))
            {
                try
                {
                    var bind = binds.FirstOrDefault(b => b.Tracker.Equals("MyAnimeList", StringComparison.OrdinalIgnoreCase));
                    if (bind == null && !string.IsNullOrWhiteSpace(mangaTitle))
                    {
                        var results = await SearchMalAsync(mangaTitle, malToken);
                        if (results.Count > 0)
                        {
                            bind = new Database.Entities.TrackerBindEntity
                            {
                                Provider = provider,
                                MangaId = mangaId,
                                Tracker = "MyAnimeList",
                                RemoteId = results[0].Id,
                                RemoteTitle = results[0].Title,
                                LastChapterRead = 0
                            };
                            await db.SaveTrackerBindAsync(bind);
                        }
                    }

                    if (bind != null && (chapterNumber > bind.LastChapterRead || isCompleted))
                    {
                        var progress = (int)Math.Floor(chapterNumber);
                        var ok = await UpdateMalProgressAsync(malToken, bind.RemoteId, progress, isCompleted);
                        if (ok)
                        {
                            bind.LastChapterRead = Math.Max(bind.LastChapterRead, chapterNumber);
                            await db.SaveTrackerBindAsync(bind);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MAL Sync Error] {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TrackerService.SyncChapterProgressAsync] {ex.Message}");
        }
    }
}
