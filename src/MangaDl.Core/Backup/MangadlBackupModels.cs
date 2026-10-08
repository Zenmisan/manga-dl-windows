namespace MangaDl.Core.Backup;

/// <summary>
/// Universal Manga-DL backup schema matching the Android client (com.mangadl.android.data.backup.MangaDlBackup).
/// </summary>
public sealed class MangaDlBackup
{
    public string Version { get; set; } = "1.0";
    public string App { get; set; } = "manga-dl";
    public long ExportedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public List<BackupMangaEntry> Library { get; set; } = [];
    public List<BackupProgressEntry> Progress { get; set; } = [];
    public List<string> Categories { get; set; } = [];
    public Dictionary<string, List<string>> MangaCategories { get; set; } = [];
    public List<BackupTrackerBindEntry> TrackerBinds { get; set; } = [];
}

public sealed class BackupMangaEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string CoverUrl { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Url { get; set; } = "";
    public string Type { get; set; } = "manga"; // "manga" | "novel"
    public long AddedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string? LastReadChapterId { get; set; }
    public long? LastReadAt { get; set; }
    public int TotalChapters { get; set; }
    public int ReadCount { get; set; }
    public List<string> Categories { get; set; } = [];
}

public sealed class BackupProgressEntry
{
    public string MangaId { get; set; } = "";
    public string ChapterId { get; set; } = "";
    public string Provider { get; set; } = "";
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public long ReadAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public bool Completed { get; set; }
    public double ChapterNumber { get; set; }
}

public sealed class BackupTrackerBindEntry
{
    public string Provider { get; set; } = "";
    public string MangaId { get; set; } = "";
    public string Tracker { get; set; } = ""; // "AniList" | "MyAnimeList"
    public int RemoteId { get; set; }
    public string? RemoteTitle { get; set; }
    public double LastChapterRead { get; set; }
    public double Score { get; set; }
    public string? Status { get; set; }
}

public sealed record MangadlRestoreSummary(
    int MangaRestored,
    int ProgressRestored,
    int CategoriesRestored,
    int TrackerBindsRestored);
