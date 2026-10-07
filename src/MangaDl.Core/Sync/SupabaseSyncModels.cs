using System.Text.Json.Serialization;

namespace MangaDl.Core.Sync;

public sealed class SupabaseMangaRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("provider_manga_id")]
    public string ProviderMangaId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("cover_url")]
    public string? CoverUrl { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("subscribed")]
    public bool Subscribed { get; set; } = true;

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("last_synced")]
    public string? LastSynced { get; set; }
}

public sealed class SupabaseReadTrackingRecord
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("manga_id")]
    public string MangaId { get; set; } = string.Empty;

    [JsonPropertyName("chapter_ids")]
    public List<string> ChapterIds { get; set; } = new();

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

public sealed class SupabaseReadingProgressRecord
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("manga_id")]
    public string MangaId { get; set; } = string.Empty;

    [JsonPropertyName("chapter_id")]
    public string ChapterId { get; set; } = string.Empty;

    [JsonPropertyName("last_page")]
    public int LastPage { get; set; } = 1;

    [JsonPropertyName("manga_title")]
    public string? MangaTitle { get; set; }

    [JsonPropertyName("chapter_title")]
    public string? ChapterTitle { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

public sealed class SupabaseUserCategoriesRecord
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("custom_categories")]
    public List<string> CustomCategories { get; set; } = new();

    [JsonPropertyName("manga_assignments")]
    public Dictionary<string, List<string>> MangaAssignments { get; set; } = new();

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

public sealed record SyncResult(
    bool Success,
    int PulledLibraryCount,
    int PulledProgressCount,
    int PushedLibraryCount,
    int PushedProgressCount,
    string? ErrorMessage = null);
