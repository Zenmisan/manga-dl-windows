namespace MangaDl.Core.Tracking;

public sealed record TrackerSearchResult(
    int Id,
    string Title,
    int TotalChapters,
    string CoverUrl,
    string Tracker); // "AniList" or "MyAnimeList"

public sealed record TrackerTokenResult(
    string AccessToken,
    string? RefreshToken,
    int? ExpiresIn,
    string? Username);

public sealed record TrackerProfile(
    int Id,
    string Username,
    string? AvatarUrl);
