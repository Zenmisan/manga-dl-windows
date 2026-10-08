namespace MangaDl.Core.Backup;

public sealed record BackupChapter(
    string Url,
    string Name,
    string? Scanlator,
    bool Read,
    bool Bookmark,
    int LastPageRead,
    double ChapterNumber,
    int SourceOrder);

public sealed record BackupHistoryEntry(string Url, long LastRead);

public sealed record BackupTracking(
    int SyncId,
    long MediaId,
    string Title,
    double LastChapterRead,
    double Score,
    int Status,
    string TrackingUrl);

public sealed record BackupManga(
    long Source,
    string Url,
    string Title,
    string? Artist,
    string? Author,
    string? Description,
    string? ThumbnailUrl,
    bool Favorite,
    List<BackupChapter> Chapters,
    List<BackupTracking> Tracking,
    List<BackupHistoryEntry> History,
    List<string> CategoryNames,
    string SourceName);

public sealed record BackupCategoryEntry(string Name, int Order);

public sealed record BackupSourceEntry(string Name, long SourceId);

public sealed record TachiyomiBackupResult(
    List<BackupManga> Manga,
    List<BackupCategoryEntry> Categories,
    List<BackupSourceEntry> Sources);
