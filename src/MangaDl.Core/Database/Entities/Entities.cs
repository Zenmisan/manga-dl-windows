using SQLite;

namespace MangaDl.Core.Database.Entities;

[Table("library")]
public sealed class LibraryEntity
{
    [PrimaryKey]
    [Column("id")]
    public string Id { get; set; } = string.Empty; // "{provider}/{manga_id}"

    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Column("manga_id")]
    public string MangaId { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("cover_url")]
    public string? CoverUrl { get; set; }

    [Column("url")]
    public string? Url { get; set; }

    [Column("type")]
    public string Type { get; set; } = "manga"; // "manga" | "novel"

    [Column("added_at")]
    public long AddedAt { get; set; }

    [Column("updated_at")]
    public long UpdatedAt { get; set; }
}

[Table("reading_progress")]
public sealed class ProgressEntity
{
    [PrimaryKey]
    [Column("id")]
    public string Id { get; set; } = string.Empty; // "{provider}/{manga_id}/{chapter_id}"

    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Column("manga_id")]
    public string MangaId { get; set; } = string.Empty;

    [Column("chapter_id")]
    public string ChapterId { get; set; } = string.Empty;

    [Column("chapter_number")]
    public double ChapterNumber { get; set; }

    [Column("page")]
    public int Page { get; set; }

    [Column("completed")]
    public int Completed { get; set; } // 0 or 1

    [Column("read_at")]
    public long ReadAt { get; set; }
}

[Table("downloads")]
public sealed class DownloadEntity
{
    [PrimaryKey]
    [Column("id")]
    public string Id { get; set; } = string.Empty; // "{provider}/{manga_id}/{chapter_id}"

    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Column("manga_id")]
    public string MangaId { get; set; } = string.Empty;

    [Column("manga_title")]
    public string MangaTitle { get; set; } = string.Empty;

    [Column("chapter_id")]
    public string ChapterId { get; set; } = string.Empty;

    [Column("chapter_title")]
    public string? ChapterTitle { get; set; }

    [Column("chapter_number")]
    public double ChapterNumber { get; set; }

    [Column("cbz_path")]
    public string? CbzPath { get; set; }

    [Column("status")]
    public string Status { get; set; } = "queued"; // queued|downloading|completed|failed|paused

    [Column("progress")]
    public int Progress { get; set; }

    [Column("total_pages")]
    public int TotalPages { get; set; }

    [Column("queued_at")]
    public long QueuedAt { get; set; }

    [Column("completed_at")]
    public long? CompletedAt { get; set; }
}

[Table("categories")]
public sealed class CategoryEntity
{
    [PrimaryKey]
    [Column("id")]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("sort_order")]
    public int SortOrder { get; set; }
}

[Table("library_categories")]
public sealed class LibraryCategoryEntity
{
    [Column("library_id")]
    [Indexed(Name = "idx_lib_cat", Order = 1, Unique = true)]
    public string LibraryId { get; set; } = string.Empty;

    [Column("category_id")]
    [Indexed(Name = "idx_lib_cat", Order = 2, Unique = true)]
    public string CategoryId { get; set; } = string.Empty;
}

[Table("history")]
public sealed class HistoryEntity
{
    [PrimaryKey]
    [Column("id")]
    public string Id { get; set; } = string.Empty; // "{provider}/{manga_id}/{chapter_id}"

    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Column("manga_id")]
    public string MangaId { get; set; } = string.Empty;

    [Column("manga_title")]
    public string MangaTitle { get; set; } = string.Empty;

    [Column("chapter_id")]
    public string ChapterId { get; set; } = string.Empty;

    [Column("chapter_title")]
    public string? ChapterTitle { get; set; }

    [Column("cover_url")]
    public string? CoverUrl { get; set; }

    [Column("read_at")]
    public long ReadAt { get; set; }
}
